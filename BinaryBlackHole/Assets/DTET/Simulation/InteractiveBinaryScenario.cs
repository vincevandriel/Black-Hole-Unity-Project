#nullable enable
using System;

namespace BlackHole.Staging.Simulation
{
    public enum InteractiveScenarioMode { RepresentativeReplay, AdjustableExperiment }
    public enum InteractiveBinaryPhase { NumericalInspiral, NumericalRemnant, BoundedInspiral, ConceptualMerger, ConceptualRemnant }
    public enum InteractiveSignalKind { NormalizedSxsMode, QuadrupoleStrain }
    public enum InteractiveSignalCoverage { Available, BeforeHistory, AfterHistory, ModelCutoff }

    public sealed class InteractiveBinaryParameters
    {
        public double Mass1Solar { get; }
        public double Mass2Solar { get; }
        public double InitialSeparationRg { get; }
        public double ObserverDistanceMeters { get; }
        public double InclinationRadians { get; }
        public double PolarizationRadians { get; }
        public double InitialPhaseRadians { get; }
        public double TotalMassSolar => Mass1Solar + Mass2Solar;
        public double SymmetricMassRatio => Mass1Solar * Mass2Solar / (TotalMassSolar * TotalMassSolar);
        public static InteractiveBinaryParameters Default => new InteractiveBinaryParameters();
        public InteractiveBinaryParameters(double mass1Solar = 35, double mass2Solar = 30,
            double initialSeparationRg = 30, double observerDistanceMeters = 1e12,
            double inclinationRadians = Math.PI / 3, double polarizationRadians = 0, double initialPhaseRadians = 0)
        {
            RequireRange(mass1Solar, 5, 80, nameof(mass1Solar)); RequireRange(mass2Solar, 5, 80, nameof(mass2Solar));
            if (Math.Min(mass1Solar, mass2Solar) / Math.Max(mass1Solar, mass2Solar) < .1)
                throw new ArgumentException("Choose component masses with a smaller/larger ratio of at least 0.1.");
            RequireRange(initialSeparationRg, 25, 50, nameof(initialSeparationRg));
            RequireRange(inclinationRadians, 0, Math.PI, nameof(inclinationRadians));
            ReferenceFlowModel.RequireFinite(polarizationRadians, nameof(polarizationRadians));
            ReferenceFlowModel.RequireFinite(initialPhaseRadians, nameof(initialPhaseRadians));
            double rg = InteractiveBinaryScenario.NominalSolarGM * (mass1Solar + mass2Solar)
                / (InteractiveBinaryScenario.SpeedOfLight * InteractiveBinaryScenario.SpeedOfLight);
            // Fixed observer must be well outside the longest wavelength in this bounded scenario.
            double minimumDistance = 100 * Math.PI * Math.Pow(initialSeparationRg, 1.5) * rg;
            RequireRange(observerDistanceMeters, minimumDistance, 1e25, nameof(observerDistanceMeters));
            Mass1Solar = mass1Solar; Mass2Solar = mass2Solar; InitialSeparationRg = initialSeparationRg;
            ObserverDistanceMeters = observerDistanceMeters; InclinationRadians = inclinationRadians;
            PolarizationRadians = polarizationRadians % Math.PI;
            InitialPhaseRadians = initialPhaseRadians % (2 * Math.PI);
        }
        private static void RequireRange(double value, double min, double max, string name)
        {
            if (!NumericalEventHistory.IsFinite(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name, "Value lies outside the declared scenario domain.");
        }
    }

    public readonly struct InteractiveBinaryBody
    {
        public NumericalVector3 PositionRg { get; }
        public NumericalVector3 PositionMeters { get; }
        public double MassSolar { get; }
        public double MassFraction { get; }
        public double HorizonRadiusRg { get; }
        public double HorizonRadiusMeters { get; }
        internal InteractiveBinaryBody(ReducedBinaryFlowBody gasBody, double totalMass, double rg)
        { PositionRg = gasBody.PositionRg; PositionMeters = gasBody.PositionRg.Scale(rg);
            MassFraction = gasBody.MassFraction; MassSolar = MassFraction * totalMass;
            HorizonRadiusRg = gasBody.HorizonRadiusRg; HorizonRadiusMeters = HorizonRadiusRg * rg; }
    }

