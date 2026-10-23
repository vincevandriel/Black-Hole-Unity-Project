using System;

namespace BlackHole.Staging.Simulation
{
    public readonly struct SyntheticSkyStar
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double ApparentMagnitude { get; }
        public double RelativeFlux { get; }
        public double TemperatureKelvin { get; }
        public double Red { get; }
        public double Green { get; }
        public double Blue { get; }
        public SyntheticSkyStar(double x, double y, double z, double magnitude, double temperature,
            double red, double green, double blue)
        {
            X = x; Y = y; Z = z; ApparentMagnitude = magnitude;
            RelativeFlux = Math.Pow(10, -.4 * magnitude); TemperatureKelvin = temperature;
            Red = red; Green = green; Blue = blue;
        }
    }

    /// <summary>Synthetic fixed directions and apparent fluxes; not a measured star catalogue.</summary>
    public static class SyntheticSkyCatalog
    {
        public const uint DefaultSeed = 150914;
        public const int MaximumStars = 8192;
        public const int ReferenceImageHeight = 900;
        public const double DefaultExposure = 12;
        private const double TruncatedGaussianMass = .9946076967722628; // erf(3/sqrt(2))^2

        public static SyntheticSkyStar[] Create(int count, uint seed = DefaultSeed)
        {
            if (count < 1 || count > MaximumStars) throw new ArgumentOutOfRangeException(nameof(count));
            var stars = new SyntheticSkyStar[count];
            for (int i = 0; i < count; i++) stars[i] = Star(i, seed);
            return stars;
        }

        // The random variates depend on index and seed only: adding dimmer stars never moves,
        // brightens, recolours or renormalizes the retained bright catalogue prefix.
        public static SyntheticSkyStar Star(int index, uint seed = DefaultSeed)
        {
            if (index < 0 || index >= MaximumStars) throw new ArgumentOutOfRangeException(nameof(index));
            uint state = unchecked(seed ^ ((uint)index + 1) * 747796405u);
            double y = 2 * Uniform(ref state) - 1, phi = 2 * Math.PI * Uniform(ref state);
            double radius = Math.Sqrt(Math.Max(0, 1 - y * y));
            double magnitude = -1.46 + (5.0 / 3) * Math.Log10(1 + 12.0 * index);
            double temperature = 2800 + 9200 * Math.Pow(Uniform(ref state), 2);
            // Three Planck samples, normalized against 6500 K, gently desaturated for display.
            // This is a colour cue, not integration through measured photometric passbands.
            double r = PlanckRatio(610e-9, temperature), g = PlanckRatio(545e-9, temperature), b = PlanckRatio(460e-9, temperature);
            double luminance = .2126 * r + .7152 * g + .0722 * b;
            r = .4 + .6 * r / luminance; g = .4 + .6 * g / luminance; b = .4 + .6 * b / luminance;
            return new SyntheticSkyStar(radius * Math.Cos(phi), y, radius * Math.Sin(phi), magnitude, temperature, r, g, b);
        }

        public static double PsfSigmaPixels(double flux, int renderHeight)
        {
            if (!Finite(flux) || flux <= 0 || renderHeight < 2) throw new ArgumentOutOfRangeException(nameof(flux));
            return Math.Max(.65, (.65 + .55 * Math.Pow(flux, .25)) * renderHeight / ReferenceImageHeight);
        }

        /// <summary>Pixel-integrated, square-truncated Gaussian. Summing a complete pixel lattice
        /// yields one for every subpixel location and sigma, up to the stated erf approximation.</summary>
        public static double PsfPixelMass(double xFromCenterPixels, double yFromCenterPixels, double sigmaPixels)
        {
            if (!Finite(xFromCenterPixels) || !Finite(yFromCenterPixels) || !Finite(sigmaPixels) || sigmaPixels <= 0)
                throw new ArgumentOutOfRangeException(nameof(sigmaPixels));
            double s = sigmaPixels * Math.Sqrt(2), extent = 3 * sigmaPixels;
            double dx = Erf(Clamp(xFromCenterPixels + .5, -extent, extent) / s)
                - Erf(Clamp(xFromCenterPixels - .5, -extent, extent) / s);
            double dy = Erf(Clamp(yFromCenterPixels + .5, -extent, extent) / s)
                - Erf(Clamp(yFromCenterPixels - .5, -extent, extent) / s);
            return .25 * dx * dy / TruncatedGaussianMass;
        }

        public static double IntegratedDisplayFlux(double flux, int renderHeight)
        {
            if (!Finite(flux) || flux <= 0 || renderHeight < 2) throw new ArgumentOutOfRangeException(nameof(flux));
            double scale = (double)renderHeight / ReferenceImageHeight;
            return DefaultExposure * flux * scale * scale;
        }

        // Receives translation to make its intentional irrelevance testable. All camera locations
        // in this ~black-hole-sized scene share the same distant source directions.
        public static void DirectionAtObserver(SyntheticSkyStar star, double observerX, double observerY, double observerZ,
            out double x, out double y, out double z)
        {
            if (!Finite(observerX) || !Finite(observerY) || !Finite(observerZ)) throw new ArgumentOutOfRangeException(nameof(observerX));
            x = star.X; y = star.Y; z = star.Z;
        }

        public static ulong Fingerprint(SyntheticSkyStar[] stars)
        {
            if (stars == null) throw new ArgumentNullException(nameof(stars));
            ulong h = 14695981039346656037UL;
            foreach (var s in stars)
            {
                h = Hash(h, s.X); h = Hash(h, s.Y); h = Hash(h, s.Z);
                h = Hash(h, s.ApparentMagnitude); h = Hash(h, s.TemperatureKelvin);
            }
            return h;
        }

        private static ulong Hash(ulong h, double value) => unchecked((h ^ (ulong)BitConverter.DoubleToInt64Bits(value)) * 1099511628211UL);
        private static double PlanckRatio(double wavelength, double temperature)
        {
            const double hcOverK = .014387768775039337;
            return (Math.Exp(hcOverK / (wavelength * 6500)) - 1) / (Math.Exp(hcOverK / (wavelength * temperature)) - 1);
        }
        private static double Uniform(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            uint value = state;
            value ^= value >> 16; value = unchecked(value * 2246822519u); value ^= value >> 13;
            value = unchecked(value * 3266489917u); value ^= value >> 16;
            return (value + .5) / 4294967296.0;
        }
        private static double Erf(double value)
        {
            double x = Math.Abs(value), t = 1 / (1 + .3275911 * x);
            double p = (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - .284496736) * t + .254829592) * t;
            return Math.Sign(value) * (1 - p * Math.Exp(-x * x));
        }
        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
