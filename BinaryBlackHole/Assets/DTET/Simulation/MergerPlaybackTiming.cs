using System;

namespace BlackHole.Staging.Simulation
{
    // Monotone presentation-time map; physical source samples are unchanged.
    public static class MergerPlaybackTiming
    {
        public const double MinimumFactor = .12;
        public const double WidthTg = 100;

        public static double Factor(double sourceSeconds, double mergerSeconds, double gravitationalSeconds)
        {
            double x = (sourceSeconds - mergerSeconds) / (WidthTg * gravitationalSeconds);
            return (x * x + MinimumFactor) / (x * x + 1);
        }

        private static double Coordinate(double sourceSeconds, double mergerSeconds, double width)
        {
            double root = Math.Sqrt(MinimumFactor);
            return sourceSeconds + width * (1 - MinimumFactor) / root
                * Math.Atan((sourceSeconds - mergerSeconds) / (width * root));
        }

        public static double DisplayDuration(double startSeconds, double endSeconds, double baseRate,
            double mergerSeconds, double gravitationalSeconds)
        {
            ReferenceFlowModel.RequireFinite(startSeconds, nameof(startSeconds));
            ReferenceFlowModel.RequireFinite(endSeconds, nameof(endSeconds));
            ReferenceFlowModel.RequireFinite(baseRate, nameof(baseRate));
            ReferenceFlowModel.RequireFinite(mergerSeconds, nameof(mergerSeconds));
            ReferenceFlowModel.RequireFinite(gravitationalSeconds, nameof(gravitationalSeconds));
            if (baseRate == 0 || gravitationalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(baseRate));
            double width = WidthTg * gravitationalSeconds;
            return Math.Abs((Coordinate(endSeconds, mergerSeconds, width) - Coordinate(startSeconds, mergerSeconds, width)) / baseRate);
        }

        public static double Advance(double sourceSeconds, double wallSeconds, double baseRate,
            double mergerSeconds, double gravitationalSeconds, double durationSeconds)
        {
            ReferenceFlowModel.RequireFinite(sourceSeconds, nameof(sourceSeconds));
            ReferenceFlowModel.RequireFinite(wallSeconds, nameof(wallSeconds));
            ReferenceFlowModel.RequireFinite(baseRate, nameof(baseRate));
            ReferenceFlowModel.RequireFinite(mergerSeconds, nameof(mergerSeconds));
            ReferenceFlowModel.RequireFinite(gravitationalSeconds, nameof(gravitationalSeconds));
            ReferenceFlowModel.RequireFinite(durationSeconds, nameof(durationSeconds));
            if (wallSeconds < 0 || gravitationalSeconds <= 0 || durationSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(wallSeconds));
            sourceSeconds = Math.Max(0, Math.Min(durationSeconds, sourceSeconds));
            if (wallSeconds == 0 || baseRate == 0) return sourceSeconds;
            double width = WidthTg * gravitationalSeconds;
            double target = Coordinate(sourceSeconds, mergerSeconds, width) + wallSeconds * baseRate;
            if (target <= Coordinate(0, mergerSeconds, width)) return 0;
            if (target >= Coordinate(durationSeconds, mergerSeconds, width)) return durationSeconds;
            double lower = 0, upper = durationSeconds;
            for (int i = 0; i < 54; i++)
            {
                double middle = .5 * (lower + upper);
                if (Coordinate(middle, mergerSeconds, width) < target) lower = middle;
                else upper = middle;
            }
            return .5 * (lower + upper);
        }
    }
}
