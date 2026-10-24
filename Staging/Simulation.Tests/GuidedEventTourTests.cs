using BlackHole.Staging.Simulation;

internal static class GuidedEventTourTests
{
    private static int assertions;
    private static void Check(bool condition, string label)
    { assertions++; if (!condition) throw new Exception(label); }
    private static void Equal(double actual, double expected, string label)
    { Check(actual == expected, label + $": {actual:R} != {expected:R}"); }
    private static void Reject(Action action, string label)
    { try { action(); } catch (ArgumentException) { assertions++; return; } throw new Exception(label); }

    public static void Run()
    {
        const double start = .68, merger = 1, tg = .00032, end = 1.25;
        const long generation = 7;
        var tour = new GuidedEventTour(start, merger, tg, end, generation);
        Check(tour.Checkpoints.Select(p => p.Id).SequenceEqual(new[] {
            "inspiral", "gas_tides", "final_approach", "coalescence", "remnant" }), "Five ordered phase IDs");
        Equal(tour.Checkpoints[0].Seconds, start, "Actual gas coverage starts tour");
        Equal(tour.Checkpoints[3].Seconds, merger, "Coalescence uses exact model epoch");
        Check(tour.Checkpoints.All(p => p.Seconds >= start && p.Seconds <= end), "All bookmarks inside gas coverage");
        Check(!tour.IsComplete && !tour.IsPausedAtCheckpoint && tour.CurrentCheckpoint == null, "Fresh tour has no latched pause");

        double time = start;
        foreach (var expected in tour.Checkpoints)
        {
            time = tour.ClampForward(time, end + 10, generation, out var checkpoint);
            Check(ReferenceEquals(checkpoint, expected) && ReferenceEquals(tour.CurrentCheckpoint, expected), "Large step finds earliest unseen bookmark");
            Equal(time, expected.Seconds, "Stop has exact stored time");
            Equal(tour.ClampForward(time, end, generation, out var held), time, "Latched checkpoint holds until Continue");
            Check(ReferenceEquals(held, expected) && tour.IsPausedAtCheckpoint, "Pause remains latched");
            Check(!tour.Continue(generation + 1) && tour.IsPausedAtCheckpoint, "Stale generation cannot resume pause");
            Check(tour.Continue(generation) && !tour.IsPausedAtCheckpoint, "Continue acknowledges bookmark");
            Equal(tour.ClampForward(time, time, generation, out var repeated), time, "Acknowledgement does not advance source clock");
            Check(repeated == null && !tour.Continue(generation), "Acknowledged point cannot immediately retrigger");
        }
        Check(tour.IsComplete, "Tour complete after acknowledging all bookmarks");
        Equal(tour.ClampForward(time, end, generation, out var completed), end, "Completed tour permits scene end");
        Check(completed == null, "End has no fabricated bookmark");

        double exact = tour.Checkpoints[2].Seconds;
        tour.NotifySeek(exact, generation);
        Check(!tour.IsPausedAtCheckpoint && !tour.IsComplete, "Seek only synchronizes progress; it does not latch or play");
        Equal(tour.ClampForward(exact, end, generation, out var sought), exact, "Seek to bookmark can show it on next advance");
        Check(sought?.Id == "final_approach", "Earlier bookmarks remain skipped after forward seek");
        tour.Continue(generation);
        tour.NotifySeek(end, generation);
        Check(tour.IsComplete, "Seeking beyond every bookmark is complete");
        Equal(tour.ClampForward(end, start, generation, out var reversed), start, "Reverse playback is not blocked by tour");
        Check(reversed == null && !tour.IsPausedAtCheckpoint && !tour.IsComplete, "Reverse rearms future bookmarks without pausing");
        Equal(tour.ClampForward(start, end, generation, out var rewound), start, "Rewind replays introduction exactly");
        Check(rewound?.Id == "inspiral", "Rewind resets first checkpoint");

        tour.SetEnabled(false, start, generation);
        Check(!tour.Enabled && !tour.IsPausedAtCheckpoint, "Disabling releases an active presentation pause");
        double between = .5 * (tour.Checkpoints[1].Seconds + tour.Checkpoints[2].Seconds);
        Equal(tour.ClampForward(start, between, generation, out var free), between, "Disabled mode allows free exploration");
        Check(free == null, "No hidden disabled checkpoint pause");
        tour.SetEnabled(true, between, generation);
        Equal(tour.ClampForward(between, end, generation, out var enabled), exact, "Re-enable resumes from actual current time");
        Check(enabled?.Id == "final_approach", "Re-enable does not replay earlier phases");
        double oldPause = tour.CurrentCheckpoint!.Seconds;
        Equal(tour.ClampForward(0, end, generation + 1, out var stale), end, "Stale generation cannot clamp another scenario");
        tour.NotifySeek(0, generation + 1);
        tour.SetEnabled(false, 0, generation + 1);
        Check(stale == null && tour.Enabled && tour.CurrentCheckpoint!.Seconds == oldPause, "Stale commands leave presentation state unchanged");

        tour.Reset(.1, .3, .001, .5, generation + 1);
        Check(tour.Generation == generation + 1 && tour.Enabled && !tour.IsPausedAtCheckpoint && !tour.IsComplete, "Scenario reset clears progress and generation");
        Equal(tour.Checkpoints[0].Seconds, .1, "Scenario reset replaces physical timing bounds");
        Reject(() => tour.Reset(.6, .3, .001, .5, 99), "Out-of-coverage start rejected");
        Check(tour.Generation == generation + 1, "Invalid reset preserves prior valid tour");
        Reject(() => new GuidedEventTour(0, 1, 0, 2), "Invalid tg rejected");
        Reject(() => new GuidedEventTour(0, 3, .01, 2), "Out-of-range merger rejected");
        Reject(() => new GuidedEventTour(double.NaN, 1, .01, 2), "Nonfinite timing rejected");
        Reject(() => tour.NotifySeek(double.PositiveInfinity, tour.Generation), "Nonfinite seek rejected");

        var afterMerger = new GuidedEventTour(1.1, 1, tg, end);
        Check(afterMerger.Checkpoints.Count == 1 && afterMerger.Checkpoints[0].Id == "remnant", "Late gas start does not invent inspiral/coalescence coverage");
        Equal(afterMerger.Checkpoints[0].Seconds, 1.1, "Late remnant bookmark remains inside coverage");
        var endpoint = new GuidedEventTour(end, end, tg, end);
        Check(endpoint.Checkpoints.Count == 1 && endpoint.Checkpoints[0].Id == "coalescence", "Zero-width coverage retains exact model event only");
        Equal(endpoint.ClampForward(end, end, 0, out var last), end, "Endpoint checkpoint is reachable");
        Check(last != null && endpoint.Continue(0) && endpoint.IsComplete, "Endpoint checkpoint completes without time advancement");
        var tiny = new GuidedEventTour(1 - 1e-13, 1, tg, 1 + 1e-13);
        Check(tiny.Checkpoints.Count == 1 && tiny.Checkpoints[0].Id == "coalescence", "Near-coincident bookmarks deduplicate with event priority");
        Equal(tiny.Checkpoints[0].Seconds, 1, "Deduplication preserves exact merger time");
        Check(new GuidedEventTour(0, 1, tg, end).Checkpoints.Count == 5, "Earlier gas start preserves all covered phases");

        string root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes")))
            root = Directory.GetParent(root)?.FullName ?? throw new Exception("Repository data not found");
        using var stream = File.OpenRead(Path.Combine(root, "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes"));
        var replay = new InteractiveBinaryScenario(NumericalEventHistory.Load(stream));
        foreach (var scenario in new[] { replay,
            new InteractiveBinaryScenario(new InteractiveBinaryParameters(40, 25, 35)),
            new InteractiveBinaryScenario(new InteractiveBinaryParameters(5, 5, 25)),
            new InteractiveBinaryScenario(new InteractiveBinaryParameters(80, 80, 50)) })
        {
            double gasStart = Math.Max(0, scenario.MergerTimeSeconds - 1000 * scenario.GravitationalTimeSeconds);
            var physicalBefore = scenario.Evaluate(scenario.MergerTimeSeconds);
            string[] ExpectedStops(int fps)
            {
                var steps = new GuidedEventTour(gasStart, scenario.MergerTimeSeconds,
                    scenario.GravitationalTimeSeconds, scenario.DurationSeconds, 19);
                var ids = new List<string>();
                double t = gasStart;
                double rate = (scenario.DurationSeconds - gasStart) / 2;
                int iterations = 0;
                while (t < scenario.DurationSeconds && iterations++ < 10000)
                {
                    double proposed = MergerPlaybackTiming.Advance(t, 1.0 / fps, rate,
                        scenario.MergerTimeSeconds, scenario.GravitationalTimeSeconds, scenario.DurationSeconds);
                    t = steps.ClampForward(t, proposed, 19, out var point);
                    if (point != null) { ids.Add(point.Id); steps.Continue(19); }
                }
                Check(steps.IsComplete && t == scenario.DurationSeconds, "Replay/custom tours reach endpoint at " + fps + " fps");
                return ids.ToArray();
            }
            Check(ExpectedStops(30).SequenceEqual(ExpectedStops(144)), "Checkpoint sequence is independent of rendered frame rate");
            Check(ExpectedStops(30).SequenceEqual(new[] { "inspiral", "gas_tides", "final_approach", "coalescence", "remnant" }),
                "Actual replay/custom coverage contains all five checkpoints");
            var physicalAfter = scenario.Evaluate(scenario.MergerTimeSeconds);
            Equal(physicalAfter.ElapsedSeconds, physicalBefore.ElapsedSeconds, "Tour leaves physical source clock unchanged");
            Equal(physicalAfter.SeparationRg, physicalBefore.SeparationRg, "Tour leaves physical trajectory unchanged");
        }
        Console.WriteLine($"PASS: guided event tour ({assertions} checks), exact crossing, resume, seek/reverse, generation, coverage and frame independence.");
    }
}
