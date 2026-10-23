using System;
using System.IO;
using System.Text;

namespace BlackHole.Staging.Simulation
{
    // SXS:BBH:0305 Lev6, raw horizon histories and CoM-corrected N2 (2,2) radiation mode.
    // This fixed representative solution is scaled to 64.5 solar masses; it is not an event posterior sample.
    public sealed class NumericalEventHistory
    {
        public const string ModelId = "SXS-BBH-0305-Lev6-N2-v1";
        private const int MaximumRows = 100000;
        private readonly HorizonRow[] a, b, common;
        private readonly NumericalWaveformSample[] waveform;
        public double RawMassSum { get; }
        public double TotalMassSolar { get; }
        public double SolarMassGravitationalParameter { get; }
        public double SpeedOfLight { get; }
        public double StartNativeTime { get; }
        public double EndNativeTime => common[common.Length - 1].Time;
        public double LengthMetersPerRawUnit { get; }
        public double SourceSecondsPerRawTime { get; }
        public double WaveformSecondsPerNormalizedTime { get; }
        public double DurationSeconds => (EndNativeTime - StartNativeTime) * SourceSecondsPerRawTime;
        public double CommonHorizonNativeTime => common[0].Time;
        public double CommonHorizonElapsedSeconds => (CommonHorizonNativeTime - StartNativeTime) * SourceSecondsPerRawTime;
        public double WaveformPeakNativeTime { get; }
        public double WaveformPeakAmplitude { get; }
        public double UsefulWaveformStartNativeTime { get; }
        public double UsefulWaveformEndNativeTime => waveform[waveform.Length - 1].NativeRetardedTime;
        public int WaveformCount => waveform.Length;
        public int HorizonACount => a.Length;
        public int HorizonBCount => b.Length;
        public int CommonHorizonCount => common.Length;

        private NumericalEventHistory(double rawMass, double totalMass, double gmSolar, double c,
            double relaxation, HorizonRow[] a, HorizonRow[] b, HorizonRow[] common, NumericalWaveformSample[] waveform)
        {
            RawMassSum = rawMass; TotalMassSolar = totalMass; SolarMassGravitationalParameter = gmSolar;
            SpeedOfLight = c; StartNativeTime = relaxation;
            this.a = a; this.b = b; this.common = common; this.waveform = waveform;
            LengthMetersPerRawUnit = gmSolar * totalMass / (c * c) / rawMass;
            WaveformSecondsPerNormalizedTime = gmSolar * totalMass / (c * c * c);
            SourceSecondsPerRawTime = WaveformSecondsPerNormalizedTime / rawMass;
            UsefulWaveformStartNativeTime = Math.Max(waveform[0].NativeRetardedTime, relaxation / rawMass);
            int peak = 0;
            for (int i = 1; i < waveform.Length; i++)
                if (waveform[i].Amplitude > waveform[peak].Amplitude) peak = i;
            WaveformPeakNativeTime = waveform[peak].NativeRetardedTime;
            WaveformPeakAmplitude = waveform[peak].Amplitude;
            if (a[0].Time > relaxation || b[0].Time > relaxation || common[0].Time <= relaxation
                || a[a.Length - 1].Time < common[0].Time || b[b.Length - 1].Time < common[0].Time
                || UsefulWaveformStartNativeTime >= UsefulWaveformEndNativeTime
                || WaveformPeakNativeTime < UsefulWaveformStartNativeTime || WaveformPeakAmplitude <= 0)
                throw new InvalidDataException("SXS histories do not cover the declared replay and reference epochs.");
        }

