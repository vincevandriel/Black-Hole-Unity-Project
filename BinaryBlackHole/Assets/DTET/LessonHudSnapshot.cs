using System;
using BlackHole.Staging.Simulation;
namespace DTET.VisualPrototype
{
    /// <summary>Presentation data, not a second simulation clock. Null physical values mean unavailable.</summary>
    public readonly struct LessonHudSnapshot
    {
        public readonly float Progress, PlaybackRate, VisualSeparationRatio;
        public readonly bool Playing;
        public readonly double? SeparationKm, OrbitsPerSecond, WaveFrequencyHz;
        public readonly PhysicalSceneSnapshot Physics;
        public bool PhysicalModel => Physics != null;
        public bool ConceptualEndpoint => PhysicalModel ? Physics.AtCutoff : Progress >= 0.98f;
        public float PlaybackSecondsLeft => PhysicalModel ? (float)Physics.PlaybackSecondsLeft : (1f - Progress) * 40f / Math.Abs(PlaybackRate);

        public LessonHudSnapshot(PhysicalSceneSnapshot frame)
        {
            Physics = frame ?? throw new ArgumentNullException(nameof(frame));
            Progress = (float)frame.Progress;
            PlaybackRate = (float)frame.PlaybackRate;
            VisualSeparationRatio = (float)(frame.State.SeparationMeters / frame.Model.Parameters.InitialSeparationMeters);
            Playing = frame.Playing;
            SeparationKm = frame.AtCutoff ? (double?)null : frame.State.SeparationMeters / 1000;
            OrbitsPerSecond = frame.State.OrbitalFrequencyHertz;
            WaveFrequencyHz = frame.State.GravitationalWaveFrequencyHertz;
        }

        public LessonHudSnapshot(float progress, float playbackRate, float visualSeparationRatio, bool playing,
            double? separationKm = null, double? orbitsPerSecond = null, double? waveFrequencyHz = null)
        {
            Physics = null;
            Progress = progress;
            PlaybackRate = playbackRate;
            VisualSeparationRatio = visualSeparationRatio;
            Playing = playing;
            SeparationKm = separationKm;
            OrbitsPerSecond = orbitsPerSecond;
            WaveFrequencyHz = waveFrequencyHz;
        }
    }

    public interface ILessonHudProvider
    {
        LessonHudSnapshot CaptureHudSnapshot();
        void TogglePlayback();
        void ResetPlayback();
        void SeekStoryboard(float progress);
        void SetHudPlaybackRate(float rate);
        void SetLearningVisible(bool visible);
    }
}
