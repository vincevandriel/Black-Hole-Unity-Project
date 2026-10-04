using BlackHole.Staging.Simulation;

static void Near(double actual, double expected, double relativeTolerance, string label)
{
    double scale = Math.Max(1.0, Math.Abs(expected));
    if (double.IsNaN(actual) || Math.Abs(actual - expected) > relativeTolerance * scale)
        throw new Exception($"{label}: expected {expected:R}, got {actual:R}");
}

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
}

var baseline = new CircularInspiral(StagingScenarios.CreateParameters(StagingScenarios.Baseline.InitialSeparationMeters));
var wider = new CircularInspiral(StagingScenarios.CreateParameters(StagingScenarios.Wider.InitialSeparationMeters));
var initial = baseline.Evaluate(0);
var widerInitial = wider.Evaluate(0);
Check(initial.Phase == InspiralPhase.Inspiral, "initial phase");
Check(initial.OrbitalFrequencyHertz > widerInitial.OrbitalFrequencyHertz, "smaller separation raises orbital frequency");
Near(initial.GravitationalWaveFrequencyHertz!.Value / initial.OrbitalFrequencyHertz!.Value, 2, 1e-12, "GW/orbit ratio");

double mass = InspiralParameters.SolarMassesToKilograms(30);
double cutoff = StagingScenarios.CutoffSeparationGravitationalRadii * StagingScenarios.GravitationalRadiusMeters;
double r = 30 * StagingScenarios.GravitationalRadiusMeters;
var doubled = new CircularInspiral(new InspiralParameters(mass, mass, 2 * r, cutoff)).Evaluate(0);
Near(doubled.OrbitalFrequencyHertz!.Value / initial.OrbitalFrequencyHertz.Value,
    Math.Pow(2, -1.5), 1e-12, "doubling separation frequency law");

double lastR = double.PositiveInfinity;
for (int i = 0; i < 1000; i++)
{
    var state = baseline.Evaluate(baseline.CutoffTimeSeconds * i / 1000.0);
    Check(state.SeparationMeters < lastR, "monotonic inspiral");
    Check(double.IsFinite(state.PhaseRadians) && double.IsFinite(state.GravitationalWaveFrequencyHertz!.Value), "finite inspiral");
    Near(state.Body1XMeters + state.Body2XMeters, 0, 1e-8, "equal mass COM x");
    Near(state.Body1ZMeters + state.Body2ZMeters, 0, 1e-8, "equal mass COM z");
    lastR = state.SeparationMeters;
}
var cutoffState = baseline.Evaluate(baseline.CutoffTimeSeconds);
Check(cutoffState.Phase == InspiralPhase.ConceptualMerger && !cutoffState.OrbitalFrequencyHertz.HasValue,
    "cutoff replaces frequencies with conceptual state");
Near(cutoffState.SeparationMeters, cutoff, 1e-12, "cutoff radius");
Check(baseline.Evaluate(baseline.EndTimeSeconds).Phase == InspiralPhase.ConceptualRemnant,
    "conceptual remnant follows merger");
var graph = FrequencyGraph.Sample(baseline, baseline.CutoffTimeSeconds, 256);
Check(graph.Count == 256 && graph[^1].PhysicalTimeSeconds < baseline.CutoffTimeSeconds,
    "graph stops before conceptual cutoff");
Check(graph[0].GravitationalWaveFrequencyHertz < graph[^1].GravitationalWaveFrequencyHertz,
    "linked frequency graph rises");

var unequal = new CircularInspiral(new InspiralParameters(20 * InspiralParameters.SolarMassKilograms,
    40 * InspiralParameters.SolarMassKilograms, r, cutoff)).Evaluate(0);
Near(20 * unequal.Body1XMeters + 40 * unequal.Body2XMeters, 0, 1e-8, "unequal mass COM x");
Near(20 * unequal.Body1ZMeters + 40 * unequal.Body2ZMeters, 0, 1e-8, "unequal mass COM z");
Near(Math.Sqrt(Math.Pow(unequal.Body1XMeters - unequal.Body2XMeters, 2)
    + Math.Pow(unequal.Body1ZMeters - unequal.Body2ZMeters, 2)), r, 1e-12, "body separation");

var session = new SimulationSession();
double chosen = baseline.CutoffTimeSeconds * 0.42;
session.Seek(chosen);
var sought = session.State;
session.Seek(baseline.CutoffTimeSeconds * 0.8);
session.Seek(chosen);
Near(session.State.SeparationMeters, sought.SeparationMeters, 1e-12, "seek radius replay");
Near(session.State.PhaseRadians, sought.PhaseRadians, 1e-12, "seek phase replay");
Near(session.State.GravitationalWaveFrequencyHertz!.Value, sought.GravitationalWaveFrequencyHertz!.Value, 1e-12, "seek frequency replay");
session.SetPlaybackRate(4);
Near(session.State.GravitationalWaveFrequencyHertz!.Value, sought.GravitationalWaveFrequencyHertz.Value, 1e-12, "playback rate does not change physical frequency");
session.SelectLayer(InformationLayer.Model);
Near(session.PlaybackTimeSeconds, chosen, 1e-12, "layer change preserves time");
Near(session.State.SeparationMeters, sought.SeparationMeters, 1e-12, "layer change preserves physics");
session.Pause();
session.Tick(20);
Near(session.PlaybackTimeSeconds, chosen, 1e-12, "pause freezes physical state");
var snapshot = TutorSnapshot.Capture(session, DateTimeOffset.UtcNow, 1e6, "Illustrative spheres");
var reply = await new OfflineStagingTutorProvider().AnswerAsync(new TutorQuestion("Why?", snapshot), CancellationToken.None);
Check(reply.IsOfflinePlaceholder && reply.Text.Contains("OFFLINE STAGING TUTOR"), "tutor is clearly labeled");
Check(TutorReplyValidity.IsCurrent(reply, session), "reply matches capture context");
session.Reset();
Check(session.ContextVersion != snapshot.ContextVersion, "reset invalidates prior tutor context");
Check(!TutorReplyValidity.IsCurrent(reply, session), "stale reply is rejected after reset");
Near(session.State.SeparationMeters, initial.SeparationMeters, 1e-12, "reset initial state");
session.SelectScenario(StagingScenarios.Wider);
Near(session.State.SeparationMeters, widerInitial.SeparationMeters, 1e-12, "scenario updates model state");
Check(session.Scenario.Id == StagingScenarios.Wider.Id && session.PlaybackTimeSeconds == 0, "scenario resets clock");

foreach (Action invalid in new Action[] {
    () => new InspiralParameters(-1, mass, r, cutoff),
    () => new InspiralParameters(mass, mass, cutoff, cutoff),
    () => session.SetPlaybackRate(100),
    () => session.SetInitialSeparation(cutoff)
})
{
    try { invalid(); throw new Exception("invalid input was accepted"); }
    catch (ArgumentException) { }
}
Check(session.Scenario.Id == StagingScenarios.Wider.Id, "invalid scenario leaves recoverable state");
Console.WriteLine("PASS: staging model, graph, clock, scenario, cutoff, snapshot and validation checks");