    public readonly struct InteractiveSignalSample
    {
        public InteractiveSignalKind Kind { get; }
        public InteractiveSignalCoverage Coverage { get; }
        public bool IsAvailable => Coverage == InteractiveSignalCoverage.Available;
        public double EmissionElapsedSeconds { get; }
        public double? FrequencyHertz { get; }
        public double? HPlus { get; }
        public double? HCross { get; }
        public double? NormalizedRealH22 { get; }
        public double? NormalizedImaginaryH22 { get; }
        public double? DimensionlessRealH22 { get; }
        public double? DimensionlessImaginaryH22 { get; }
        public double ObserverDistanceMeters { get; }
        public double InclinationRadians { get; }
        public double PolarizationRadians { get; }
        public double? StrainEnvelope { get; }
        internal InteractiveSignalSample(InteractiveSignalKind kind, InteractiveSignalCoverage coverage, double t,
            double? frequency = null, double? plus = null, double? cross = null, double? re = null, double? im = null,
            double distance = 0, double inclination = 0, double polarization = 0, double? envelope = null,
            double? rawRealH22 = null, double? rawImaginaryH22 = null)
        { Kind = kind; Coverage = coverage; EmissionElapsedSeconds = t; FrequencyHertz = frequency;
            HPlus = plus; HCross = cross; NormalizedRealH22 = re; NormalizedImaginaryH22 = im;
            ObserverDistanceMeters = distance; InclinationRadians = inclination; PolarizationRadians = polarization; StrainEnvelope = envelope;
            DimensionlessRealH22 = rawRealH22; DimensionlessImaginaryH22 = rawImaginaryH22; }
    }

    public readonly struct InteractiveBinarySnapshot
    {
        public InteractiveScenarioMode Mode { get; }
        public InteractiveBinaryPhase Phase { get; }
        public long Generation { get; }
        public long ContextVersion { get; }
        public string ScenarioId => Mode == InteractiveScenarioMode.RepresentativeReplay
            ? NumericalEventHistory.ModelId : "adjustable-nominal-GM-quadrupole-v1";
        public double ElapsedSeconds { get; }
        public double SourceTimeSeconds { get; }
        public double DurationSeconds { get; }
        public double GravitationalRadiusMeters { get; }
        public double GravitationalTimeSeconds { get; }
        public double Mass1Solar { get; }
        public double Mass2Solar { get; }
        public double TotalMassSolar { get; }
        public double SeparationRg { get; }
        public double SeparationMeters => SeparationRg * GravitationalRadiusMeters;
        public double OrbitalPhaseRadians { get; }
        public double MergerProgress { get; }
        public InteractiveBinaryBody? BodyA { get; }
        public InteractiveBinaryBody? BodyB { get; }
        public InteractiveBinaryBody? Remnant { get; }
        public ReducedBinaryFlowState GasState { get; }
        public InteractiveSignalSample Signal { get; }
        public bool IsConceptual => Phase == InteractiveBinaryPhase.ConceptualMerger || Phase == InteractiveBinaryPhase.ConceptualRemnant;
        public string RadiusConvention => Mode == InteractiveScenarioMode.RepresentativeReplay
            ? "Retained equal-area coordinate sphere" : "Nonspinning Schwarzschild radius proxy";
        internal InteractiveBinarySnapshot(InteractiveScenarioMode mode, InteractiveBinaryPhase phase, long generation,
            long context, double elapsed, double source, double duration, double rg, double tg, double m1, double m2,
            double total, double separation, double angle, double merger, ReducedBinaryFlowState gas, InteractiveSignalSample signal)
        {
            Mode = mode; Phase = phase; Generation = generation; ContextVersion = context; ElapsedSeconds = elapsed;
            SourceTimeSeconds = source; DurationSeconds = duration; GravitationalRadiusMeters = rg; GravitationalTimeSeconds = tg;
            Mass1Solar = m1; Mass2Solar = m2; TotalMassSolar = total; SeparationRg = separation;
            OrbitalPhaseRadians = angle; MergerProgress = merger; GasState = gas; Signal = signal;
            BodyA = gas.IsCommonHorizon ? (InteractiveBinaryBody?)null : new InteractiveBinaryBody(gas.BodyA, total, rg);
            BodyB = gas.IsCommonHorizon ? (InteractiveBinaryBody?)null : new InteractiveBinaryBody(gas.BodyB, total, rg);
            Remnant = gas.IsCommonHorizon ? new InteractiveBinaryBody(gas.Remnant, total, rg) : (InteractiveBinaryBody?)null;
        }
    }

