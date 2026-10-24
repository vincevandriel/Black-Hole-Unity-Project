using BlackHole.Staging.Simulation;
using System.Text.Json;

static class MiniDiskInspection
{
    public static void Compare(string coarsePath, string refinedPath)
    {
        using var a = MiniDiskCache.Open(coarsePath); using var b = MiniDiskCache.Open(refinedPath);
        if (b.GridSize <= a.GridSize || a.HalfExtentRg != b.HalfExtentRg || a.StartSeconds != b.StartSeconds || a.EndSeconds != b.EndSeconds)
            throw new ArgumentException("Refinement comparison requires the same physical domain and clock.");
        int na = a.GridSize, nb = b.GridSize; double ratio = (double)nb / na;
        var sa = new float[na * na]; var ax = new float[sa.Length]; var az = new float[sa.Length];
        var sb = new float[nb * nb]; var bx = new float[sb.Length]; var bz = new float[sb.Length];
        var rows = new List<object>();
        foreach (int frame in new[] { 0, a.FrameCount / 4, a.FrameCount / 2, 3 * a.FrameCount / 4, a.FrameCount - 1 }) {
            double time = a.GetFrameTime(frame);
            if (!b.TryGetFrames(time, out int lo, out int hi, out double blend) || (lo != hi && Math.Min(blend, 1 - blend) > 1e-10))
                throw new ArgumentException("Refined output does not contain the same comparison epochs.");
            a.ReadFrame(frame, sa, ax, az); b.ReadFrame(blend > .5 ? hi : lo, sb, bx, bz);
            double error = 0, norm = 0, ma = 0, momentumError = 0;
            for (int z = 0; z < na; z++) for (int x = 0; x < na; x++) {
                double s = 0, px = 0, pz = 0;
                for (int jz = (int)(z * ratio); jz < Math.Ceiling((z + 1) * ratio); jz++)
                for (int jx = (int)(x * ratio); jx < Math.Ceiling((x + 1) * ratio); jx++) {
                    double w = Math.Max(0, Math.Min((x + 1) * ratio, jx + 1) - Math.Max(x * ratio, jx))
                        * Math.Max(0, Math.Min((z + 1) * ratio, jz + 1) - Math.Max(z * ratio, jz)) / (ratio * ratio);
                    int j = jz * nb + jx; s += w * sb[j]; px += w * sb[j] * bx[j]; pz += w * sb[j] * bz[j];
                }
                int i = z * na + x; error += Math.Abs(sa[i] - s); norm += s; ma += sa[i];
                momentumError += Math.Sqrt(Math.Pow(sa[i] * ax[i] - px, 2) + Math.Pow(sa[i] * az[i] - pz, 2));
            }
            var row = new { coarseGrid = na, refinedGrid = nb, frame, seconds = time,
                densityRelativeL1 = error / norm, relativeMassDifference = Math.Abs(ma - norm) / norm,
                momentumL1PerMass = momentumError / norm };
            rows.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
        }
        File.WriteAllText(refinedPath + ".refinement.json", JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static void Run(InteractiveBinaryScenario scenario, string path)
    {
        using var cache = MiniDiskCache.Open(path);
        int n = cache.GridSize, count = n * n;
        var sigma = new float[count]; var vx = new float[count]; var vz = new float[count];
        var rows = new List<object>();
        int nearest(double time) => (int)Math.Round((time - cache.StartSeconds) / (cache.EndSeconds - cache.StartSeconds) * (cache.FrameCount - 1));
        foreach (int frame in new[] { 0, cache.FrameCount / 4, cache.FrameCount / 2, 3 * cache.FrameCount / 4,
            Math.Clamp(nearest(scenario.MergerTimeSeconds), 0, cache.FrameCount - 1), cache.FrameCount - 1 }.Distinct().Order()) {
            cache.ReadFrame(frame, sigma, vx, vz); var state = scenario.Evaluate(cache.GetFrameTime(frame));
            double mass = 0, denseMaxSpeed = 0, superluminalMass = 0, fastMass = 0, miniA = 0, miniB = 0;
            double area = cache.CellWidthRg * cache.CellWidthRg;
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) {
                int i = z * n + x; double m = sigma[i] * area, speed = Math.Sqrt(vx[i] * vx[i] + vz[i] * vz[i]); mass += m;
                if (sigma[i] > MiniDiskSimulation.VacuumVelocityCutoff) denseMaxSpeed = Math.Max(denseMaxSpeed, speed);
                if (speed > 1) superluminalMass += m; if (speed > .5) fastMass += m;
                double px = -cache.HalfExtentRg + (x + .5) * cache.CellWidthRg, pz = -cache.HalfExtentRg + (z + .5) * cache.CellWidthRg;
                double Inventory(InteractiveBinaryBody? body, bool primary) {
                    if (!body.HasValue) return 0;
                    double r = Math.Sqrt(Math.Pow(px - body.Value.PositionRg.X, 2) + Math.Pow(pz - body.Value.PositionRg.Z, 2));
                    return r > 1.3 * body.Value.HorizonRadiusRg && r < MiniDiskSimulation.OuterRadius(state, primary) ? m : 0;
                }
                miniA += Inventory(state.BodyA, true); miniB += Inventory(state.BodyB, false);
            }
            var d = cache.GetDiagnostics(frame);
            var row = new { frame, timeTg = cache.GetFrameTime(frame) / cache.GravitationalTimeSeconds,
                alignedTg = (cache.GetFrameTime(frame) - scenario.MergerTimeSeconds) / cache.GravitationalTimeSeconds,
                mass, retained = mass / d.InitialMass, sigmaMax = sigma.Max(), denseMaxSpeed,
                superluminalMassFraction = superluminalMass / mass, massFractionAboveHalfC = fastMass / mass,
                miniDiskAnnulusMassA = miniA, miniDiskAnnulusMassB = miniB, d.MassBudgetResidual, d.StepCount };
            rows.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
        }
        Console.WriteLine($"readerResidentPayloadBytes={cache.ResidentPayloadBytes} fileBytes={cache.FileBytes}");
        File.WriteAllText(path + ".inspection.json", JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
