#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BlackHole.Staging.Simulation
{
    // Independent gzip frames permit random seeks without retaining the full history.
    public sealed class MiniDiskCache : IDisposable
    {
        internal const string Magic = "DTETMD01";
        internal const int IndexBytes = 100;
        private readonly FileStream stream;
        private readonly double[] times;
        private readonly long[] offsets;
        private readonly int[] lengths;
        private readonly GeneratedFluidDiagnostics[] diagnostics;
        private readonly byte[] raw, compressed;
        private readonly long indexStart;
        private bool disposed;
        public int GridSize { get; }
        public int FrameCount => times.Length;
        public double HalfExtentRg { get; }
        public double CellWidthRg => 2 * HalfExtentRg / GridSize;
        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public double GravitationalRadiusMeters { get; }
        public double GravitationalTimeSeconds { get; }
        public double TotalMassSolar { get; }
        public double Mass1Solar { get; }
        public double Mass2Solar { get; }
        public double MergerTimeSeconds { get; }
        public double InitialSeparationRg { get; }
        public InteractiveScenarioMode ScenarioMode { get; }
        public double VacuumVelocityCutoff { get; }
        public string ModelId { get; }
        public string SourceModelId { get; }
        public string SourceSha256 { get; }
        public long ResidentPayloadBytes => raw.LongLength + compressed.LongLength + FrameCount * (8L + 8 + 4 + 80);
        public long FileBytes => stream.Length;
        public static MiniDiskCache Open(string path) => new MiniDiskCache(path);
        public MiniDiskCache(string path)
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != Magic || reader.ReadInt32() != 1)
                        throw new InvalidDataException("Unsupported mini-disk cache format.");
                    GridSize = reader.ReadInt32(); int frames = reader.ReadInt32();
                    if (GridSize < 32 || GridSize > 256 || frames < 2 || frames > 4097)
                        throw new InvalidDataException("Mini-disk cache dimensions exceed the supported limits.");
                    HalfExtentRg = ReadPositive(reader); StartSeconds = ReadFinite(reader); EndSeconds = ReadFinite(reader);
                    GravitationalRadiusMeters = ReadPositive(reader); GravitationalTimeSeconds = ReadPositive(reader);
                    TotalMassSolar = ReadPositive(reader); Mass1Solar = ReadPositive(reader); Mass2Solar = ReadPositive(reader);
                    MergerTimeSeconds = ReadFinite(reader); VacuumVelocityCutoff = ReadPositive(reader);
                    InitialSeparationRg = ReadPositive(reader); ScenarioMode = (InteractiveScenarioMode)reader.ReadInt32();
                    ModelId = ReadText(reader); SourceModelId = ReadText(reader); SourceSha256 = ReadText(reader);
                    if (ModelId != MiniDiskSimulation.ModelId || StartSeconds < 0 || EndSeconds <= StartSeconds
                        || !Enum.IsDefined(typeof(InteractiveScenarioMode), ScenarioMode))
                        throw new InvalidDataException("Invalid mini-disk model or interval.");
                    times = new double[frames]; offsets = new long[frames]; lengths = new int[frames];
                    diagnostics = new GeneratedFluidDiagnostics[frames];
                    indexStart = stream.Position;
                    long earliest = indexStart + (long)IndexBytes * frames, lastEnd = earliest;
                    int maxCompressed = 0, rawBytes = checked(6 * GridSize * GridSize);
                    for (int i = 0; i < frames; i++)
                    {
                        times[i] = ReadFinite(reader); offsets[i] = reader.ReadInt64(); lengths[i] = reader.ReadInt32();
                        diagnostics[i] = ReadDiagnostics(reader);
                        if ((i > 0 && times[i] <= times[i - 1]) || times[i] < StartSeconds || times[i] > EndSeconds
                            || offsets[i] != lastEnd || lengths[i] < 18 || lengths[i] > rawBytes + 65536
                            || offsets[i] > stream.Length - lengths[i])
                            throw new InvalidDataException("Invalid mini-disk frame index.");
                        lastEnd = offsets[i] + lengths[i]; maxCompressed = Math.Max(maxCompressed, lengths[i]);
                    }
                    if (times[0] != StartSeconds || times[frames - 1] != EndSeconds || lastEnd != stream.Length)
                        throw new InvalidDataException("Incomplete mini-disk cache coverage.");
                    raw = new byte[rawBytes]; compressed = new byte[maxCompressed];
                }
            }
            catch { stream.Dispose(); throw; }
        }
        public double GetFrameTime(int index) { CheckIndex(index); return times[index]; }
        public GeneratedFluidDiagnostics GetDiagnostics(int index) { CheckIndex(index); return diagnostics[index]; }
        public bool TryGetFrames(double seconds, out int lower, out int upper, out double blend)
        {
            ThrowIfDisposed(); lower = upper = 0; blend = 0;
            if (!NumericalEventHistory.IsFinite(seconds) || seconds < StartSeconds || seconds > EndSeconds) return false;
            int index = Array.BinarySearch(times, seconds);
            if (index >= 0) { lower = upper = index; return true; }
            upper = ~index; lower = upper - 1;
            blend = (seconds - times[lower]) / (times[upper] - times[lower]); return true;
        }
        public void ReadFrame(int index, float[] sigma, float[] vx, float[] vz)
        {
            CheckIndex(index); int count = GridSize * GridSize;
            if (sigma == null || vx == null || vz == null || sigma.Length < count || vx.Length < count || vz.Length < count)
                throw new ArgumentException("Three destination arrays must each hold one grid.");
            stream.Position = offsets[index]; ReadExactly(stream, compressed, lengths[index]);
            using (var packed = new MemoryStream(compressed, 0, lengths[index], false))
            using (var gzip = new GZipStream(packed, CompressionMode.Decompress))
            {
                ReadExactly(gzip, raw, raw.Length);
                if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected mini-disk frame payload.");
            }
            for (int i = 0; i < count; i++)
            {
                sigma[i] = MiniDiskHalf.Decode(raw, 2 * i);
                vx[i] = MiniDiskHalf.Decode(raw, 2 * (i + count));
                vz[i] = MiniDiskHalf.Decode(raw, 2 * (i + 2 * count));
                if (!NumericalEventHistory.IsFinite(sigma[i]) || sigma[i] < 0 ||
                    !NumericalEventHistory.IsFinite(vx[i]) || !NumericalEventHistory.IsFinite(vz[i]))
                    throw new InvalidDataException("Nonfinite or negative mini-disk frame data.");
            }
        }
        // Bilinear cell-centre interpolation matches the GPU's clamped texture sampling.
        public GeneratedFluidSample SampleFrame(float[] sigma, float[] vx, float[] vz, double xRg, double zRg)
        {
            ThrowIfDisposed(); int count = GridSize * GridSize;
            if (sigma.Length < count || vx.Length < count || vz.Length < count || !NumericalEventHistory.IsFinite(xRg)
                || !NumericalEventHistory.IsFinite(zRg) || Math.Abs(xRg) > HalfExtentRg || Math.Abs(zRg) > HalfExtentRg)
                throw new ArgumentOutOfRangeException(nameof(xRg));
            double x = Math.Max(0, Math.Min(GridSize - 1, (xRg + HalfExtentRg) / CellWidthRg - .5));
            double z = Math.Max(0, Math.Min(GridSize - 1, (zRg + HalfExtentRg) / CellWidthRg - .5));
            int ix = (int)x, iz = (int)z, jx = Math.Min(ix + 1, GridSize - 1), jz = Math.Min(iz + 1, GridSize - 1);
            double Read(float[] v)
            {
                double a = v[iz * GridSize + ix] * (1 - (x - ix)) + v[iz * GridSize + jx] * (x - ix);
                double b = v[jz * GridSize + ix] * (1 - (x - ix)) + v[jz * GridSize + jx] * (x - ix);
                return a * (1 - (z - iz)) + b * (z - iz);
            }
            return new GeneratedFluidSample(Read(sigma), Read(vx), Read(vz));
        }
        // Offline temporal subsampling copies compressed frames exactly; the PDE state is untouched.
        public void WriteSubsampled(string path, int stride)
        {
            ThrowIfDisposed();
            if (stride < 2 || (FrameCount - 1) % stride != 0) throw new ArgumentOutOfRangeException(nameof(stride));
            string target = Path.GetFullPath(path);
            if (string.Equals(target, stream.Name, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Subsampling requires a separate destination.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string temporary = target + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                int frames = (FrameCount - 1) / stride + 1;
                var header = new byte[(int)indexStart]; stream.Position = 0; ReadExactly(stream, header, header.Length);
                for (int i = 0; i < 4; i++) header[16 + i] = (byte)((uint)frames >> (8 * i));
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                using (var writer = new BinaryWriter(output))
                {
                    output.Write(header, 0, header.Length); output.SetLength(indexStart + (long)frames * IndexBytes);
                    for (int i = 0; i < frames; i++)
                    {
                        int source = i * stride; stream.Position = offsets[source]; ReadExactly(stream, compressed, lengths[source]);
                        long position = output.Length; output.Position = position; output.Write(compressed, 0, lengths[source]);
                        output.Position = indexStart + (long)i * IndexBytes;
                        writer.Write(times[source]); writer.Write(position); writer.Write(lengths[source]); WriteDiagnostics(writer, diagnostics[source]);
                    }
                    writer.Flush(); output.Flush();
                }
                if (File.Exists(target)) File.Delete(target); File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void CheckIndex(int i)
        { ThrowIfDisposed(); if (i < 0 || i >= FrameCount) throw new ArgumentOutOfRangeException(nameof(i)); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(MiniDiskCache)); }
        public void Dispose() { if (disposed) return; disposed = true; stream.Dispose(); }
        internal static void WriteText(BinaryWriter writer, string value)
        { byte[] bytes = Encoding.UTF8.GetBytes(value); if (bytes.Length > 256) throw new ArgumentException("Cache identifier too long."); writer.Write(bytes.Length); writer.Write(bytes); }
        private static string ReadText(BinaryReader reader)
        { int length = reader.ReadInt32(); if (length < 0 || length > 256) throw new InvalidDataException("Invalid cache identifier.");
            byte[] bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException(); return Encoding.UTF8.GetString(bytes); }
        private static double ReadFinite(BinaryReader reader)
        { double value = reader.ReadDouble(); if (!NumericalEventHistory.IsFinite(value)) throw new InvalidDataException("Nonfinite cache metadata."); return value; }
        private static double ReadPositive(BinaryReader reader)
        { double value = ReadFinite(reader); if (value <= 0) throw new InvalidDataException("Nonpositive cache scale."); return value; }
        internal static void WriteDiagnostics(BinaryWriter w, GeneratedFluidDiagnostics d)
        { w.Write(d.InitialMass); w.Write(d.FinalMass); w.Write(d.RemovedSinkMass); w.Write(d.NetOutflowMass); w.Write(d.AddedFloorMass);
            w.Write(d.StepCount); w.Write(d.MaxCourantNumber); w.Write(d.MinimumDensity); w.Write(d.MaximumSpeed); w.Write(d.ReconstructionFallbackCount); }
        private static GeneratedFluidDiagnostics ReadDiagnostics(BinaryReader r)
        {
            double initial = ReadPositive(r), final = ReadPositive(r), sink = ReadFinite(r), flow = ReadFinite(r), floor = ReadFinite(r);
            long steps = r.ReadInt64(); double courant = ReadFinite(r), minimum = ReadFinite(r), speed = ReadFinite(r); long fallbacks = r.ReadInt64();
            if (sink < 0 || floor < 0 || steps < 0 || courant < 0 || minimum < 0 || speed < 0 || fallbacks < 0)
                throw new InvalidDataException("Invalid cache diagnostics.");
            return new GeneratedFluidDiagnostics(initial, final, sink, flow, floor, steps, courant, minimum, speed, fallbacks);
        }
        private static void ReadExactly(Stream input, byte[] buffer, int length)
        { int offset = 0; while (offset < length) { int n = input.Read(buffer, offset, length - offset);
                if (n == 0) throw new EndOfStreamException("Truncated mini-disk frame."); offset += n; } }
    }

    internal sealed class MiniDiskCacheWriter : IDisposable
    {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly long indexOffset;
        private readonly int count, frames;
        private readonly byte[] raw;
        private readonly MemoryStream packed;
        private readonly double cellArea;
        private int written;
        public long WorkingArrayBytes => raw.LongLength + packed.Capacity;
        internal MiniDiskCacheWriter(string path, InteractiveBinaryScenario scenario, InteractiveBinarySnapshot initial,
            MiniDiskSettings settings, double start, double end, double extent, string sourceSha256)
        {
            count = settings.GridSize * settings.GridSize; frames = settings.FrameCount;
            raw = new byte[6 * count]; packed = new MemoryStream(raw.Length + 65536);
            cellArea = Math.Pow(2 * extent / settings.GridSize, 2);
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Encoding.ASCII.GetBytes(MiniDiskCache.Magic)); writer.Write(1); writer.Write(settings.GridSize); writer.Write(frames);
            writer.Write(extent); writer.Write(start); writer.Write(end);
            writer.Write(scenario.GravitationalRadiusMeters); writer.Write(scenario.GravitationalTimeSeconds);
            writer.Write(initial.TotalMassSolar); writer.Write(initial.Mass1Solar); writer.Write(initial.Mass2Solar);
            writer.Write(scenario.MergerTimeSeconds); writer.Write(MiniDiskSimulation.VacuumVelocityCutoff);
            writer.Write(scenario.Evaluate(0).SeparationRg); writer.Write((int)scenario.Mode);
            MiniDiskCache.WriteText(writer, MiniDiskSimulation.ModelId); MiniDiskCache.WriteText(writer, initial.ScenarioId);
            MiniDiskCache.WriteText(writer, sourceSha256);
            indexOffset = stream.Position; stream.SetLength(indexOffset + (long)MiniDiskCache.IndexBytes * frames);
            stream.Position = stream.Length;
        }
        public void WriteFrame(int frame, double seconds, float[] sigma, float[] vx, float[] vz,
            GeneratedFluidDiagnostics diagnostics, MiniDiskGenerationReport report)
        {
            if (frame != written || frame >= frames) throw new InvalidOperationException("Cache frames must be written in order.");
            double encodedMass = 0;
            for (int i = 0; i < count; i++)
            {
                MiniDiskHalf.Encode(sigma[i], raw, 2 * i);
                bool vacuum = sigma[i] <= MiniDiskSimulation.VacuumVelocityCutoff;
                MiniDiskHalf.Encode(vacuum ? 0 : vx[i], raw, 2 * (i + count));
                MiniDiskHalf.Encode(vacuum ? 0 : vz[i], raw, 2 * (i + 2 * count));
                encodedMass += MiniDiskHalf.Decode(raw, 2 * i) * cellArea;
                if (!vacuum)
                {
                    double ex = MiniDiskHalf.Decode(raw, 2 * (i + count)) - vx[i];
                    double ez = MiniDiskHalf.Decode(raw, 2 * (i + 2 * count)) - vz[i];
                    report.MaximumDenseVelocityAbsoluteError = Math.Max(report.MaximumDenseVelocityAbsoluteError, Math.Sqrt(ex * ex + ez * ez));
                }
            }
            report.MaximumEncodedMassRelativeError = Math.Max(report.MaximumEncodedMassRelativeError,
                Math.Abs(encodedMass - diagnostics.FinalMass) / diagnostics.FinalMass);
            packed.Position = 0; packed.SetLength(0);
            using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
            long offset = stream.Length; stream.Position = offset;
            stream.Write(packed.GetBuffer(), 0, (int)packed.Length);
            stream.Position = indexOffset + (long)frame * MiniDiskCache.IndexBytes;
            writer.Write(seconds); writer.Write(offset); writer.Write((int)packed.Length); MiniDiskCache.WriteDiagnostics(writer, diagnostics);
            written++;
        }
        public void Complete() { if (written != frames) throw new InvalidOperationException("Incomplete cache."); writer.Flush(); stream.Flush(); }
        public void Dispose() { writer.Dispose(); stream.Dispose(); packed.Dispose(); }
    }

    internal static class MiniDiskHalf
    {
        internal static void Encode(float value, byte[] bytes, int offset)
        {
            uint bits = (uint)BitConverter.SingleToInt32Bits(value), sign = (bits >> 16) & 0x8000;
            int exp = (int)((bits >> 23) & 255) - 127 + 15; uint mantissa = bits & 0x7fffff, half;
            if (exp >= 31) throw new InvalidDataException("Mini-disk value exceeds finite binary16 range.");
            if (exp <= 0)
            {
                if (exp < -10) half = sign;
                else { mantissa |= 0x800000; int shift = 14 - exp;
                    uint rounded = mantissa + ((1u << (shift - 1)) - 1) + ((mantissa >> shift) & 1);
                    half = sign | (rounded >> shift); }
            }
            else
            {
                uint rounded = mantissa + 0xfff + ((mantissa >> 13) & 1);
                half = sign | ((uint)exp << 10) | (rounded >> 13);
                if (rounded >= 0x800000) half = sign | ((uint)(exp + 1) << 10);
                if ((half & 0x7c00) == 0x7c00) throw new InvalidDataException("Binary16 overflow.");
            }
            bytes[offset] = (byte)half; bytes[offset + 1] = (byte)(half >> 8);
        }
        internal static float Decode(byte[] bytes, int offset)
        {
            uint half = (uint)(bytes[offset] | bytes[offset + 1] << 8), sign = (half & 0x8000) << 16;
            int exp = (int)((half >> 10) & 31); uint mantissa = half & 1023, result;
            if (exp == 0)
            {
                if (mantissa == 0) result = sign;
                else { int e = -14; while ((mantissa & 1024) == 0) { mantissa <<= 1; e--; }
                    result = sign | (uint)(e + 127) << 23 | (mantissa & 1023) << 13; }
            }
            else result = sign | (uint)(exp == 31 ? 255 : exp - 15 + 127) << 23 | mantissa << 13;
            return BitConverter.Int32BitsToSingle((int)result);
        }
    }
}
