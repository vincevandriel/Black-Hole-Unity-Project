using System;

namespace BlackHole.Staging.Simulation
{
    /// <summary>
    /// Exactly two allocated decoded frames. This small synchronous reference reconstructs indexed
    /// frames analytically; it does not preload a history or provide past-light-cone access.
    /// A snapshot's buffers are borrowed until the next Seek call. Upload both before publishing time.
    /// </summary>
    public sealed class ReferenceFlowCache
    {
        private readonly ReferenceFlowModel model;
        private ReferenceFlowFrame lower, upper;
        public int Resolution { get; }
        public long AllocatedFieldBytes => 2L * Resolution * Resolution * Resolution * 4 * sizeof(float);
        public int ResidentFrameCount => 2;
        public int GeneratedFrameCount { get; private set; }
        public long Generation { get; private set; }
        public ReferenceFlowSnapshot Snapshot { get; private set; }

        public ReferenceFlowCache(ReferenceFlowModel model, int resolution = 32)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            if (resolution < 8 || resolution > 128) throw new ArgumentOutOfRangeException(nameof(resolution));
            Resolution = resolution;
            lower = new ReferenceFlowFrame(resolution); upper = new ReferenceFlowFrame(resolution);
            Seek(0);
        }

        public ReferenceFlowSnapshot Seek(double timeTg)
        {
            double time = ReferenceFlowModel.ClampTime(timeTg);
            int lo = Math.Min(ReferenceFlowModel.FrameCount - 2, (int)Math.Floor(time / ReferenceFlowModel.FrameSpacingTg));
            int hi = lo + 1;
            if (lower.Index == hi || upper.Index == lo)
            { ReferenceFlowFrame swap = lower; lower = upper; upper = swap; }
            if (lower.Index != lo) Fill(lower, lo);
            if (upper.Index != hi) Fill(upper, hi);
            ++Generation;
            Snapshot = new ReferenceFlowSnapshot(lower, upper,
                (time - lo * ReferenceFlowModel.FrameSpacingTg) / ReferenceFlowModel.FrameSpacingTg, time, Generation);
            return Snapshot;
        }

