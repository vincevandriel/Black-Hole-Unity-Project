using System;

namespace BlackHole.Staging.Simulation
{
    // Prescribed circular flow around an isolated hole; no fluid, magnetic or thermal evolution.
    // Disk: XZ, normal +Y. Units: rg=GM/c^2 and tg=GM/c^3. No GW150914 gas reconstruction.
    public sealed class ReferenceFlowModel
    {
        public const string ModelId = "assumed-isolated-thick-torus-v1";
        public const double InnerRadiusRg = 6;
        public const double OuterRadiusRg = 18;
        public const double HalfExtentXZ = 20;
        public const double HalfExtentY = 6;
        public const double TemperatureScaleKelvin = 1e7;
        public const double GrayExtinctionPerRg = .3;
        public const int DefaultResolution = 32;
        public const double FrameSpacingTg = 8;
        public const double DurationTg = 1024;
        public const int FrameCount = 129;
        public const double PatternContrast = .3;
        // Normalized mass integral rho/rho0 dV/rg^3; azimuthal contrast integrates to zero.
        public static double IntegratedRelativeDensityVolumeRg3 => .64 * Math.PI * (936 - 432 / (Math.PI * Math.PI));

        public ReferenceFlowSample Evaluate(double xRg, double yRg, double zRg, double timeTg)
        {
            RequireFinite(xRg, nameof(xRg)); RequireFinite(yRg, nameof(yRg));
            RequireFinite(zRg, nameof(zRg)); timeTg = ClampTime(timeTg);
            return EvaluateUnchecked(xRg, yRg, zRg, timeTg);
        }

        internal ReferenceFlowSample EvaluateUnchecked(double x, double y, double z, double time)
        {
            double radius = Math.Sqrt(x * x + z * z);
            if (radius <= InnerRadiusRg || radius >= OuterRadiusRg) return default;
            double heightRatio = y / (.30 * radius);
            if (Math.Abs(heightRatio) >= 1) return default;
            double radial = Math.Sin(Math.PI * (radius - InnerRadiusRg) / (OuterRadiusRg - InnerRadiusRg));
            double vertical = 1 - heightRatio * heightRatio;
            double omega = AngularVelocityPerTg(radius);
            double phase = Math.Atan2(z, x) - omega * time;
            double density = radial * radial * vertical * vertical * (1 + PatternContrast * Math.Cos(2 * phase));
            double temperature = TemperatureScaleKelvin * Math.Pow(radius / 8, -.75);
            return new ReferenceFlowSample(density, temperature, -omega * z, omega * x);
        }

        // Prescribe Schwarzschild equatorial circular frequency throughout the torus; off-plane flow is not geodesic.
        public static double AngularVelocityPerTg(double cylindricalRadiusRg)
        {
            RequireFinite(cylindricalRadiusRg, nameof(cylindricalRadiusRg));
            if (cylindricalRadiusRg < InnerRadiusRg || cylindricalRadiusRg > OuterRadiusRg)
                throw new ArgumentOutOfRangeException(nameof(cylindricalRadiusRg));
            return 1 / Math.Pow(cylindricalRadiusRg, 1.5);
        }

        public static double ClampTime(double timeTg)
        {
            RequireFinite(timeTg, nameof(timeTg));
            return Math.Max(0, Math.Min(DurationTg, timeTg));
        }

        internal static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }

    public readonly struct ReferenceFlowSample
    {
        public double RelativeDensity { get; }
        public double TemperatureKelvin { get; }
        public double VelocityXOverC { get; }
        public double VelocityZOverC { get; }
        public double ExtinctionPerRg => ReferenceFlowModel.GrayExtinctionPerRg * RelativeDensity;

        public ReferenceFlowSample(double density, double temperature, double vx, double vz)
        { RelativeDensity = density; TemperatureKelvin = temperature; VelocityXOverC = vx; VelocityZOverC = vz; }
    }
}
