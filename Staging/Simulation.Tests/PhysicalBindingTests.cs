using BlackHole.Staging.Simulation;

internal static class PhysicalBindingTests
{
    public static void Run()
    {
        static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        static void Near(double a, double b, double tolerance, string label)
        { Check(double.IsFinite(a) && Math.Abs(a - b) <= tolerance * Math.Max(1, Math.Abs(b)), label); }
        var session = new SimulationSession();
        session.SelectScenario(StagingScenarios.Unequal);
        var first = PhysicalSceneSnapshot.Capture(session, 1e6);
        session.Seek(session.Model.CutoffTimeSeconds * 0.4);
        var a = PhysicalSceneSnapshot.Capture(session, 1e6);
        Near((a.Body1X - a.Body2X) * 1e6, a.State.Body1XMeters - a.State.Body2XMeters, 1e-12, "display scale x");
        Near(20 * a.Body1X + 40 * a.Body2X, 0, 1e-11, "display barycentre x");
        Near(20 * a.Body1Z + 40 * a.Body2Z, 0, 1e-11, "display barycentre z");
        Near(Math.Sqrt(a.Body1X * a.Body1X + a.Body1Z * a.Body1Z)
            / Math.Sqrt(a.Body2X * a.Body2X + a.Body2Z * a.Body2Z), 2, 1e-12, "unequal orbit radii");
        Check(first.PlaybackTimeSeconds == 0 && first.ScenarioVersion == a.ScenarioVersion, "snapshot immutability");
        session.SetPlaybackRate(2);
        Near(session.State.GravitationalWaveFrequencyHertz!.Value, a.State.GravitationalWaveFrequencyHertz!.Value, 1e-12, "rate preserves frequency");
        session.Play();session.Tick(0.25);session.Pause();
        Near(session.PlaybackTimeSeconds, a.PlaybackTimeSeconds + .5, 1e-12, "source time playback");
        session.SetPlaybackRate(-2);session.Play();session.Tick(.25);
        Near(session.State.PhaseRadians, a.State.PhaseRadians, 1e-11, "reverse exact phase");
        session.Seek(session.Model.EndTimeSeconds);
        Check(!session.IsPlaying, "seek pauses");
        var endpoint = PhysicalSceneSnapshot.Capture(session, 1e6);
        Check(endpoint.AtCutoff && !endpoint.State.GravitationalWaveFrequencyHertz.HasValue, "no frequency after cutoff");
        session.Play();session.Tick(.1);
        Check(session.IsPlaying && session.PlaybackTimeSeconds < session.Model.EndTimeSeconds, "reverse from end");
        session.Tick(1e6);
        Check(!session.IsPlaying && session.PlaybackTimeSeconds == 0, "reverse clamps at start");
        var generation = session.ScenarioVersion;
        session.SetInitialSeparation(45 * session.Scenario.GravitationalRadiusMeters);
        Check(session.ScenarioVersion == generation + 1 && session.Scenario.Mass1SolarMasses == 20
            && session.Scenario.Mass2SolarMasses == 40 && !session.IsPlaying, "custom start preserves masses and resets");
        foreach (var invalid in new[] { 0.0, double.NaN, double.PositiveInfinity })
        {
            bool threw = false;
            try { PhysicalSceneSnapshot.Capture(session, invalid); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "reject invalid scale");
        }
        double? referencePhase = null;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            var run = new SimulationSession();run.SetPlaybackRate(.5);run.Play();
            run.Tick(0);
            Check(run.IsPlaying && run.PlaybackTimeSeconds == 0, "zero frame delta preserves forward playback at start");
            for (int i = 0; i < fps * 10; i++) run.Tick(1.0 / fps);
            Near(run.PlaybackTimeSeconds, 5, 1e-12, "frame independent time");
            if (referencePhase.HasValue) Near(run.State.PhaseRadians, referencePhase.Value, 1e-11, "frame independent phase");
            referencePhase = run.State.PhaseRadians;
        }
        var fixture = new CircularInspiral(new InspiralParameters(2e30, 3e30, 2e7, 1e7));
        Near(fixture.Evaluate(0).GravitationalWaveFrequencyHertz!.Value, 0.06501192082918597, 1e-12, "independent SI frequency fixture");
        Console.WriteLine("PASS: physical snapshot binding, unequal barycentre, units, reverse, cutoff and render-rate invariance");
    }
}
