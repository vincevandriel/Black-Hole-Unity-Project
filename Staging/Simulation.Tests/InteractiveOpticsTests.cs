using BlackHole.Staging.Simulation;

internal static class InteractiveOpticsTests
{
    private static void Check(bool valid, string name) { if (!valid) throw new Exception(name); }
    private static void Near(double x, double y, double tolerance, string name)
    { Check(double.IsFinite(x) && Math.Abs(x - y) <= tolerance, $"{name}: {x:R} / {y:R}"); }

    public static void Run()
    {
        InteractiveOpticsReference.LensOffset(1, 0, 1000, .01, .02, double.PositiveInfinity, 1, out double dx, out double dy);
        Near(dx, -4 * .01 / 1000, 1e-13, "Weak-deflection coefficient 4GM/(c squared b) at large impact");
        Near(dy, 0, 0, "Point-lens radial offset has no transverse component");
        InteractiveOpticsReference.LensOffset(1, 0, 1000, .02, .04, double.PositiveInfinity, 1, out double doubled, out _);
        Near(doubled / dx, 2, 2e-9, "Doubling mass at fixed geometry doubles weak deflection");
        InteractiveOpticsReference.LensOffset(2, 0, 1000, .01, .02, double.PositiveInfinity, 1, out double wider, out _);
        Near(wider / dx, .5, 2e-9, "Doubling impact parameter halves weak deflection");
        InteractiveOpticsReference.LensOffset(1, 0, 1000, .01, .02, 2000, 1, out double finiteDepth, out _);
        Near(finiteDepth / dx, .5, 1e-14, "Finite source-lens geometry supplies Dls/Ds");
        foreach (double depth in new[] { -10.0, 0, 1, 999, 1000 })
        {
            InteractiveOpticsReference.LensOffset(.1, .2, 1000, .5, 1, depth, 2, out double x, out double y);
            Check(x == 0 && y == 0, "Source at or ahead of lens has zero displacement");
        }
        for (int i = -50; i <= 50; i++)
        {
            InteractiveOpticsReference.LensOffset(i * .0001, .0001, 1, 1, 2, 100, 2, out double x, out double y);
            Check(double.IsFinite(x) && double.IsFinite(y) && Math.Sqrt(x * x + y * y) <= .160000001,
                "Softened near-hole extension remains finite and bounded");
        }
        InteractiveOpticsReference.LensOffset(.1, .2, 10, .5, 1, 100, 0, out dx, out dy);
        Check(dx == 0 && dy == 0, "Zero lens gain is identity");
        foreach (double gain in new[] { .05, .35, 1, 2 })
        foreach (double mass in new[] { .1, .5, 1 })
        {
            double lastMapped = 0;
            for (int i = 1; i <= 1000; i++)
            {
                double angle = i * .001;
                InteractiveOpticsReference.LensOffset(angle, 0, 15, mass, 2 * mass,
                    double.PositiveInfinity, gain, out double x, out double y);
                Check(Math.Abs(x) <= InteractiveOpticsReference.MaximumLensFraction * mass * angle + 1e-14,
                    "Near-field composite displacement obeys the mass-weighted impact bound");
                Check(angle + x > lastMapped && y == 0,
                    "Lens image mapping cannot fold the luminous disk into nested copies");
                lastMapped = angle + x;
            }
        }
        InteractiveOpticsReference.SkyApparentOffset(.1, -.2, 25, .5, 1, .35, out dx, out dy);
        Check(dx > 0 && dy < 0, "Distant apparent star centre deflects away from the lens");
        InteractiveOpticsReference.LensOffset(.1, -.2, 25, .5, 1, double.PositiveInfinity, .35,
            out double inverseX, out double inverseY);
        Near(dx, -inverseX, 0, "Star centre uses the forward sign of the bounded weak lens");
        Near(dy, -inverseY, 0, "Both forward star coordinates share the lens convention");
        InteractiveOpticsReference.SkyApparentOffset(.1, -.2, 25, .5, 1, 0, out dx, out dy);
        Check(dx == 0 && dy == 0, "Zero sky lens gain leaves immutable star centres unchanged");
        InteractiveOpticsReference.WaveOffset(.1, .1, 1, 0, 1, out dx, out dy);
        Near(dx, -dy, 1e-15, "Plus polarization stretches and squeezes perpendicular axes");
        InteractiveOpticsReference.WaveOffset(.1, 0, 0, 1, 1, out dx, out dy);
        Check(dx == 0 && dy > 0, "Cross polarization supplies rotated transverse response");
        InteractiveOpticsReference.WaveOffset(1, 1, 99, 99, 2, out dx, out dy);
        Check(Math.Sqrt(dx * dx + dy * dy) <= .035000001, "Enhanced wave gain remains bounded");
        InteractiveOpticsReference.WaveOffset(.1, .2, 2, 3, 0, out dx, out dy);
        Check(dx == 0 && dy == 0, "Zero wave gain is identity");
        InteractiveOpticsReference.WaveOffset(0, 0, 1, 1, 2, out dx, out dy);
        Check(dx == 0 && dy == 0, "Wave display has no singular centre");
        InteractiveOpticsReference.WaveOffset(.2, .1, .2, 0, 1.3, out dx, out dy);
        Check(Math.Sqrt(dx * dx + dy * dy) > .005,
            "A modest normalized wave produces a legible amplified angular displacement");
        InteractiveOpticsReference.WaveOffset(.2, .1, -.2, 0, 1.3, out double reverseX, out double reverseY);
        Near(dx, -reverseX, 1e-15, "Opposite wave phase reverses horizontal distortion");
        Near(dy, -reverseY, 1e-15, "Opposite wave phase reverses vertical distortion");

        var model = new InteractiveBinaryScenario(new InteractiveBinaryParameters());
        double time = .5 * model.CutoffTimeSeconds;
        Near(InteractiveOpticsReference.WaveHistoryRadius(8), 96, 0,
            "Wave presentation declares twelvefold radial compression");
        double radius1 = InteractiveOpticsReference.WaveHistoryRadius(4);
        double radius2 = InteractiveOpticsReference.WaveHistoryRadius(9);
        double propagationSeconds = (radius2 - radius1) * model.GravitationalTimeSeconds;
        var innerFront = model.EvaluateRetardedSignal(time, radius1);
        var outerFront = model.EvaluateRetardedSignal(time + propagationSeconds, radius2);
        Near(innerFront.HPlus!.Value, outerFront.HPlus!.Value, 1e-19,
            "A signed front propagates outward with exact source timing through compressed display radii");
        Near(innerFront.HCross!.Value, outerFront.HCross!.Value, 1e-19,
            "Both wave polarizations share the same compressed outward propagation delay");
        for (int i = 0; i <= 255; i++)
        {
            double radius = 4 * model.HalfExtentXZ * i / 255;
            double emission = InteractiveOpticsReference.RetardedTime(time, radius, model.GravitationalTimeSeconds);
            var direct = model.EvaluateSignal(emission); var retarded = model.EvaluateRetardedSignal(time, radius);
            Check(retarded.Coverage == direct.Coverage, "Retarded history has identical coverage to explicitly delayed sample");
            Near(retarded.HPlus!.Value, direct.HPlus!.Value, 0, "Retarded plus signal uses source history exactly");
            Near(retarded.HCross!.Value, direct.HCross!.Value, 0, "Retarded cross signal uses source history exactly");
        }
        Check(!model.EvaluateRetardedSignal(0, 1).IsAvailable, "Before-history radial bins remain unavailable");
        var late = model.Evaluate(model.CutoffTimeSeconds + .1 * model.GravitationalTimeSeconds);
        Check(!late.Signal.IsAvailable && late.Signal.Kind == InteractiveSignalKind.QuadrupoleStrain,
            "Missing present-time signal retains its physical signal kind");
        Check(model.EvaluateRetardedSignal(late.ElapsedSeconds, 1).IsAvailable,
            "Outgoing delayed signal can remain available after source cutoff");
        var signal = model.EvaluateSignal(time);
        InteractiveOpticsReference.WaveOffset(.1, .2, signal.HPlus!.Value / InteractiveBinaryScenario.ReferenceStrainAmplitude,
            signal.HCross!.Value / InteractiveBinaryScenario.ReferenceStrainAmplitude, 2, out _, out _);
        var after = model.EvaluateSignal(time);
        Near(after.HPlus!.Value, signal.HPlus.Value, 0, "Cosmetic gain does not mutate source waveform");
        Near(after.HCross!.Value, signal.HCross.Value, 0, "Cosmetic gain leaves the other polarization unchanged");
    }
}
