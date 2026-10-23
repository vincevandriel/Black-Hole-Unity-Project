using System;

namespace BlackHole.Staging.Simulation
{
    public enum InformationLayer { Story, Graph, Model }

    public sealed class ScenarioPreset
    {
        public string Id { get; }
        public string Name { get; }
        public double InitialSeparationMeters { get; }
        public double Mass1SolarMasses { get; }
        public double Mass2SolarMasses { get; }
        public double GravitationalRadiusMeters => InspiralParameters.GravitationalConstant
            * InspiralParameters.SolarMassKilograms * (Mass1SolarMasses + Mass2SolarMasses)
            / Math.Pow(InspiralParameters.SpeedOfLight, 2.0);

        public ScenarioPreset(string id, string name, double initialSeparationMeters,
            double mass1SolarMasses = 30.0, double mass2SolarMasses = 30.0)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Scenario ID and name are required.");
            Id = id;
            Name = name;
            InitialSeparationMeters = initialSeparationMeters;
            // Validate masses before a scenario can be published to a session.
            InspiralParameters.SolarMassesToKilograms(mass1SolarMasses);
            InspiralParameters.SolarMassesToKilograms(mass2SolarMasses);
            Mass1SolarMasses = mass1SolarMasses;
            Mass2SolarMasses = mass2SolarMasses;
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
        public static ScenarioPreset Unequal => new ScenarioPreset("unequal-20-40", "Unequal: 20 + 40 solar masses", 30.0 * GravitationalRadiusMeters, 20, 40);

        public static InspiralParameters CreateParameters(double initialSeparationMeters)
            => CreateParameters(new ScenarioPreset("parameters", "Parameters", initialSeparationMeters));

        public static InspiralParameters CreateParameters(ScenarioPreset scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            double rg = scenario.GravitationalRadiusMeters;
            double initialSeparationMeters = scenario.InitialSeparationMeters;
            if (double.IsNaN(initialSeparationMeters) || double.IsInfinity(initialSeparationMeters)
                || initialSeparationMeters < MinimumInitialSeparationGravitationalRadii * rg
                || initialSeparationMeters > MaximumInitialSeparationGravitationalRadii * rg)
                throw new ArgumentOutOfRangeException(nameof(initialSeparationMeters),
                    "Choose an initial separation between 25 and 50 GM/c² for this staging model.");
            return new InspiralParameters(InspiralParameters.SolarMassesToKilograms(scenario.Mass1SolarMasses),
                InspiralParameters.SolarMassesToKilograms(scenario.Mass2SolarMasses), initialSeparationMeters,
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

        public void Play() { IsPlaying = PlaybackRate > 0 ? PlaybackTimeSeconds < Model.EndTimeSeconds : PlaybackTimeSeconds > 0; }
        public void Pause() { IsPlaying = false; }

        public void Tick(double wallDeltaSeconds)
        {
            if (wallDeltaSeconds < 0.0 || double.IsNaN(wallDeltaSeconds) || double.IsInfinity(wallDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(wallDeltaSeconds), "Wall-clock step must be finite and non-negative.");
            if (!IsPlaying) return;
            PlaybackTimeSeconds = Math.Max(0, Math.Min(Model.EndTimeSeconds,
                PlaybackTimeSeconds + wallDeltaSeconds * PlaybackRate));
            if ((PlaybackRate > 0 && PlaybackTimeSeconds >= Model.EndTimeSeconds)
                || (PlaybackRate < 0 && PlaybackTimeSeconds <= 0)) IsPlaying = false;
        }

        public void Seek(double playbackTimeSeconds)
        {
            if (double.IsNaN(playbackTimeSeconds) || double.IsInfinity(playbackTimeSeconds))
                throw new ArgumentOutOfRangeException(nameof(playbackTimeSeconds), "Seek time must be finite.");
            PlaybackTimeSeconds = Math.Max(0.0, Math.Min(Model.EndTimeSeconds, playbackTimeSeconds));
            IsPlaying = false;
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
                || Math.Abs(rate) < StagingScenarios.MinimumPlaybackRate
                || Math.Abs(rate) > StagingScenarios.MaximumPlaybackRate)
                throw new ArgumentOutOfRangeException(nameof(rate), "Choose a signed playback rate with magnitude 0.1× to 4×.");
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
            CircularInspiral next = new CircularInspiral(StagingScenarios.CreateParameters(scenario));
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
            SelectScenario(new ScenarioPreset("custom", "Custom start", initialSeparationMeters,
                Scenario.Mass1SolarMasses, Scenario.Mass2SolarMasses));
        }
    }
}
