using System;

namespace BlackHole.Staging.Simulation
{
    // Hypothetical solar-radius star at one AU, mapped by a thin point-mass lens with a bounded core.
    // This is not a binary spacetime model or an observed GW150914 companion.
    public static class StellarLensReference
    {
        public const double AstronomicalUnitMeters = 149597870700;
        public const double SolarRadiusMeters = 695700000;
        public const double MaximumDeflectionRadians = 1;
        public const double CoreHorizonFraction = .25;

        public static void SourceDimensions(double gravitationalRadiusMeters, out double distanceRg, out double radiusRg)
        {
            if (!Finite(gravitationalRadiusMeters) || gravitationalRadiusMeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(gravitationalRadiusMeters));
            distanceRg = AstronomicalUnitMeters / gravitationalRadiusMeters;
            radiusRg = SolarRadiusMeters / gravitationalRadiusMeters;
        }

        public static double EinsteinRadiusSquared(double lensDepthRg, double massFraction, double sourceDepthRg, double gain = 1)
        {
            if (!Finite(lensDepthRg) || !Finite(massFraction) || !Finite(gain)
                || double.IsNaN(sourceDepthRg) || sourceDepthRg == double.NegativeInfinity)
                throw new ArgumentOutOfRangeException(nameof(lensDepthRg));
            if (lensDepthRg <= .01 || massFraction <= 0 || sourceDepthRg <= lensDepthRg || gain <= 0) return 0;
            double geometry = double.IsPositiveInfinity(sourceDepthRg) ? 1 : 1 - lensDepthRg / sourceDepthRg;
            return 4 * massFraction * geometry * Math.Min(gain, 2) / lensDepthRg;
        }

        // Positive bending alpha; source angle is image angle minus alpha.
        public static void Deflection(double relativeAngleX, double relativeAngleY, double lensDepthRg,
            double massFraction, double horizonRadiusRg, double sourceDepthRg, double gain, out double x, out double y)
        {
            if (!Finite(relativeAngleX) || !Finite(relativeAngleY) || !Finite(horizonRadiusRg))
                throw new ArgumentOutOfRangeException(nameof(relativeAngleX));
            double strength = EinsteinRadiusSquared(lensDepthRg, massFraction, sourceDepthRg, gain);
            x = y = 0;
            if (strength == 0) return;
            double core = Math.Max(CoreHorizonFraction * Math.Max(0, horizonRadiusRg), .03 * massFraction) / lensDepthRg;
            double denominator = Math.Max(1e-12, Math.Max(relativeAngleX * relativeAngleX + relativeAngleY * relativeAngleY, core * core));
            x = strength * relativeAngleX / denominator; y = strength * relativeAngleY / denominator;
            double length = Math.Sqrt(x * x + y * y);
            if (length > MaximumDeflectionRadians) { x /= length; y /= length; }
        }

        // Assumed linear limb darkening, not a fitted stellar atmosphere.
        public static double SurfaceBrightness(double normalizedRadius)
        {
            if (!Finite(normalizedRadius) || normalizedRadius < 0) throw new ArgumentOutOfRangeException(nameof(normalizedRadius));
            return normalizedRadius > 1 ? 0 : .45 + .55 * Math.Sqrt(Math.Max(0, 1 - normalizedRadius * normalizedRadius));
        }

        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