        private void Fill(ReferenceFlowFrame frame, int index)
        {
            double time = index * ReferenceFlowModel.FrameSpacingTg;
            int n = Resolution, output = 0;
            for (int z = 0; z < n; ++z)
                for (int y = 0; y < n; ++y)
                    for (int x = 0; x < n; ++x)
                    {
                        ReferenceFlowSample field = model.EvaluateUnchecked(
                            ((x + .5) / n * 2 - 1) * ReferenceFlowModel.HalfExtentXZ,
                            ((y + .5) / n * 2 - 1) * ReferenceFlowModel.HalfExtentY,
                            ((z + .5) / n * 2 - 1) * ReferenceFlowModel.HalfExtentXZ, time);
                        double rho = field.RelativeDensity;
                        frame.PackedRgba[output++] = (float)rho;
                        frame.PackedRgba[output++] = (float)(rho * field.TemperatureKelvin / ReferenceFlowModel.TemperatureScaleKelvin);
                        frame.PackedRgba[output++] = (float)(rho * field.VelocityXOverC);
                        frame.PackedRgba[output++] = (float)(rho * field.VelocityZOverC);
                    }
            frame.SetIndex(index); ++GeneratedFrameCount;
        }
    }

    public sealed class ReferenceFlowFrame
    {
        public int Index { get; private set; } = -1;
        public double TimeTg => Index * ReferenceFlowModel.FrameSpacingTg;
        public int Resolution { get; }
        // Borrowed read-only by contract. x-fastest float4: rho, rho*T/T0, rho*vx/c, rho*vz/c.
        // Suitable for Texture3D RGBAFloat, or explicitly measured RGBAHalf conversion.
        public float[] PackedRgba { get; }
        internal ReferenceFlowFrame(int resolution)
        { Resolution = resolution; PackedRgba = new float[checked(4 * resolution * resolution * resolution)]; }
        internal void SetIndex(int index) { Index = index; }

        public ReferenceFlowSample Sample(double xRg, double yRg, double zRg)
        {
            ReferenceFlowModel.RequireFinite(xRg, nameof(xRg)); ReferenceFlowModel.RequireFinite(yRg, nameof(yRg));
            ReferenceFlowModel.RequireFinite(zRg, nameof(zRg));
            if (Math.Abs(xRg) > ReferenceFlowModel.HalfExtentXZ || Math.Abs(yRg) > ReferenceFlowModel.HalfExtentY
                || Math.Abs(zRg) > ReferenceFlowModel.HalfExtentXZ) return default;
            Coordinates(xRg, ReferenceFlowModel.HalfExtentXZ, out int x, out int xx, out double fx);
            Coordinates(yRg, ReferenceFlowModel.HalfExtentY, out int y, out int yy, out double fy);
            Coordinates(zRg, ReferenceFlowModel.HalfExtentXZ, out int z, out int zz, out double fz);
            double rho = Channel(0, x, xx, y, yy, z, zz, fx, fy, fz);
            if (rho <= 0) return default;
            return new ReferenceFlowSample(rho,
                Channel(1, x, xx, y, yy, z, zz, fx, fy, fz) / rho * ReferenceFlowModel.TemperatureScaleKelvin,
                Channel(2, x, xx, y, yy, z, zz, fx, fy, fz) / rho,
                Channel(3, x, xx, y, yy, z, zz, fx, fy, fz) / rho);
        }

        private void Coordinates(double value, double halfExtent, out int lower, out int upper, out double fraction)
        {
            double voxel = Math.Max(0, Math.Min(Resolution - 1, (value / halfExtent + 1) * .5 * Resolution - .5));
            lower = (int)Math.Floor(voxel); upper = Math.Min(Resolution - 1, lower + 1); fraction = voxel - lower;
        }
        private double Channel(int component, int x, int xx, int y, int yy, int z, int zz, double fx, double fy, double fz)
        {
            double a = Lerp(Value(component, x, y, z), Value(component, xx, y, z), fx);
            double b = Lerp(Value(component, x, yy, z), Value(component, xx, yy, z), fx);
            double c = Lerp(Value(component, x, y, zz), Value(component, xx, y, zz), fx);
            double d = Lerp(Value(component, x, yy, zz), Value(component, xx, yy, zz), fx);
            return Lerp(Lerp(a, b, fy), Lerp(c, d, fy), fz);
        }
        private double Value(int component, int x, int y, int z) => PackedRgba[4 * (x + Resolution * (y + Resolution * z)) + component];
        internal static double Lerp(double a, double b, double f) => a + (b - a) * f;
    }

    public readonly struct ReferenceFlowSnapshot
    {
        public ReferenceFlowFrame Lower { get; }
        public ReferenceFlowFrame Upper { get; }
        public double Blend { get; }
        public double TimeTg { get; }
        public long Generation { get; }
        internal ReferenceFlowSnapshot(ReferenceFlowFrame lower, ReferenceFlowFrame upper, double blend, double time, long generation)
        { Lower = lower; Upper = upper; Blend = blend; TimeTg = time; Generation = generation; }

        public ReferenceFlowSample Sample(double xRg, double yRg, double zRg)
        {
            ReferenceFlowSample a = Lower.Sample(xRg, yRg, zRg), b = Upper.Sample(xRg, yRg, zRg);
            double rho = ReferenceFlowFrame.Lerp(a.RelativeDensity, b.RelativeDensity, Blend);
            if (rho <= 0) return default;
            return new ReferenceFlowSample(rho,
                ReferenceFlowFrame.Lerp(a.RelativeDensity * a.TemperatureKelvin, b.RelativeDensity * b.TemperatureKelvin, Blend) / rho,
                ReferenceFlowFrame.Lerp(a.RelativeDensity * a.VelocityXOverC, b.RelativeDensity * b.VelocityXOverC, Blend) / rho,
                ReferenceFlowFrame.Lerp(a.RelativeDensity * a.VelocityZOverC, b.RelativeDensity * b.VelocityZOverC, Blend) / rho);
        }
    }
}
