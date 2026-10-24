using BlackHole.Staging.Simulation;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NumericalEventTests
{
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); }
    private static void Near(double actual, double expected, double tolerance, string label)
    { Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected)),
        $"{label}: expected {expected:R}, got {actual:R}"); }

    public static void Run()
    {
        string root = FindRepository();
        byte[] bytes = File.ReadAllBytes(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes"));
        using JsonDocument fixtureDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Docs/Data/SXS0305/fixtures.json")));
        JsonElement fixtures = fixtureDocument.RootElement;
        Check(Convert.ToHexString(SHA256.HashData(bytes)).Equals(fixtures.GetProperty("asset_sha256").GetString(),
            StringComparison.OrdinalIgnoreCase), "Retained fixture asset hash");
        using var stream = new MemoryStream(bytes, false);
        var history = NumericalEventHistory.Load(stream);
        Check(stream.CanRead, "Loader leaves caller stream open");
        Check(history.HorizonACount == 7376 && history.HorizonBCount == 7376
            && history.CommonHorizonCount == 5464 && history.WaveformCount == 13869, "All selected native rows retained");
        Near(history.LengthMetersPerRawUnit, 95240.79097700247, 1e-13, "IAU length conversion");
        Near(history.SourceSecondsPerRawTime, .00031768908268200156, 1e-15, "Raw time includes relaxed mass normalization");
        Near(history.WaveformSecondsPerNormalizedTime, .00031769416612286176, 1e-15, "Waveform time is already normalized");
        Near(history.WaveformSecondsPerNormalizedTime / history.SourceSecondsPerRawTime,
            1.000016001308, 1e-14, "Waveform must not receive raw normalization twice");
        Near(history.UsefulWaveformStartNativeTime, 540 / 1.000016001308, 1e-14, "Conservative relaxation reference for radiation");

        foreach (JsonProperty fixture in fixtures.GetProperty("horizons").EnumerateObject())
        {
            JsonElement value = fixture.Value;
            double nativeTime = value.GetProperty("native_row")[0].GetDouble();
            double elapsed = (nativeTime - history.StartNativeTime) * history.SourceSecondsPerRawTime;
            NumericalEventState state = history.Evaluate(elapsed);
            NumericalHorizonState horizon = (fixture.Name[0] == 'A' ? state.BodyA
                : fixture.Name[0] == 'B' ? state.BodyB : state.Remnant)
                ?? throw new Exception("Expected horizon missing at " + fixture.Name);
            JsonElement position = value.GetProperty("position_metres"), spin = value.GetProperty("dimensionless_spin");
            Near(state.NativeCoordinateTime, nativeTime, 1e-13, fixture.Name + " native time");
            Near(horizon.PositionMeters.X, position[0].GetDouble(), 1e-12, fixture.Name + " SI x");
            Near(horizon.PositionMeters.Y, position[1].GetDouble(), 1e-12, fixture.Name + " SI y");
            Near(horizon.PositionMeters.Z, position[2].GetDouble(), 1e-12, fixture.Name + " SI z");
            Near(horizon.MassSolar, value.GetProperty("mass_solar").GetDouble(), 1e-12, fixture.Name + " mass");
            Near(horizon.ArealRadiusMeters, value.GetProperty("areal_radius_metres").GetDouble(), 1e-12, fixture.Name + " equal area radius");
            Near(horizon.Spin.X, spin[0].GetDouble(), 1e-12, fixture.Name + " spin x");
            Near(horizon.Spin.Y, spin[1].GetDouble(), 1e-12, fixture.Name + " spin y");
            Near(horizon.Spin.Z, spin[2].GetDouble(), 1e-12, fixture.Name + " spin z");
        }

        NumericalEventState before = history.Evaluate(history.CommonHorizonElapsedSeconds - 1e-10);
        NumericalEventState at = history.Evaluate(history.CommonHorizonElapsedSeconds);
        Check(!before.HasCommonHorizon && before.BodyA.HasValue && before.BodyB.HasValue && !before.Remnant.HasValue,
            "Individual horizons retained until first common horizon");
        Check(at.HasCommonHorizon && !at.BodyA.HasValue && !at.BodyB.HasValue && at.Remnant.HasValue && !at.SeparationMeters.HasValue,
            "Common horizon switches at actual first C timestamp");
        Near(at.NativeCoordinateTime, 3685.496267868691, 1e-14, "Unrounded common horizon timestamp");
        Near(at.SourceReferenceAlignedSeconds, 0, 1e-15, "Horizon comparison reference epoch");
        NumericalRadiationState peak = at.Radiation ?? throw new Exception("Peak radiation missing");
        JsonElement waveFixtures = fixtures.GetProperty("radiation");
        JsonElement peakRow = waveFixtures.GetProperty("peak_native_row");
        Near(peak.NativeRetardedTime, peakRow[0].GetDouble(), 1e-14, "Distinct radiation reference epoch retained");
        Check(Math.Abs(peak.NativeRetardedTime - at.NativeCoordinateTime) > 1,
            "Reference alignment does not relabel horizon coordinate time as wave retarded time");
        Near(peak.RealH22, peakRow[1].GetDouble(), 1e-13, "Peak real mode fixture");
        Near(peak.ImaginaryH22, peakRow[2].GetDouble(), 1e-13, "Peak imaginary mode fixture");
        Near(history.WaveformPeakAmplitude, waveFixtures.GetProperty("peak_amplitude").GetDouble(), 1e-13, "Peak amplitude fixture");
        JsonElement offKnot = waveFixtures.GetProperty("off_knot_native_row");
        double offKnotElapsed = history.CommonHorizonElapsedSeconds
            + (offKnot[0].GetDouble() - history.WaveformPeakNativeTime) * history.WaveformSecondsPerNormalizedTime;
        var interpolatedWave = history.Evaluate(offKnotElapsed).Radiation!.Value;
        Near(interpolatedWave.RealH22, offKnot[1].GetDouble(), 2e-12, "Complex mode linear interpolation real");
        Near(interpolatedWave.ImaginaryH22, offKnot[2].GetDouble(), 2e-12, "Complex mode linear interpolation imaginary");
        Check(!history.Evaluate(history.DurationSeconds).Radiation.HasValue, "Late remnant has no extrapolated radiation");
        double lastWaveElapsed = history.CommonHorizonElapsedSeconds
            + (history.UsefulWaveformEndNativeTime - history.WaveformPeakNativeTime) * history.WaveformSecondsPerNormalizedTime;
        Check(history.Evaluate(lastWaveElapsed - 1e-10).Radiation.HasValue
            && !history.Evaluate(lastWaveElapsed + 1e-10).Radiation.HasValue, "Radiation coverage ends independently of horizons");
        Near(history.Evaluate(-100).NativeCoordinateTime, 540, 1e-14, "Replay lower clamp");
        Near(history.Evaluate(double.MaxValue).NativeCoordinateTime, 4456.868780630533, 1e-14, "Replay upper clamp");
        ExpectArgument(() => history.Evaluate(double.NaN), "Nonfinite replay time rejected");

        var session = new NumericalEventSession(history);
        Check(!session.IsPlaying && session.PlaybackRate == .05, "Readable paused initial replay");
        session.Play(); session.Tick(0);
        Check(session.IsPlaying && session.PlaybackTimeSeconds == 0, "Zero frame preserves forward playback");
        session.Tick(2); session.Pause();
        Near(session.PlaybackTimeSeconds, .1, 1e-14, "Source seconds per wall second");
        NumericalEventState saved = session.State;
        session.SetPlaybackRate(-2);
        Near(session.State.BodyA!.Value.PositionMeters.X, saved.BodyA!.Value.PositionMeters.X, 1e-14,
            "Speed and sign never alter a physical state");
        session.Tick(9); Near(session.PlaybackTimeSeconds, .1, 1e-14, "Paused tick is frozen");
        session.Seek(history.DurationSeconds); session.Play(); session.Tick(.01);
        Check(session.IsPlaying && session.PlaybackTimeSeconds < history.DurationSeconds, "Reverse can leave endpoint");
        session.Tick(100); Check(session.PlaybackTimeSeconds == 0 && !session.IsPlaying, "Reverse stops at start");
        session.SetPlaybackRate(4); session.Play(); session.Tick(100);
        Check(session.PlaybackTimeSeconds == history.DurationSeconds && !session.IsPlaying, "Forward stops at end");
        session.Seek(.1); NumericalEventState replayed = session.State;
        Near(replayed.BodyA!.Value.PositionMeters.X, saved.BodyA!.Value.PositionMeters.X, 1e-14, "Seek replay is deterministic");
        Check(!session.IsPlaying, "Seek pauses");
        long context = session.ContextVersion; session.Reset();
        Check(session.ContextVersion == context + 1 && !session.IsPlaying && session.PlaybackTimeSeconds == 0, "Reset invalidates context");
        foreach (double rate in new[] { 0.0, .009, -4.01, double.NaN, double.PositiveInfinity })
            ExpectArgument(() => session.SetPlaybackRate(rate), "Invalid signed rate rejected");
        ExpectArgument(() => session.Tick(-1), "Negative wall time rejected");
        ExpectArgument(() => session.Seek(double.PositiveInfinity), "Nonfinite seek rejected");
        double? referencePosition = null;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var frameRun = new NumericalEventSession(history); frameRun.Play();
            for (int frame = 0; frame < fps * 8; frame++) frameRun.Tick(1.0 / fps);
            Near(frameRun.PlaybackTimeSeconds, .4, 1e-12, "Replay render-rate independent clock");
            double position = frameRun.State.BodyA!.Value.PositionMeters.X;
            if (referencePosition.HasValue) Near(position, referencePosition.Value, 1e-10, "Replay render-rate independent position");
            referencePosition = position;
        }

        InvalidAsset(bytes, data => data[0] = 0, "Magic");
        InvalidAsset(bytes, data => BitConverter.GetBytes(2u).CopyTo(data, 8), "Version");
        InvalidAsset(bytes, data => BitConverter.GetBytes(1.0).CopyTo(data, 12), "Raw mass normalization");
        InvalidAsset(bytes, data => BitConverter.GetBytes(uint.MaxValue).CopyTo(data, 52), "Bounded row count");
        InvalidAsset(bytes, data => BitConverter.GetBytes(double.NaN).CopyTo(data, 64), "Finite row data");
        InvalidAsset(bytes, data => Array.Copy(data, 56, data, 56 + 72, 8), "Strict horizon time order");
        InvalidAsset(bytes, data => BitConverter.GetBytes(1e-300).CopyTo(data, 88), "Physical mass bound");
        ExpectInvalid(bytes[..^1], "Truncation");
        ExpectInvalid(bytes.Concat(new byte[] { 0 }).ToArray(), "Trailing data");

        for (int i = 0; i < 100; i++) history.Evaluate(.5);
        long allocated = GC.GetAllocatedBytesForCurrentThread(); double sink = 0;
        for (int i = 0; i < 1000; i++) sink += history.Evaluate(history.DurationSeconds * i / 1000).NativeCoordinateTime;
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated && sink > 0, "Per-frame numerical history evaluation allocates no managed memory");
        Console.WriteLine("PASS: numerical SXS histories, independent SI fixtures, common horizon, distinct wave epoch/coverage, deterministic controls and malformed-data rejection");
    }

    private static void ExpectArgument(Action action, string label)
    { try { action(); } catch (ArgumentException) { return; } throw new Exception(label); }
    private static void InvalidAsset(byte[] bytes, Action<byte[]> mutation, string label)
    { byte[] changed = (byte[])bytes.Clone(); mutation(changed); ExpectInvalid(changed, label); }
    private static void ExpectInvalid(byte[] bytes, string label)
    { try { using var stream = new MemoryStream(bytes); NumericalEventHistory.Load(stream); }
        catch (InvalidDataException) { return; } throw new Exception("Invalid asset accepted: " + label); }
    private static string FindRepository()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (DirectoryInfo? directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "Docs/Data/SXS0305/fixtures.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run numerical event tests from this repository or its build tree.");
    }
}
