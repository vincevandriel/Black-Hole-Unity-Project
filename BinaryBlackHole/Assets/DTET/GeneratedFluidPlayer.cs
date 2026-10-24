using System;
using System.Threading;
using System.Threading.Tasks;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DTET.VisualPrototype
{
    /// <summary>Owns a cancellable CPU solve and two GPU playback frames; never solves on Unity's main thread.</summary>
    public sealed class GeneratedFluidPlayer : MonoBehaviour
    {
        private sealed class Work
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public readonly InteractiveBinaryScenario Scenario;
            public readonly long Generation;
            public Task<GeneratedFluidHistory> Task;
            private long progressBits;
            public Work(InteractiveBinaryScenario scenario, long generation) { Scenario = scenario; Generation = generation; }
            public void Report(double value) => Interlocked.Exchange(ref progressBits, BitConverter.DoubleToInt64Bits(value));
            public double Progress => BitConverter.Int64BitsToDouble(Interlocked.Read(ref progressBits));
        }

        private Work work;
        private InteractiveBinaryScenario sourceScenario;
        private long sourceGeneration;
        private int raySteps, lowerFrame = -1, upperFrame = -1;
        private Texture2D frameA, frameB;
        private Color[] pixels;
        private float[] sigma, velocityX, velocityZ;
        private GameObject volume;
        private MeshRenderer volumeRenderer;
        private float blend;
        private TextureFormat textureFormat;
        private bool disposed;
        public GeneratedFluidHistory History { get; private set; }
        public Material Material { get; private set; }
        public bool IsGenerating => work != null;
        public bool Ready => History != null && Material != null;
        public bool Selected { get; private set; }
        public bool IsCovered { get; private set; }
        public bool RendererVisible => volumeRenderer != null && volumeRenderer.enabled;
        public long Generation => sourceGeneration;
        public int TextureUploads { get; private set; }
        public int LowerFrame => lowerFrame;
        public int UpperFrame => upperFrame;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal Texture2D LowerFrameTextureForQa => frameA;
#endif
        public double FrameBlend => blend;
        public double GenerationProgress => work == null ? (Ready ? 1 : 0) : work.Progress;
        public string Status { get; private set; } = "No generated history. SOLVE GAS computes this scenario.";
        public long PlaybackPayloadBytes => History == null ? 0 : History.MemoryBytes
            + (long)History.GridSize * History.GridSize * (28 + (textureFormat == TextureFormat.RGBAHalf ? 32 : 64));
        public const float GrayOpacity = .16f;
        public const float DisplayExposure = 2.5f;

        public void Initialize(int steps) { raySteps = steps; }

        public void StartGeneration(InteractiveBinaryScenario scenario, long generation)
        {
            if (disposed) return;
            Reset("Preparing a new fluid solve.");
            var shader = Resources.Load<Shader>("DTET/GeneratedFluidVolume");
            if (shader == null || !shader.isSupported || !TrySelectFilteredFloatFormat(out textureFormat))
            { Status = "Generated fluid unavailable: filtered floating-point texture or shader support."; return; }
            sourceScenario = scenario; sourceGeneration = generation;
            var request = new Work(scenario, generation);
            work = request;
            Status = "Solving 2D isothermal fluid in the background; playback is paused.";
            request.Task = Task.Run(() => GeneratedFluidHistory.Generate(scenario, request.Cancellation.Token, request.Report));
        }

        /// <summary>Publish only on Unity's main thread and only into the identical scenario generation.</summary>
        public bool Poll(InteractiveBinaryScenario scenario, long generation)
        {
            if (sourceScenario != null && (!ReferenceEquals(sourceScenario, scenario) || sourceGeneration != generation))
            { Reset("Scenario changed: previous fluid history was discarded."); return false; }
            var request = work;
            if (request == null || !request.Task.IsCompleted) return false;
            work = null;
            try
            {
                if (request.Task.IsCanceled || request.Cancellation.IsCancellationRequested)
                { Status = "Fluid solve cancelled. No incomplete history is displayed."; return false; }
                if (request.Task.IsFaulted)
                {
                    string message = request.Task.Exception.GetBaseException().Message;
                    Status = "Fluid solve failed: " + (message.Length > 180 ? message.Substring(0, 180) : message);
                    return false;
                }
                if (disposed || !ReferenceEquals(request.Scenario, scenario) || request.Generation != generation) return false;
                History = request.Task.Result;
                BuildPlaybackResources();
                Selected = true;
                Status = "Generated history ready. Use the time and camera controls to explore.";
                return true;
            }
            catch (Exception error)
            {
                ReleasePlayback();
                Status = "Fluid playback unavailable: " + error.Message;
                return false;
            }
            finally { request.Cancellation.Dispose(); }
        }

        public void CancelGeneration()
        {
            AbandonWork();
            Status = "Fluid solve cancelled. No incomplete history is displayed.";
        }

        private void AbandonWork()
        {
            var request = work; work = null;
            if (request == null) return;
            request.Cancellation.Cancel();
            // A cancelled worker may finish after this component is destroyed. Observe its result
            // without capturing Unity objects, and dispose the CTS only after the worker is done.
            request.Task.ContinueWith(completed =>
            {
                if (completed.IsFaulted) { var observed = completed.Exception; }
                request.Cancellation.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Select(bool value)
        {
            Selected = value && Ready;
            if (!Selected && volumeRenderer != null) volumeRenderer.enabled = false;
        }

        public void Present(InteractiveBinarySnapshot snapshot, bool gasVisible)
        {
            if (!Ready) return;
            IsCovered = snapshot.Generation == sourceGeneration
                && History.TryGetFrames(snapshot.ElapsedSeconds, out int lower, out int upper, out double mix);
            if (IsCovered)
            {
                // Upload only on a frame boundary. Scrubbing in either direction uses the same immutable cache.
                History.TryGetFrames(snapshot.ElapsedSeconds, out lower, out upper, out mix);
                if (lowerFrame != lower) { Upload(frameA, lower); lowerFrame = lower; }
                if (upperFrame != upper) { Upload(frameB, upper); upperFrame = upper; }
                blend = (float)mix;
            }
            BindTo(Material);
            Material.SetVector("_HorizonA", Horizon(snapshot.BodyA));
            Material.SetVector("_HorizonB", Horizon(snapshot.BodyB));
            Material.SetVector("_HorizonR", Horizon(snapshot.Remnant));
            volumeRenderer.enabled = Selected && gasVisible && IsCovered;
        }

        public void BindTo(Material target)
        {
            if (target == null) return;
            target.SetTexture("_GeneratedFrameA", frameA);
            target.SetTexture("_GeneratedFrameB", frameB);
            target.SetVector("_GeneratedGrid", new Vector4(History == null ? 0 : (float)History.HalfExtentRg,
                (float)GeneratedFluidHistory.HeightRg, blend, Ready && IsCovered ? 1 : 0));
            target.SetFloat("_GeneratedOpacity", GrayOpacity);
            target.SetFloat("_GeneratedExposure", DisplayExposure);
        }

        private void BuildPlaybackResources()
        {
            int count = checked(History.GridSize * History.GridSize);
            pixels = new Color[count]; sigma = new float[count]; velocityX = new float[count]; velocityZ = new float[count];
            frameA = FrameTexture("Generated fluid lower frame"); frameB = FrameTexture("Generated fluid upper frame");
            Material = new Material(Resources.Load<Shader>("DTET/GeneratedFluidVolume")) { name = "Generated isothermal fluid / assumed emission" };
            Material.SetFloat("_Steps", raySteps);
            float half = (float)History.HalfExtentRg, height = 6 * (float)GeneratedFluidHistory.HeightRg;
            Material.SetVector("_HalfExtent", new Vector4(half, height, half, 0));
            volume = GameObject.CreatePrimitive(PrimitiveType.Cube); volume.name = "Generated 2D fluid with normalized Gaussian lift";
            volume.transform.SetParent(transform, false); volume.transform.localScale = new Vector3(2 * half, 2 * height, 2 * half);
            Destroy(volume.GetComponent<Collider>());
            volumeRenderer = volume.GetComponent<MeshRenderer>(); volumeRenderer.sharedMaterial = Material; volumeRenderer.enabled = false;
        }

        private Texture2D FrameTexture(string label) => new Texture2D(History.GridSize, History.GridSize, textureFormat, false, true)
        { name = label, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        internal static bool TrySelectFilteredFloatFormat(out TextureFormat format)
        {
            bool forceHalf = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Exercise the real fallback in QA without pretending unsupported hardware can use it.
            forceHalf = Environment.GetEnvironmentVariable("DTET_FORCE_HALF_TEXTURES_QA") == "1";
#endif
            // A GPU may support float textures without supporting their linear filtering.
            if (!forceHalf && SupportsFilteredFormat(TextureFormat.RGBAFloat, GraphicsFormat.R32G32B32A32_SFloat))
            { format = TextureFormat.RGBAFloat; return true; }
            if (SupportsFilteredFormat(TextureFormat.RGBAHalf, GraphicsFormat.R16G16B16A16_SFloat))
            { format = TextureFormat.RGBAHalf; return true; }
            format = default; return false;
        }

        private static bool SupportsFilteredFormat(TextureFormat texture, GraphicsFormat graphics) =>
            SystemInfo.SupportsTextureFormat(texture)
            && SystemInfo.IsFormatSupported(graphics, GraphicsFormatUsage.Sample)
            && SystemInfo.IsFormatSupported(graphics, GraphicsFormatUsage.Linear);

        private void Upload(Texture2D texture, int index)
        {
            History.CopyFrame(index, sigma, velocityX, velocityZ);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(sigma[i], velocityX[i], velocityZ[i], 0);
            texture.SetPixels(pixels); texture.Apply(false, false); TextureUploads++;
        }

        private static Vector4 Horizon(InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return Vector4.zero;
            var b = body.Value;
            return new Vector4((float)b.PositionRg.X, (float)b.PositionRg.Y, (float)b.PositionRg.Z, (float)b.HorizonRadiusRg);
        }

        public void Reset(string message = "No generated history. SOLVE GAS computes this scenario.")
        {
            AbandonWork(); ReleasePlayback(); sourceScenario = null; sourceGeneration = 0; Status = message;
        }

        private void ReleasePlayback()
        {
            Selected = IsCovered = false;
            if (volume != null) { volume.SetActive(false); Destroy(volume); } volume = null; volumeRenderer = null;
            if (Material != null) Destroy(Material); Material = null;
            if (frameA != null) Destroy(frameA); if (frameB != null) Destroy(frameB); frameA = frameB = null;
            History = null; pixels = null; sigma = velocityX = velocityZ = null;
            lowerFrame = upperFrame = -1; blend = 0; TextureUploads = 0;
        }

        public void Release() { disposed = true; Reset(); }
        private void OnDestroy() { Release(); }
    }
}
