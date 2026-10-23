using BlackHole.Staging.Simulation;
using System.Diagnostics;

internal static class GeneratedFluidTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Near(double actual, double expected, double tolerance, string message)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"{message}: expected {expected:R}, actual {actual:R}"); }
    private static void Advance(GeneratedFluidSolver solver, double duration, GeneratedFluidForcing forcing = default)
    {
        double t = 0;
        while (duration - t > 1e-14) { double dt = solver.StableStep(duration - t, forcing); solver.Step(dt, forcing); t += dt; }
    }
    private static void CheckBudget(GeneratedFluidDiagnostics d, string name)
    {
        Check(d.FinalMass > 0 && double.IsFinite(d.FinalMass) && d.MinimumDensity > 0, name + " positive finite mass and density");
        Near(d.MassBudgetResidual, 0, 2e-10 * Math.Max(1, d.InitialMass), name + " conservative mass ledger closes");
        Check(d.AddedFloorMass >= 0 && d.RemovedSinkMass >= 0 && d.NetOutflowMass >= 0, name + " source/loss ledger signs");
        Check(d.MaxCourantNumber <= GeneratedFluidSolver.CourantLimit * (1 + 1e-11), name + " actual multidimensional CFL bounded");
    }
    private static double SmoothModeError(int n, bool acoustic)
    {
        const double epsilon = 1e-5, cs = .4, drift = .3, time = 1.1;
        double amplitude = Math.Sin(Math.PI / n) / (Math.PI / n); // Exact initial cell average of sin(pi*x).
        var grid = new GeneratedFluidSolver(n, 1, cs, 1e-10, (x, z) =>
        {
            double wave = epsilon * amplitude * Math.Sin(Math.PI * x);
            return acoustic ? new GeneratedFluidSample(1 + wave, cs * wave, 0)
                : new GeneratedFluidSample(1, drift, wave);
        }, GeneratedFluidBoundary.Periodic);
        Advance(grid, time); CheckBudget(grid.Diagnostics, acoustic ? "acoustic" : "transverse advection");
        double error = 0;
        for (int x = 0; x < n; x++)
        {
            double reference = epsilon * amplitude * Math.Sin(Math.PI * (grid.CellCenter(x) - (acoustic ? cs : drift) * time));
            double actual = acoustic ? grid.GetCell(x, n / 2).SurfaceDensity - 1 : grid.GetCell(x, n / 2).VelocityZRgPerTg;
            error += Math.Abs(actual - reference) / n / epsilon;
        }
        return error;
    }
    private static double ShockError(int n, out double starDensity)
    {
        // Independent exact isothermal Riemann solution: rhoL=4, rhoR=1, uL=uR=0, cs=1.
        // Left rarefaction and right shock meet where ln(rho*/4)+(rho*-1)/sqrt(rho*)=0.
        double low = 1, high = 4;
        for (int i = 0; i < 100; i++)
        { double m = .5 * (low + high); if (Math.Log(m / 4) + (m - 1) / Math.Sqrt(m) > 0) high = m; else low = m; }
        starDensity = .5 * (low + high); double rhoStar = starDensity, uStar = -Math.Log(rhoStar / 4), shockSpeed = Math.Sqrt(rhoStar);
        const double time = .2;
        var grid = new GeneratedFluidSolver(n, 1, 1, 1e-10,
            (x, z) => new GeneratedFluidSample(x < 0 ? 4 : 1, 0, 0), GeneratedFluidBoundary.Outflow);
        Advance(grid, time); CheckBudget(grid.Diagnostics, "isothermal shock tube");
        double error = 0;
        for (int x = 0; x < n; x++)
        {
            double xi = grid.CellCenter(x) / time;
            double exact = xi < -1 ? 4 : xi < uStar - 1 ? 4 * Math.Exp(-(xi + 1)) : xi < shockSpeed ? rhoStar : 1;
            error += Math.Abs(grid.GetCell(x, n / 2).SurfaceDensity - exact) / n;
        }
        int starCell = (int)((.12 + 1) / grid.CellWidthRg);
        Near(grid.GetCell(starCell, n / 2).SurfaceDensity, rhoStar, n >= 128 ? .08 : .22, "shock star plateau approaches exact jump solution");
        return error;
    }
    public static void Run()
    {
        var uniform = new GeneratedFluidSolver(16, 1, .2, 1e-9, (x, z) => new GeneratedFluidSample(2, .1, -.07), GeneratedFluidBoundary.Periodic);
        Advance(uniform, 1);
        foreach (int z in new[] { 0, 5, 15 }) foreach (int x in new[] { 0, 9, 15 })
        { var c = uniform.GetCell(x, z); Near(c.SurfaceDensity, 2, 1e-14, "uniform periodic density"); Near(c.VelocityXRgPerTg, .1, 1e-14, "uniform periodic vx"); Near(c.VelocityZRgPerTg, -.07, 1e-14, "uniform periodic vz"); }
        CheckBudget(uniform.Diagnostics, "uniform periodic");

        double adv32 = SmoothModeError(32, false), adv64 = SmoothModeError(64, false);
        double sound32 = SmoothModeError(32, true), sound64 = SmoothModeError(64, true), sound128 = SmoothModeError(128, true);
        Check(adv64 < .7 * adv32 && adv64 < .09, "independent exact transverse-advection solution converges under doubling");
        Check(sound64 < .7 * sound32 && sound128 < .7 * sound64 && sound128 < .05,
            "right-moving acoustic phase/amplitude converge toward independent linearized sound solution");
        double shock64 = ShockError(64, out double star), shock128 = ShockError(128, out _);
        Check(shock128 < .8 * shock64, "nonlinear isothermal rarefaction/shock solution improves with spatial refinement");
        Console.WriteLine($"Generated fluid PDE fixtures: advection L1 {adv32:G6}->{adv64:G6}; sound {sound32:G6}->{sound64:G6}->{sound128:G6}; shock {shock64:G6}->{shock128:G6}, rho*={star:G9}.");

        var source = new GeneratedFluidSource(2, -1, .6, 2, 3, 4);
        var forcing = new GeneratedFluidForcing(source);
        forcing.Evaluate(5, 3, out double gx, out double gz, out double sink);
        Near(gx, -.6 * 3 / Math.Pow(29, 1.5), 1e-16, "independent softened Newtonian gx");
        Near(gz, -.6 * 4 / Math.Pow(29, 1.5), 1e-16, "independent softened Newtonian gz"); Near(sink, 0, 0, "outside sink support");
        forcing.Evaluate(2, -1, out gx, out gz, out sink);
        Near(gx, 0, 0, "softened center finite gx"); Near(gz, 0, 0, "softened center finite gz"); Near(sink, .25, 1e-16, "center sink time");
        forcing.Evaluate(5, -1, out _, out _, out sink); Near(sink, 0, 0, "sink boundary continuous to zero");
        var draining = new GeneratedFluidSolver(32, 4, .04, 1e-7, (x, z) => new GeneratedFluidSample(1, .2, -.1));
        Advance(draining, 4, forcing); CheckBudget(draining.Diagnostics, "forced draining field");
        Check(draining.Diagnostics.RemovedSinkMass > 0 && draining.Diagnostics.NetOutflowMass > 0, "sinks and open edges remove accounted mass");
        var floor = new GeneratedFluidSolver(16, 1, .1, 1e-7, (x, z) => new GeneratedFluidSample(1e-7, .3, 0));
        Advance(floor, .2); CheckBudget(floor.Diagnostics, "floor bookkeeping");
        Check(floor.Diagnostics.AddedFloorMass > 0, "floor injections measured separately from accretion/outflow");
        try { uniform.Step(100); throw new Exception("Unstable step was accepted"); } catch (ArgumentOutOfRangeException) { }

        string root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Repository data not found");
        using var data = File.OpenRead(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes"));
        var scenario = new InteractiveBinaryScenario(NumericalEventHistory.Load(data));
        using (var canceled = new CancellationTokenSource())
        { canceled.Cancel(); try { GeneratedFluidHistory.Generate(scenario, canceled.Token); throw new Exception("Pre-canceled generation succeeded"); } catch (OperationCanceledException) { } }
        using (var canceled = new CancellationTokenSource())
        { try { GeneratedFluidHistory.Generate(scenario, new GeneratedFluidSettings(16, 5, 20), canceled.Token, p => { if (p > .3) canceled.Cancel(); }); throw new Exception("Canceled generation succeeded"); } catch (OperationCanceledException) { } }
        var watch = Stopwatch.StartNew(); double lastProgress = -1;
        var history = GeneratedFluidHistory.Generate(scenario, CancellationToken.None, p => { Check(p >= lastProgress && p >= 0 && p <= 1, "monotone bounded generation progress"); lastProgress = p; });
        watch.Stop(); Near(lastProgress, 1, 0, "generation reaches completion");
        Check(history.GridSize == 96 && history.FrameCount == 129, "fixed physical default independent of renderer quality");
        Near(history.StartSeconds, Math.Max(0, scenario.DurationSeconds - 4000 * scenario.GravitationalTimeSeconds), 1e-14, "bounded cache starts at declared absolute epoch");
        Near(history.EndSeconds, scenario.DurationSeconds, 0, "cache includes final scene epoch");
        Check(history.MemoryBytes < 15_000_000 && history.GenerationWorkingMemoryBytes < 16_000_000, "bounded payload memory reported independently of renderer/process");
        foreach (var d in history.FrameDiagnostics) CheckBudget(d, "generated frame");
        Check(history.Diagnostics.RemovedSinkMass > 0 && history.Diagnostics.StepCount > 100, "history performs forced PDE evolution with accretion");
        Check(!history.TrySample(history.StartSeconds - 1e-8, 0, 0, out _) && !history.TrySample(history.EndSeconds + 1e-8, 0, 0, out _), "out-of-history epochs stay uncovered");
        Check(!history.TrySample(history.StartSeconds, history.HalfExtentRg + .001, 0, out _), "outside-grid spatial sample stays uncovered");
        Check(history.TrySample(history.StartSeconds, history.HalfExtentRg, history.HalfExtentRg, out _), "physical-square boundary uses clamped edge-cell centers");
        Check(!history.TrySample(double.NaN, 0, 0, out _) && !history.TrySample(history.StartSeconds, double.PositiveInfinity, 0, out _), "nonfinite samples rejected");
        var sigma = new float[96 * 96]; var vx = new float[sigma.Length]; var vz = new float[sigma.Length];
        var first = new float[sigma.Length]; history.CopyFrame(0, first, vx, vz);
        double evolvingDifference = 0;
        foreach (int frame in new[] { 0, history.FrameCount / 2, history.FrameCount - 1 })
        {
            history.CopyFrame(frame, sigma, vx, vz); double max = 0, min = double.PositiveInfinity, mass = 0;
            for (int i = 0; i < sigma.Length; i++)
            { Check(float.IsFinite(sigma[i]) && sigma[i] > 0 && float.IsFinite(vx[i]) && float.IsFinite(vz[i]), "cache values finite/positive");
                max = Math.Max(max, sigma[i]); min = Math.Min(min, sigma[i]); mass += sigma[i] * history.CellWidthRg * history.CellWidthRg;
                if (frame == history.FrameCount - 1) evolvingDifference += Math.Abs(sigma[i] - first[i]); }
            Near(mass, history.FrameDiagnostics[frame].FinalMass, 2e-6 * mass, "float cache mass matches conservative double field within quantization");
            Console.WriteLine($"Generated SXS fluid frame {frame}: Sigma=[{min:G7},{max:G7}], mass={mass:G10}, peak/mean={max / (mass / (4 * history.HalfExtentRg * history.HalfExtentRg)):G6}.");
        }
        Check(evolvingDifference > 1, "cached gas structure actually evolves rather than replaying initial geometry");
        double t = .37 * history.StartSeconds + .63 * history.EndSeconds, px = .21 * history.HalfExtentRg, pz = -.1 * history.HalfExtentRg;
        Check(history.TrySample(t, px, pz, out var a), "covered interpolation");
        history.TrySample(history.EndSeconds, 0, 0, out _); history.TrySample(history.StartSeconds, 0, 0, out _);
        Check(history.TrySample(t, px, pz, out var b), "seek returns covered field");
        Near(a.SurfaceDensity, b.SurfaceDensity, 0, "rewind deterministic density"); Near(a.VelocityXRgPerTg, b.VelocityXRgPerTg, 0, "rewind deterministic velocity");
        Check(history.TryGetFrames(t, out int lo, out int hi, out double blend), "cache temporal brackets");
        history.TrySample(history.FrameTimes[lo], px, pz, out var l); history.TrySample(history.FrameTimes[hi], px, pz, out var h);
        Near(a.SurfaceDensity, l.SurfaceDensity + blend * (h.SurfaceDensity - l.SurfaceDensity), 1e-14, "time interpolation matches renderer primitive channels");
        history.CopyFrame(0, sigma, vx, vz); float initialCell = sigma[0]; sigma[0] = 999; history.CopyFrame(0, sigma, vx, vz);
        Near(sigma[0], initialCell, 0, "copy cannot mutate immutable history");
        Near(GeneratedFluidHistory.ReconstructedVolumeDensity(1, 0), 1 / (2 * Math.Sqrt(2 * Math.PI)), 1e-16, "fixed Gaussian height closure normalization");
        double verticalMass = 0, dy = .002;
        for (double y = -16 + dy / 2; y < 16; y += dy) verticalMass += GeneratedFluidHistory.ReconstructedVolumeDensity(1, y) * dy;
        Near(verticalMass, 1, 1e-11, "independent vertical quadrature recovers surface density");

        var custom = new InteractiveBinaryScenario(new InteractiveBinaryParameters(12, 60, 25));
        var small = new GeneratedFluidSettings(24, 5, 100);
        var one = GeneratedFluidHistory.Generate(custom, small, CancellationToken.None);
        var two = GeneratedFluidHistory.Generate(custom, small, CancellationToken.None);
        var refined = GeneratedFluidHistory.Generate(custom, new GeneratedFluidSettings(48, 5, 100), CancellationToken.None);
        var s1 = new float[24 * 24]; var s2 = new float[s1.Length]; var v1 = new float[s1.Length]; var v2 = new float[s1.Length];
        one.CopyFrame(4, s1, v1, v2); two.CopyFrame(4, s2, v1, v2);
        Check(s1.SequenceEqual(s2), "repeated generation is deterministic, independent of wall clock and rendering profile");
        CheckBudget(one.Diagnostics, "coarse custom"); CheckBudget(refined.Diagnostics, "doubled custom");
        double massDifference = Math.Abs(one.Diagnostics.FinalMass - refined.Diagnostics.FinalMass) / refined.Diagnostics.FinalMass;
        Check(massDifference < .12, "short generated history integrated mass agrees under doubled spatial resolution");
        Console.WriteLine($"Generated binary refinement24->48: final mass {one.Diagnostics.FinalMass:G9}->{refined.Diagnostics.FinalMass:G9}, relative difference {massDifference:G6}; no claim of full binary morphological convergence.");
        var dFinal = history.Diagnostics;
        Console.WriteLine($"Generated default SXS history: {watch.Elapsed.TotalSeconds:F3}s, {dFinal.StepCount} steps, CFLmax={dFinal.MaxCourantNumber:G8}, M0={dFinal.InitialMass:G10}, Mf={dFinal.FinalMass:G10}, sink={dFinal.RemovedSinkMass:G9}, outflow={dFinal.NetOutflowMass:G9}, floor={dFinal.AddedFloorMass:G9}, residual={dFinal.MassBudgetResidual:G5}, vmax={dFinal.MaximumSpeed:G6}c, payload={history.MemoryBytes} bytes.");
    }
}
