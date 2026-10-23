using System;

namespace BlackHole.Staging.Simulation
{
    public enum InspiralPhase
    {
        Inspiral,
        ConceptualMerger,
        ConceptualRemnant
    }

    // SI units throughout this type. Scene scale is a separate presentation choice.
    public sealed class InspiralParameters
    {
        public const double GravitationalConstant = 6.67430e-11; // m^3 kg^-1 s^-2
        public const double SpeedOfLight = 299792458.0; // m s^-1
        public const double SolarMassKilograms = 1.98847e30;

        public double Mass1Kilograms { get; }
        public double Mass2Kilograms { get; }
        public double InitialSeparationMeters { get; }
        public double CutoffSeparationMeters { get; }
        public double InitialPhaseRadians { get; }

        public InspiralParameters(double mass1Kilograms, double mass2Kilograms,
            double initialSeparationMeters, double cutoffSeparationMeters,
            double initialPhaseRadians = 0.0)
        {
            RequirePositiveFinite(mass1Kilograms, nameof(mass1Kilograms));
            RequirePositiveFinite(mass2Kilograms, nameof(mass2Kilograms));
            RequirePositiveFinite(initialSeparationMeters, nameof(initialSeparationMeters));
            RequirePositiveFinite(cutoffSeparationMeters, nameof(cutoffSeparationMeters));
            if (cutoffSeparationMeters >= initialSeparationMeters)
                throw new ArgumentException("Initial separation must exceed the model cutoff; choose a larger initial separation.", nameof(initialSeparationMeters));
            if (double.IsNaN(initialPhaseRadians) || double.IsInfinity(initialPhaseRadians))
                throw new ArgumentOutOfRangeException(nameof(initialPhaseRadians), "Phase must be finite radians.");

            Mass1Kilograms = mass1Kilograms;
            Mass2Kilograms = mass2Kilograms;
            InitialSeparationMeters = initialSeparationMeters;
            CutoffSeparationMeters = cutoffSeparationMeters;
            InitialPhaseRadians = initialPhaseRadians;
        }

        public static double SolarMassesToKilograms(double solarMasses)
        {
            RequirePositiveFinite(solarMasses, nameof(solarMasses));
            double kilograms = solarMasses * SolarMassKilograms;
            RequirePositiveFinite(kilograms, nameof(solarMasses));
            return kilograms;
        }

        private static void RequirePositiveFinite(double value, string name)
        {
            if (value <= 0.0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
        }
    }

    public sealed class InspiralState
    {
        public InspiralPhase Phase { get; }
        public double PhysicalTimeSeconds { get; }
        public double ConceptualTimeSeconds { get; }
        public double SeparationMeters { get; }
        public double PhaseRadians { get; }
        // Null after cutoff: these formulas must not be presented as post-merger predictions.
        public double? OrbitalFrequencyHertz { get; }
        public double? GravitationalWaveFrequencyHertz { get; }
        public double Body1XMeters { get; }
        public double Body1ZMeters { get; }
        public double Body2XMeters { get; }
        public double Body2ZMeters { get; }

        internal InspiralState(InspiralPhase phase, double physicalTimeSeconds,
            double conceptualTimeSeconds, double separationMeters, double phaseRadians,
            double? orbitalFrequencyHertz, double? gravitationalWaveFrequencyHertz,
            double body1XMeters, double body1ZMeters, double body2XMeters, double body2ZMeters)
        {
            Phase = phase;
            PhysicalTimeSeconds = physicalTimeSeconds;
            ConceptualTimeSeconds = conceptualTimeSeconds;
            SeparationMeters = separationMeters;
            PhaseRadians = phaseRadians;
            OrbitalFrequencyHertz = orbitalFrequencyHertz;
            GravitationalWaveFrequencyHertz = gravitationalWaveFrequencyHertz;
            Body1XMeters = body1XMeters;
            Body1ZMeters = body1ZMeters;
            Body2XMeters = body2XMeters;
            Body2ZMeters = body2ZMeters;
        }
    }

