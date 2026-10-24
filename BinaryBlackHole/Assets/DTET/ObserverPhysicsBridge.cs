using System;
using UnityEngine;

namespace DTET.VisualPrototype
{
    // Display pose; the provider maps it to metric coordinates, physical time and a timelike worldline.
    public readonly struct ObserverViewRequest
    {
        public readonly Matrix4x4 DisplayPose;
        public readonly long Epoch, WorldlineRevision;
        public ObserverViewRequest(Matrix4x4 displayPose, long epoch, long worldlineRevision)
        { DisplayPose = displayPose; Epoch = epoch; WorldlineRevision = worldlineRevision; }
    }

    // Backward-traced camera ray; FrequencyRatio = nu_observer / nu_emitter.
    public readonly struct StellarRaySample
    {
        public readonly bool Available, Captured;
        public readonly int? SourceId;
        public readonly double? FrequencyRatio;
        public double? Redshift => FrequencyRatio.HasValue ? 1.0 / FrequencyRatio.Value - 1.0 : (double?)null;
        public StellarRaySample(bool available, bool captured, int? sourceId, double? frequencyRatio)
        {
            if (sourceId.HasValue && sourceId.Value < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            if (!available && (captured || sourceId.HasValue || frequencyRatio.HasValue))
                throw new ArgumentException("Unavailable ray tracing cannot invent an observation.");
            if (captured && (sourceId.HasValue || frequencyRatio.HasValue))
                throw new ArgumentException("Captured rays have no observed stellar frequency.");
            if (frequencyRatio.HasValue && (!sourceId.HasValue || frequencyRatio.Value <= 0
                || double.IsNaN(frequencyRatio.Value) || double.IsInfinity(frequencyRatio.Value)))
                throw new ArgumentException("An observed frequency needs a source and a finite positive ratio.");
            Available = available; Captured = captured; SourceId = sourceId; FrequencyRatio = frequencyRatio;
        }
    }

    // Provider contract for metric, light propagation and complete clock histories; no GR provider is installed.
    public interface IObserverPhysicsProvider
    {
        string ModelId { get; }
        string Status { get; }
        RelativityClockSnapshot CaptureClocks(ObserverViewRequest observer);
        StellarRaySample TracePastDirectedRay(ObserverViewRequest observer, Vector3 displayWorldDirection);
    }

    public sealed class UnavailableObserverPhysicsProvider : IObserverPhysicsProvider
    {
        public string ModelId => "unmapped-visual-staging";
        public string Status => "Relativistic optics and clocks not connected";
        public RelativityClockSnapshot CaptureClocks(ObserverViewRequest observer) =>
            new RelativityClockSnapshot(ModelId, observer.Epoch, observer.WorldlineRevision, RelativityFidelity.Unavailable);
        public StellarRaySample TracePastDirectedRay(ObserverViewRequest observer, Vector3 displayWorldDirection) =>
            new StellarRaySample(false, false, null, null);
    }

    // Shared clock/optics attachment. Teleports invalidate worldline history; timeline changes invalidate the epoch.
    public sealed class ObserverPhysicsBridge : MonoBehaviour
    {
        private ExhibitCameraRig rig;
        private IObserverPhysicsProvider provider = new UnavailableObserverPhysicsProvider();
        public long Epoch { get; private set; }
        public long WorldlineRevision { get; private set; }
        public string Status => provider.Status;
        public ObserverViewRequest Request => new ObserverViewRequest(transform.localToWorldMatrix, Epoch, WorldlineRevision);

        public void Initialize(ExhibitCameraRig visitor)
        {
            if (rig != null) rig.PoseRelocated -= InvalidateWorldline;
            rig = visitor;
            rig.PoseRelocated += InvalidateWorldline;
        }

        public void BindProvider(IObserverPhysicsProvider source)
        {
            provider = source ?? new UnavailableObserverPhysicsProvider();
            InvalidateEpoch();
        }

        public RelativityClockSnapshot CaptureClocks()
        {
            var snapshot = provider.CaptureClocks(Request);
            // Asynchronous/precomputed providers must not publish stale history after seek/teleport.
            return snapshot.Matches(provider.ModelId, Epoch, WorldlineRevision) ? snapshot
                : new RelativityClockSnapshot(provider.ModelId, Epoch, WorldlineRevision, RelativityFidelity.Unavailable);
        }

        public void InvalidateEpoch() { Epoch++; WorldlineRevision++; }
        public void InvalidateWorldline() => WorldlineRevision++;

        private void OnDestroy()
        {
            if (rig != null) rig.PoseRelocated -= InvalidateWorldline;
        }
    }
}
