using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BlackHole.Staging.Simulation;

namespace DTET.VisualPrototype
{
    // Cancellable scenario preparation; only complete matching files are published.
    internal sealed class MiniDiskPreparation : IDisposable
    {
        private sealed class Work
        {
            public InteractiveBinaryScenario Scenario;
            public long Generation;
            public string Path;
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public Task Task;
            public bool Published;
            private long progress;
            public double Progress => BitConverter.Int64BitsToDouble(Interlocked.Read(ref progress));
            public void Report(double value) => Interlocked.Exchange(ref progress, BitConverter.DoubleToInt64Bits(value));
        }
        private Work work;
        public bool Running => work != null && !work.Published;
        public double Progress => work == null ? 0 : work.Progress;
        public string Status { get; private set; } = "";

        public void Start(InteractiveBinaryScenario scenario, long generation, string directory)
        {
            Cancel(); Directory.CreateDirectory(directory);
            var request = new Work { Scenario = scenario, Generation = generation,
                Path = System.IO.Path.Combine(directory, "disks-" + Guid.NewGuid().ToString("N") + ".mdf") };
            work = request; Status = "Computing pressure and gravity for this binary. Playback is paused.";
            request.Task = Task.Run(() => MiniDiskSimulation.GenerateCache(scenario, request.Path, 128, 513,
                request.Cancellation.Token, request.Report));
        }

        public bool TryComplete(InteractiveBinaryScenario scenario, long generation, out string path)
        {
            path = null; var request = work;
            if (request == null || request.Published) return false;
            if (!ReferenceEquals(request.Scenario, scenario) || request.Generation != generation) { Cancel(); return false; }
            if (!request.Task.IsCompleted) return false;
            request.Published = true;
            if (request.Task.IsCanceled || request.Cancellation.IsCancellationRequested)
            { Status = "Disk preparation cancelled."; return false; }
            if (request.Task.IsFaulted)
            { Status = "Disk preparation failed: " + request.Task.Exception.GetBaseException().Message; return false; }
            path = request.Path; Status = "Evolved disks ready."; return true;
        }

        public void Cancel()
        {
            var request = work; work = null;
            if (request == null) return;
            request.Cancellation.Cancel();
            request.Task.ContinueWith(completed =>
            {
                if (completed.IsFaulted) { var observed = completed.Exception; }
                request.Cancellation.Dispose();
                try { File.Delete(request.Path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            Status = "Disk preparation cancelled.";
        }
        public void Dispose() => Cancel();
    }
}
