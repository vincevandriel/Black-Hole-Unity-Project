using BlackHole.Staging.Simulation;

internal static class ReducedBinaryFlowTests
{
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); }
    private static void Near(double actual, double expected, double tolerance, string label)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1e-30, Math.Abs(expected)),
        $"{label}: expected {expected:R}, got {actual:R}"); }
    private static void Reject(Action action, string label)
    { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejected input: " + label); }

    public static void Run()
    {
        string directory = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(directory, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes")))
            directory = Directory.GetParent(directory)?.FullName ?? throw new Exception("Repository data not found");
        using var input = File.OpenRead(Path.Combine(directory, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes"));
        NumericalEventHistory history = NumericalEventHistory.Load(input);
        var model = new ReducedBinaryFlowModel(history);
        ReducedBinaryFlowState initial = model.EvaluateState(0), late = model.EvaluateState(model.DurationSeconds);
        NumericalHorizonState sourceA = history.Evaluate(0).BodyA!.Value;
        Near(model.GravitationalRadiusMeters, 95242.31495423306, 1e-13, "Total mass rg has one raw-normalization conversion");
        Near(initial.BodyA.PositionRg.X, sourceA.PositionMeters.X / model.GravitationalRadiusMeters, 1e-14, "Measured coordinate x mapping");
        Near(initial.BodyA.PositionRg.Y, sourceA.PositionMeters.Z / model.GravitationalRadiusMeters, 1e-14, "SXS vertical z maps to Unity y");
        Near(initial.BodyA.PositionRg.Z, sourceA.PositionMeters.Y / model.GravitationalRadiusMeters, 1e-14, "SXS orbital y maps to Unity z");
        Check(initial.BinaryWeight == 1 && initial.RemnantWeight == 0 && !initial.IsCommonHorizon,
            "Retained binary state selects two-hole field parameters");
        Check(late.BinaryWeight == 0 && late.RemnantWeight == 1 && late.IsCommonHorizon,
            "Retained endpoint selects fully developed assumed remnant ring");
        Near(initial.ReservoirInnerRadiusRg, 1.8 * model.InitialSeparationRg, 1e-14, "Fixed cavity set by initial separation");
        Near(model.EvaluateState(-1).TimeTg, 0, 0, "Time clamps to recorded start");
        Near(model.EvaluateState(model.DurationSeconds + 1).TimeTg, late.TimeTg, 1e-14, "Time clamps to recorded endpoint");
        Reject(() => model.EvaluateState(double.NaN), "Nonfinite epoch");
        Reject(() => model.Sample(initial, double.PositiveInfinity, 0, 0), "Nonfinite position");
        Reject(() => model.Sample(default, 0, 0, 0), "Uninitialized field state");

        double ringMid = (initial.ReservoirInnerRadiusRg + initial.ReservoirOuterRadiusRg) * .5;
        ReducedBinaryFlowSample reservoir = model.Sample(initial, ringMid, 0, 0);
        Near(reservoir.RelativeDensity, .66, 1e-13, "Independent reservoir crest density");
        Near(reservoir.NormalizedBolometricEmission, .66 * Math.Pow(.35, 4)
            * Math.Pow(initial.ReservoirInnerRadiusRg / ringMid, 3), 1e-13, "Reservoir emissivity fixture");
        Near(reservoir.VelocityZOverC, 1 / Math.Sqrt(ringMid), 1e-13, "Prescribed circular coordinate velocity");
        Near(reservoir.VelocityXOverC, -.004, 1e-13, "Declared small reservoir inflow");
        double miniRadius = .5 * (initial.BodyA.InnerRadiusRg + initial.BodyA.OuterRadiusRg);
        ReducedBinaryFlowSample mini = model.Sample(initial, initial.BodyA.PositionRg.X,
            initial.BodyA.PositionRg.Y, initial.BodyA.PositionRg.Z + miniRadius);
        Near(mini.RelativeDensity, 3, 1e-12, "Independent initial minidisk crest fixture");
        Near(mini.NormalizedBolometricEmission, 3 * Math.Pow(1.1, 4)
            * Math.Pow(initial.BodyA.InnerRadiusRg / miniRadius, 3), 1e-12, "Minidisk emissivity fixture");
        ReducedBinaryFlowStream stream = initial.StreamA;
        double streamX = .5 * (stream.StartRg.X + stream.EndRg.X) + 2.5 * stream.BowNormal.X;
        double streamY = .5 * (stream.StartRg.Y + stream.EndRg.Y);
        double streamZ = .5 * (stream.StartRg.Z + stream.EndRg.Z) + 2.5 * stream.BowNormal.Z;
        ReducedBinaryFlowSample streamMid = model.Sample(initial, streamX, streamY, streamZ);
        Near(streamMid.RelativeDensity, .9, 1e-12, "Independent parabolic stream center fixture");
        Near(streamMid.NormalizedBolometricEmission, .9 * Math.Pow(.65, 4), 1e-12, "Stream emissivity fixture");
        double remnantRadius = .5 * (late.Remnant.InnerRadiusRg + late.Remnant.OuterRadiusRg);
        ReducedBinaryFlowSample remnant = model.Sample(late, late.Remnant.PositionRg.X + remnantRadius,
            late.Remnant.PositionRg.Y, late.Remnant.PositionRg.Z);
        Near(remnant.RelativeDensity, 2, 1e-13, "Remnant crest with full smooth envelope");
        Near(remnant.TemperatureKelvin, 1.1e7 * Math.Pow(late.Remnant.InnerRadiusRg / remnantRadius, .75),
            1e-13, "Effective source temperature follows declared fourth-power emission");

        foreach (double time in new[] { 0, .2 * model.DurationSeconds, model.CommonHorizonElapsedSeconds, model.DurationSeconds })
        {
            ReducedBinaryFlowState state = model.EvaluateState(time);
            for (int i = 0; i < 2048; ++i)
            {
                double x = (2 * RadicalInverse(i + 1, 2) - 1) * model.HalfExtentXZ;
                double y = (2 * RadicalInverse(i + 1, 3) - 1) * model.HalfExtentY;
                double z = (2 * RadicalInverse(i + 1, 5) - 1) * model.HalfExtentXZ;
                ReducedBinaryFlowSample p = model.Sample(state, x, y, z);
                Check(double.IsFinite(p.RelativeDensity) && p.RelativeDensity >= 0
                    && double.IsFinite(p.NormalizedBolometricEmission) && p.NormalizedBolometricEmission >= 0
                    && double.IsFinite(p.TemperatureKelvin) && double.IsFinite(p.VelocityXOverC)
                    && double.IsFinite(p.VelocityYOverC) && double.IsFinite(p.VelocityZOverC), "Finite spatial field at all major epochs");
                double velocity2 = p.VelocityXOverC * p.VelocityXOverC + p.VelocityYOverC * p.VelocityYOverC
                    + p.VelocityZOverC * p.VelocityZOverC;
                Check(velocity2 < 1, "Prescribed coordinate velocity remains bounded in tested samples");
            }
            Check(model.Sample(state, model.HalfExtentXZ, 0, 0).RelativeDensity == 0
                && model.Sample(state, 0, model.HalfExtentY, 0).RelativeDensity == 0
                && model.Sample(state, 0, 0, -model.HalfExtentXZ).RelativeDensity == 0,
                "Compact supported field rejects outer box boundaries");
            ReducedBinaryFlowBody center = state.IsCommonHorizon ? state.Remnant : state.BodyA;
            ReducedBinaryFlowSample inside = model.Sample(state, center.PositionRg.X, center.PositionRg.Y, center.PositionRg.Z);
            Check(inside.RelativeDensity == 0 && inside.NormalizedBolometricEmission == 0, "No emission within coordinate horizon proxy");
        }

        double chosen = .27 * model.DurationSeconds;
        ReducedBinaryFlowState sought = model.EvaluateState(chosen);
        ReducedBinaryFlowSample held = model.Sample(sought, ringMid, .4, 1);
        model.EvaluateState(model.DurationSeconds); model.EvaluateState(0);
        ReducedBinaryFlowSample replay = model.Sample(model.EvaluateState(chosen), ringMid, .4, 1);
        Check(held.RelativeDensity == replay.RelativeDensity && held.NormalizedBolometricEmission == replay.NormalizedBolometricEmission
            && held.VelocityXOverC == replay.VelocityXOverC && sought.BodyA.PositionRg.X == model.EvaluateState(chosen).BodyA.PositionRg.X,
            "Arbitrary reverse seek reconstructs bit-identical field independent of previous states and viewpoints");
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        double accumulated = 0;
        for (int i = 0; i < 1000; ++i)
        {
            ReducedBinaryFlowState state = model.EvaluateState(i % 2 == 0 ? chosen : model.DurationSeconds);
            accumulated += model.Sample(state, ringMid, .4, 1).RelativeDensity;
        }
        Check(GC.GetAllocatedBytesForCurrentThread() == beforeAllocation && accumulated > 0,
            "Pure absolute state reconstruction and sampling allocate no per-seek storage");

        // The transition is an explicitly prescribed C1 envelope, not gas evolution from a solver.
        double epsilon = 1e-4 * model.GravitationalTimeSeconds;
        var pre = model.EvaluateState(model.CommonHorizonElapsedSeconds - epsilon);
        var common = model.EvaluateState(model.CommonHorizonElapsedSeconds);
        var post = model.EvaluateState(model.CommonHorizonElapsedSeconds + epsilon);
        Check(pre.BinaryWeight < 3e-12 && common.BinaryWeight == 0 && common.RemnantWeight == 0
            && post.RemnantWeight < 2e-12, "Binary fade and remnant rise have zero value and slope at common horizon");
        for (int i = 0; i < 512; ++i)
        {
            double x = (2 * RadicalInverse(i + 1, 2) - 1) * model.HalfExtentXZ;
            double y = (2 * RadicalInverse(i + 1, 3) - 1) * model.HalfExtentY;
            double z = (2 * RadicalInverse(i + 1, 5) - 1) * model.HalfExtentXZ;
            double difference = Math.Abs(model.Sample(pre, x, y, z).RelativeDensity - model.Sample(post, x, y, z).RelativeDensity);
            Check(difference < 1e-6, "No field jump when numerical individual horizons switch to common horizon");
        }

        // Independent cylindrical/elliptical volume integrals compare complete Cartesian grids.
        // The ring angular contrast integrates to zero. Stream coordinate Jacobian is chord length;
        // the across-stream integral of (1-v^2-h^2)^2 is pi*width*height/3.
        double maximumCoarseMassError = 0, maximumFineMassError = 0;
        double maximumCoarseEmissionError = 0, maximumFineEmissionError = 0;
        foreach (ReducedBinaryFlowState state in new[] { initial, common, late })
        {
            (double expectedMass, double expectedEmission) = IndependentIntegrals(state);
            (double coarseMass, double coarseEmission) = GridIntegrals(model, state, 96);
            (double fineMass, double fineEmission) = GridIntegrals(model, state, 192);
            Near(coarseMass, expectedMass, .001, "96-cubed relative mass versus independent component integrals");
            Near(coarseEmission, expectedEmission, .02, "96-cubed emission proxy versus independent component integrals");
            Near(fineMass, expectedMass, .0001, "192-cubed relative mass versus independent component integrals");
            Near(fineEmission, expectedEmission, .002, "192-cubed emission proxy versus independent component integrals");
            maximumCoarseMassError = Math.Max(maximumCoarseMassError, Math.Abs(coarseMass / expectedMass - 1));
            maximumFineMassError = Math.Max(maximumFineMassError, Math.Abs(fineMass / expectedMass - 1));
            maximumCoarseEmissionError = Math.Max(maximumCoarseEmissionError, Math.Abs(coarseEmission / expectedEmission - 1));
            maximumFineEmissionError = Math.Max(maximumFineEmissionError, Math.Abs(fineEmission / expectedEmission - 1));
            Console.WriteLine($"M4 field integral t={state.ElapsedSeconds:R}: unmasked component mass={expectedMass:R}, emission={expectedEmission:R}; 96 mass={coarseMass:R}, emission={coarseEmission:R}; 192 mass={fineMass:R}, emission={fineEmission:R}");
        }
        // Error for an individual shifted compact feature is not strictly monotone under midpoint refinement.
        Check(maximumFineMassError < maximumCoarseMassError && maximumFineEmissionError < maximumCoarseEmissionError,
            "Spatial refinement improves worst-epoch mass and emission error without changing the source field");
        Console.WriteLine("PASS M4 reduced binary field, SXS binding, deterministic seeks, bounded support, merger continuity and integral refinement");
    }

    private static (double mass, double emission) IndependentIntegrals(ReducedBinaryFlowState s)
    {
        (double mass, double emission) total = RingIntegrals(s.ReservoirInnerRadiusRg, s.ReservoirOuterRadiusRg, .18, .55, .35);
        foreach (ReducedBinaryFlowBody body in new[] { s.BodyA, s.BodyB })
        {
            double strength = 3 * s.BinaryWeight * ReducedBinaryFlowModel.SmoothStep(body.OuterRadiusRg - body.InnerRadiusRg);
            var disk = RingIntegrals(body.InnerRadiusRg, body.OuterRadiusRg, .24, strength, 1.1);
            total.mass += disk.mass; total.emission += disk.emission;
        }
        var remnant = RingIntegrals(s.Remnant.InnerRadiusRg, s.Remnant.OuterRadiusRg, .24, 2 * s.RemnantWeight, 1.1);
        total.mass += remnant.mass; total.emission += remnant.emission;
        foreach (ReducedBinaryFlowStream stream in new[] { s.StreamA, s.StreamB })
        {
            double factor = .9 * s.BinaryWeight * Math.Sqrt(stream.ChordLengthSquaredRg2) * Math.PI * 1.4 * .8 / 3;
            total.mass += factor * 8 / 15;
            total.emission += factor * Simpson(u => 16 * u * u * (1 - u) * (1 - u) * Math.Pow(.4 + .5 * u, 4), 0, 1);
        }
        return total;
    }

    private static (double mass, double emission) RingIntegrals(double a, double b, double aspect, double strength, double temperature)
    {
        if (b <= a || strength == 0) return (0, 0);
        double d = b - a, factor = 2 * Math.PI * aspect * 16 / 15 * strength;
        double radialMass = 16 * d * (a * a / 30 + a * d / 30 + d * d / 105);
        double radialEmission = Simpson(r => { double u = (r - a) / d; return 16 * u * u * (1 - u) * (1 - u) / r; }, a, b);
        return (factor * radialMass, factor * Math.Pow(temperature, 4) * a * a * a * radialEmission);
    }

    private static double Simpson(Func<double, double> f, double a, double b)
    {
        const int n = 2048; double result = f(a) + f(b), step = (b - a) / n;
        for (int i = 1; i < n; ++i) result += (i % 2 == 0 ? 2 : 4) * f(a + i * step);
        return result * step / 3;
    }

    private static (double mass, double emission) GridIntegrals(ReducedBinaryFlowModel model, ReducedBinaryFlowState state, int n)
    {
        double mass = 0, emission = 0;
        for (int z = 0; z < n; ++z)
        for (int y = 0; y < n; ++y)
        for (int x = 0; x < n; ++x)
        {
            var s = model.Sample(state, ((x + .5) / n * 2 - 1) * model.HalfExtentXZ,
                ((y + .5) / n * 2 - 1) * model.HalfExtentY, ((z + .5) / n * 2 - 1) * model.HalfExtentXZ);
            mass += s.RelativeDensity; emission += s.NormalizedBolometricEmission;
        }
        double volume = 8 * model.HalfExtentXZ * model.HalfExtentXZ * model.HalfExtentY / (n * n * n);
        return (mass * volume, emission * volume);
    }

    private static double RadicalInverse(int index, int radix)
    {
        double value = 0, fraction = 1.0 / radix;
        while (index > 0) { value += fraction * (index % radix); index /= radix; fraction /= radix; }
        return value;
    }
}