    // Shared deterministic scenario for scene, gas, graphs and display optics.
    public sealed class InteractiveBinaryScenario
    {
        public const double NominalSolarGM = 1.3271244e20;
        public const double SpeedOfLight = 299792458;
        public const double CutoffSeparationRg = 10;
        public const double ConceptualMergerDurationTg = 200;
        public const double ConceptualRemnantDurationTg = 400;
        public const double ReferenceStrainAmplitude = 1e-8;
        private readonly NumericalEventHistory? history;
        private readonly double mass1, mass2, totalMass, decayPerTg, cutoffPhase;
        public InteractiveScenarioMode Mode { get; }
        public InteractiveBinaryParameters? Parameters { get; }
        public ReducedBinaryFlowModel GasSampler { get; }
        public double HalfExtentXZ => GasSampler.HalfExtentXZ;
        public double HalfExtentY => GasSampler.HalfExtentY;
        public double GravitationalRadiusMeters { get; }
        public double GravitationalTimeSeconds { get; }
        public double DurationSeconds { get; }
        public double CutoffTimeSeconds { get; }
        public double MergerTimeSeconds { get; }
        public double ReferenceSxsModeAmplitude => history == null ? 0 : history.WaveformPeakAmplitude;

        public InteractiveBinaryScenario(NumericalEventHistory history)
        {
            this.history = history ?? throw new ArgumentNullException(nameof(history)); Mode = InteractiveScenarioMode.RepresentativeReplay;
            GasSampler = new ReducedBinaryFlowModel(history); GravitationalRadiusMeters = GasSampler.GravitationalRadiusMeters;
            GravitationalTimeSeconds = GasSampler.GravitationalTimeSeconds; DurationSeconds = history.DurationSeconds;
            CutoffTimeSeconds = MergerTimeSeconds = history.CommonHorizonElapsedSeconds;
            NumericalEventState initial = history.Evaluate(0);
            mass1 = initial.BodyA.GetValueOrDefault().MassSolar; mass2 = initial.BodyB.GetValueOrDefault().MassSolar;
            totalMass = history.TotalMassSolar;
        }

        public InteractiveBinaryScenario(InteractiveBinaryParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters)); Mode = InteractiveScenarioMode.AdjustableExperiment;
            mass1 = parameters.Mass1Solar; mass2 = parameters.Mass2Solar; totalMass = parameters.TotalMassSolar;
            GravitationalRadiusMeters = NominalSolarGM * totalMass / (SpeedOfLight * SpeedOfLight);
            GravitationalTimeSeconds = GravitationalRadiusMeters / SpeedOfLight;
            decayPerTg = 64.0 / 5 * parameters.SymmetricMassRatio;
            CutoffTimeSeconds = (Math.Pow(parameters.InitialSeparationRg, 4) - Math.Pow(CutoffSeparationRg, 4))
                / (4 * decayPerTg) * GravitationalTimeSeconds;
            MergerTimeSeconds = CutoffTimeSeconds + ConceptualMergerDurationTg * GravitationalTimeSeconds;
            DurationSeconds = MergerTimeSeconds + ConceptualRemnantDurationTg * GravitationalTimeSeconds;
            cutoffPhase = InspiralPhaseAt(CutoffSeparationRg);
            GasSampler = new ReducedBinaryFlowModel(GravitationalRadiusMeters, GravitationalTimeSeconds,
                parameters.InitialSeparationRg, DurationSeconds, MergerTimeSeconds);
        }

