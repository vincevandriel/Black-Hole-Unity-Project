#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace BlackHole.Staging.Simulation
{
    // Physical grid configuration, independent of rendering quality.
    public sealed class GeneratedFluidSettings
    {
        public int GridSize { get; }
        public int FrameCount { get; }
        public double MaximumDurationTg { get; }
        public static GeneratedFluidSettings Default => new GeneratedFluidSettings();
        public GeneratedFluidSettings(int gridSize = 96, int frameCount = 129, double maximumDurationTg = 4000)
        {
            if (gridSize < 16 || gridSize > 192) throw new ArgumentOutOfRangeException(nameof(gridSize));
            if (frameCount < 2 || frameCount > 257) throw new ArgumentOutOfRangeException(nameof(frameCount));
            GeneratedFluidSource.RequireFinite(maximumDurationTg);
            if (maximumDurationTg <= 0 || maximumDurationTg > 4000) throw new ArgumentOutOfRangeException(nameof(maximumDurationTg));
            GridSize = gridSize; FrameCount = frameCount; MaximumDurationTg = maximumDurationTg;
        }
    }

    // Immutable finite-volume surface-density history from the Newtonian solver.
    public sealed class GeneratedFluidHistory
    {
        public const string ModelId = "generated-isothermal-newtonian-muscl-ssprk2-v1";
        public const double HeightRg = 2;
        public const double SoundSpeedRgPerTg = .04;
        public const double DensityFloor = 1e-7;
        public const double SofteningRg = 2;
        public const double SinkRadiusRg = 3;
        public const double SinkTimeTg = 4;
        private readonly float[] sigma, vx, vz;
        private readonly double[] frameTimes;
        private readonly ReadOnlyCollection<double> readOnlyTimes;
        private readonly ReadOnlyCollection<GeneratedFluidDiagnostics> frameDiagnostics;
        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public int GridSize { get; }
        public int FrameCount => frameTimes.Length;
        public double HalfExtentRg { get; }
        public double CellWidthRg => 2 * HalfExtentRg / GridSize;
        public double GravitationalRadiusMeters { get; }
        public double GravitationalTimeSeconds { get; }
        public InteractiveScenarioMode ScenarioMode { get; }
        public IReadOnlyList<double> FrameTimes => readOnlyTimes;
        public IReadOnlyList<GeneratedFluidDiagnostics> FrameDiagnostics => frameDiagnostics;
        public GeneratedFluidDiagnostics Diagnostics => frameDiagnostics[frameDiagnostics.Count - 1];
        // Array payload only; excludes object headers, source scenario and renderer textures.
        public long MemoryBytes => 3L * sigma.Length * sizeof(float) + frameTimes.Length * sizeof(double)
            + frameDiagnostics.Count * (8L * sizeof(double) + 2L * sizeof(long));
        // Peak generation array payload: completed cache plus working conservative grid.
        public long GenerationWorkingMemoryBytes => MemoryBytes + 18L * GridSize * GridSize * sizeof(double);
        private GeneratedFluidHistory(InteractiveBinaryScenario scenario, GeneratedFluidSettings settings, double start, double extent,
            float[] sigma, float[] vx, float[] vz, double[] times, GeneratedFluidDiagnostics[] diagnostics)
        {
            ScenarioMode = scenario.Mode; GravitationalRadiusMeters = scenario.GravitationalRadiusMeters;
            GravitationalTimeSeconds = scenario.GravitationalTimeSeconds;
            GridSize = settings.GridSize; StartSeconds = start; EndSeconds = scenario.DurationSeconds;
            HalfExtentRg = extent; this.sigma = sigma; this.vx = vx; this.vz = vz; frameTimes = times;
            readOnlyTimes = Array.AsReadOnly(times); frameDiagnostics = Array.AsReadOnly(diagnostics);
        }
        public static GeneratedFluidHistory Generate(InteractiveBinaryScenario scenario, CancellationToken token,
            Action<double>? progress = null) => Generate(scenario, GeneratedFluidSettings.Default, token, progress);
        public static GeneratedFluidHistory Generate(InteractiveBinaryScenario scenario, GeneratedFluidSettings settings,
            CancellationToken token, Action<double>? progress = null)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            token.ThrowIfCancellationRequested(); progress?.Invoke(0);
            double tg = scenario.GravitationalTimeSeconds;
            double durationTg = Math.Min(settings.MaximumDurationTg, scenario.DurationSeconds / tg);
            double start = Math.Max(0, scenario.DurationSeconds - durationTg * tg);
            InteractiveBinarySnapshot initialSnapshot = scenario.Evaluate(start);
            // Initialize the assumed ring at cache start; no earlier evolution is available.
            double separation = Math.Max(8, initialSnapshot.SeparationRg);
            double extent = Math.Max(32, 3.5 * separation), ringRadius = 2.1 * separation, width = .35 * separation;
            double InitialDensity(double r) => Math.Exp(-.5 * Math.Pow((r - ringRadius) / width, 2));
            var solver = new GeneratedFluidSolver(settings.GridSize, extent, SoundSpeedRgPerTg, DensityFloor, (x, z) =>
            {
                double r = Math.Sqrt(x * x + z * z), ring = InitialDensity(r), rho = DensityFloor + ring;
                double softened = r * r + SofteningRg * SofteningRg;
                // Monopole circular balance with the Gaussian ring pressure gradient, not exact binary equilibrium.
                double v2 = r * r / Math.Pow(softened, 1.5)
                    - SoundSpeedRgPerTg * SoundSpeedRgPerTg * r * (r - ringRadius) / (width * width);
                double speed = Math.Sqrt(Math.Max(0, v2)) * ring / rho;
                return r > 0 ? new GeneratedFluidSample(rho, -speed * z / r, speed * x / r)
                    : new GeneratedFluidSample(rho, 0, 0);
            });
            int count = settings.GridSize * settings.GridSize, total = checked(count * settings.FrameCount);
            float[] densities = new float[total], velocitiesX = new float[total], velocitiesZ = new float[total];
            double[] times = new double[settings.FrameCount];
            GeneratedFluidDiagnostics[] diagnostics = new GeneratedFluidDiagnostics[settings.FrameCount];
            double tau = 0;
            for (int frame = 0; frame < settings.FrameCount; frame++)
            {
                token.ThrowIfCancellationRequested();
                double targetTg = durationTg * frame / (settings.FrameCount - 1);
                while (tau < targetTg)
                {
                    token.ThrowIfCancellationRequested();
                    double remaining = targetTg - tau;
                    if (remaining <= 1e-12 * Math.Max(1, targetTg)) { tau = targetTg; break; }
                    double maximum = Math.Min(2, remaining);
                    // Global mass bound covers the force kick independently of body positions.
                    GeneratedFluidForcing initialForcing = GeneratedFluidForcing.FromSnapshot(scenario.Evaluate(start + tau * tg));
                    double dt = solver.StableStep(maximum, initialForcing);
                    GeneratedFluidForcing midpoint = GeneratedFluidForcing.FromSnapshot(scenario.Evaluate(start + (tau + .5 * dt) * tg));
                    double allowed = solver.StableStep(dt, midpoint);
                    if (allowed < dt) { dt = allowed; midpoint = GeneratedFluidForcing.FromSnapshot(scenario.Evaluate(start + (tau + .5 * dt) * tg)); }
                    // Step performs the final stability check.
                    solver.Step(dt, midpoint); tau += dt;
                }
                times[frame] = frame == settings.FrameCount - 1 ? scenario.DurationSeconds : start + targetTg * tg;
                solver.CopyFrame(densities, velocitiesX, velocitiesZ, frame * count);
                diagnostics[frame] = solver.Diagnostics;
                progress?.Invoke((frame + 1.0) / settings.FrameCount);
            }
            token.ThrowIfCancellationRequested();
            return new GeneratedFluidHistory(scenario, settings, start, extent, densities, velocitiesX, velocitiesZ, times, diagnostics);
        }
        public bool TryGetFrames(double seconds, out int lower, out int upper, out double blend)
        {
            lower = upper = 0; blend = 0;
            if (!NumericalEventHistory.IsFinite(seconds) || seconds < StartSeconds || seconds > EndSeconds) return false;
            int index = Array.BinarySearch(frameTimes, seconds);
            if (index >= 0) { lower = upper = index; return true; }
            upper = ~index; lower = upper - 1;
            blend = (seconds - frameTimes[lower]) / (frameTimes[upper] - frameTimes[lower]); return true;
        }
        public void CopyFrame(int index, float[] surfaceDensity, float[] velocityX, float[] velocityZ)
        {
            int count = GridSize * GridSize;
            if (index < 0 || index >= FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (surfaceDensity == null || velocityX == null || velocityZ == null || surfaceDensity.Length < count || velocityX.Length < count || velocityZ.Length < count)
                throw new ArgumentException("Three non-null destination arrays must each hold one grid.");
            Array.Copy(sigma, index * count, surfaceDensity, 0, count); Array.Copy(vx, index * count, velocityX, 0, count);
            Array.Copy(vz, index * count, velocityZ, 0, count);
        }
        public bool TrySample(double seconds, double xRg, double zRg, out GeneratedFluidSample sample)
        {
            sample = default;
            if (!NumericalEventHistory.IsFinite(xRg) || !NumericalEventHistory.IsFinite(zRg)
                || Math.Abs(xRg) > HalfExtentRg || Math.Abs(zRg) > HalfExtentRg
                || !TryGetFrames(seconds, out int lo, out int hi, out double blend)) return false;
            double x = Math.Max(0, Math.Min(GridSize - 1, (xRg + HalfExtentRg) / CellWidthRg - .5));
            double z = Math.Max(0, Math.Min(GridSize - 1, (zRg + HalfExtentRg) / CellWidthRg - .5));
            int ix = (int)x, iz = (int)z, jx = Math.Min(ix + 1, GridSize - 1), jz = Math.Min(iz + 1, GridSize - 1);
            double Read(float[] values, int frame)
            {
                int offset = frame * GridSize * GridSize;
                double a = Lerp(values[offset + iz * GridSize + ix], values[offset + iz * GridSize + jx], x - ix);
                double b = Lerp(values[offset + jz * GridSize + ix], values[offset + jz * GridSize + jx], x - ix);
                return Lerp(a, b, z - iz);
            }
            double Field(float[] values) => Lerp(Read(values, lo), Read(values, hi), blend);
            sample = new GeneratedFluidSample(Field(sigma), Field(vx), Field(vz)); return true;
        }
        public static double ReconstructedVolumeDensity(double surfaceDensity, double yRg)
        {
            if (!NumericalEventHistory.IsFinite(surfaceDensity) || surfaceDensity < 0 || !NumericalEventHistory.IsFinite(yRg))
                throw new ArgumentOutOfRangeException(nameof(surfaceDensity));
            return surfaceDensity * Math.Exp(-.5 * yRg * yRg / (HeightRg * HeightRg)) / (Math.Sqrt(2 * Math.PI) * HeightRg);
        }
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
