#nullable enable
using System;
using System.IO;
using System.Threading;

namespace BlackHole.Staging.Simulation
{
    public sealed class MiniDiskMaterialReport
    {
        public int TracerCount { get; internal set; }
        public int[] Survivors { get; internal set; } = Array.Empty<int>();
        public double[] DensityWeightedRms { get; internal set; } = Array.Empty<double>();
        public double[] DensityCoverage { get; internal set; } = Array.Empty<double>();
        public long Substeps { get; internal set; }
        public long InvalidSpeedRemovals { get; internal set; }
        public long EmptyOrOutflowRemovals { get; internal set; }
        public double MinimumStepTg { get; internal set; } = double.PositiveInfinity;
        public double MaximumStepTg { get; internal set; }
        public long WorkingArrayBytes { get; internal set; }
        public long FileBytes { get; internal set; }
        public string FluidSha256 { get; internal set; } = "";
    }

    public static class MiniDiskMaterialTransport
    {
        // Material tags follow the cached coordinate velocity; invalid relativistic speeds reject the tag.
        public static bool AdvanceRk2(double x, double z, double timeTg, double dtTg,
            Func<double, double, double, GeneratedFluidSample> field, out double nextX, out double nextZ,
            double minimumDensity = 0) => Advance(x, z, timeTg, dtTg, field, out nextX, out nextZ, out _, minimumDensity);
        internal static bool Advance(double x, double z, double timeTg, double dtTg,
            Func<double, double, double, GeneratedFluidSample> field, out double nextX, out double nextZ,
            out bool invalidSpeed, double minimumDensity)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            if (!NumericalEventHistory.IsFinite(dtTg) || dtTg <= 0) throw new ArgumentOutOfRangeException(nameof(dtTg));
            var a = field(timeTg, x, z); nextX = x; nextZ = z;
            invalidSpeed = SpeedInvalid(a); if (!Valid(a, minimumDensity)) return false;
            var b = field(timeTg + .5 * dtTg, x + .5 * dtTg * a.VelocityXRgPerTg, z + .5 * dtTg * a.VelocityZRgPerTg);
            invalidSpeed = SpeedInvalid(b); if (!Valid(b, minimumDensity)) return false;
            nextX += dtTg * b.VelocityXRgPerTg; nextZ += dtTg * b.VelocityZRgPerTg;
            var c = field(timeTg + dtTg, nextX, nextZ); invalidSpeed = SpeedInvalid(c); return Valid(c, minimumDensity);
        }
        private static bool SpeedInvalid(GeneratedFluidSample sample) => !NumericalEventHistory.IsFinite(sample.VelocityXRgPerTg)
            || !NumericalEventHistory.IsFinite(sample.VelocityZRgPerTg)
            || sample.VelocityXRgPerTg * sample.VelocityXRgPerTg + sample.VelocityZRgPerTg * sample.VelocityZRgPerTg >= 1;
        private static bool Valid(GeneratedFluidSample sample, double minimumDensity) =>
            NumericalEventHistory.IsFinite(sample.SurfaceDensity) && sample.SurfaceDensity > minimumDensity
            && NumericalEventHistory.IsFinite(sample.VelocityXRgPerTg) && NumericalEventHistory.IsFinite(sample.VelocityZRgPerTg)
            && sample.VelocityXRgPerTg * sample.VelocityXRgPerTg + sample.VelocityZRgPerTg * sample.VelocityZRgPerTg < 1;
    }

    // Passive material tags change no fluid fields and are never respawned.
    public static class MiniDiskMaterialAdvection
    {
        public const string ModelId = "passive-material-rk2-splats-v1";
        public const int DefaultTracerCount = 8192;
        public const double MinimumDensity = 1e-5;
        public const double SplatSigmaCells = 1.25;
        public const double SplatSupportCells = 3.5;
        public const double DefaultCourant = .75;
        public const uint Seed = 0x4d445431;
        public static string SidecarPath(string fluidPath) => Path.ChangeExtension(fluidPath, ".mdt");
        public static MiniDiskMaterialReport GenerateCache(string fluidPath, string detailPath,
            CancellationToken cancellation = default, Action<double>? progress = null,
            int tracerCount = DefaultTracerCount, double maximumCourant = DefaultCourant)
        {
            if (tracerCount < 256 || tracerCount > 32768) throw new ArgumentOutOfRangeException(nameof(tracerCount));
            if (!NumericalEventHistory.IsFinite(maximumCourant) || maximumCourant <= 0 || maximumCourant > 1)
                throw new ArgumentOutOfRangeException(nameof(maximumCourant));
            cancellation.ThrowIfCancellationRequested();
            using (var fluid = MiniDiskCache.Open(fluidPath))
            {
                var report = new MiniDiskMaterialReport { TracerCount = tracerCount, Survivors = new int[fluid.FrameCount],
                    DensityWeightedRms = new double[fluid.FrameCount], DensityCoverage = new double[fluid.FrameCount],
                    FluidSha256 = MiniDiskMaterialCache.ComputeFileSha256(fluidPath) };
                var fields = new Fields(fluid); var sample = new Func<double, double, double, GeneratedFluidSample>(fields.Sample);
                int count = fluid.GridSize * fluid.GridSize;
                var x = new double[tracerCount]; var z = new double[tracerCount]; var label = new float[tracerCount]; var alive = new bool[tracerCount];
                var numerator = new double[count]; var denominator = new double[count]; var concentration = new float[count];
                SeedTags(fluid, fields.SigmaCurrent, x, z, label, alive);
                string target = Path.GetFullPath(detailPath);
                if (string.Equals(target, Path.GetFullPath(fluidPath), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The material file must differ from the fluid file.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                string temporary = target + ".partial-" + Guid.NewGuid().ToString("N");
                try
                {
                    using (var writer = new MiniDiskMaterialWriter(temporary, fluid, report.FluidSha256))
                    {
                        double tau = 0;
                        for (int frame = 0; frame < fluid.FrameCount; frame++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (frame > 0)
                            {
                                fields.LoadNext(frame);
                                double end = (fluid.GetFrameTime(frame) - fluid.StartSeconds) / fluid.GravitationalTimeSeconds;
                                double maximum = Math.Min(.5, maximumCourant * fluid.CellWidthRg / fields.MaximumValidSpeed);
                                while (tau < end)
                                {
                                    cancellation.ThrowIfCancellationRequested(); double dt = Math.Min(maximum, end - tau);
                                    if (dt < 1e-12 * Math.Max(1, end)) { tau = end; break; }
                                    for (int i = 0; i < tracerCount; i++) if (alive[i])
                                    {
                                        if (MiniDiskMaterialTransport.Advance(x[i], z[i], tau, dt, sample,
                                            out double nx, out double nz, out bool invalidSpeed, MinimumDensity)) { x[i] = nx; z[i] = nz; }
                                        else
                                        {
                                            if (invalidSpeed) report.InvalidSpeedRemovals++; else report.EmptyOrOutflowRemovals++;
                                            alive[i] = false;
                                        }
                                    }
                                    tau += dt; report.Substeps++; report.MinimumStepTg = Math.Min(report.MinimumStepTg, dt);
                                    report.MaximumStepTg = Math.Max(report.MaximumStepTg, dt);
                                }
                            }
                            Reconstruct(fluid, fields.SigmaCurrent, fields.VxCurrent, fields.VzCurrent,
                                x, z, label, alive, numerator, denominator, concentration,
                                out int survivors, out double rms, out double coverage);
                            report.Survivors[frame] = survivors; report.DensityWeightedRms[frame] = rms; report.DensityCoverage[frame] = coverage;
                            writer.WriteFrame(frame, concentration); progress?.Invoke((frame + 1.0) / fluid.FrameCount);
                        }
                        writer.Complete();
                        report.WorkingArrayBytes = fields.WorkingArrayBytes + 21L * tracerCount + 20L * count
                            + writer.WorkingArrayBytes + report.Survivors.LongLength * 20;
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if (File.Exists(target)) File.Delete(target); File.Move(temporary, target);
                    report.FileBytes = new FileInfo(target).Length; return report;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private static void SeedTags(MiniDiskCache fluid, float[] sigma, double[] x, double[] z, float[] label, bool[] alive)
        {
            double total = 0; for (int i = 0; i < sigma.Length; i++) total += Math.Max(0, sigma[i] - MinimumDensity);
            if (total <= 0) return;
            int cell = 0; double cumulative = Math.Max(0, sigma[0] - MinimumDensity);
            for (int i = 0; i < x.Length; i++)
            {
                double target = total * (i + .5) / x.Length;
                while (cumulative < target && cell + 1 < sigma.Length) cumulative += Math.Max(0, sigma[++cell] - MinimumDensity);
                double px = cell % fluid.GridSize + Unit((uint)i * 2 + Seed), pz = cell / fluid.GridSize + Unit((uint)i * 2 + 1 + Seed);
                x[i] = -fluid.HalfExtentRg + px * fluid.CellWidthRg; z[i] = -fluid.HalfExtentRg + pz * fluid.CellWidthRg;
                label[i] = (float)(.6 * Patch(px / 4, pz / 4) + .4 * Patch(px / 8, pz / 8)); alive[i] = true;
            }
        }
        private static double Unit(uint v)
        { unchecked { v ^= v >> 16; v *= 0x7feb352d; v ^= v >> 15; v *= 0x846ca68b; v ^= v >> 16; } return (v + .5) / 4294967296.0; }
        private static double Patch(double x, double z)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z); double u = x - ix, v = z - iz;
            u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
            double At(int a, int b) => 2 * Unit(unchecked((uint)a * 73856093 ^ (uint)b * 19349663 ^ Seed)) - 1;
            return (1 - v) * ((1 - u) * At(ix, iz) + u * At(ix + 1, iz))
                + v * ((1 - u) * At(ix, iz + 1) + u * At(ix + 1, iz + 1));
        }
        private static void Reconstruct(MiniDiskCache fluid, float[] sigma, float[] vx, float[] vz,
            double[] x, double[] z, float[] label, bool[] alive, double[] numerator, double[] denominator,
            float[] result, out int survivors, out double rms, out double coverage)
        {
            Array.Clear(numerator, 0, numerator.Length); Array.Clear(denominator, 0, denominator.Length);
            survivors = 0; int n = fluid.GridSize; double width = fluid.CellWidthRg;
            for (int i = 0; i < x.Length; i++) if (alive[i])
            {
                survivors++; double px = (x[i] + fluid.HalfExtentRg) / width - .5, pz = (z[i] + fluid.HalfExtentRg) / width - .5;
                int x0 = Math.Max(0, (int)Math.Ceiling(px - SplatSupportCells)), x1 = Math.Min(n - 1, (int)Math.Floor(px + SplatSupportCells));
                int z0 = Math.Max(0, (int)Math.Ceiling(pz - SplatSupportCells)), z1 = Math.Min(n - 1, (int)Math.Floor(pz + SplatSupportCells));
                for (int jz = z0; jz <= z1; jz++) for (int jx = x0; jx <= x1; jx++)
                {
                    double r2 = (jx - px) * (jx - px) + (jz - pz) * (jz - pz);
                    if (r2 >= SplatSupportCells * SplatSupportCells) continue;
                    double taper = 1 - r2 / (SplatSupportCells * SplatSupportCells);
                    double w = Math.Exp(-.5 * r2 / (SplatSigmaCells * SplatSigmaCells)) * taper * taper;
                    int k = jz * n + jx; numerator[k] += w * label[i]; denominator[k] += w;
                }
            }
            double mass = 0, colouredMass = 0, sum = 0;
            for (int i = 0; i < result.Length; i++)
            {
                double v2 = vx[i] * vx[i] + vz[i] * vz[i], rho = sigma[i];
                double mask = Math.Max(0, Math.Min(1, (rho - MinimumDensity) / (9 * MinimumDensity)));
                double c = rho > MinimumDensity && v2 < 1 ? mask * numerator[i] / (denominator[i] + .1) : 0;
                result[i] = (float)Math.Max(-1, Math.Min(1, c));
                mass += rho; sum += rho * c * c; if (denominator[i] > .1 && rho > MinimumDensity && v2 < 1) colouredMass += rho;
            }
            rms = mass > 0 ? Math.Sqrt(sum / mass) : 0; coverage = mass > 0 ? colouredMass / mass : 0;
        }

        private sealed class Fields
        {
            private readonly MiniDiskCache fluid;
            private float[] s0, x0, z0, s1, x1, z1;
            private double t0, t1;
            public float[] SigmaCurrent => s1;
            public float[] VxCurrent => x1;
            public float[] VzCurrent => z1;
            public double MaximumValidSpeed { get; private set; }
            public long WorkingArrayBytes => 6L * s0.Length * sizeof(float) + fluid.ResidentPayloadBytes;
            internal Fields(MiniDiskCache fluid)
            {
                this.fluid = fluid; int count = fluid.GridSize * fluid.GridSize;
                s0 = new float[count]; x0 = new float[count]; z0 = new float[count];
                s1 = new float[count]; x1 = new float[count]; z1 = new float[count];
                fluid.ReadFrame(0, s1, x1, z1); Array.Copy(s1, s0, count); Array.Copy(x1, x0, count); Array.Copy(z1, z0, count);
            }
            internal void LoadNext(int index)
            {
                void Swap(ref float[] a, ref float[] b) { var temporary = a; a = b; b = temporary; }
                Swap(ref s0, ref s1); Swap(ref x0, ref x1); Swap(ref z0, ref z1);
                fluid.ReadFrame(index, s1, x1, z1);
                t0 = (fluid.GetFrameTime(index - 1) - fluid.StartSeconds) / fluid.GravitationalTimeSeconds;
                t1 = (fluid.GetFrameTime(index) - fluid.StartSeconds) / fluid.GravitationalTimeSeconds;
                MaximumValidSpeed = .04;
                for (int i = 0; i < s0.Length; i++)
                {
                    double a = x0[i] * x0[i] + z0[i] * z0[i], b = x1[i] * x1[i] + z1[i] * z1[i];
                    if (s0[i] > MinimumDensity && a < 1) MaximumValidSpeed = Math.Max(MaximumValidSpeed, Math.Sqrt(a));
                    if (s1[i] > MinimumDensity && b < 1) MaximumValidSpeed = Math.Max(MaximumValidSpeed, Math.Sqrt(b));
                }
            }
            internal GeneratedFluidSample Sample(double time, double x, double z)
            {
                if (Math.Abs(x) > fluid.HalfExtentRg || Math.Abs(z) > fluid.HalfExtentRg) return default;
                double mix = t1 > t0 ? Math.Max(0, Math.Min(1, (time - t0) / (t1 - t0))) : 0;
                var a = fluid.SampleFrame(s0, x0, z0, x, z); var b = fluid.SampleFrame(s1, x1, z1, x, z);
                return new GeneratedFluidSample(a.SurfaceDensity + mix * (b.SurfaceDensity - a.SurfaceDensity),
                    a.VelocityXRgPerTg + mix * (b.VelocityXRgPerTg - a.VelocityXRgPerTg),
                    a.VelocityZRgPerTg + mix * (b.VelocityZRgPerTg - a.VelocityZRgPerTg));
            }
        }
    }
}
