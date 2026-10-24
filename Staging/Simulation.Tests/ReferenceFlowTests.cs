using BlackHole.Staging.Simulation;

internal static class ReferenceFlowTests
{
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); }
    private static void Near(double actual, double expected, double tolerance, string label)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1e-300, Math.Abs(expected)),
        $"{label}: expected {expected:R}, got {actual:R}"); }
    private static void Reject(Action action, string label)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Expected rejected input: " + label);
    }

    public static void Run()
    {
        var model = new ReferenceFlowModel();
        double omega = 1 / Math.Pow(12, 1.5);
        ReferenceFlowSample crest = model.Evaluate(12, 0, 0, 0);
        Near(crest.RelativeDensity, 1.3, 1e-14, "Analytic density crest");
        Near(crest.TemperatureKelvin, 1e7 * Math.Pow(1.5, -.75), 1e-14, "Declared prescribed gas temperature");
        Near(crest.VelocityZOverC, 1 / Math.Sqrt(12), 1e-14, "Coordinate circular speed");
        Near(model.Evaluate(12, 0, 0, Math.PI / (2 * omega)).RelativeDensity, .7, 1e-14, "Advected two-arm trough");
        double time = 123.45, angle = omega * time;
        ReferenceFlowSample advected = model.Evaluate(12 * Math.Cos(angle), 0, 12 * Math.Sin(angle), time);
        Near(advected.RelativeDensity, crest.RelativeDensity, 1e-14, "Density follows circular characteristic");
        Near(model.Evaluate(12, 0, 0, -1).RelativeDensity, crest.RelativeDensity, 1e-14, "Time clamps without wrapping");
        Check(model.Evaluate(0, 0, 0, 0).RelativeDensity == 0 && model.Evaluate(6, 0, 0, 0).RelativeDensity == 0
            && model.Evaluate(18, 0, 0, 0).RelativeDensity == 0 && model.Evaluate(12, 4, 0, 0).RelativeDensity == 0,
            "Compact supported torus and empty cavity");
        Reject(() => model.Evaluate(double.NaN, 0, 0, 0), "Nonfinite position");
        Reject(() => model.Evaluate(12, 0, 0, double.PositiveInfinity), "Nonfinite time");

        var cache = new ReferenceFlowCache(model, 32);
        Check(cache.ResidentFrameCount == 2 && cache.AllocatedFieldBytes == 1048576, "Exactly two 32-cubed float4 frames");
        Check(cache.GeneratedFrameCount == 2 && cache.Snapshot.Lower.Index == 0 && cache.Snapshot.Upper.Index == 1,
            "Indexed cache initial bracket");
        ReferenceFlowSnapshot snapshot = cache.Seek(8.5);
        Check(cache.GeneratedFrameCount == 3 && snapshot.Lower.Index == 1 && snapshot.Upper.Index == 2,
            "Sequential seek reuses one frame");
        Near(snapshot.Blend, .0625, 1e-15, "Temporal blend is physical epoch fraction");
        int generated = cache.GeneratedFrameCount;
        cache.Seek(9); Check(cache.GeneratedFrameCount == generated, "Same bracket does not regenerate data");
        snapshot = cache.Seek(ReferenceFlowModel.DurationTg);
        Check(snapshot.Lower.Index == 127 && snapshot.Upper.Index == 128 && snapshot.Blend == 1,
            "Last endpoint has valid two-frame bracket");
        snapshot = cache.Seek(120.5);
        float[] retained = (float[])snapshot.Lower.PackedRgba.Clone();
        ReferenceFlowSample retainedSample = snapshot.Sample(11, .6, 3);
        cache.Seek(950); snapshot = cache.Seek(120.5);
        Check(retained.AsSpan().SequenceEqual(snapshot.Lower.PackedRgba), "Evicted frame reconstructs identically on reverse seek");
        Near(snapshot.Sample(11, .6, 3).RelativeDensity, retainedSample.RelativeDensity, 1e-15,
            "Cache displacement does not alter requested sample");
        Check(snapshot.Sample(30, 0, 0).RelativeDensity == 0, "Out-of-domain is vacuum in bounded reference");
        Reject(() => cache.Seek(double.NaN), "NaN cache request");
        Reject(() => new ReferenceFlowCache(model, 129), "Unbounded field size");

        // Check cell-centre packing and density-weighted interpolation.
        snapshot = cache.Seek(80);
        int ix = 23, iy = 16, iz = 17, n = cache.Resolution;
        double x = ((ix + .5) / n * 2 - 1) * 20, y = ((iy + .5) / n * 2 - 1) * 6, z = ((iz + .5) / n * 2 - 1) * 20;
        ReferenceFlowSample analytic = model.Evaluate(x, y, z, 80), sampled = snapshot.Sample(x, y, z);
        Near(sampled.RelativeDensity, analytic.RelativeDensity, 7e-8, "Float4 source density round-trip");
        Near(sampled.TemperatureKelvin, analytic.TemperatureKelvin, 1.5e-7, "Density-weighted temperature round-trip");
        Near(sampled.VelocityXOverC, analytic.VelocityXOverC, 1.5e-7, "Density-weighted velocity round-trip");

        // Compare independent cylindrical integrals with Cartesian voxel sums.
        // Emission proxy: integral rho*(T/T0)^4 dV.
        const double expectedMass = 1793.9333458743354;
        const double expectedBolometricProxy = 533.0242888578038;
        Near(ReferenceFlowModel.IntegratedRelativeDensityVolumeRg3, expectedMass, 1e-14, "Analytic normalized mass fixture");
        (double mass32, double emission32) = Integrals(cache.Seek(0).Lower);
        Near(mass32, expectedMass, .001, "32-cubed density integral versus analytic integral");
        Near(emission32, expectedBolometricProxy, .001, "32-cubed emission integral versus independent cylindrical quadrature");
        var fine = new ReferenceFlowCache(model, 64);
        (double mass64, double emission64) = Integrals(fine.Snapshot.Lower);
        Near(mass64, expectedMass, .00005, "64-cubed density integral convergence");
        Near(emission64, expectedBolometricProxy, .00005, "64-cubed emission integral convergence");
        Check(Math.Abs(mass64 - expectedMass) < Math.Abs(mass32 - expectedMass)
            && Math.Abs(emission64 - expectedBolometricProxy) < Math.Abs(emission32 - expectedBolometricProxy),
            "Refinement improves both field diagnostics");
        Near(Integrals(cache.Seek(312).Lower).mass, mass32, 1e-7, "Azimuthal pattern does not change integrated mass");
        var tinyCache = new ReferenceFlowCache(model, 8);
        tinyCache.Seek(16);
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 32; ++i) tinyCache.Seek(i % 2 == 0 ? 800 : 16);
        Check(GC.GetAllocatedBytesForCurrentThread() == allocationStart, "Seeks refill bounded buffers without allocating");

        ReferenceFlowTracer[] tracers = ReferenceFlowTracers.Create(4096);
        Near(tracers.Sum(t => t.WeightRelativeMass), expectedMass, 1e-14, "Tracer total normalized mass independent of count");
        ReferenceFlowTracer[] nearBoundaryTracers = ReferenceFlowTracers.Create(65536);
        Check(nearBoundaryTracers.All(t => t.WeightRelativeMass > 0), "Maximum marker count retains positive near-boundary weights");
        Near(nearBoundaryTracers.Sum(t => t.WeightRelativeMass), expectedMass, 1e-13, "Large marker set retains normalized mass");
        foreach (int count in new[] { 1, 17, 129, 8192 })
        {
            ReferenceFlowTracer[] reduced = ReferenceFlowTracers.Resample(tracers, count);
            Check(reduced.Length == count && reduced.All(t => t.WeightRelativeMass > 0), "Resampling positive finite weights");
            Near(reduced.Sum(t => t.WeightRelativeMass), expectedMass, 1e-14, "Resampling conserves represented measure");
        }
        ReferenceFlowTracer tracer = tracers[19];
        ReferenceFlowPosition p = tracer.Evaluate(73), q = tracer.Evaluate(800), replay = tracer.Evaluate(73);
        Check(p.X == replay.X && p.Y == replay.Y && p.Z == replay.Z && p.X != q.X, "Absolute trajectory seeking is deterministic");
        Near(Math.Sqrt(p.X * p.X + p.Z * p.Z), tracer.RadiusRg, 1e-14, "Tracer circular radius retained");
        Near(p.Y, tracer.HeightRg, 1e-14, "Tracer height retained");
        ReferenceFlowPosition before = tracer.Evaluate(73 - 1e-4), after = tracer.Evaluate(73 + 1e-4);
        ReferenceFlowSample along = model.Evaluate(p.X, p.Y, p.Z, 73);
        Near((after.X - before.X) / 2e-4, along.VelocityXOverC, 1e-8, "Tracer derivative matches declared velocity x");
        Near((after.Z - before.Z) / 2e-4, along.VelocityZOverC, 1e-8, "Tracer derivative matches declared velocity z");
        ReferenceFlowPosition initial = tracer.Evaluate(0);
        Near(along.RelativeDensity, model.Evaluate(initial.X, initial.Y, initial.Z, 0).RelativeDensity, 1e-13,
            "Tracer and density pattern share the same circular characteristic");
        Reject(() => ReferenceFlowTracers.Create(0), "Zero tracers use renderer-off rather than invalid sample");
        Reject(() => ReferenceFlowTracers.Resample(Array.Empty<ReferenceFlowTracer>(), 10), "Empty resampling source");

        // Fixtures independently evaluated in Python with SI 2019 exact h,k,c and math.expm1.
        Near(RadiativeTransferReference.PlanckRadiance(5e14, 5778), 2.9430382085089117e-8, 3e-14, "Optical Planck fixture");
        Near(RadiativeTransferReference.PlanckRadiance(2.417989242e17, 1e7), 95.1242046607645, 3e-14, "One-keV Planck fixture");
        Near(RadiativeTransferReference.PlanckRadiance(1e10, 1e7), 3.072358300755773e-13, 3e-14, "Rayleigh-Jeans stable small-exponent branch");
        Check(RadiativeTransferReference.PlanckRadiance(1e20, 1) == 0, "Wien tail underflow is bounded");
        double exact = 2 * Math.Exp(-1.2) + 7 * (1 - Math.Exp(-1.2));
        Near(RadiativeTransferReference.ConstantSlab(2, 7, .3, 4), exact, 1e-15, "Exact constant slab reference");
        double divided = 2;
        for (int i = 0; i < 100; ++i) divided = RadiativeTransferReference.ConstantSlab(divided, 7, .3, .04);
        Near(divided, exact, 1e-14, "Segmented transfer agrees with exact whole slab");
        Near(RadiativeTransferReference.EmissionFraction(1e-12), 9.999999999995e-13, 1e-14, "Tiny optical depth does not cancel to zero");
        Check(RadiativeTransferReference.ConstantSlab(2, 7, 0, 4) == 2
            && RadiativeTransferReference.ConstantSlab(2, 7, 1e3, 4) == 7, "Transparent and saturated limits");
        Near(RadiativeTransferReference.SchwarzschildStaticFrequencyRatio(8), Math.Sqrt(.75), 1e-15, "Static Schwarzschild redshift to infinity");
        Near(RadiativeTransferReference.SchwarzschildStaticFrequencyRatio(8, 4), Math.Sqrt(1.5), 1e-15, "Static finite-observer blueshift");
        Near(RadiativeTransferReference.SpecialRelativisticDoppler(.36, .6), 2, 1e-15, "Approaching local SR shift");
        Near(RadiativeTransferReference.SpecialRelativisticDoppler(.36, -.6), .5, 1e-15, "Receding local SR shift");
        double shift = .71, frequency = 2.417989242e17;
        Near(RadiativeTransferReference.ShiftedPlanckRadiance(frequency, 1e7, shift),
            shift * shift * shift * RadiativeTransferReference.PlanckRadiance(frequency / shift, 1e7), 1e-14,
            "Invariant radiance shift is applied once, not twice");
        Reject(() => RadiativeTransferReference.SchwarzschildStaticFrequencyRatio(2), "Horizon static observer");
        Reject(() => RadiativeTransferReference.SpecialRelativisticDoppler(1, 0), "Non-timelike local emitter");
        Reject(() => RadiativeTransferReference.ConstantSlab(0, 1, -.2, 1), "Negative opacity");

        double peakRadius = 49.0 / 36 * 6;
        double peakT = RadiativeTransferReference.NewtonianZeroTorqueTemperature(peakRadius);
        Near(peakT, 3104509.5393404234, 1e-14, "Independent Newtonian thin-disk peak temperature fixture");
        Check(peakT > RadiativeTransferReference.NewtonianZeroTorqueTemperature(peakRadius * .99)
            && peakT > RadiativeTransferReference.NewtonianZeroTorqueTemperature(peakRadius * 1.01), "Zero-torque peak at 49/36 inner radius");
        Check(RadiativeTransferReference.NewtonianZeroTorqueTemperature(6) == 0, "Zero-torque inner boundary");
        double t12 = RadiativeTransferReference.NewtonianZeroTorqueTemperature(12), t24 = RadiativeTransferReference.NewtonianZeroTorqueTemperature(24);
        Near(Math.Pow(t12 / t24, 4), 8 * (1 - Math.Sqrt(.5)) / .5, 1e-14, "Thin-disk radial surface flux law");
        Near(RadiativeTransferReference.NewtonianZeroTorqueTemperature(12, 35, 8e16) / t12, 2, 1e-14, "Effective temperature follows supply to one-quarter power");
        Near(RadiativeTransferReference.NewtonianZeroTorqueTemperature(12, 70) / t12, 1 / Math.Sqrt(2), 1e-14,
            "At fixed radius in rg and fixed supply, effective temperature scales as mass to minus one-half");
        Console.WriteLine("PASS M3 reference flow, bounded cache, weighted tracers, emission and shift fixtures");
    }

    private static (double mass, double emission) Integrals(ReferenceFlowFrame frame)
    {
        double mass = 0, emission = 0;
        for (int i = 0; i < frame.PackedRgba.Length; i += 4)
        {
            double rho = frame.PackedRgba[i];
            if (rho <= 0) continue;
            double normalizedTemperature = frame.PackedRgba[i + 1] / rho;
            mass += rho; emission += rho * Math.Pow(normalizedTemperature, 4);
        }
        double voxel = 40.0 * 12 * 40 / (frame.Resolution * frame.Resolution * frame.Resolution);
        return (mass * voxel, emission * voxel);
    }
}
