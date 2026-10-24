#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BlackHole.Staging.Simulation
{
    // Signed passive-material contrast; density and velocity remain in the separate fluid cache.
    public sealed class MiniDiskMaterialCache : IDisposable
    {
        internal const string Magic = "DTETMT01";
        internal const int IndexBytes = 20;
        private readonly FileStream stream;
        private readonly double[] times;
        private readonly long[] offsets;
        private readonly int[] lengths;
        private readonly byte[] raw, compressed;
        private bool disposed;
        public int GridSize { get; }
        public int FrameCount => times.Length;
        public double HalfExtentRg { get; }
        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public string FluidSha256 { get; }
        public string ModelId { get; }
        public long ResidentPayloadBytes => raw.LongLength + compressed.LongLength + 20L * FrameCount;
        public long FileBytes => stream.Length;
        public static MiniDiskMaterialCache Open(string path) => new MiniDiskMaterialCache(path);
        public MiniDiskMaterialCache(string path)
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                using (var r = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (Encoding.ASCII.GetString(r.ReadBytes(8)) != Magic || r.ReadInt32() != 1)
                        throw new InvalidDataException("Unsupported material cache.");
                    GridSize = r.ReadInt32(); int frames = r.ReadInt32();
                    if (GridSize < 32 || GridSize > 256 || frames < 2 || frames > 4097) throw new InvalidDataException("Invalid material grid.");
                    HalfExtentRg = r.ReadDouble(); StartSeconds = r.ReadDouble(); EndSeconds = r.ReadDouble();
                    FluidSha256 = Encoding.ASCII.GetString(r.ReadBytes(64));
                    int length = r.ReadInt32();
                    if (length < 1 || length > 128) throw new InvalidDataException("Invalid material model identifier.");
                    ModelId = Encoding.UTF8.GetString(r.ReadBytes(length));
                    if (ModelId != MiniDiskMaterialAdvection.ModelId || !NumericalEventHistory.IsFinite(HalfExtentRg)
                        || HalfExtentRg <= 0 || !NumericalEventHistory.IsFinite(StartSeconds) || StartSeconds < 0
                        || !NumericalEventHistory.IsFinite(EndSeconds) || EndSeconds <= StartSeconds || FluidSha256.Length != 64)
                        throw new InvalidDataException("Invalid material metadata.");
                    times = new double[frames]; offsets = new long[frames]; lengths = new int[frames];
                    long last = stream.Position + (long)IndexBytes * frames; int max = 0;
                    for (int i = 0; i < frames; i++)
                    {
                        times[i] = r.ReadDouble(); offsets[i] = r.ReadInt64(); lengths[i] = r.ReadInt32();
                        if (!NumericalEventHistory.IsFinite(times[i]) || times[i] < StartSeconds || times[i] > EndSeconds
                            || (i > 0 && times[i] <= times[i - 1]) || offsets[i] != last || lengths[i] < 18
                            || lengths[i] > 2 * GridSize * GridSize + 65536 || offsets[i] > stream.Length - lengths[i])
                            throw new InvalidDataException("Invalid material frame index.");
                        last += lengths[i]; max = Math.Max(max, lengths[i]);
                    }
                    if (times[0] != StartSeconds || times[frames - 1] != EndSeconds || last != stream.Length)
                        throw new InvalidDataException("Incomplete material timeline.");
                    raw = new byte[2 * GridSize * GridSize]; compressed = new byte[max];
                }
            }
            catch { stream.Dispose(); throw; }
        }
        public bool Matches(MiniDiskCache fluid, string fluidSha256)
        {
            ThrowIfDisposed();
            if (fluid == null || !string.Equals(FluidSha256, fluidSha256, StringComparison.OrdinalIgnoreCase)
                || GridSize != fluid.GridSize || FrameCount != fluid.FrameCount || HalfExtentRg != fluid.HalfExtentRg
                || StartSeconds != fluid.StartSeconds || EndSeconds != fluid.EndSeconds) return false;
            for (int i = 0; i < FrameCount; i++) if (times[i] != fluid.GetFrameTime(i)) return false;
            return true;
        }
        public bool MatchesFile(MiniDiskCache fluid, string fluidPath) => Matches(fluid, ComputeFileSha256(fluidPath));
        public static string ComputeFileSha256(string path)
        {
            using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(input); var result = new StringBuilder(64);
                foreach (byte b in hash) result.Append(b.ToString("x2")); return result.ToString();
            }
        }
        public double GetFrameTime(int index) { CheckIndex(index); return times[index]; }
        public void ReadFrame(int index, float[] concentration)
        {
            CheckIndex(index); if (concentration == null || concentration.Length < GridSize * GridSize)
                throw new ArgumentException("Destination must hold one concentration grid.");
            stream.Position = offsets[index]; ReadExactly(stream, compressed, lengths[index]);
            using (var packed = new MemoryStream(compressed, 0, lengths[index], false))
            using (var gzip = new GZipStream(packed, CompressionMode.Decompress))
            { ReadExactly(gzip, raw, raw.Length); if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected material payload."); }
            for (int i = 0; i < GridSize * GridSize; i++)
            {
                float value = MiniDiskHalf.Decode(raw, 2 * i);
                if (!NumericalEventHistory.IsFinite(value) || Math.Abs(value) > 1) throw new InvalidDataException("Invalid material concentration.");
                concentration[i] = value;
            }
        }
        private void CheckIndex(int index)
        { ThrowIfDisposed(); if (index < 0 || index >= FrameCount) throw new ArgumentOutOfRangeException(nameof(index)); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(MiniDiskMaterialCache)); }
        internal static void ReadExactly(Stream input, byte[] buffer, int length)
        { int offset = 0; while (offset < length) { int n = input.Read(buffer, offset, length - offset);
                if (n == 0) throw new EndOfStreamException("Truncated material frame."); offset += n; } }
        public void Dispose() { if (disposed) return; disposed = true; stream.Dispose(); }
    }

    internal sealed class MiniDiskMaterialWriter : IDisposable
    {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly long indexStart;
        private readonly byte[] raw;
        private readonly MemoryStream packed;
        private readonly MiniDiskCache fluid;
        private int written;
        public long WorkingArrayBytes => raw.LongLength + packed.Capacity;
        internal MiniDiskMaterialWriter(string path, MiniDiskCache fluid, string hash)
        {
            this.fluid = fluid; raw = new byte[2 * fluid.GridSize * fluid.GridSize]; packed = new MemoryStream(raw.Length + 65536);
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Encoding.ASCII.GetBytes(MiniDiskMaterialCache.Magic)); writer.Write(1);
            writer.Write(fluid.GridSize); writer.Write(fluid.FrameCount); writer.Write(fluid.HalfExtentRg);
            writer.Write(fluid.StartSeconds); writer.Write(fluid.EndSeconds); writer.Write(Encoding.ASCII.GetBytes(hash));
            MiniDiskCache.WriteText(writer, MiniDiskMaterialAdvection.ModelId);
            indexStart = stream.Position; stream.SetLength(indexStart + (long)fluid.FrameCount * MiniDiskMaterialCache.IndexBytes);
        }
        internal void WriteFrame(int index, float[] concentration)
        {
            if (index != written) throw new InvalidOperationException("Material frames must be sequential.");
            for (int i = 0; i < concentration.Length; i++) MiniDiskHalf.Encode(concentration[i], raw, 2 * i);
            packed.Position = 0; packed.SetLength(0);
            using (var gzip = new GZipStream(packed, CompressionLevel.Optimal, true)) gzip.Write(raw, 0, raw.Length);
            long offset = stream.Length; stream.Position = offset; stream.Write(packed.GetBuffer(), 0, (int)packed.Length);
            stream.Position = indexStart + (long)index * MiniDiskMaterialCache.IndexBytes;
            writer.Write(fluid.GetFrameTime(index)); writer.Write(offset); writer.Write((int)packed.Length); written++;
        }
        internal void Complete()
        { if (written != fluid.FrameCount) throw new InvalidOperationException("Incomplete material history."); writer.Flush(); stream.Flush(); }
        public void Dispose() { writer.Dispose(); stream.Dispose(); packed.Dispose(); }
    }
}
