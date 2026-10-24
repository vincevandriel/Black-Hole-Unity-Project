#nullable enable
using System;
using System.IO;
using System.Threading;

namespace BlackHole.Staging.Simulation
{
    public sealed class MiniDiskSettings
    {
        public int GridSize { get; }
        public int FrameCount { get; }
        public double MaximumDurationTg { get; }
        public double? EndSecondsOverride { get; }
        public double InitializationLeadTg { get; }
        public MiniDiskSettings(int gridSize = 128, int frameCount = 513, double maximumDurationTg = 4000,
            double? endSecondsOverride = null, double initializationLeadTg = 1000)
        {
            if (gridSize < 32 || gridSize > 256) throw new ArgumentOutOfRangeException(nameof(gridSize));
            if (frameCount < 2 || frameCount > 4097) throw new ArgumentOutOfRangeException(nameof(frameCount));
            if (!NumericalEventHistory.IsFinite(maximumDurationTg) || maximumDurationTg <= 0 || maximumDurationTg > 4000)
                throw new ArgumentOutOfRangeException(nameof(maximumDurationTg));
            if (endSecondsOverride.HasValue && (!NumericalEventHistory.IsFinite(endSecondsOverride.Value) || endSecondsOverride.Value <= 0))
                throw new ArgumentOutOfRangeException(nameof(endSecondsOverride));
            if (!NumericalEventHistory.IsFinite(initializationLeadTg) || initializationLeadTg <= 0 || initializationLeadTg > 4000)
                throw new ArgumentOutOfRangeException(nameof(initializationLeadTg));
            GridSize = gridSize; FrameCount = frameCount; MaximumDurationTg = maximumDurationTg;
            EndSecondsOverride = endSecondsOverride; InitializationLeadTg = initializationLeadTg;
        }
    }

    public sealed class MiniDiskGenerationReport
    {
        public GeneratedFluidDiagnostics Diagnostics { get; internal set; }
        public double StartSeconds { get; internal set; }
        public double EndSeconds { get; internal set; }
        public double HalfExtentRg { get; internal set; }
        public double MaximumEncodedMassRelativeError { get; internal set; }
        public double MaximumDenseVelocityAbsoluteError { get; internal set; }
        public long FileBytes { get; internal set; }
        public long WorkingArrayBytes { get; internal set; }
        public double InitialRadialCellsA { get; internal set; }
        public double InitialRadialCellsB { get; internal set; }
    }

    // Conditional, inviscid 2D isothermal Euler gas driven by prescribed coordinate tracks.
    public static class MiniDiskSimulation
    {
        public const string ModelId = "finite-minidisks-newtonian-muscl-ssprk2-v1";
        public const double HalfExtentRg = 18;
        public const double SoundSpeed = .04;
        public const double DensityFloor = 1e-7;
        public const double VacuumVelocityCutoff = 1e-6;
        public const double SofteningHorizonRatio = .5;
        public const double SinkHorizonRatio = 1.1;
        public const double SinkTimeTg = 4;
        public const double PerturbationAmplitude = .1;

        public static MiniDiskGenerationReport GenerateCache(InteractiveBinaryScenario scenario, string path,
            int gridSize = 128, int frameCount = 513, CancellationToken cancellation = default,
            Action<double>? progress = null, string sourceSha256 = "") =>
            GenerateCache(scenario, path, new MiniDiskSettings(gridSize, frameCount), cancellation, progress, sourceSha256);

