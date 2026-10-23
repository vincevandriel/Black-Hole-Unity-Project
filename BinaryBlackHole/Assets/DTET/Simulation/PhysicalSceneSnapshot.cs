using System;

namespace BlackHole.Staging.Simulation
{
    /// <summary>One immutable time/units contract for all scene consumers. Not detector strain or a metric.</summary>
    public sealed class PhysicalSceneSnapshot
    {
        public const int SchemaVersion = 1;
        public const string ModelId = "circular-quadrupole-SI-v1";
        public CircularInspiral Model { get; }
        public InspiralState State { get; }
        public string ScenarioId { get; }
        public long ScenarioVersion { get; }
        public long ContextVersion { get; }
        public double PlaybackTimeSeconds { get; }
        public double PlaybackRate { get; }
        public bool Playing { get; }
        public double MetersPerDisplayUnit { get; }
        public bool AtCutoff => State.Phase != InspiralPhase.Inspiral;
        public double Progress => PlaybackTimeSeconds / Model.EndTimeSeconds;
        public double GraphProgress => Math.Min(1, PlaybackTimeSeconds / Model.CutoffTimeSeconds);
        public double Body1X => State.Body1XMeters / MetersPerDisplayUnit;
        public double Body1Z => State.Body1ZMeters / MetersPerDisplayUnit;
        public double Body2X => State.Body2XMeters / MetersPerDisplayUnit;
        public double Body2Z => State.Body2ZMeters / MetersPerDisplayUnit;
        public double PlaybackSecondsLeft => (PlaybackRate > 0 ? Model.EndTimeSeconds - PlaybackTimeSeconds
            : PlaybackTimeSeconds) / Math.Abs(PlaybackRate);

        private PhysicalSceneSnapshot(SimulationSession session, double metersPerDisplayUnit)
        {
            Model = session.Model;
            State = session.State;
            ScenarioId = session.Scenario.Id;
            ScenarioVersion = session.ScenarioVersion;
            ContextVersion = session.ContextVersion;
            PlaybackTimeSeconds = session.PlaybackTimeSeconds;
            PlaybackRate = session.PlaybackRate;
            Playing = session.IsPlaying;
            MetersPerDisplayUnit = metersPerDisplayUnit;
        }

        public static PhysicalSceneSnapshot Capture(SimulationSession session, double metersPerDisplayUnit)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (metersPerDisplayUnit <= 0 || double.IsNaN(metersPerDisplayUnit) || double.IsInfinity(metersPerDisplayUnit))
                throw new ArgumentOutOfRangeException(nameof(metersPerDisplayUnit));
            return new PhysicalSceneSnapshot(session, metersPerDisplayUnit);
        }
    }
}
