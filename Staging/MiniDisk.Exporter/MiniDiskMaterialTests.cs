using BlackHole.Staging.Simulation;

static class MiniDiskMaterialTests
{
    static int checks;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
    public static void Run(InteractiveBinaryScenario scenario)
    {
        GeneratedFluidSample Translation(double t, double x, double z) => new(1, .2, -.1);
        Check(MiniDiskMaterialTransport.AdvanceRk2(1, 2, 7, 2, Translation, out double tx, out double tz)
            && Math.Abs(tx - 1.4) < 1e-14 && Math.Abs(tz - 1.8) < 1e-14, "Analytic translation failed.");
        GeneratedFluidSample Frozen(double t, double x, double z) => new(1, 0, 0);
        Check(MiniDiskMaterialTransport.AdvanceRk2(1.5, -2, 4, 10, Frozen, out tx, out tz) && tx == 1.5 && tz == -2,
            "Frozen field moved material.");
        GeneratedFluidSample Accelerating(double t, double x, double z) => new(1, .01 * t, 0);
        Check(MiniDiskMaterialTransport.AdvanceRk2(0, 0, 3, 2, Accelerating, out tx, out tz) && Math.Abs(tx - .08) < 1e-14,
            "RK2 source-clock sampling failed.");
        GeneratedFluidSample Rotation(double t, double x, double z) => new(1, -.1 * z, .1 * x);
        double Error(double dt) {
            double x = 1, z = 0;
            for (int i = 0; i < (int)Math.Round(20 / dt); i++) {
                Check(MiniDiskMaterialTransport.AdvanceRk2(x, z, i * dt, dt, Rotation, out double nx, out double nz), "Valid rotation rejected."); x = nx; z = nz;
            }
            return Math.Sqrt(Math.Pow(x - Math.Cos(2), 2) + Math.Pow(z - Math.Sin(2), 2));
        }
        double coarse = Error(.4), fine = Error(.2);
        Check(coarse / fine > 3.8 && coarse / fine < 4.2 && fine < .0002, "Solid-body rotation lacks second-order timestep refinement.");
        Check(!MiniDiskMaterialTransport.AdvanceRk2(0, 0, 0, 1, (t, x, z) => new(1, 1.1, 0), out _, out _),
            "Superluminal transport was accepted.");
        Check(!MiniDiskMaterialTransport.AdvanceRk2(0, 0, 0, 1, (t, x, z) => new(0, 0, 0), out _, out _, 1e-5),
            "Vacuum transport was accepted.");
        string folder = Path.Combine(Path.GetTempPath(), "dtet-material-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string fluidPath = Path.Combine(folder, "fluid.mdf"), detail = Path.Combine(folder, "fluid.mdt");
        try {
            MiniDiskSimulation.GenerateCache(scenario, fluidPath, new MiniDiskSettings(64, 5, 4000, 8 * scenario.GravitationalTimeSeconds, 4000));
            string originalHash = MiniDiskMaterialCache.ComputeFileSha256(fluidPath);
            var report = MiniDiskMaterialAdvection.GenerateCache(fluidPath, detail, tracerCount: 256);
            Check(report.Substeps > 0 && report.Survivors.All(x => x <= 256), "Material transport did not advance.");
            Check(MiniDiskMaterialCache.ComputeFileSha256(fluidPath) == originalHash, "Material transport modified fluid data.");
            using (var fluid = MiniDiskCache.Open(fluidPath)) using (var cache = MiniDiskMaterialCache.Open(detail)) {
                Check(cache.MatchesFile(fluid, fluidPath) && !cache.Matches(fluid, new string('0', 64)), "Material/fluid identity binding failed.");
                Check(cache.GridSize == fluid.GridSize && cache.FrameCount == fluid.FrameCount
                    && cache.GetFrameTime(4) == fluid.GetFrameTime(4), "Material timeline differs from fluid.");
                var c = new float[4096]; cache.ReadFrame(0, c); var initial = (float[])c.Clone(); cache.ReadFrame(4, c);
                Check(c.All(x => float.IsFinite(x) && Math.Abs(x) <= 1) && c.Zip(initial, (x, y) => Math.Abs(x - y)).Sum() > .01,
                    "Concentration is nonfinite, unbounded or static.");
                var sigma = new float[4096]; var vx = new float[4096]; var vz = new float[4096]; fluid.ReadFrame(4, sigma, vx, vz);
                Check(c.Where((v, i) => sigma[i] <= MiniDiskMaterialAdvection.MinimumDensity).All(v => v == 0), "Material exists in empty fluid.");
                cache.ReadFrame(0, c); Check(c.SequenceEqual(initial), "Recorded rewind is not deterministic.");
            }
            MiniDiskMaterialAdvection.GenerateCache(fluidPath, detail + ".repeat", tracerCount: 256);
            Check(File.ReadAllBytes(detail).SequenceEqual(File.ReadAllBytes(detail + ".repeat")), "Repeated material generation is not deterministic.");
            byte[] bytes = File.ReadAllBytes(detail); bytes[^8] ^= 1; File.WriteAllBytes(detail + ".bad", bytes);
            bool rejected = false;
            try { using var bad = MiniDiskMaterialCache.Open(detail + ".bad"); bad.ReadFrame(4, new float[4096]); }
            catch (InvalidDataException) { rejected = true; } Check(rejected, "Corrupt material frame was accepted.");
            string savedHash = MiniDiskMaterialCache.ComputeFileSha256(detail); var cancel = new CancellationTokenSource(); bool cancelled = false;
            try { MiniDiskMaterialAdvection.GenerateCache(fluidPath, detail, cancel.Token, _ => cancel.Cancel(), 256); }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && MiniDiskMaterialCache.ComputeFileSha256(detail) == savedHash && !Directory.GetFiles(folder, "*.partial-*").Any(),
                "Cancellation exposed a partial sidecar or destroyed the previous one.");
            Console.WriteLine($"MiniDisk material tests passed: {checks} assertions; rotation refinement={coarse / fine:G8}.");
        }
        finally { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
}
