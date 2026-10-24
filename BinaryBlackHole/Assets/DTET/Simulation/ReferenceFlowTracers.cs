using System;

namespace BlackHole.Staging.Simulation
{
    // Optional markers follow prescribed circular paths; emission is independent of marker count.
    // Weights represent rho/rho0 dV/rg^3, not measured gas mass.
    public static class ReferenceFlowTracers
    {
        public static ReferenceFlowTracer[] Create(int count)
        {
            RequireCount(count);
            var model = new ReferenceFlowModel();
            var result = new ReferenceFlowTracer[count];
            double sum = 0; int largest = 0;
            for (int i = 0; i < count; ++i)
            {
                double radius = ReferenceFlowModel.InnerRadiusRg + 12 * RadicalInverse(i + 1, 2);
                double height = .30 * radius * (2 * RadicalInverse(i + 1, 3) - 1);
                double phase = 2 * Math.PI * RadicalInverse(i + 1, 5);
                double rho = model.EvaluateUnchecked(radius * Math.Cos(phase), height, radius * Math.Sin(phase), 0).RelativeDensity;
                // Uniform radius, normalized height and azimuth sampling has a Jacobian proportional to R^2.
                double weight = rho * radius * radius;
                sum += weight;
                result[i] = new ReferenceFlowTracer(i, radius, height, phase, weight);
                if (weight > result[largest].WeightRelativeMass) largest = i;
            }
            double total = ReferenceFlowModel.IntegratedRelativeDensityVolumeRg3;
            double written = 0;
            for (int i = 0; i < count; ++i)
            {
                double weight = result[i].WeightRelativeMass * total / sum;
                result[i] = result[i].WithWeight(weight); written += weight;
            }
            // Correct the largest weight; the last sample may be smaller than roundoff near the boundary.
            result[largest] = result[largest].WithWeight(result[largest].WeightRelativeMass + (total - written));
            return result;
        }

        // Resample the mass CDF at deterministic midpoints; duplicates preserve mass without adding field resolution.
        public static ReferenceFlowTracer[] Resample(ReferenceFlowTracer[] source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Length == 0) throw new ArgumentException("At least one source marker is required.", nameof(source));
            RequireCount(count);
            double total = 0;
            for (int i = 0; i < source.Length; ++i)
            {
                double weight = source[i].WeightRelativeMass;
                ReferenceFlowModel.RequireFinite(weight, nameof(source));
                if (weight <= 0) throw new ArgumentException("Source marker weights must be positive.", nameof(source));
                total += weight;
            }
            ReferenceFlowModel.RequireFinite(total, nameof(source));
            var result = new ReferenceFlowTracer[count];
            int selected = 0; double cumulative = source[0].WeightRelativeMass, written = 0;
            for (int i = 0; i < count; ++i)
            {
                double target = total * ((i + .5) / count);
                while (selected < source.Length - 1 && target > cumulative) cumulative += source[++selected].WeightRelativeMass;
                double weight = i == count - 1 ? total - written : total / count;
                ReferenceFlowTracer original = source[selected];
                result[i] = new ReferenceFlowTracer(i, original.RadiusRg, original.HeightRg, original.InitialAzimuthRadians, weight);
                written += weight;
            }
            return result;
        }

        public static void FillPositions(ReferenceFlowTracer[] tracers, double timeTg, ReferenceFlowPosition[] output)
        {
            if (tracers == null) throw new ArgumentNullException(nameof(tracers));
            if (output == null || output.Length < tracers.Length) throw new ArgumentException("Output capacity is too small.", nameof(output));
            double time = ReferenceFlowModel.ClampTime(timeTg);
            for (int i = 0; i < tracers.Length; ++i) output[i] = tracers[i].Evaluate(time);
        }
        private static void RequireCount(int count)
        { if (count < 1 || count > 65536) throw new ArgumentOutOfRangeException(nameof(count)); }
        private static double RadicalInverse(int index, int radix)
        {
            double value = 0, factor = 1.0 / radix;
            while (index > 0) { value += (index % radix) * factor; index /= radix; factor /= radix; }
            return value;
        }
    }

    public readonly struct ReferenceFlowTracer
    {
        public int Id { get; }
        public double RadiusRg { get; }
        public double HeightRg { get; }
        public double InitialAzimuthRadians { get; }
        public double WeightRelativeMass { get; }
        internal ReferenceFlowTracer(int id, double radius, double height, double phase, double weight)
        { Id = id; RadiusRg = radius; HeightRg = height; InitialAzimuthRadians = phase; WeightRelativeMass = weight; }
        internal ReferenceFlowTracer WithWeight(double weight) => new ReferenceFlowTracer(Id, RadiusRg, HeightRg, InitialAzimuthRadians, weight);
        public ReferenceFlowPosition Evaluate(double timeTg)
        {
            double time = ReferenceFlowModel.ClampTime(timeTg);
            double phase = InitialAzimuthRadians + ReferenceFlowModel.AngularVelocityPerTg(RadiusRg) * time;
            return new ReferenceFlowPosition(RadiusRg * Math.Cos(phase), HeightRg, RadiusRg * Math.Sin(phase));
        }
    }

    public readonly struct ReferenceFlowPosition
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public ReferenceFlowPosition(double x, double y, double z) { X = x; Y = y; Z = z; }
    }
}