    public sealed class CircularInspiral
    {
        public const double ConceptualMergerDurationSeconds = 1.5;

        public InspiralParameters Parameters { get; }
        public double TotalMassKilograms { get; }
        public double DecayCoefficientMeters4PerSecond { get; }
        public double CutoffTimeSeconds { get; }
        public double EndTimeSeconds => CutoffTimeSeconds + ConceptualMergerDurationSeconds;

        public CircularInspiral(InspiralParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            TotalMassKilograms = parameters.Mass1Kilograms + parameters.Mass2Kilograms;
            double g = InspiralParameters.GravitationalConstant;
            double c = InspiralParameters.SpeedOfLight;
            DecayCoefficientMeters4PerSecond = (64.0 / 5.0) * Math.Pow(g, 3.0)
                * parameters.Mass1Kilograms * parameters.Mass2Kilograms * TotalMassKilograms
                / Math.Pow(c, 5.0);
            CutoffTimeSeconds = (Math.Pow(parameters.InitialSeparationMeters, 4.0)
                - Math.Pow(parameters.CutoffSeparationMeters, 4.0))
                / (4.0 * DecayCoefficientMeters4PerSecond);
            if (!IsPositiveFinite(DecayCoefficientMeters4PerSecond) || !IsPositiveFinite(CutoffTimeSeconds))
                throw new ArgumentException("Masses and separation produce a non-finite or zero-duration model. Choose smaller positive values and a cutoff below the initial separation.", nameof(parameters));
        }

        public InspiralState Evaluate(double playbackTimeSeconds)
        {
            if (playbackTimeSeconds < 0.0 || double.IsNaN(playbackTimeSeconds) || double.IsInfinity(playbackTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(playbackTimeSeconds), "Seek time must be finite and at least zero seconds.");

            double physicalTime = Math.Min(playbackTimeSeconds, CutoffTimeSeconds);
            bool conceptual = playbackTimeSeconds >= CutoffTimeSeconds;
            double conceptualTime = Math.Max(0.0, playbackTimeSeconds - CutoffTimeSeconds);
            double r0 = Parameters.InitialSeparationMeters;
            double rCutoff = Parameters.CutoffSeparationMeters;
            double separation = conceptual
                ? rCutoff
                : Math.Pow(Math.Max(Math.Pow(rCutoff, 4.0), Math.Pow(r0, 4.0)
                    - 4.0 * DecayCoefficientMeters4PerSecond * physicalTime), 0.25);

            double phase = Parameters.InitialPhaseRadians
                + 2.0 * Math.Sqrt(InspiralParameters.GravitationalConstant * TotalMassKilograms)
                / (5.0 * DecayCoefficientMeters4PerSecond)
                * (Math.Pow(r0, 2.5) - Math.Pow(separation, 2.5));
            double angle = phase % (2.0 * Math.PI);
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double mass1Radius = Parameters.Mass2Kilograms / TotalMassKilograms * separation;
            double mass2Radius = Parameters.Mass1Kilograms / TotalMassKilograms * separation;

            double? fOrbit = conceptual ? (double?)null
                : Math.Sqrt(InspiralParameters.GravitationalConstant * TotalMassKilograms
                    / Math.Pow(separation, 3.0)) / (2.0 * Math.PI);
            double? fGw = fOrbit.HasValue ? 2.0 * fOrbit.Value : (double?)null;
            InspiralPhase statePhase = !conceptual ? InspiralPhase.Inspiral
                : conceptualTime < ConceptualMergerDurationSeconds
                    ? InspiralPhase.ConceptualMerger : InspiralPhase.ConceptualRemnant;

            return new InspiralState(statePhase, physicalTime, conceptualTime, separation,
                phase, fOrbit, fGw, mass1Radius * cos, mass1Radius * sin,
                -mass2Radius * cos, -mass2Radius * sin);
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0.0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