        public InteractiveBinarySnapshot Evaluate(double elapsedSeconds, long generation = 0, long contextVersion = 0)
        {
            ReferenceFlowModel.RequireFinite(elapsedSeconds, nameof(elapsedSeconds));
            double elapsed = Math.Max(0, Math.Min(DurationSeconds, elapsedSeconds));
            if (history != null)
            {
                NumericalEventState n = history.Evaluate(elapsed);
                ReducedBinaryFlowState gas = GasSampler.EvaluateState(elapsed);
                double angle = n.HasCommonHorizon ? 0 : Math.Atan2(gas.BodyA.PositionRg.Z - gas.BodyB.PositionRg.Z,
                    gas.BodyA.PositionRg.X - gas.BodyB.PositionRg.X);
                return new InteractiveBinarySnapshot(Mode, n.HasCommonHorizon ? InteractiveBinaryPhase.NumericalRemnant
                    : InteractiveBinaryPhase.NumericalInspiral, generation, contextVersion, elapsed, n.SourceCoordinateTimeSeconds,
                    DurationSeconds, GravitationalRadiusMeters, GravitationalTimeSeconds, mass1, mass2, totalMass,
                    gas.SeparationRg, angle, n.HasCommonHorizon ? 1 : 0, gas, EvaluateSignal(elapsed));
            }
            double tau = elapsed / GravitationalTimeSeconds;
            bool inspiral = elapsed < CutoffTimeSeconds, remnant = elapsed >= MergerTimeSeconds;
            double progress = ReducedBinaryFlowModel.SmoothStep((elapsed - CutoffTimeSeconds)
                / (ConceptualMergerDurationTg * GravitationalTimeSeconds));
            double radius = inspiral ? InspiralRadiusAt(elapsed) : CutoffSeparationRg * (1 - progress);
            double phase = inspiral ? InspiralPhaseAt(radius) : cutoffPhase
                + Math.Min(ConceptualMergerDurationTg, (elapsed - CutoffTimeSeconds) / GravitationalTimeSeconds)
                / Math.Pow(CutoffSeparationRg, 1.5);
            ReducedBinaryFlowBody a = default, b = default, c = default;
            ReducedBinaryFlowStream sa = default, sb = default;
            if (!remnant)
            {
                double dr = inspiral ? -decayPerTg / Math.Pow(radius, 3)
                    : -CutoffSeparationRg * SmoothStepDerivative((elapsed - CutoffTimeSeconds)
                        / (ConceptualMergerDurationTg * GravitationalTimeSeconds)) / ConceptualMergerDurationTg;
                double omega = inspiral ? 1 / Math.Pow(radius, 1.5) : 1 / Math.Pow(CutoffSeparationRg, 1.5);
                a = MakeExperimentBody(mass1 / totalMass, mass2 / totalMass, radius, phase, dr, omega,
                    .8 * RocheLobeFraction(mass1 / mass2) * radius);
                b = MakeExperimentBody(mass2 / totalMass, -mass1 / totalMass, radius, phase, dr, omega,
                    .8 * RocheLobeFraction(mass2 / mass1) * radius);
                sa = GasSampler.MakeStream(a); sb = GasSampler.MakeStream(b);
            }
            else c = new ReducedBinaryFlowBody(default, default, 1, 2,
                ReducedBinaryFlowModel.EmittingInnerHorizonRatio * 2, 16);
            double aligned = (elapsed - MergerTimeSeconds) / GravitationalTimeSeconds;
            double mid = .5 * (GasSampler.ReservoirInnerRadiusRg + GasSampler.ReservoirOuterRadiusRg);
            double pattern = tau / Math.Pow(mid, 1.5);
            ReducedBinaryFlowState field = new ReducedBinaryFlowState(elapsed, tau, aligned, radius,
                1 - progress, ReducedBinaryFlowModel.SmoothStep(aligned / ReducedBinaryFlowModel.RemnantRiseTg),
                GasSampler.ReservoirInnerRadiusRg, GasSampler.ReservoirOuterRadiusRg,
                Math.Cos(2 * pattern), Math.Sin(2 * pattern), a, b, c, sa, sb);
            return new InteractiveBinarySnapshot(Mode, inspiral ? InteractiveBinaryPhase.BoundedInspiral
                : remnant ? InteractiveBinaryPhase.ConceptualRemnant : InteractiveBinaryPhase.ConceptualMerger,
                generation, contextVersion, elapsed, elapsed, DurationSeconds, GravitationalRadiusMeters, GravitationalTimeSeconds,
                mass1, mass2, totalMass, radius, phase, progress, field, EvaluateSignal(elapsed));
        }