        public static NumericalEventHistory Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("A readable stream is required.", nameof(stream));
            try
            {
                using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "DTETSXS2" || reader.ReadUInt32() != 1)
                        throw new InvalidDataException("Unsupported numerical event data format.");
                    double rawMass = reader.ReadDouble(), totalMass = reader.ReadDouble(), gm = reader.ReadDouble();
                    double c = reader.ReadDouble(), relaxation = reader.ReadDouble();
                    RequireConstant(rawMass, 1.000016001308, "relaxed mass normalization");
                    RequireConstant(totalMass, 64.5, "source mass scale");
                    RequireConstant(gm, 1.3271244e20, "nominal solar GM");
                    RequireConstant(c, 299792458, "speed of light");
                    RequireConstant(relaxation, 540, "relaxation epoch");
                    HorizonRow[] a = ReadHorizon(reader), b = ReadHorizon(reader), common = ReadHorizon(reader);
                    int count = ReadCount(reader, 3);
                    var waveform = new NumericalWaveformSample[count];
                    for (int i = 0; i < count; i++)
                    {
                        double time = ReadFinite(reader), real = ReadFinite(reader), imaginary = ReadFinite(reader);
                        if ((i > 0 && time <= waveform[i - 1].NativeRetardedTime)
                            || Math.Abs(real) > 100 || Math.Abs(imaginary) > 100)
                            throw new InvalidDataException("Invalid waveform row or time ordering.");
                        waveform[i] = new NumericalWaveformSample(time, real, imaginary);
                    }
                    if (stream.ReadByte() != -1) throw new InvalidDataException("Unexpected trailing numerical event data.");
                    return new NumericalEventHistory(rawMass, totalMass, gm, c, relaxation, a, b, common, waveform);
                }
            }
            catch (EndOfStreamException error)
            { throw new InvalidDataException("Truncated numerical event data.", error); }
        }

        public NumericalWaveformSample GetWaveformSample(int index) => waveform[index];

        public NumericalEventState Evaluate(double elapsedSeconds)
        {
            if (!IsFinite(elapsedSeconds)) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            double elapsed = Math.Max(0, Math.Min(DurationSeconds, elapsedSeconds));
            bool hasCommon = elapsed >= CommonHorizonElapsedSeconds;
            double time = elapsed == CommonHorizonElapsedSeconds ? CommonHorizonNativeTime
                : elapsed == DurationSeconds ? EndNativeTime : StartNativeTime + elapsed / SourceSecondsPerRawTime;
            double aligned = elapsed - CommonHorizonElapsedSeconds;
            // Explicit epoch alignment only. Raw horizon t and asymptotic retarded u are distinct coordinates/frames.
            double u = WaveformPeakNativeTime + aligned / WaveformSecondsPerNormalizedTime;
            NumericalRadiationState? radiation = null;
            if (u >= UsefulWaveformStartNativeTime && u <= UsefulWaveformEndNativeTime)
            {
                int lower = FindWaveformLower(u);
                NumericalWaveformSample left = waveform[lower], right = waveform[lower + 1];
                double fraction = (u - left.NativeRetardedTime) / (right.NativeRetardedTime - left.NativeRetardedTime);
                radiation = new NumericalRadiationState(u, u * WaveformSecondsPerNormalizedTime, aligned,
                    left.RealH22 + (right.RealH22 - left.RealH22) * fraction,
                    left.ImaginaryH22 + (right.ImaginaryH22 - left.ImaginaryH22) * fraction);
            }
            return new NumericalEventState(elapsed, time, time * SourceSecondsPerRawTime, aligned, hasCommon,
                hasCommon ? (NumericalHorizonState?)null : Interpolate(a, time),
                hasCommon ? (NumericalHorizonState?)null : Interpolate(b, time),
                hasCommon ? Interpolate(common, time) : (NumericalHorizonState?)null, radiation);
        }

        private NumericalHorizonState Interpolate(HorizonRow[] rows, double time)
        {
            if (time < rows[0].Time || time > rows[rows.Length - 1].Time)
                throw new InvalidOperationException("A horizon cannot be extrapolated beyond its data coverage.");
            int low = 0, high = rows.Length - 1;
            while (high - low > 1) { int mid = (low + high) / 2; if (rows[mid].Time <= time) low = mid; else high = mid; }
            HorizonRow left = rows[low], right = rows[high];
            double f = (time - left.Time) / (right.Time - left.Time);
            double mass = left.Mass + (right.Mass - left.Mass) * f;
            double mirr = left.IrreducibleMass + (right.IrreducibleMass - left.IrreducibleMass) * f;
            return new NumericalHorizonState(NumericalVector3.Lerp(left.Position, right.Position, f).Scale(LengthMetersPerRawUnit),
                mass * TotalMassSolar / RawMassSum, mirr * TotalMassSolar / RawMassSum,
                2 * mirr * LengthMetersPerRawUnit,
                NumericalVector3.Lerp(left.DimensionfulSpin, right.DimensionfulSpin, f).Scale(1 / (mass * mass)));
        }

        private int FindWaveformLower(double time)
        {
            int low = 0, high = waveform.Length - 1;
            while (high - low > 1) { int mid = (low + high) / 2;
                if (waveform[mid].NativeRetardedTime <= time) low = mid; else high = mid; }
            return low;
        }
        private static HorizonRow[] ReadHorizon(BinaryReader reader)
        {
            int count = ReadCount(reader, 9);
            var rows = new HorizonRow[count];
            for (int i = 0; i < count; i++)
            {
                double time = ReadFinite(reader);
                var position = new NumericalVector3(ReadFinite(reader), ReadFinite(reader), ReadFinite(reader));
                double mass = ReadFinite(reader), mirr = ReadFinite(reader);
                var spin = new NumericalVector3(ReadFinite(reader), ReadFinite(reader), ReadFinite(reader));
                if ((i > 0 && time <= rows[i - 1].Time) || Math.Abs(time) > 1e8 || position.Magnitude > 1e8
                    || mass < 1e-6 || mass > 10 || mirr < 1e-6 || mirr > mass * 1.000001
                    || spin.Magnitude / (mass * mass) > 1.0001)
                    throw new InvalidDataException("Invalid horizon row or time ordering.");
                rows[i] = new HorizonRow(time, position, mass, mirr, spin);
            }
            return rows;
        }
        private static int ReadCount(BinaryReader reader, int doublesPerRow)
        {
            uint count = reader.ReadUInt32();
            if (count < 2 || count > MaximumRows) throw new InvalidDataException("Invalid numerical event row count.");
            Stream stream = reader.BaseStream;
            if (stream.CanSeek && stream.Length - stream.Position < (long)count * doublesPerRow * sizeof(double))
                throw new InvalidDataException("Truncated numerical event rows.");
            return (int)count;
        }
        private static double ReadFinite(BinaryReader reader)
        {
            double value = reader.ReadDouble();
            if (!IsFinite(value)) throw new InvalidDataException("Nonfinite numerical event value.");
            return value;
        }
        private static void RequireConstant(double value, double expected, string name)
        {
            if (!IsFinite(value) || Math.Abs(value - expected) > Math.Abs(expected) * 1e-13)
                throw new InvalidDataException("Unexpected " + name + ".");
        }
        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private readonly struct HorizonRow
        {
            public readonly double Time, Mass, IrreducibleMass;
            public readonly NumericalVector3 Position, DimensionfulSpin;
            public HorizonRow(double time, NumericalVector3 position, double mass, double irreducibleMass, NumericalVector3 spin)
            { Time = time; Position = position; Mass = mass; IrreducibleMass = irreducibleMass; DimensionfulSpin = spin; }
        }
    }
}
