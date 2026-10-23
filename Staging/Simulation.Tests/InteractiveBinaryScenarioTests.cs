using BlackHole.Staging.Simulation;

internal static class InteractiveBinaryScenarioTests
{
    private static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
    private static void Near(double actual, double expected, double relative, string label)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= relative * Math.Max(1e-30, Math.Abs(expected)),
        $"{label}: expected {expected:R}, actual {actual:R}"); }
    private static void Reject(Action action, string label)
    { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection: " + label); }

    public static void Run()
    {
        string root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Repository data not found");
        using var stream = File.OpenRead(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes"));
        var history = NumericalEventHistory.Load(stream);
        var model = new InteractiveBinaryScenario(new InteractiveBinaryParameters());
        var initial = model.Evaluate(0, 7, 11);
        Check(initial.Generation == 7 && initial.ContextVersion == 11 && initial.Mode == InteractiveScenarioMode.AdjustableExperiment,
            "One immutable scenario epoch carries requested versions and mode");
        Check(initial.Mass1Solar == 35 && initial.Mass2Solar == 30 && initial.TotalMassSolar == 65,
            "Custom mass values are retained without default-constructor replacement");
        Near(initial.GravitationalRadiusMeters, 1.3271244e20 * 65 / (299792458.0 * 299792458.0), 1e-15, "Nominal solar GM is the replay convention");
        Near(initial.GravitationalTimeSeconds, initial.GravitationalRadiusMeters / 299792458, 1e-15, "Length/time units share c");
        Check(initial.BodyA.HasValue && initial.BodyB.HasValue && !initial.Remnant.HasValue, "Initial binary bodies exist");
        Near(initial.BodyA!.Value.HorizonRadiusMeters, 2 * 1.3271244e20 * 35 / (299792458.0 * 299792458.0), 1e-14,
            "Nonspinning radius derives from its own mass");
        Near(initial.BodyB!.Value.HorizonRadiusRg, 60.0 / 65, 1e-14, "Body B radius scales in total mass rg");
        Check(Math.Abs(35 * initial.BodyA.Value.PositionRg.X + 30 * initial.BodyB.Value.PositionRg.X) < 1e-12,
            "Unequal-mass orbit is centered at its barycenter");
        Near(initial.SeparationRg, 30, 1e-15, "Initial separation uses declared total gravitational radii");

        // Compare against existing model only after converting nominal GM through G, preserving custom masses.
        double g = InspiralParameters.GravitationalConstant;
        var reference = new CircularInspiral(new InspiralParameters(35 * InteractiveBinaryScenario.NominalSolarGM / g,
            30 * InteractiveBinaryScenario.NominalSolarGM / g, 30 * initial.GravitationalRadiusMeters,
            10 * initial.GravitationalRadiusMeters));
        Near(model.CutoffTimeSeconds, reference.CutoffTimeSeconds, 5e-15, "Existing bounded inspiral cutoff reproduced with nominal GM");
        foreach (double fraction in new[] { 0.0, .25, .7, .99 })
        {
            double time = fraction * model.CutoffTimeSeconds;
            var state = model.Evaluate(time); var old = reference.Evaluate(time);
            Near(state.SeparationMeters, old.SeparationMeters, 1e-13, "Existing bounded orbit separation");
            Near(state.Signal.FrequencyHertz!.Value, old.GravitationalWaveFrequencyHertz!.Value, 1e-13, "Existing bounded source frequency");
            if (fraction > 0) Near(state.OrbitalPhaseRadians, old.PhaseRadians, 1e-13, "Existing bounded integrated phase");
        }

        // An independent RK4 integration of the dimensionless ODE verifies the analytic solution.
        double tauEnd = .97 * model.CutoffTimeSeconds / model.GravitationalTimeSeconds;
        double r = 30, phase = 0, eta = 35.0 * 30 / (65.0 * 65);
        const int steps = 20000; double dt = tauEnd / steps;
        for (int i = 0; i < steps; ++i)
        {
            (double dr, double dp) f(double radius) => (-64.0 / 5 * eta / Math.Pow(radius, 3), 1 / Math.Pow(radius, 1.5));
            var a = f(r); var b = f(r + a.dr * dt / 2); var c = f(r + b.dr * dt / 2); var d = f(r + c.dr * dt);
            r += dt * (a.dr + 2 * b.dr + 2 * c.dr + d.dr) / 6;
            phase += dt * (a.dp + 2 * b.dp + 2 * c.dp + d.dp) / 6;
        }
        var integrated = model.Evaluate(tauEnd * model.GravitationalTimeSeconds);
        Near(integrated.SeparationRg, r, 1e-10, "Independent RK4 separation");
        Near(integrated.OrbitalPhaseRadians, phase, 1e-10, "Independent RK4 orbital phase");

        var signal = initial.Signal;
        double amplitude = 4 * eta * initial.GravitationalRadiusMeters / (1e12 * 30);
        Near(signal.StrainEnvelope!.Value, amplitude, 1e-14, "Independent SI quadrupole amplitude fixture");
        Near(signal.HPlus!.Value, -amplitude * .625, 1e-14, "Inclination pi/3 plus polarization fixture");
        Check(signal.HCross == 0 && !signal.NormalizedRealH22.HasValue && !signal.NormalizedImaginaryH22.HasValue,
            "Physical quadrupole strain is not relabeled normalized SXS mode");
        Near(signal.FrequencyHertz!.Value, Math.Sqrt(1.3271244e20 * 65 / Math.Pow(initial.SeparationMeters, 3)) / Math.PI,
            1e-14, "Independent dimensional Kepler quadrupole frequency");
        var face = new InteractiveBinaryScenario(new InteractiveBinaryParameters(inclinationRadians: 0));
        Near(face.EvaluateSignal(0).HPlus!.Value, -amplitude, 1e-14, "Face-on plus amplitude");
        var edge = new InteractiveBinaryScenario(new InteractiveBinaryParameters(inclinationRadians: Math.PI / 2, initialPhaseRadians: Math.PI / 4));
        Check(Math.Abs(edge.EvaluateSignal(0).HCross!.Value) < amplitude * 1e-15, "Edge-on cross suppression");
        var rotated = new InteractiveBinaryScenario(new InteractiveBinaryParameters(polarizationRadians: Math.PI / 4));
        Check(Math.Abs(rotated.EvaluateSignal(0).HPlus!.Value) < amplitude * 1e-15, "Polarization rotation plus channel");
        Near(rotated.EvaluateSignal(0).HCross!.Value, -signal.HPlus.Value, 1e-14, "Polarization rotates basis separately from orbit");
        var distant = new InteractiveBinaryScenario(new InteractiveBinaryParameters(observerDistanceMeters: 2e12));
        Near(distant.EvaluateSignal(0).HPlus!.Value, signal.HPlus.Value / 2, 1e-14, "Strain decreases as inverse observer distance");
        Near(distant.EvaluateSignal(0).FrequencyHertz!.Value, signal.FrequencyHertz.Value, 1e-14, "Observer distance leaves orbit and source frequency unchanged");

        var equal = new InteractiveBinaryScenario(new InteractiveBinaryParameters(30, 30, 30));
        var massive = new InteractiveBinaryScenario(new InteractiveBinaryParameters(60, 60, 30));
        Near(massive.EvaluateSignal(0).FrequencyHertz!.Value, equal.EvaluateSignal(0).FrequencyHertz!.Value / 2, 1e-14,
            "At fixed a/rg doubling total mass halves frequency");
        Near(massive.EvaluateSignal(0).StrainEnvelope!.Value, equal.EvaluateSignal(0).StrainEnvelope!.Value * 2, 1e-14,
            "At fixed a/rg and observer metres doubling mass doubles amplitude");
        Near(massive.CutoffTimeSeconds, equal.CutoffTimeSeconds * 2, 1e-14, "Mass scaling changes all physical times together");
        var fixedMetersSmall = new InteractiveBinaryScenario(new InteractiveBinaryParameters(15, 15, 40));
        var fixedMetersLarge = new InteractiveBinaryScenario(new InteractiveBinaryParameters(20, 20, 30));
        Near(fixedMetersSmall.Evaluate(0).SeparationMeters, fixedMetersLarge.Evaluate(0).SeparationMeters, 1e-14, "Comparison truly fixes metres");
        Near(fixedMetersLarge.EvaluateSignal(0).FrequencyHertz!.Value / fixedMetersSmall.EvaluateSignal(0).FrequencyHertz!.Value,
            Math.Sqrt(4.0 / 3), 1e-14, "At fixed metres frequency increases with square root mass");
        Near(fixedMetersLarge.EvaluateSignal(0).StrainEnvelope!.Value / fixedMetersSmall.EvaluateSignal(0).StrainEnvelope!.Value,
            16.0 / 9, 1e-14, "At fixed metres equal-ratio quadrupole amplitude scales as total mass squared");
        var unequal = new InteractiveBinaryScenario(new InteractiveBinaryParameters(10, 50, 30));
        Near(unequal.EvaluateSignal(0).FrequencyHertz!.Value, equal.EvaluateSignal(0).FrequencyHertz!.Value, 1e-14,
            "Mass ratio at fixed total mass does not alter leading Kepler frequency");
        Near(unequal.EvaluateSignal(0).StrainEnvelope!.Value / equal.EvaluateSignal(0).StrainEnvelope!.Value,
            (10.0 * 50) / (30 * 30), 1e-14, "Mass ratio changes amplitude via eta");

        var cutoff = model.Evaluate(model.CutoffTimeSeconds); var end = model.Evaluate(model.DurationSeconds);
        Check(cutoff.Phase == InteractiveBinaryPhase.ConceptualMerger && cutoff.Signal.Coverage == InteractiveSignalCoverage.ModelCutoff
            && !cutoff.Signal.FrequencyHertz.HasValue && !cutoff.Signal.HPlus.HasValue && !cutoff.Signal.HCross.HasValue,
            "At exact cutoff quantitative waveform terminates rather than continuing into illustrative merger");
        Check(end.Phase == InteractiveBinaryPhase.ConceptualRemnant && end.Remnant.HasValue && !end.BodyA.HasValue,
            "Conceptual transition ends with one explicitly illustrative remnant proxy");
        Near(end.Remnant!.Value.MassSolar, 65, 1e-14, "Conceptual proxy does not invent a radiated mass fit");
        Near(end.Remnant.Value.HorizonRadiusRg, 2, 1e-14, "Conceptual final radius uses declared nonspinning total-mass convention");
        Check(model.EvaluateSignal(-.01).Coverage == InteractiveSignalCoverage.BeforeHistory
            && model.EvaluateSignal(model.DurationSeconds + 1).Coverage == InteractiveSignalCoverage.AfterHistory,
            "Signal history has explicit finite coverage without clamping");
        double at = .4 * model.CutoffTimeSeconds, radiusRg = 75;
        var retarded = model.EvaluateRetardedSignal(at, radiusRg);
        var source = model.EvaluateSignal(at - radiusRg * model.GravitationalTimeSeconds);
        Check(retarded.HPlus == source.HPlus && retarded.HCross == source.HCross && retarded.FrequencyHertz == source.FrequencyHertz,
            "Retarded signal is exact source-history lookup with one rg/tg propagation speed");
        Check(!model.EvaluateRetardedSignal(0, 1).IsAvailable, "Unavailable prehistory cannot be repeated as wave train");
        Check(model.EvaluateRetardedSignal(model.CutoffTimeSeconds + 10 * model.GravitationalTimeSeconds, 20).IsAvailable,
            "Previously emitted valid waves can remain outward after the source cutoff");
        Reject(() => model.EvaluateRetardedSignal(at, -1), "Negative propagation radius");

        // Roche truncation is a geometric prescription, with body/companion ratio orientation tested.
        Near(InteractiveBinaryScenario.RocheLobeFraction(1), .49 / (.6 + Math.Log(2)), 1e-14, "Equal-mass Eggleton lobe fixture");
        var extreme = new InteractiveBinaryScenario(new InteractiveBinaryParameters(8, 80, 25));
        var extremeState = extreme.Evaluate(0);
        Check(extremeState.GasState.BodyA.OuterRadiusRg < extremeState.GasState.BodyB.OuterRadiusRg,
            "Smaller component has smaller geometric minidisk truncation");
        Near(extremeState.GasState.BodyA.OuterRadiusRg,
            .8 * InteractiveBinaryScenario.RocheLobeFraction(.1) * 25, 1e-14, "Mass-ratio convention matches A's own lobe");
        Check(extremeState.GasState.BodyA.OuterRadiusRg + extremeState.GasState.BodyB.OuterRadiusRg < 25,
            "Initial Roche-based rings do not intersect at the mass-ratio boundary");
        Near(extreme.HalfExtentXZ, 77, 1e-14, "Gas bounds rebuild from new starting separation");
        foreach (var scenario in new[] { model, extreme, new InteractiveBinaryScenario(new InteractiveBinaryParameters(80, 8, 50)) })
        foreach (double t in new[] { 0, scenario.CutoffTimeSeconds, scenario.MergerTimeSeconds - 1e-10,
            scenario.MergerTimeSeconds, scenario.DurationSeconds })
        {
            var snapshot = scenario.Evaluate(t);
            for (int i = 0; i < 512; ++i)
            {
                double x = (2 * RadicalInverse(i + 1, 2) - 1) * scenario.HalfExtentXZ;
                double y = (2 * RadicalInverse(i + 1, 3) - 1) * scenario.HalfExtentY;
                double z = (2 * RadicalInverse(i + 1, 5) - 1) * scenario.HalfExtentXZ;
                var gas = scenario.SampleGas(snapshot.GasState, x, y, z);
                Check(double.IsFinite(gas.RelativeDensity) && gas.RelativeDensity >= 0 && double.IsFinite(gas.NormalizedBolometricEmission)
                    && gas.NormalizedBolometricEmission >= 0 && double.IsFinite(gas.TemperatureKelvin), "Finite gas at parameter/transition boundaries");
            }
            Check(snapshot.BodyA?.PositionRg.Magnitude < scenario.HalfExtentXZ || snapshot.Remnant.HasValue, "Bodies remain inside rebuilt domain");
        }
        double epsilon = 1e-6 * model.GravitationalTimeSeconds;
        var leftMerger = model.Evaluate(model.MergerTimeSeconds - epsilon);
        var rightMerger = model.Evaluate(model.MergerTimeSeconds + epsilon);
        Check(leftMerger.SeparationRg < 1e-12 && rightMerger.SeparationRg == 0
            && leftMerger.GasState.BinaryWeight < 1e-12 && rightMerger.GasState.RemnantWeight < 1e-12,
            "Conceptual positions and gas envelopes meet continuously at proxy replacement");

        var replay = new InteractiveBinaryScenario(history);
        var originalGas = new ReducedBinaryFlowModel(history);
        foreach (double t in new[] { 0, .25, history.CommonHorizonElapsedSeconds, history.DurationSeconds })
        {
            var snapshot = replay.Evaluate(t); var n = history.Evaluate(t); var original = originalGas.EvaluateState(t);
            Check(snapshot.GasState.PatternCos2 == original.PatternCos2 && snapshot.GasState.BinaryWeight == original.BinaryWeight
                && snapshot.GasState.RemnantWeight == original.RemnantWeight, "Replay gas remains bit-identical through new adapter");
            if (n.Radiation.HasValue)
                Check(snapshot.Signal.DimensionlessRealH22 == n.Radiation.Value.RealH22
                    && snapshot.Signal.DimensionlessImaginaryH22 == n.Radiation.Value.ImaginaryH22
                    && snapshot.Signal.NormalizedRealH22 == n.Radiation.Value.RealH22 / history.WaveformPeakAmplitude
                    && snapshot.Signal.NormalizedImaginaryH22 == n.Radiation.Value.ImaginaryH22 / history.WaveformPeakAmplitude
                    && !snapshot.Signal.HPlus.HasValue && !snapshot.Signal.HCross.HasValue,
                    "Replay publishes retained complex mode without inventing detector or polarization strain");
        }
        Check(!replay.Evaluate(history.DurationSeconds).Signal.IsAvailable, "Late replay explicitly loses waveform coverage");
        Check(replay.Evaluate(history.CommonHorizonElapsedSeconds).Signal.FrequencyHertz > 0,
            "Covered SXS mode supplies finite phase-derived source frequency");
        var peakSignal = replay.EvaluateSignal(history.CommonHorizonElapsedSeconds);
        Near(Math.Sqrt(Math.Pow(peakSignal.NormalizedRealH22!.Value, 2) + Math.Pow(peakSignal.NormalizedImaginaryH22!.Value, 2)),
            1, 1e-14, "SXS display mode is peak-normalized exactly once");

        var session = new InteractiveBinarySession(history);
        session.SelectExperiment(new InteractiveBinaryParameters(20, 45, 37));
        long generation = session.Generation;
        session.Seek(.37 * session.Model.CutoffTimeSeconds); var held = session.Snapshot;
        session.Seek(session.Model.DurationSeconds); session.Seek(held.ElapsedSeconds); var repeated = session.Snapshot;
        Check(held.Generation == repeated.Generation && held.ContextVersion != repeated.ContextVersion
            && held.SeparationRg == repeated.SeparationRg && held.OrbitalPhaseRadians == repeated.OrbitalPhaseRadians
            && held.Signal.HPlus == repeated.Signal.HPlus && held.GasState.PatternCos2 == repeated.GasState.PatternCos2,
            "Rewind changes context but reconstructs a complete identical same-generation epoch");
        session.SetPlaybackRate(2); Check(session.Snapshot.Signal.FrequencyHertz == repeated.Signal.FrequencyHertz, "Playback rate never rescales physical frequency");
        session.Play(); session.Tick(.01); Check(session.PlaybackTimeSeconds == repeated.ElapsedSeconds + .02, "Wall-time viewing rate");
        session.Pause(); double frozen = session.PlaybackTimeSeconds; session.Tick(10); Check(session.PlaybackTimeSeconds == frozen, "Pause freezes event only");
        session.SetPlaybackRate(-4); session.Play(); session.Tick(1e6); Check(!session.IsPlaying && session.PlaybackTimeSeconds == 0, "Reverse endpoint is bounded");
        session.SelectReplay(); Check(session.Generation == generation + 1 && session.PlaybackTimeSeconds == 0 && !session.IsPlaying,
            "Mode replacement publishes one new generation with reset and pause");
        session.SelectExperiment(new InteractiveBinaryParameters(25, 40, 35));
        var retainedModel = session.Model; long retainedGeneration = session.Generation;
        Reject(() => session.SelectExperiment(null!), "Null model replacement");
        Check(session.Model == retainedModel && session.Generation == retainedGeneration, "Invalid replacement is atomic and preserves active scenario");
        Reject(() => new InteractiveBinaryParameters(5, 80), "Mass ratio below supported domain");
        Reject(() => new InteractiveBinaryParameters(80, 5), "Mass ratio checked in both orientations");
        Reject(() => new InteractiveBinaryParameters(4, 30), "Component mass range");
        Reject(() => new InteractiveBinaryParameters(30, 30, 24), "Initial separation range");
        Reject(() => new InteractiveBinaryParameters(observerDistanceMeters: 1e5), "Near-zone distance cannot claim far-field strain");
        Reject(() => new InteractiveBinaryParameters(initialSeparationRg: double.NaN), "Nonfinite parameter");
        Check(new InteractiveBinaryScenario(new InteractiveBinaryParameters(initialPhaseRadians: double.MaxValue,
            polarizationRadians: double.MaxValue)).EvaluateSignal(0).IsAvailable, "Finite cyclic angles normalize before double-angle evaluation");
        Reject(() => InteractiveBinaryScenario.RocheLobeFraction(1e-100), "Unsupported Roche-ratio extreme");
        Reject(() => model.Evaluate(double.NaN), "Nonfinite seek");
        Reject(() => session.SetPlaybackRate(0), "Pause is explicit rather than zero physical playback rate");

        // Snapshot production is plain immutable value state, without gas-frame or history allocations.
        model.Evaluate(at); long before = GC.GetAllocatedBytesForCurrentThread(); double total = 0;
        for (int i = 0; i < 1000; ++i) total += model.Evaluate(i % 2 == 0 ? at : model.DurationSeconds).GasState.PatternCos2;
        Check(GC.GetAllocatedBytesForCurrentThread() == before && double.IsFinite(total), "Adjustable snapshots allocate no per-epoch storage");
        Console.WriteLine("PASS M4.1 shared scenario, nominal-GM bounded inspiral, polarizations, retarded coverage, mass-ratio gas and atomic rewind");
    }

    private static double RadicalInverse(int index, int radix)
    { double value = 0, fraction = 1.0 / radix; while (index > 0) { value += fraction * (index % radix); index /= radix; fraction /= radix; } return value; }
}