        public ReducedBinaryFlowSample SampleGas(ReducedBinaryFlowState state, double x, double y, double z)
            => GasSampler.Sample(state, x, y, z);

        public InteractiveSignalSample EvaluateSignal(double emissionElapsedSeconds)
        {
            ReferenceFlowModel.RequireFinite(emissionElapsedSeconds, nameof(emissionElapsedSeconds));
            if (emissionElapsedSeconds < 0) return Unavailable(emissionElapsedSeconds, InteractiveSignalCoverage.BeforeHistory);
            if (emissionElapsedSeconds > DurationSeconds) return Unavailable(emissionElapsedSeconds, InteractiveSignalCoverage.AfterHistory);
            if (history != null)
            {
                NumericalRadiationState? radiation = history.Evaluate(emissionElapsedSeconds).Radiation;
                if (!radiation.HasValue)
                {
                    double u = history.WaveformPeakNativeTime + (emissionElapsedSeconds - history.CommonHorizonElapsedSeconds) / GravitationalTimeSeconds;
                    return Unavailable(emissionElapsedSeconds, u < history.UsefulWaveformStartNativeTime
                        ? InteractiveSignalCoverage.BeforeHistory : InteractiveSignalCoverage.AfterHistory);
                }
                double earliest = Math.Max(0, history.CommonHorizonElapsedSeconds
                    + (history.UsefulWaveformStartNativeTime - history.WaveformPeakNativeTime) * GravitationalTimeSeconds);
                double latest = Math.Min(DurationSeconds, history.CommonHorizonElapsedSeconds
                    + (history.UsefulWaveformEndNativeTime - history.WaveformPeakNativeTime) * GravitationalTimeSeconds);
                double lo = Math.Max(earliest, emissionElapsedSeconds - .5 * GravitationalTimeSeconds);
                double hi = Math.Min(latest, emissionElapsedSeconds + .5 * GravitationalTimeSeconds);
                NumericalRadiationState? left = history.Evaluate(lo).Radiation, right = history.Evaluate(hi).Radiation;
                double? frequency = null;
                if (left.HasValue && right.HasValue && hi > lo && left.Value.Amplitude > 1e-20 && right.Value.Amplitude > 1e-20)
                {
                    double cross = right.Value.ImaginaryH22 * left.Value.RealH22 - right.Value.RealH22 * left.Value.ImaginaryH22;
                    double dot = right.Value.RealH22 * left.Value.RealH22 + right.Value.ImaginaryH22 * left.Value.ImaginaryH22;
                    double f = -Math.Atan2(cross, dot) / (2 * Math.PI * (hi - lo));
                    if (f > 0 && NumericalEventHistory.IsFinite(f)) frequency = f;
                }
                return new InteractiveSignalSample(InteractiveSignalKind.NormalizedSxsMode, InteractiveSignalCoverage.Available,
                    emissionElapsedSeconds, frequency, re: radiation.Value.RealH22 / history.WaveformPeakAmplitude,
                    im: radiation.Value.ImaginaryH22 / history.WaveformPeakAmplitude,
                    rawRealH22: radiation.Value.RealH22, rawImaginaryH22: radiation.Value.ImaginaryH22);
            }
            if (emissionElapsedSeconds >= CutoffTimeSeconds) return Unavailable(emissionElapsedSeconds, InteractiveSignalCoverage.ModelCutoff);
            InteractiveBinaryParameters p = Parameters!;
            double radius = InspiralRadiusAt(emissionElapsedSeconds), phase = InspiralPhaseAt(radius);
            double frequencyHz = 1 / (Math.PI * GravitationalTimeSeconds * Math.Pow(radius, 1.5));
            double amplitude = 4 * p.SymmetricMassRatio * GravitationalRadiusMeters / (p.ObserverDistanceMeters * radius);
            double ci = Math.Cos(p.InclinationRadians), cos2 = Math.Cos(2 * phase), sin2 = Math.Sin(2 * phase);
            // One fixed TT basis; a polarization-basis rotation is separate from orbital phase.
            double plus0 = -amplitude * (1 + ci * ci) / 2 * cos2, cross0 = -amplitude * ci * sin2;
            double cp = Math.Cos(2 * p.PolarizationRadians), sp = Math.Sin(2 * p.PolarizationRadians);
            return new InteractiveSignalSample(InteractiveSignalKind.QuadrupoleStrain, InteractiveSignalCoverage.Available,
                emissionElapsedSeconds, frequencyHz, plus0 * cp + cross0 * sp, -plus0 * sp + cross0 * cp,
                distance: p.ObserverDistanceMeters, inclination: p.InclinationRadians, polarization: p.PolarizationRadians, envelope: amplitude);
        }

