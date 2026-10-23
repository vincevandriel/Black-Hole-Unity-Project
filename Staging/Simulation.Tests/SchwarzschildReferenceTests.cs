using BlackHole.Staging.Simulation;

internal static class SchwarzschildReferenceTests
{
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); }
    private static void Near(double actual, double expected, double tolerance, string label)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected)),
        $"{label}: expected {expected:R}, got {actual:R}"); }

    public static void Run()
    {
        Near(SchwarzschildReference.CriticalImpactParameterM, Math.Sqrt(27), 1e-14, "Critical impact parameter in M units");
        // Bruneton uses r_s=1, so b_author=b_M/2 and e_author=1/b_author.
        Near(Math.Pow(2 / SchwarzschildReference.CriticalImpactParameterM, 2), 4.0 / 27, 1e-14,
            "Bruneton critical e squared after unit conversion");
        Check(SchwarzschildReference.HorizonRadiusM == 2 && SchwarzschildReference.PhotonSphereRadiusM == 3
            && SchwarzschildReference.InnermostStableCircularOrbitRadiusM == 6, "Distinct horizon, photon sphere and ISCO");

        foreach (double radius in new[] { 2.1, 2.5, 10.0, 100.0 })
        {
            var inward = SchwarzschildReference.Trace(new NumericalVector3(radius, 0, 0), new NumericalVector3(-1, 0, 0));
            var outward = SchwarzschildReference.Trace(new NumericalVector3(radius, 0, 0), new NumericalVector3(1, 0, 0));
            Check(inward.Outcome == SchwarzschildRayOutcome.Captured && outward.Outcome == SchwarzschildRayOutcome.Escaped,
                "Radial capture and escape, including observers inside the photon sphere");
            Near(inward.PositionM.Magnitude, SchwarzschildReference.CaptureRadiusM, 1e-14, "Capture boundary");
            Near(outward.PositionM.Magnitude, 1000, 1e-14, "Escape boundary");
        }

        foreach (double relativeOffset in new[] { -0.01, -0.00001, 0.00001, 0.01 })
        {
            double impact = SchwarzschildReference.CriticalImpactParameterM * (1 + relativeOffset);
            var ray = Scatter(impact, 1000, 0.005);
            Check(ray.Outcome == (relativeOffset < 0 ? SchwarzschildRayOutcome.Captured : SchwarzschildRayOutcome.Escaped),
                "Near-critical capture / escape classification");
            Check(ray.MaximumNullResidual < 2e-8, "Near-critical null first-integral residual");
        }

        // Independent quadrature of the first integral, not a second copy of the RK4 evolution.
        // x=r_min/r, x=1-t^2 removes the turning-point singularity analytically.
        foreach (double periapsis in new[] { 3.01, 3.1, 3.5, 6.0, 10.0, 30.0 })
        {
            double impact = periapsis / Math.Sqrt(1 - 2 / periapsis);
            double quadratureAngle = ScatteringAngleByQuadrature(periapsis, 1000);
            var ray = Scatter(impact, 1000, 0.005);
            Check(ray.Outcome == SchwarzschildRayOutcome.Escaped, "Scattering ray escapes");
            Near(ray.SweptAngleRadians, quadratureAngle, 3e-8, "Independent apsis quadrature");
            Check(ray.MaximumNullResidual < 2e-8, "Scattering null first integral");
            var coarse = Scatter(impact, 1000, 0.02);
            Check(Math.Abs(ray.SweptAngleRadians - quadratureAngle) <=
                Math.Abs(coarse.SweptAngleRadians - quadratureAngle) + 1e-10, "Angular-step refinement converges");
        }

        var criticalOrbit = SchwarzschildReference.Trace(new NumericalVector3(3, 0, 0),
            new NumericalVector3(0, 1, 0), angularStep: 0.005, maximumSteps: 20);
        Check(criticalOrbit.Outcome == SchwarzschildRayOutcome.StepLimit && criticalOrbit.Steps == 20,
            "Unresolved photon orbit reports its work limit, never a fabricated capture");
        Near(criticalOrbit.PositionM.Magnitude, 3, 1e-12, "Circular photon orbit radius");

        double b = 9, sin = b * Math.Sqrt(0.98) / 100;
        double cos = Math.Sqrt(1 - sin * sin);
        var original = SchwarzschildReference.Trace(new NumericalVector3(100, 0, 0), new NumericalVector3(-cos, sin, 0));
        var rotated = SchwarzschildReference.Trace(new NumericalVector3(0, 100, 0), new NumericalVector3(0, -cos, sin));
        Check(original.Outcome == rotated.Outcome && original.Steps == rotated.Steps, "Rotating the arbitrary viewpoint preserves outcome");
        Near(original.PositionM.X, rotated.PositionM.Y, 1e-12, "Rotated ray x to y");
        Near(original.PositionM.Y, rotated.PositionM.Z, 1e-12, "Rotated ray y to z");

        var normal = new NumericalVector3(0, 1, 0);
        var above = SchwarzschildReference.TraceDisk(new NumericalVector3(0, 30, 0), new NumericalVector3(10, -30, 0), normal);
        var below = SchwarzschildReference.TraceDisk(new NumericalVector3(0, -30, 0), new NumericalVector3(10, 30, 0), normal);
        Check(above.Outcome == SchwarzschildRayOutcome.DiskHit && below.Outcome == SchwarzschildRayOutcome.DiskHit,
            "Opaque disk intersections from above and below");
        Near(above.PositionM.Y, 0, 1e-12, "Exact disk-plane intersection");
        Near(above.PositionM.X, below.PositionM.X, 1e-12, "Disk reflection symmetry");
        Near(above.ObservedToEmittedFrequencyRatio, below.ObservedToEmittedFrequencyRatio, 1e-12,
            "Face-on frequency shift symmetry");
        Near(above.ObservedToEmittedFrequencyRatio,
            Math.Sqrt((1 - 3 / above.PositionM.Magnitude) / (1 - 2.0 / 30)), 1e-12,
            "Circular transverse Doppler and gravitational shift, counted once");
        var approaching = SchwarzschildReference.TraceDisk(new NumericalVector3(0, 8, -30),
            new NumericalVector3(10, -8, 30), normal);
        var receding = SchwarzschildReference.TraceDisk(new NumericalVector3(0, 8, -30),
            new NumericalVector3(-10, -8, 30), normal);
        Check(approaching.Outcome == SchwarzschildRayOutcome.DiskHit && receding.Outcome == SchwarzschildRayOutcome.DiskHit,
            "Inclined observer sees both disk sides");
        Near(approaching.PositionM.Magnitude, receding.PositionM.Magnitude, 1e-12, "Symmetric disk hit radii");
        Check(approaching.ObservedToEmittedFrequencyRatio > receding.ObservedToEmittedFrequencyRatio,
            "Approaching prograde side is blueshifted relative to receding side");
        Check(SchwarzschildReference.TraceDisk(new NumericalVector3(30, 0, 0), new NumericalVector3(-1, 0, 1), normal)
            .Outcome == SchwarzschildRayOutcome.CoplanarDisk, "Infinitely thin coplanar ambiguity is explicit");

        Near(SchwarzschildReference.StaticFrequencyRatio(6, 30), Math.Sqrt(5.0 / 7), 1e-14, "Static gravitational frequency shift");
        Near(SchwarzschildReference.StaticFrequencyRatio(6, 30) * SchwarzschildReference.StaticFrequencyRatio(30, 6),
            1, 1e-14, "Reciprocal static redshift");
        Near(SchwarzschildReference.CircularEmitterFrequencyRatio(6, 30, 1)
            / SchwarzschildReference.CircularEmitterFrequencyRatio(6, 30, -1), 3, 1e-14, "ISCO Doppler ratio from beta=0.5");

        MustReject(() => SchwarzschildReference.Trace(new NumericalVector3(2, 0, 0), new NumericalVector3(1, 0, 0)), "Observer on horizon");
        MustReject(() => SchwarzschildReference.Trace(new NumericalVector3(10, 0, 0), default), "Zero direction");
        MustReject(() => SchwarzschildReference.Trace(new NumericalVector3(10, 0, 0), new NumericalVector3(1, 0, 0), maximumSteps: 0), "Zero work budget");
        MustReject(() => SchwarzschildReference.TraceDisk(new NumericalVector3(10, 0, 0), new NumericalVector3(1, 1, 0), normal, innerRadiusM: 5), "Disk below stable-orbit domain");
        MustReject(() => SchwarzschildReference.CircularEmitterFrequencyRatio(6, 30, double.NaN), "Nonfinite angle");

        // Warm up once, then verify the control is allocation-free per ray.
        _ = Scatter(9, 100, 0.005);
        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) _ = Scatter(9, 100, 0.005);
        Check(GC.GetAllocatedBytesForCurrentThread() == bytesBefore, "No managed allocation per optical reference ray");
    }

    private static SchwarzschildRayResult Scatter(double impact, double observerRadius, double step)
    {
        double sin = impact * Math.Sqrt(1 - 2 / observerRadius) / observerRadius;
        return SchwarzschildReference.Trace(new NumericalVector3(observerRadius, 0, 0),
            new NumericalVector3(-Math.Sqrt(1 - sin * sin), sin, 0), observerRadius, step);
    }

    private static double ScatteringAngleByQuadrature(double periapsis, double boundary)
    {
        const int intervals = 20000;
        double limit = Math.Sqrt(1 - periapsis / boundary), width = limit / intervals;
        double sum = Integrand(0, periapsis) + Integrand(limit, periapsis);
        for (int i = 1; i < intervals; i++) sum += (i % 2 == 0 ? 2 : 4) * Integrand(i * width, periapsis);
        return 2 * width * sum / 3;
    }

    private static double Integrand(double t, double periapsis)
    {
        double x = 1 - t * t;
        return 2 / Math.Sqrt(1 + x - (2 / periapsis) * (1 + x + x * x));
    }

    private static void MustReject(Action action, string label)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Optical reference did not reject " + label);
    }
}
