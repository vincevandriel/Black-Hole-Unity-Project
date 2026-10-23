using System;

namespace BlackHole.Staging.Simulation
{
    // Absolute-time queries keep pause, scrub and reverse independent of the render frame rate.
    public sealed class NumericalEventSession
    {
        public const double MinimumPlaybackRate = .01;
        public const double MaximumPlaybackRate = 4;
        public NumericalEventHistory Model { get; }
        public double PlaybackTimeSeconds { get; private set; }
        public double PlaybackRate { get; private set; } = .05;
        public bool IsPlaying { get; private set; }
        public long ContextVersion { get; private set; } = 1;
        public NumericalEventState State => Model.Evaluate(PlaybackTimeSeconds);
        public NumericalEventSession(NumericalEventHistory model)
        { Model = model ?? throw new ArgumentNullException(nameof(model)); }
        public void Play()
        { IsPlaying = PlaybackRate > 0 ? PlaybackTimeSeconds < Model.DurationSeconds : PlaybackTimeSeconds > 0; }
        public void Pause() { IsPlaying = false; }
        public void Tick(double wallDeltaSeconds)
        {
            if (!NumericalEventHistory.IsFinite(wallDeltaSeconds) || wallDeltaSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(wallDeltaSeconds));
            if (!IsPlaying) return;
            PlaybackTimeSeconds = Math.Max(0, Math.Min(Model.DurationSeconds,
                PlaybackTimeSeconds + wallDeltaSeconds * PlaybackRate));
            if ((PlaybackRate > 0 && PlaybackTimeSeconds >= Model.DurationSeconds)
                || (PlaybackRate < 0 && PlaybackTimeSeconds <= 0)) IsPlaying = false;
        }
        public void Seek(double elapsedSeconds)
        {
            if (!NumericalEventHistory.IsFinite(elapsedSeconds)) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            PlaybackTimeSeconds = Math.Max(0, Math.Min(Model.DurationSeconds, elapsedSeconds));
            IsPlaying = false; ContextVersion++;
        }
        public void Reset() { PlaybackTimeSeconds = 0; IsPlaying = false; ContextVersion++; }
        public void SetPlaybackRate(double rate)
        {
            if (!NumericalEventHistory.IsFinite(rate) || Math.Abs(rate) < MinimumPlaybackRate
                || Math.Abs(rate) > MaximumPlaybackRate) throw new ArgumentOutOfRangeException(nameof(rate));
            PlaybackRate = rate;
        }
    }
}