        // Radius selects retarded history time; strain stays at the declared far-field observer distance.
        public InteractiveSignalSample EvaluateRetardedSignal(double receptionElapsedSeconds, double radiusRg)
        {
            ReferenceFlowModel.RequireFinite(receptionElapsedSeconds, nameof(receptionElapsedSeconds));
            ReferenceFlowModel.RequireFinite(radiusRg, nameof(radiusRg));
            if (radiusRg < 0) throw new ArgumentOutOfRangeException(nameof(radiusRg));
            return EvaluateSignal(receptionElapsedSeconds - radiusRg * GravitationalTimeSeconds);
        }

        public static double RocheLobeFraction(double bodyToCompanionMassRatio)
        {
            ReferenceFlowModel.RequireFinite(bodyToCompanionMassRatio, nameof(bodyToCompanionMassRatio));
            if (bodyToCompanionMassRatio < .1 || bodyToCompanionMassRatio > 10)
                throw new ArgumentOutOfRangeException(nameof(bodyToCompanionMassRatio), "The exposed geometric prescription is restricted to mass ratios 0.1 through 10.");
            double third = Math.Pow(bodyToCompanionMassRatio, 1.0 / 3), twoThird = third * third;
            return .49 * twoThird / (.6 * twoThird + Math.Log(1 + third));
        }

