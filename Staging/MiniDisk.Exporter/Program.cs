using BlackHole.Staging.Simulation;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

string Value(string key, string fallback) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
string source = Value("--source", "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes");
using var data = File.OpenRead(source);
var scenario = new InteractiveBinaryScenario(NumericalEventHistory.Load(data));
if (args.Contains("--m1") || args.Contains("--m2"))
    scenario = new InteractiveBinaryScenario(new InteractiveBinaryParameters(
        double.Parse(Value("--m1", "35"), System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(Value("--m2", "30"), System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(Value("--separation", "30"), System.Globalization.CultureInfo.InvariantCulture)));
if (args.Contains("--self-test")) { MiniDiskTests.Run(scenario); return; }
if (args.Contains("--material-self-test")) { MiniDiskMaterialTests.Run(scenario); return; }
if (args.Contains("--material")) {
    string fluidPath = Value("--material", ""), detailPath = Value("--output", MiniDiskMaterialAdvection.SidecarPath(fluidPath));
    var timer = Stopwatch.StartNew(); double previous = -1;
    var detailReport = MiniDiskMaterialAdvection.GenerateCache(fluidPath, detailPath, CancellationToken.None,
        p => { if (p > previous + .05 || p == 1) { previous = p; Console.WriteLine($"progress={p:F5} elapsedSeconds={timer.Elapsed.TotalSeconds:F3}"); } },
        int.Parse(Value("--tracers", "8192")), double.Parse(Value("--courant", ".75"), System.Globalization.CultureInfo.InvariantCulture));
    string detailJson = JsonSerializer.Serialize(new { modelId = MiniDiskMaterialAdvection.ModelId, elapsedSeconds = timer.Elapsed.TotalSeconds,
        detailReport, MiniDiskMaterialAdvection.MinimumDensity, MiniDiskMaterialAdvection.SplatSigmaCells,
        MiniDiskMaterialAdvection.SplatSupportCells, MiniDiskMaterialAdvection.Seed,
        assumption = "Finite passive material tags sampled from initial density, with coherent seeded labels; RK2 transport through stored coordinate velocity, no respawn or fluid modification. Reconstructed signed material concentration is a subgrid display assumption, not measured turbulence, temperature or new matter. Invalid speed >= c removes a tag or neutralizes a pixel; this does not repair the Newtonian fluid." },
        new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(detailPath + ".json", detailJson); Console.WriteLine(detailJson); return;
}
if (args.Contains("--inspect")) { MiniDiskInspection.Run(scenario, Value("--inspect", "")); return; }
if (args.Contains("--compare")) {
    int ci = Array.IndexOf(args, "--compare"); MiniDiskInspection.Compare(args[ci + 1], args[ci + 2]); return;
}
if (args.Contains("--decimate")) {
    string original = Value("--decimate", ""), target = Value("--output", original + ".subsampled.mdf");
    int stride = int.Parse(Value("--stride", "2"));
    using (var cache = MiniDiskCache.Open(original)) cache.WriteSubsampled(target, stride);
    File.WriteAllText(target + ".decimation.json", JsonSerializer.Serialize(new {
        source = original, sourceSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))).ToLowerInvariant(),
        stride, method = "Exact copies of every Nth independently compressed source frame, preserving both endpoint times and full-double diagnostics; no PDE recomputation or additional numeric quantization."
    }, new JsonSerializerOptions { WriteIndented = true }));
    MiniDiskInspection.Run(scenario, target); return;
}
string sourceHash = scenario.Mode == InteractiveScenarioMode.RepresentativeReplay
    ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() : "";
int grid = int.Parse(Value("--grid", "128")), frames = int.Parse(Value("--frames", "513"));
double lead = double.Parse(Value("--lead-tg", "1000"), System.Globalization.CultureInfo.InvariantCulture);
double? end = args.Contains("--stop-tg") ? double.Parse(Value("--stop-tg", "200"), System.Globalization.CultureInfo.InvariantCulture)
    * scenario.GravitationalTimeSeconds : null;
if (args.Contains("--run-tg")) end = Math.Max(0, Math.Max(scenario.DurationSeconds - 4000 * scenario.GravitationalTimeSeconds,
    scenario.MergerTimeSeconds - lead * scenario.GravitationalTimeSeconds)) +
    double.Parse(Value("--run-tg", "200"), System.Globalization.CultureInfo.InvariantCulture) * scenario.GravitationalTimeSeconds;
string output = Value("--output", $"work/minidisks/minidisks-{grid}.mdf");
var watch = Stopwatch.StartNew(); double last = -1;
var report = MiniDiskSimulation.GenerateCache(scenario, output, new MiniDiskSettings(grid, frames, 4000, end, lead),
    CancellationToken.None, progress => {
        if (progress >= last + .05 || progress == 1) { last = progress; Console.WriteLine($"progress={progress:F5} elapsedSeconds={watch.Elapsed.TotalSeconds:F3}"); }
    }, sourceHash);
var info = new {
    modelId = MiniDiskSimulation.ModelId, sourceModelId = scenario.Evaluate(0).ScenarioId, sourceSha256 = sourceHash,
    sourcePath = scenario.Mode == InteractiveScenarioMode.RepresentativeReplay ? source : null,
    scenarioMode = scenario.Mode.ToString(), parameters = scenario.Parameters,
    gridSize = grid, frameCount = frames, halfExtentRg = report.HalfExtentRg,
    initializationLeadTg = lead,
    report.StartSeconds, report.EndSeconds, scenario.GravitationalRadiusMeters, scenario.GravitationalTimeSeconds,
    scenario.MergerTimeSeconds, elapsedSeconds = watch.Elapsed.TotalSeconds,
    soundSpeedOverC = MiniDiskSimulation.SoundSpeed, densityFloor = MiniDiskSimulation.DensityFloor,
    softeningHorizonRatio = MiniDiskSimulation.SofteningHorizonRatio, sinkHorizonRatio = MiniDiskSimulation.SinkHorizonRatio,
    sinkTimeTg = MiniDiskSimulation.SinkTimeTg, perturbationAmplitude = MiniDiskSimulation.PerturbationAmplitude,
    report.InitialRadialCellsA, report.InitialRadialCellsB, report.Diagnostics, report.MaximumEncodedMassRelativeError,
    report.MaximumDenseVelocityAbsoluteError, report.FileBytes, report.WorkingArrayBytes,
    encoding = "Independent gzip-compressed planar IEEE binary16 Sigma,vx,vz frames. Sigma is retained in every cell; velocity is zero only where pre-encoding Sigma <= 1e-6. Full-double solver diagnostics accompany each frame.",
    initialState = "Two finite, prograde, smooth compact rings; inner radius 1.3 host equal-area horizon radius; outer radius .8 Eggleton Roche-lobe radius. Softened circular velocity plus host coordinate velocity. 10% m=2 density perturbation. No relaxation or injected streams/reservoir.",
    limitations = "Assumed gas, not measured GW150914 gas. Inviscid 2D isothermal Newtonian Euler, no MHD, energy equation, radiation transport, relativistic metric or backreaction. SXS coordinate tracks prescribe Newtonian forcing; common-horizon formation replaces the binary potential by one softened source. Inner radii are assumptions, not relativistic stable-orbit claims. Finite inventory may drain before merger. Numerical and temporal refinement require separate assessment; render fidelity does not change PDE resolution."
};
string json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(output + ".json", json); Console.WriteLine(json);
MiniDiskInspection.Run(scenario, output);
