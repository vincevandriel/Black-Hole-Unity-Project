using System;

namespace BlackHole.Staging.Simulation
{
    public enum InformationLayer { Story, Graph, Model }

    public sealed class ScenarioPreset
    {
        public string Id { get; }
        public string Name { get; }
        public double InitialSeparationMeters { get; }

        public ScenarioPreset(string id, string name, double initialSeparationMeters)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Scenario ID and name are required.");
            Id = id;
            Name = name;
            InitialSeparationMeters = initialSeparationMeters;
        }
    }

    public static class StagingScenarios
    {
        public const double MinimumInitialSeparationGravitationalRadii = 25.0;
        public const double MaximumInitialSeparationGravitationalRadii = 50.0;
        public const double CutoffSeparationGravitationalRadii = 10.0;
        public const double MinimumPlaybackRate = 0.1;
        public const double MaximumPlaybackRate = 4.0;

        public static double TotalMassKilograms => 60.0 * InspiralParameters.SolarMassKilograms;
        public static double GravitationalRadiusMeters => InspiralParameters.GravitationalConstant
            * TotalMassKilograms / Math.Pow(InspiralParameters.SpeedOfLight, 2.0);

        public static ScenarioPreset Baseline => new ScenarioPreset("baseline-30rg", "Baseline: 30 GM/c²", 30.0 * GravitationalRadiusMeters);
        public static ScenarioPreset Wider => new ScenarioPreset("wider-45rg", "Wider start: 45 GM/c²", 45.0 * GravitationalRadiusMeters);

        public static InspiralParameters CreateParameters(double initialSeparationMeters)
        {
            double rg = GravitationalRadiusMeters;
            if (initialSeparationMeters < MinimumInitialSeparationGravitationalRadii * rg
                || initialSeparationMeters > MaximumInitialSeparationGravitationalRadii * rg)
                throw new ArgumentOutOfRangeException(nameof(initialSeparationMeters),
                    "Choose an initial separation between 25 and 50 GM/c² for this staging model.");
            double mass = InspiralParameters.SolarMassesToKilograms(30.0);
            return new InspiralParameters(mass, mass, initialSeparationMeters,
                CutoffSeparationGravitationalRadii * rg);
        }
    }

    // Commands, UI, graph and tutor all read one session generation and one model state.
    public sealed class SimulationSession
    {
        public CircularInspiral Model { get; private set; }
        public ScenarioPreset Scenario { get; private set; }
        public long ScenarioVersion { get; private set; }
        public long ContextVersion { get; private set; }
        public double PlaybackTimeSeconds { get; private set; }
        public double PlaybackRate { get; private set; }
        public bool IsPlaying { get; private set; }
        public InformationLayer Layer { get; private set; }
        public InspiralState State => Model.Evaluate(PlaybackTimeSeconds);

        public SimulationSession()
        {
            Scenario = StagingScenarios.Baseline;
            Model = new CircularInspiral(StagingScenarios.CreateParameters(Scenario.InitialSeparationMeters));
            ScenarioVersion = 1;
            ContextVersion = 1;
            PlaybackRate = 1.0;
            Layer = InformationLayer.Story;
        }

        public void Play() { IsPlaying = PlaybackTimeSeconds < Model.EndTimeSeconds; }
        public void Pause() { IsPlaying = false; }

        public void Tick(double wallDeltaSeconds)
        {
            if (wallDeltaSeconds < 0.0 || double.IsNaN(wallDeltaSeconds) || double.IsInfinity(wallDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(wallDeltaSeconds), "Wall-clock step must be finite and non-negative.");
            if (!IsPlaying) return;
            PlaybackTimeSeconds = Math.Min(Model.EndTimeSeconds,
                PlaybackTimeSeconds + wallDeltaSeconds * PlaybackRate);
            if (PlaybackTimeSeconds >= Model.EndTimeSeconds) IsPlaying = false;
        }

        public void Seek(double playbackTimeSeconds)
        {
            if (double.IsNaN(playbackTimeSeconds) || double.IsInfinity(playbackTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(playbackTimeSeconds), "Seek time must be finite.");
            PlaybackTimeSeconds = Math.Max(0.0, Math.Min(Model.EndTimeSeconds, playbackTimeSeconds));
            if (PlaybackTimeSeconds >= Model.EndTimeSeconds) IsPlaying = false;
            ContextVersion++;
        }

        public void Reset()
        {
            PlaybackTimeSeconds = 0.0;
            IsPlaying = false;
            ContextVersion++; // invalidates graph/tutor context after reset
        }

        public void SetPlaybackRate(double rate)
        {
            if (double.IsNaN(rate) || double.IsInfinity(rate)
                || rate < StagingScenarios.MinimumPlaybackRate
                || rate > StagingScenarios.MaximumPlaybackRate)
                throw new ArgumentOutOfRangeException(nameof(rate), "Choose a playback rate from 0.1× to 4×.");
            PlaybackRate = rate;
        }

        public void SelectLayer(InformationLayer layer)
        {
            if (!Enum.IsDefined(typeof(InformationLayer), layer))
                throw new ArgumentOutOfRangeException(nameof(layer));
            Layer = layer;
        }

        public void SelectScenario(ScenarioPreset scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            CircularInspiral next = new CircularInspiral(StagingScenarios.CreateParameters(scenario.InitialSeparationMeters));
            Scenario = scenario;
            Model = next;
            PlaybackTimeSeconds = 0.0;
            IsPlaying = false;
            ScenarioVersion++;
            ContextVersion++;
        }

        // Setting the slider commits a new scenario; it never modifies an ongoing orbit.
        public void SetInitialSeparation(double initialSeparationMeters)
        {
            SelectScenario(new ScenarioPreset("custom", "Custom start", initialSeparationMeters));
        }
    }
}