        private double InspiralRadiusAt(double seconds) => Math.Pow(Math.Max(Math.Pow(CutoffSeparationRg, 4),
            Math.Pow(Parameters!.InitialSeparationRg, 4) - 4 * decayPerTg * seconds / GravitationalTimeSeconds), .25);
        private double InspiralPhaseAt(double radius) => Parameters!.InitialPhaseRadians
            + 2 / (5 * decayPerTg) * (Math.Pow(Parameters.InitialSeparationRg, 2.5) - Math.Pow(radius, 2.5));
        private static double SmoothStepDerivative(double value) => value <= 0 || value >= 1 ? 0 : 6 * value * (1 - value);
        private static ReducedBinaryFlowBody MakeExperimentBody(double mass, double orbitFraction, double radius,
            double phase, double radialVelocity, double omega, double outer)
        {
            double cos = Math.Cos(phase), sin = Math.Sin(phase);
            var p = new NumericalVector3(orbitFraction * radius * cos, 0, orbitFraction * radius * sin);
            var v = new NumericalVector3(orbitFraction * (radialVelocity * cos - radius * omega * sin), 0,
                orbitFraction * (radialVelocity * sin + radius * omega * cos));
            return new ReducedBinaryFlowBody(p, v, mass, 2 * mass,
                ReducedBinaryFlowModel.EmittingInnerHorizonRatio * 2 * mass, outer);
        }
        private InteractiveSignalSample Unavailable(double time, InteractiveSignalCoverage coverage)
            => new InteractiveSignalSample(history != null ? InteractiveSignalKind.NormalizedSxsMode : InteractiveSignalKind.QuadrupoleStrain,
                coverage, time, distance: Parameters?.ObserverDistanceMeters ?? 0, inclination: Parameters?.InclinationRadians ?? 0,
                polarization: Parameters?.PolarizationRadians ?? 0);
    }

    public sealed class InteractiveBinarySession
    {
        private readonly NumericalEventHistory history;
        public InteractiveBinaryScenario Model { get; private set; }
        public InteractiveBinarySnapshot Snapshot => Model.Evaluate(PlaybackTimeSeconds, Generation, ContextVersion);
        public long Generation { get; private set; } = 1;
        public long ContextVersion { get; private set; } = 1;
        public double PlaybackTimeSeconds { get; private set; }
        public double PlaybackRate { get; private set; } = .05;
        public bool IsPlaying { get; private set; }
        public InteractiveBinarySession(NumericalEventHistory history)
        { this.history = history ?? throw new ArgumentNullException(nameof(history)); Model = new InteractiveBinaryScenario(history); }
        public void SelectReplay() => Replace(new InteractiveBinaryScenario(history));
        public void SelectExperiment(InteractiveBinaryParameters parameters) => Replace(new InteractiveBinaryScenario(parameters));
        private void Replace(InteractiveBinaryScenario model)
        { Model = model; PlaybackTimeSeconds = 0; IsPlaying = false; Generation++; ContextVersion++; }
        public void Seek(double elapsedSeconds)
        {
            ReferenceFlowModel.RequireFinite(elapsedSeconds, nameof(elapsedSeconds));
            PlaybackTimeSeconds = Math.Max(0, Math.Min(Model.DurationSeconds, elapsedSeconds)); IsPlaying = false; ContextVersion++;
        }
        public void Reset() { PlaybackTimeSeconds = 0; IsPlaying = false; ContextVersion++; }
        public void Play() { IsPlaying = PlaybackRate > 0 ? PlaybackTimeSeconds < Model.DurationSeconds : PlaybackTimeSeconds > 0; }
        public void Pause() { IsPlaying = false; }
        public void SetPlaybackRate(double rate)
        {
            ReferenceFlowModel.RequireFinite(rate, nameof(rate));
            if (Math.Abs(rate) < .01 || Math.Abs(rate) > 4) throw new ArgumentOutOfRangeException(nameof(rate));
            PlaybackRate = rate;
        }
        public void Tick(double wallSeconds)
        {
            ReferenceFlowModel.RequireFinite(wallSeconds, nameof(wallSeconds));
            if (wallSeconds < 0) throw new ArgumentOutOfRangeException(nameof(wallSeconds));
            if (!IsPlaying) return;
            PlaybackTimeSeconds = Math.Max(0, Math.Min(Model.DurationSeconds, PlaybackTimeSeconds + wallSeconds * PlaybackRate));
            if ((PlaybackRate > 0 && PlaybackTimeSeconds >= Model.DurationSeconds) || (PlaybackRate < 0 && PlaybackTimeSeconds <= 0)) IsPlaying = false;
        }
    }
}
