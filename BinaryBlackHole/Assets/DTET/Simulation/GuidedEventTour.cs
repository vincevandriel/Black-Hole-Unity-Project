#nullable enable
using System;
using System.Collections.Generic;

namespace BlackHole.Staging.Simulation
{
    public sealed class GuidedEventCheckpoint
    {
        public string Id { get; }
        public double Seconds { get; }

        internal GuidedEventCheckpoint(string id, double seconds)
        { Id = id; Seconds = seconds; }
    }

    // Presentation bookmarks, not detections of gas transitions or a separate clock.
    public sealed class GuidedEventTour
    {
        private IReadOnlyList<GuidedEventCheckpoint> checkpoints = Array.Empty<GuidedEventCheckpoint>();
        private bool[] visited = Array.Empty<bool>();
        private int currentIndex = -1;
        private double duration;

        public IReadOnlyList<GuidedEventCheckpoint> Checkpoints => checkpoints;
        public bool Enabled { get; private set; }
        public long Generation { get; private set; }
        public bool IsPausedAtCheckpoint => currentIndex >= 0;
        public GuidedEventCheckpoint? CurrentCheckpoint => currentIndex >= 0 ? checkpoints[currentIndex] : null;
        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < visited.Length; i++) if (!visited[i]) return false;
                return true;
            }
        }

        public GuidedEventTour(double gasStartSeconds, double mergerTimeSeconds,
            double gravitationalSeconds, double durationSeconds, long generation = 0)
        { Reset(gasStartSeconds, mergerTimeSeconds, gravitationalSeconds, durationSeconds, generation); }

        public void Reset(double gasStartSeconds, double mergerTimeSeconds,
            double gravitationalSeconds, double durationSeconds, long generation = 0)
        {
            ReferenceFlowModel.RequireFinite(gasStartSeconds, nameof(gasStartSeconds));
            ReferenceFlowModel.RequireFinite(mergerTimeSeconds, nameof(mergerTimeSeconds));
            ReferenceFlowModel.RequireFinite(gravitationalSeconds, nameof(gravitationalSeconds));
            ReferenceFlowModel.RequireFinite(durationSeconds, nameof(durationSeconds));
            if (durationSeconds <= 0 || gravitationalSeconds <= 0 || gasStartSeconds < 0
                || gasStartSeconds > durationSeconds || mergerTimeSeconds < 0 || mergerTimeSeconds > durationSeconds)
                throw new ArgumentOutOfRangeException(nameof(gasStartSeconds));

            double tolerance = Math.Max(1e-6 * gravitationalSeconds, 1e-12 * Math.Max(1, durationSeconds));
            var points = new List<GuidedEventCheckpoint>(5);
            var priorities = new List<int>(5);
            void Add(string id, double seconds, int priority)
            {
                seconds = Math.Max(gasStartSeconds, Math.Min(durationSeconds, seconds));
                int previous = points.Count - 1;
                if (previous >= 0 && seconds - points[previous].Seconds <= tolerance)
                {
                    if (priority > priorities[previous])
                    { points[previous] = new GuidedEventCheckpoint(id, seconds); priorities[previous] = priority; }
                    return;
                }
                points.Add(new GuidedEventCheckpoint(id, seconds));
                priorities.Add(priority);
            }

            if (gasStartSeconds < mergerTimeSeconds)
            {
                double span = mergerTimeSeconds - gasStartSeconds;
                Add("inspiral", gasStartSeconds, 0);
                Add("gas_tides", gasStartSeconds + .45 * span, 1);
                Add("final_approach", mergerTimeSeconds - Math.Min(80 * gravitationalSeconds, .15 * span), 2);
            }
            if (gasStartSeconds <= mergerTimeSeconds) Add("coalescence", mergerTimeSeconds, 4);
            if (mergerTimeSeconds < durationSeconds)
                Add("remnant", Math.Max(gasStartSeconds, mergerTimeSeconds
                    + Math.Min(180 * gravitationalSeconds, .4 * (durationSeconds - mergerTimeSeconds))), 3);

            checkpoints = points.AsReadOnly();
            visited = new bool[points.Count];
            currentIndex = -1;
            duration = durationSeconds;
            Generation = generation;
            Enabled = true;
        }

        public double ClampForward(double fromSeconds, double proposedSeconds, long generation,
            out GuidedEventCheckpoint? checkpoint)
        {
            ReferenceFlowModel.RequireFinite(fromSeconds, nameof(fromSeconds));
            ReferenceFlowModel.RequireFinite(proposedSeconds, nameof(proposedSeconds));
            checkpoint = null;
            if (generation != Generation) return proposedSeconds;
            double from = Bound(fromSeconds), proposed = Bound(proposedSeconds);
            if (proposed < from)
            { NotifySeek(proposed, generation); return proposed; }
            if (!Enabled) return proposed;
            if (currentIndex >= 0)
            { checkpoint = checkpoints[currentIndex]; return checkpoint.Seconds; }

            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i].Seconds < from) visited[i] = true;
                if (visited[i] || checkpoints[i].Seconds < from || checkpoints[i].Seconds > proposed) continue;
                currentIndex = i;
                checkpoint = checkpoints[i];
                return checkpoint.Seconds;
            }
            return proposed;
        }

        public bool Continue(long generation)
        {
            if (generation != Generation || currentIndex < 0) return false;
            visited[currentIndex] = true;
            currentIndex = -1;
            return true;
        }

        // Exact seek targets remain eligible; seeks never start playback or latch a pause.
        public void NotifySeek(double seconds, long generation)
        {
            ReferenceFlowModel.RequireFinite(seconds, nameof(seconds));
            if (generation != Generation) return;
            seconds = Bound(seconds);
            currentIndex = -1;
            for (int i = 0; i < checkpoints.Count; i++) visited[i] = checkpoints[i].Seconds < seconds;
        }

        public void SetEnabled(bool enabled, double currentSeconds, long generation)
        {
            ReferenceFlowModel.RequireFinite(currentSeconds, nameof(currentSeconds));
            if (generation != Generation || enabled == Enabled) return;
            NotifySeek(currentSeconds, generation);
            Enabled = enabled;
        }

        private double Bound(double seconds) => Math.Max(0, Math.Min(duration, seconds));
    }
}
