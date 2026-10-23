using System;

namespace BlackHole.Staging.Simulation
{
    /// <summary>Bounded display warp references. These are not a binary metric or geodesic solver.</summary>
    public static class InteractiveOpticsReference
    {
        public const int WaveSamples = 256;
        public const double MaximumLensAngle = .16;
        public const double MaximumWaveAngle = .035;
        public const double WavePresentationScale = .025;

        public static double BehindLensFraction(double lensDepthRg, double sourceDepthRg)
        {
            if (!Finite(lensDepthRg) || lensDepthRg <= 0 || double.IsNaN(sourceDepthRg)) return 0;
            if (double.IsPositiveInfinity(sourceDepthRg)) return 1;
            return sourceDepthRg <= lensDepthRg ? 0 : 1 - lensDepthRg / sourceDepthRg;
        }

        public static void LensOffset(double thetaX, double thetaY, double lensDepthRg, double massFraction,
            double horizonRadiusRg, double sourceDepthRg, double gain, out double dx, out double dy)
        {
            dx = dy = 0;
            if (!Finite(thetaX) || !Finite(thetaY) || !Finite(massFraction) || !Finite(horizonRadiusRg)
                || !Finite(gain)) throw new ArgumentOutOfRangeException(nameof(thetaX));
            double geometry = BehindLensFraction(lensDepthRg, sourceDepthRg);
            if (geometry == 0 || massFraction <= 0 || gain <= 0) return;
            double soft = Math.Max(.75 * Math.Max(0, horizonRadiusRg), .15 * massFraction) / lensDepthRg;
            double factor = -4 * massFraction * geometry * Math.Min(gain, 2)
                / (lensDepthRg * Math.Max(1e-12, thetaX * thetaX + thetaY * thetaY + soft * soft));
            dx = thetaX * factor; dy = thetaY * factor;
            Limit(ref dx, ref dy, MaximumLensAngle);
        }

        public static void WaveOffset(double thetaX, double thetaY, double normalizedPlus, double normalizedCross,
            double gain, out double dx, out double dy)
        {
            if (!Finite(thetaX) || !Finite(thetaY) || !Finite(normalizedPlus) || !Finite(normalizedCross) || !Finite(gain))
                throw new ArgumentOutOfRangeException(nameof(thetaX));
            double plus = Clamp(normalizedPlus, -4, 4), cross = Clamp(normalizedCross, -4, 4);
            double scale = WavePresentationScale * Clamp(gain, 0, 2);
            dx = scale * (plus * thetaX + cross * thetaY);
            dy = scale * (cross * thetaX - plus * thetaY);
            Limit(ref dx, ref dy, MaximumWaveAngle);
        }

        public static double RetardedTime(double receptionSeconds, double radiusRg, double gravitationalTimeSeconds)
        {
            if (!Finite(receptionSeconds) || !Finite(radiusRg) || !Finite(gravitationalTimeSeconds)
                || radiusRg < 0 || gravitationalTimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(radiusRg));
            return receptionSeconds - radiusRg * gravitationalTimeSeconds;
        }

        private static void Limit(ref double x, ref double y, double limit)
        {
            double length = Math.Sqrt(x * x + y * y);
            if (length > limit) { x *= limit / length; y *= limit / length; }
        }
        private static double Clamp(double x, double lo, double hi) => Math.Max(lo, Math.Min(hi, x));
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
