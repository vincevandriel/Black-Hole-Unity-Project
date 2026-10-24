using BlackHole.Staging.Simulation;

static class MiniDiskTests
{
    static int checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    public static void Run(InteractiveBinaryScenario scenario)
    {
        var initial = scenario.Evaluate(0);
        foreach (bool primary in new[] { true, false }) {
            var body = primary ? initial.GasState.BodyA : initial.GasState.BodyB;
            double inner = 1.3 * body.HorizonRadiusRg, outer = MiniDiskSimulation.OuterRadius(initial, primary), r = .5 * (inner + outer);
            var s = MiniDiskSimulation.InitialSample(initial, body.PositionRg.X + r, body.PositionRg.Z);
            Check(s.SurfaceDensity > .8, "Finite mini-disk seed missing.");
            Check(s.VelocityZRgPerTg - body.VelocityOverC.Z > 0, "Seed is not prograde in host frame.");
            var absent = MiniDiskSimulation.InitialSample(initial, body.PositionRg.X + outer + .2, body.PositionRg.Z);
            Check(Math.Abs(absent.SurfaceDensity - MiniDiskSimulation.DensityFloor) < 1e-12, "Artificial reservoir outside seed.");
        }
        var forcing = MiniDiskSimulation.Forcing(initial);
        forcing.Evaluate(initial.BodyA!.Value.PositionRg.X, initial.BodyA.Value.PositionRg.Z, out double gx, out double gz, out double sink);
        Check(double.IsFinite(gx) && double.IsFinite(gz) && sink > 0, "Softened force is singular or sink missing.");
        string path = Path.Combine(Path.GetTempPath(), "dtet-minidisk-tests-" + Guid.NewGuid().ToString("N") + ".mdf");
        try {
            double end = 8 * scenario.GravitationalTimeSeconds;
            var report = MiniDiskSimulation.GenerateCache(scenario, path, new MiniDiskSettings(64, 5, 4000, end, 4000));
            Check(report.Diagnostics.StepCount > 10, "PDE did not evolve.");
            Check(Math.Abs(report.Diagnostics.MassBudgetResidual) / report.Diagnostics.InitialMass < 1e-9, "Mass ledger failed.");
            Check(report.Diagnostics.MaxCourantNumber <= GeneratedFluidSolver.CourantLimit, "CFL failed.");
            using (var cache = MiniDiskCache.Open(path)) {
                Check(cache.FrameCount == 5 && cache.GridSize == 64, "Cache shape lost.");
                Check(cache.ResidentPayloadBytes < cache.FrameCount * 64L * 64 * 3 * sizeof(float), "Reader retains full history.");
                Check(!cache.TryGetFrames(-1, out _, out _, out _) && !cache.TryGetFrames(end + 1e-5, out _, out _, out _), "Cache extrapolates.");
                Check(cache.TryGetFrames(3 * scenario.GravitationalTimeSeconds, out int a, out int b, out double t)
                    && a == 1 && b == 2 && Math.Abs(t - .5) < 1e-12, "Frame bracketing failed.");
                var sigma = new float[4096]; var vx = new float[4096]; var vz = new float[4096];
                cache.ReadFrame(0, sigma, vx, vz); var original = (float[])sigma.Clone();
                bool ieee = true;
                for (int z = 0; z < 64; z++) for (int x = 0; x < 64; x++) {
                    int i = z * 64 + x;
                    var seed = MiniDiskSimulation.InitialSample(initial, -cache.HalfExtentRg + (x + .5) * cache.CellWidthRg,
                        -cache.HalfExtentRg + (z + .5) * cache.CellWidthRg);
                    float expected = (float)(Half)(float)seed.SurfaceDensity;
                    float expectedX = seed.SurfaceDensity <= MiniDiskSimulation.VacuumVelocityCutoff ? 0 : (float)(Half)(float)seed.VelocityXRgPerTg;
                    float expectedZ = seed.SurfaceDensity <= MiniDiskSimulation.VacuumVelocityCutoff ? 0 : (float)(Half)(float)seed.VelocityZRgPerTg;
                    ieee &= sigma[i] == expected && vx[i] == expectedX && vz[i] == expectedZ;
                }
                Check(ieee, "Planar cache channels disagree with IEEE binary16 reference.");
                cache.ReadFrame(4, sigma, vx, vz);
                Check(sigma.All(float.IsFinite) && vx.All(float.IsFinite) && vz.All(float.IsFinite) && sigma.Min() >= 0, "Decoded state invalid.");
                Check(sigma.Zip(original, (x, y) => Math.Abs(x - y)).Sum() > .01, "Density is static.");
                double mass = sigma.Sum(x => (double)x) * cache.CellWidthRg * cache.CellWidthRg;
                Check(Math.Abs(mass - cache.GetDiagnostics(4).FinalMass) / mass < .001, "Binary16 mass error excessive.");
                var sample = cache.SampleFrame(sigma, vx, vz, -cache.HalfExtentRg + .5 * cache.CellWidthRg,
                    -cache.HalfExtentRg + .5 * cache.CellWidthRg);
                Check(sample.SurfaceDensity == sigma[0], "Cell-centre interpolation mismatch.");
                cache.ReadFrame(0, sigma, vx, vz); Check(sigma.SequenceEqual(original), "Backward seek is not deterministic.");
                cache.WriteSubsampled(path + ".sparse", 2);
                using var sparse = MiniDiskCache.Open(path + ".sparse");
                sparse.ReadFrame(0, sigma, vx, vz);
                Check(sparse.FrameCount == 3 && sparse.StartSeconds == cache.StartSeconds && sparse.EndSeconds == cache.EndSeconds
                    && sigma.SequenceEqual(original), "Temporal subsampling changed selected frame state or coverage.");
                sparse.ReadFrame(2, sigma, vx, vz); var sparseLast = (float[])sigma.Clone(); cache.ReadFrame(4, sigma, vx, vz);
                Check(sigma.SequenceEqual(sparseLast), "Temporal subsampling changed the final density.");
            }
            byte[] bytes = File.ReadAllBytes(path); File.WriteAllBytes(path + ".bad", bytes[..^17]);
            bool rejected = false; try { using var bad = MiniDiskCache.Open(path + ".bad"); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Truncated cache accepted.");
            bytes[^8] ^= 1; File.WriteAllBytes(path + ".crc", bytes);
            bool crcRejected = false;
            try { using var corrupt = MiniDiskCache.Open(path + ".crc"); corrupt.ReadFrame(4, new float[4096], new float[4096], new float[4096]); }
            catch (InvalidDataException) { crcRejected = true; }
            Check(crcRejected, "Corrupt gzip frame accepted.");
            var cancel = new CancellationToken(true); bool cancelled = false;
            try { MiniDiskSimulation.GenerateCache(scenario, path + ".cancel", 64, 5, cancel); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && !File.Exists(path + ".cancel"), "Cancellation exposed partial cache.");
            byte[] saved = File.ReadAllBytes(path); var during = new CancellationTokenSource(); bool stopped = false;
            try { MiniDiskSimulation.GenerateCache(scenario, path, new MiniDiskSettings(64, 5, 4000, end, 4000), during.Token,
                _ => during.Cancel()); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped && File.ReadAllBytes(path).SequenceEqual(saved), "Cancellation replaced the previous completed cache.");
            MiniDiskSimulation.GenerateCache(scenario, path + ".late", 32, 3);
            using (var late = MiniDiskCache.Open(path + ".late")) {
                double expectedStart = scenario.MergerTimeSeconds - 1000 * scenario.GravitationalTimeSeconds;
                Check(Math.Abs(late.StartSeconds - expectedStart) < 1e-14 && late.EndSeconds == scenario.DurationSeconds
                    && !late.TryGetFrames(expectedStart - 1e-8, out _, out _, out _), "Default gas initialization/coverage boundary changed.");
                Check(late.HalfExtentRg == 12 && late.GetDiagnostics(2).StepCount > 100,
                    "Default compact replay did not evolve over its retained source clock.");
            }
            Console.WriteLine($"MiniDisk model/cache tests passed: {checks} assertions.");
        }
        finally { foreach (string file in new[] { path, path + ".bad", path + ".crc", path + ".cancel", path + ".sparse", path + ".late" }) if (File.Exists(file)) File.Delete(file); }
    }
}
