using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlackHole.Staging.Simulation
{
    public sealed class TutorSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion => CurrentSchemaVersion;
        public string ScenarioId { get; }
        public long ScenarioVersion { get; }
        public long ContextVersion { get; }
        public DateTimeOffset CapturedAtUtc { get; }
        public double PhysicalTimeSeconds { get; }
        public double ConceptualTimeSeconds { get; }
        public double Mass1Kilograms { get; }
        public double Mass2Kilograms { get; }
        public double SeparationMeters { get; }
        public double PhaseRadians { get; }
        public double? OrbitalFrequencyHertz { get; }
        public double? GravitationalWaveFrequencyHertz { get; }
        public InspiralPhase Phase { get; }
        public double PlaybackRate { get; }
        public double MetersPerUnityUnit { get; }
        public string MarkerSizeDescription { get; }
        public InformationLayer Layer { get; }
        public bool LeadingOrderCircularApproximation { get; }
        public bool MergerIsConceptual { get; }
        public bool IsDetectorStrain { get; }

        private TutorSnapshot(SimulationSession session, DateTimeOffset capturedAtUtc,
            double metersPerUnityUnit, string markerSizeDescription)
        {
            InspiralState state = session.State;
            InspiralParameters p = session.Model.Parameters;
            ScenarioId = session.Scenario.Id;
            ScenarioVersion = session.ScenarioVersion;
            ContextVersion = session.ContextVersion;
            CapturedAtUtc = capturedAtUtc.ToUniversalTime();
            PhysicalTimeSeconds = state.PhysicalTimeSeconds;
            ConceptualTimeSeconds = state.ConceptualTimeSeconds;
            Mass1Kilograms = p.Mass1Kilograms;
            Mass2Kilograms = p.Mass2Kilograms;
            SeparationMeters = state.SeparationMeters;
            PhaseRadians = state.PhaseRadians;
            OrbitalFrequencyHertz = state.OrbitalFrequencyHertz;
            GravitationalWaveFrequencyHertz = state.GravitationalWaveFrequencyHertz;
            Phase = state.Phase;
            PlaybackRate = session.PlaybackRate;
            MetersPerUnityUnit = metersPerUnityUnit;
            MarkerSizeDescription = markerSizeDescription;
            Layer = session.Layer;
            LeadingOrderCircularApproximation = true;
            MergerIsConceptual = true;
            IsDetectorStrain = false;
        }

        public static TutorSnapshot Capture(SimulationSession session,
            DateTimeOffset capturedAtUtc, double metersPerUnityUnit,
            string markerSizeDescription)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (metersPerUnityUnit <= 0 || double.IsNaN(metersPerUnityUnit)
                || double.IsInfinity(metersPerUnityUnit))
                throw new ArgumentOutOfRangeException(nameof(metersPerUnityUnit));
            if (string.IsNullOrWhiteSpace(markerSizeDescription))
                throw new ArgumentException("Describe the illustrative body marker size.", nameof(markerSizeDescription));
            return new TutorSnapshot(session, capturedAtUtc, metersPerUnityUnit, markerSizeDescription);
        }
    }

    public sealed class TutorQuestion
    {
        public string Text { get; }
        public TutorSnapshot Snapshot { get; }

        public TutorQuestion(string text, TutorSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Enter a question.", nameof(text));
            Text = text.Trim(); // question is data, never authority to alter the model
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }

    public sealed class TutorReply
    {
        public string Text { get; }
        public bool IsOfflinePlaceholder { get; }
        public long ContextVersion { get; }

        public TutorReply(string text, bool isOfflinePlaceholder, long contextVersion)
        {
            Text = text;
            IsOfflinePlaceholder = isOfflinePlaceholder;
            ContextVersion = contextVersion;
        }
    }

    public interface ITutorProvider
    {
        Task<TutorReply> AnswerAsync(TutorQuestion question, CancellationToken cancellationToken);
    }

    public static class TutorReplyValidity
    {
        public static bool IsCurrent(TutorReply reply, SimulationSession session)
        {
            if (reply == null) throw new ArgumentNullException(nameof(reply));
            if (session == null) throw new ArgumentNullException(nameof(session));
            return reply.ContextVersion == session.ContextVersion;
        }
    }

    // Deliberately scripted and offline. It is a staging adapter, not the course's functional AI.
    public sealed class OfflineStagingTutorProvider : ITutorProvider
    {
        public Task<TutorReply> AnswerAsync(TutorQuestion question, CancellationToken cancellationToken)
        {
            if (question == null) throw new ArgumentNullException(nameof(question));
            cancellationToken.ThrowIfCancellationRequested();
            TutorSnapshot s = question.Snapshot;
            string explanation = s.Phase == InspiralPhase.Inspiral
                ? "At the captured state, smaller separation corresponds to faster orbital motion. "
                  + "The dominant gravitational-wave frequency in this circular model is twice the orbital frequency."
                : "This captured state is a conceptual merger or remnant. The circular inspiral equations have stopped at their cutoff.";
            string text = "OFFLINE STAGING TUTOR — scripted placeholder, not AI. "
                + $"Snapshot: {s.ScenarioId}, physical t={s.PhysicalTimeSeconds:F3} s, captured {s.CapturedAtUtc:O}. "
                + explanation + " Marker sizes and scene distances are display choices; this is not calibrated detector strain.";
            return Task.FromResult(new TutorReply(text, true, s.ContextVersion));
        }
    }
}
