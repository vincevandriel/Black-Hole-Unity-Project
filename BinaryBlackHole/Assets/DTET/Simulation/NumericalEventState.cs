using System;

namespace BlackHole.Staging.Simulation
{
    // SXS coordinates retain their original right-handed axes. The renderer owns the Unity axis mapping.
    public readonly struct NumericalVector3
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);
        public NumericalVector3(double x, double y, double z) { X = x; Y = y; Z = z; }
        internal NumericalVector3 Scale(double scale) => new NumericalVector3(X * scale, Y * scale, Z * scale);
        internal static NumericalVector3 Lerp(NumericalVector3 a, NumericalVector3 b, double fraction)
            => new NumericalVector3(a.X + (b.X - a.X) * fraction,
                a.Y + (b.Y - a.Y) * fraction, a.Z + (b.Z - a.Z) * fraction);
    }

    public readonly struct NumericalHorizonState
    {
        public NumericalVector3 PositionMeters { get; }
        public double MassSolar { get; }
        public double IrreducibleMassSolar { get; }
        // Radius of an equal-area sphere, not a coordinate horizon surface or an optical shadow.
        public double ArealRadiusMeters { get; }
        public NumericalVector3 Spin { get; }
        internal NumericalHorizonState(NumericalVector3 position, double mass, double irreducibleMass,
            double arealRadius, NumericalVector3 spin)
        { PositionMeters = position; MassSolar = mass; IrreducibleMassSolar = irreducibleMass;
            ArealRadiusMeters = arealRadius; Spin = spin; }
    }

    public readonly struct NumericalWaveformSample
    {
        public double NativeRetardedTime { get; }
        public double RealH22 { get; }
        public double ImaginaryH22 { get; }
        public double Amplitude => Math.Sqrt(RealH22 * RealH22 + ImaginaryH22 * ImaginaryH22);
        internal NumericalWaveformSample(double time, double real, double imaginary)
        { NativeRetardedTime = time; RealH22 = real; ImaginaryH22 = imaginary; }
    }

    public readonly struct NumericalRadiationState
    {
        public double NativeRetardedTime { get; }
        public double SourceRetardedTimeSeconds { get; }
        // Both epochs are independently set to zero for comparison. This is not a propagation delay.
        public double ReferenceAlignedSeconds { get; }
        public double RealH22 { get; }
        public double ImaginaryH22 { get; }
        public double Amplitude => Math.Sqrt(RealH22 * RealH22 + ImaginaryH22 * ImaginaryH22);
        internal NumericalRadiationState(double time, double seconds, double aligned, double real, double imaginary)
        { NativeRetardedTime = time; SourceRetardedTimeSeconds = seconds; ReferenceAlignedSeconds = aligned;
            RealH22 = real; ImaginaryH22 = imaginary; }
    }

    public readonly struct NumericalEventState
    {
        public double ElapsedSeconds { get; }
        public double NativeCoordinateTime { get; }
        public double SourceCoordinateTimeSeconds { get; }
        public double SourceReferenceAlignedSeconds { get; }
        public bool HasCommonHorizon { get; }
        public NumericalHorizonState? BodyA { get; }
        public NumericalHorizonState? BodyB { get; }
        public NumericalHorizonState? Remnant { get; }
        public NumericalRadiationState? Radiation { get; }
        public double? SeparationMeters
        {
            get
            {
                if (!BodyA.HasValue || !BodyB.HasValue) return null;
                NumericalVector3 a = BodyA.Value.PositionMeters, b = BodyB.Value.PositionMeters;
                return new NumericalVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z).Magnitude;
            }
        }
        internal NumericalEventState(double elapsed, double nativeTime, double sourceTime, double aligned,
            bool common, NumericalHorizonState? a, NumericalHorizonState? b,
            NumericalHorizonState? remnant, NumericalRadiationState? radiation)
        { ElapsedSeconds = elapsed; NativeCoordinateTime = nativeTime; SourceCoordinateTimeSeconds = sourceTime;
            SourceReferenceAlignedSeconds = aligned; HasCommonHorizon = common;
            BodyA = a; BodyB = b; Remnant = remnant; Radiation = radiation; }
    }
}