        public static MiniDiskGenerationReport GenerateCache(InteractiveBinaryScenario scenario, string path,
            MiniDiskSettings settings, CancellationToken cancellation = default, Action<double>? progress = null,
            string sourceSha256 = "")
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            cancellation.ThrowIfCancellationRequested();
            double tg = scenario.GravitationalTimeSeconds;
            double start = Math.Max(0, Math.Max(scenario.DurationSeconds - settings.MaximumDurationTg * tg,
                scenario.MergerTimeSeconds - settings.InitializationLeadTg * tg));
            double end = Math.Min(scenario.DurationSeconds, settings.EndSecondsOverride ?? scenario.DurationSeconds);
            if (end <= start) throw new ArgumentException("The selected interval has no covered evolution.");
            var initial = scenario.Evaluate(start);
            if (!initial.BodyA.HasValue || !initial.BodyB.HasValue)
                throw new ArgumentException("Mini-disks must be initialized before the binary ends.");
            double extent = DomainHalfExtent(initial), dx = 2 * extent / settings.GridSize;
            var solver = new GeneratedFluidSolver(settings.GridSize, extent, SoundSpeed, DensityFloor,
                (x, z) => InitialSample(initial, x, z));
            int count = settings.GridSize * settings.GridSize;
            var sigma = new float[count]; var vx = new float[count]; var vz = new float[count];
            var report = new MiniDiskGenerationReport { StartSeconds = start, EndSeconds = end, HalfExtentRg = extent,
                InitialRadialCellsA = RadialSpan(initial, true) / dx, InitialRadialCellsB = RadialSpan(initial, false) / dx };
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            string temporary = fullPath + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var writer = new MiniDiskCacheWriter(temporary, scenario, initial, settings, start, end, extent, sourceSha256))
                {
                    double tau = 0, durationTg = (end - start) / tg;
                    for (int frame = 0; frame < settings.FrameCount; frame++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        double target = durationTg * frame / (settings.FrameCount - 1);
                        while (tau < target)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            double remaining = target - tau;
                            if (remaining <= 1e-12 * Math.Max(1, target)) { tau = target; break; }
                            var forcing = Forcing(scenario.Evaluate(start + tau * tg));
                            double dt = solver.StableStep(Math.Min(1, remaining), forcing);
                            var midpoint = Forcing(scenario.Evaluate(start + (tau + .5 * dt) * tg));
                            double allowed = solver.StableStep(dt, midpoint);
                            if (allowed < dt)
                            { dt = allowed; midpoint = Forcing(scenario.Evaluate(start + (tau + .5 * dt) * tg)); }
                            solver.Step(dt, midpoint); tau += dt;
                        }
                        solver.CopyFrame(sigma, vx, vz);
                        report.Diagnostics = solver.Diagnostics;
                        writer.WriteFrame(frame, frame == settings.FrameCount - 1 ? end : start + target * tg,
                            sigma, vx, vz, report.Diagnostics, report);
                        progress?.Invoke((frame + 1.0) / settings.FrameCount);
                    }
                    writer.Complete();
                    report.WorkingArrayBytes = solver.WorkingMemoryBytes + 3L * count * sizeof(float) + writer.WorkingArrayBytes;
                }
                cancellation.ThrowIfCancellationRequested();
                // Never expose an unfinished cache to the playback reader.
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.Move(temporary, fullPath);
                report.FileBytes = new FileInfo(fullPath).Length;
                return report;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static GeneratedFluidForcing Forcing(InteractiveBinarySnapshot snapshot)
        {
            GeneratedFluidSource Source(InteractiveBinaryBody body) => new GeneratedFluidSource(body.PositionRg.X,
                body.PositionRg.Z, body.MassFraction, SofteningHorizonRatio * body.HorizonRadiusRg,
                SinkHorizonRatio * body.HorizonRadiusRg, SinkTimeTg);
            // Common-horizon formation replaces the two Newtonian sources; this is not a GR metric.
            return snapshot.Remnant.HasValue ? new GeneratedFluidForcing(Source(snapshot.Remnant.Value)) :
                new GeneratedFluidForcing(snapshot.BodyA.HasValue ? Source(snapshot.BodyA.Value) : default,
                    snapshot.BodyB.HasValue ? Source(snapshot.BodyB.Value) : default);
        }

        public static double OuterRadius(InteractiveBinarySnapshot snapshot, bool primary)
        {
            double q = primary ? snapshot.Mass1Solar / snapshot.Mass2Solar : snapshot.Mass2Solar / snapshot.Mass1Solar;
            return .8 * InteractiveBinaryScenario.RocheLobeFraction(q) * snapshot.SeparationRg;
        }
        public static double DomainHalfExtent(InteractiveBinarySnapshot initial)
        {
            double Bounds(InteractiveBinaryBody? body, bool primary) => !body.HasValue ? 0 :
                Math.Max(Math.Abs(body.Value.PositionRg.X), Math.Abs(body.Value.PositionRg.Z)) + OuterRadius(initial, primary);
            double bound = Math.Max(Bounds(initial.BodyA, true), Bounds(initial.BodyB, false));
            // Leave 15% of the compact replay domain outside both initialized disks.
            return initial.Mode == InteractiveScenarioMode.RepresentativeReplay && bound < .85 * 12 ? 12 : HalfExtentRg;
        }
        private static double RadialSpan(InteractiveBinarySnapshot snapshot, bool primary)
        {
            var body = primary ? snapshot.BodyA!.Value : snapshot.BodyB!.Value;
            return Math.Max(0, OuterRadius(snapshot, primary) - 1.3 * body.HorizonRadiusRg);
        }

        public static GeneratedFluidSample InitialSample(InteractiveBinarySnapshot snapshot, double x, double z)
        {
            double density = DensityFloor, px = 0, pz = 0;
            void Add(ReducedBinaryFlowBody body, bool primary)
            {
                double dx = x - body.PositionRg.X, dz = z - body.PositionRg.Z, r = Math.Sqrt(dx * dx + dz * dz);
                double inner = 1.3 * body.HorizonRadiusRg, outer = OuterRadius(snapshot, primary);
                if (r <= inner || r >= outer || outer <= inner) return;
                double u = (r - inner) / (outer - inner), phi = Math.Atan2(dz, dx);
                double rho = 16 * u * u * (1 - u) * (1 - u)
                    * (1 + PerturbationAmplitude * Math.Cos(2 * phi + (primary ? 0 : Math.PI / 3)));
                double eps = SofteningHorizonRatio * body.HorizonRadiusRg;
                // Softened circular speed is an initial condition, not exact binary/pressure equilibrium.
                double speed = Math.Sqrt(body.MassFraction * r * r / Math.Pow(r * r + eps * eps, 1.5));
                density += rho;
                px += rho * (body.VelocityOverC.X - speed * dz / r);
                pz += rho * (body.VelocityOverC.Z + speed * dx / r);
            }
            if (snapshot.BodyA.HasValue) Add(snapshot.GasState.BodyA, true);
            if (snapshot.BodyB.HasValue) Add(snapshot.GasState.BodyB, false);
            return new GeneratedFluidSample(density, px / density, pz / density);
        }
    }
}
