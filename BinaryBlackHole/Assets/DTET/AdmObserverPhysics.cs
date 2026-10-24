using System;

namespace DTET.VisualPrototype
{
    // Double-precision values in dataset units (G=c=1); map Unity coordinates explicitly.
    public readonly struct MetricVector3
    {
        public readonly double X, Y, Z;
        public MetricVector3(double x, double y, double z)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z)) throw new ArgumentException("Metric vectors must be finite.");
            X = x; Y = y; Z = z;
        }
        internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static MetricVector3 operator +(MetricVector3 a, MetricVector3 b) => new MetricVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public readonly struct PhotonFourMomentum
    {
        public readonly double Time;
        public readonly MetricVector3 Spatial;
        public PhotonFourMomentum(double time, MetricVector3 spatial)
        {
            if (!MetricVector3.Finite(time) || time <= 0) throw new ArgumentException("Photon momentum must be future-directed in this ADM chart.");
            Time = time; Spatial = spatial;
        }
    }

    // Local ADM sample: ds²=-alpha² dt²+gamma_ij(dx^i+beta^i dt)(dx^j+beta^j dt).
    public sealed class AdmMetricSample
    {
        private readonly double xx, xy, xz, yy, yz, zz;
        public double Lapse { get; }
        public MetricVector3 Shift { get; }

        public AdmMetricSample(double lapse, MetricVector3 shift, double gammaXX, double gammaXY,
            double gammaXZ, double gammaYY, double gammaYZ, double gammaZZ)
        {
            if (!MetricVector3.Finite(lapse) || lapse <= 0) throw new ArgumentException("A valid positive lapse is required.");
            foreach (var entry in new[] { gammaXX, gammaXY, gammaXZ, gammaYY, gammaYZ, gammaZZ })
                if (!MetricVector3.Finite(entry)) throw new ArgumentException("Spatial metric entries must be finite.");
            // Cholesky pivots check that this coordinate slice has a positive-definite spatial metric.
            if (gammaXX <= 0) throw new ArgumentException("Spatial metric is not positive definite.");
            var l00 = Math.Sqrt(gammaXX);
            var l10 = gammaXY / l00; var l20 = gammaXZ / l00;
            var pivot1 = gammaYY - l10 * l10;
            if (!MetricVector3.Finite(pivot1) || pivot1 <= 0) throw new ArgumentException("Spatial metric is not positive definite.");
            var l21 = (gammaYZ - l20 * l10) / Math.Sqrt(pivot1);
            var pivot2 = gammaZZ - l20 * l20 - l21 * l21;
            if (!MetricVector3.Finite(pivot2) || pivot2 <= 0) throw new ArgumentException("Spatial metric is not positive definite.");
            Lapse = lapse; Shift = shift;
            xx = gammaXX; xy = gammaXY; xz = gammaXZ; yy = gammaYY; yz = gammaYZ; zz = gammaZZ;
        }

        private double SpatialProduct(MetricVector3 a, MetricVector3 b) =>
            xx * a.X * b.X + yy * a.Y * b.Y + zz * a.Z * b.Z
            + xy * (a.X * b.Y + a.Y * b.X) + xz * (a.X * b.Z + a.Z * b.X)
            + yz * (a.Y * b.Z + a.Z * b.Y);

        // d tau / dt for a timelike coordinate worldline, v^i=dx^i/dt (not physical local velocity).
        public double ProperTimeRate(MetricVector3 coordinateVelocity)
        {
            var shifted = coordinateVelocity + Shift;
            var squaredRate = Lapse * Lapse - SpatialProduct(shifted, shifted);
            if (!MetricVector3.Finite(squaredRate) || squaredRate <= 0)
                throw new ArgumentException("Observer worldline is not timelike in this metric.");
            return Math.Sqrt(squaredRate);
        }

        // E = -g_mu_nu k^mu u^nu requires future-directed null momentum, even for backward-traced rays.
        public double MeasurePhotonEnergy(PhotonFourMomentum photon, MetricVector3 coordinateVelocity, double nullRelativeTolerance = 1e-8)
        {
            if (!MetricVector3.Finite(nullRelativeTolerance) || nullRelativeTolerance <= 0 || nullRelativeTolerance > 1e-3)
                throw new ArgumentOutOfRangeException(nameof(nullRelativeTolerance));
            var rate = ProperTimeRate(coordinateVelocity);
            var shiftedPhoton = new MetricVector3(photon.Spatial.X + Shift.X * photon.Time,
                photon.Spatial.Y + Shift.Y * photon.Time, photon.Spatial.Z + Shift.Z * photon.Time);
            var temporalSquare = Lapse * Lapse * photon.Time * photon.Time;
            var spatialSquare = SpatialProduct(shiftedPhoton, shiftedPhoton);
            var scale = temporalSquare + Math.Abs(spatialSquare);
            if (!MetricVector3.Finite(scale) || scale <= 0
                || Math.Abs(spatialSquare - temporalSquare) > nullRelativeTolerance * scale)
                throw new ArgumentException("Photon fails the local null constraint.");
            var energy = (Lapse * Lapse * photon.Time - SpatialProduct(shiftedPhoton, coordinateVelocity + Shift)) / rate;
            if (!MetricVector3.Finite(energy) || energy <= 0) throw new ArgumentException("Measured photon energy is invalid.");
            return energy;
        }

        public static double FrequencyRatio(double emittedEnergy, double observedEnergy)
        {
            if (!MetricVector3.Finite(emittedEnergy) || !MetricVector3.Finite(observedEnergy)
                || emittedEnergy <= 0 || observedEnergy <= 0) throw new ArgumentException("Both endpoint energies must be finite and positive.");
            var ratio = observedEnergy / emittedEnergy;
            if (!MetricVector3.Finite(ratio) || ratio <= 0) throw new ArgumentException("Frequency ratio is outside numerical range.");
            return ratio;
        }
    }
}
