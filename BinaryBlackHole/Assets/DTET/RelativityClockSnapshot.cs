using System;

namespace DTET.VisualPrototype
{
    public enum RelativityFidelity { Unavailable, Approximate, Relativistic }

    // Proper-time totals from one worldline history, independent of frame time and photon redshift.
    public readonly struct RelativityClockSnapshot
    {
        public readonly string ModelId;
        public readonly long Epoch, WorldlineRevision;
        public readonly RelativityFidelity Fidelity;
        public readonly double? VisitorProperSeconds, ReferenceProperSeconds, VisitorToReferenceRate;
        public double? DifferenceSeconds => VisitorProperSeconds - ReferenceProperSeconds;

        public RelativityClockSnapshot(string modelId, long epoch, long worldlineRevision, RelativityFidelity fidelity,
            double? visitorProperSeconds = null, double? referenceProperSeconds = null, double? visitorToReferenceRate = null)
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("A spacetime model identifier is required.", nameof(modelId));
            if (epoch < 0 || worldlineRevision < 0) throw new ArgumentOutOfRangeException(nameof(epoch));
            if (fidelity < RelativityFidelity.Unavailable || fidelity > RelativityFidelity.Relativistic)
                throw new ArgumentOutOfRangeException(nameof(fidelity));
            if (fidelity == RelativityFidelity.Unavailable)
            {
                if (visitorProperSeconds.HasValue || referenceProperSeconds.HasValue || visitorToReferenceRate.HasValue)
                    throw new ArgumentException("Unavailable physics cannot supply clock readings.");
            }
            else if (!IsNonnegativeFinite(visitorProperSeconds) || !IsNonnegativeFinite(referenceProperSeconds)
                || !IsNonnegativeFinite(visitorToReferenceRate) || visitorToReferenceRate.GetValueOrDefault() <= 0)
                throw new ArgumentException("Connected clock totals must be finite and nonnegative, with a positive local rate.");
            ModelId = modelId; Epoch = epoch; WorldlineRevision = worldlineRevision; Fidelity = fidelity;
            VisitorProperSeconds = visitorProperSeconds; ReferenceProperSeconds = referenceProperSeconds;
            VisitorToReferenceRate = visitorToReferenceRate;
        }

        public bool Matches(string modelId, long epoch, long worldlineRevision) =>
            ModelId == modelId && Epoch == epoch && WorldlineRevision == worldlineRevision;

        private static bool IsNonnegativeFinite(double? value) => value.HasValue
            && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value >= 0;
    }
}
