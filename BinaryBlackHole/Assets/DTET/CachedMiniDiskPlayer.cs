using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    // Streams an evolved surface-density history; optical/vertical closures are display assumptions.
    public sealed class CachedMiniDiskPlayer : MonoBehaviour
    {
        private Texture2D frameA, frameB;
        private TextureFormat textureFormat;
        private float[] sigma, velocityX, velocityZ;
        private Color[] pixels;
        private Mesh mesh;
        private GameObject surface;
        private MeshRenderer surfaceRenderer;
        private Camera view;
        private InteractiveBinaryScenario scenario;
        private Vector4 endpointA, endpointB, endpointR, opticalWeights;
        private int lowerFrame = -1, upperFrame = -1, rayBudget = 256;
        private long generation;
        private double initialSeparationRg;
        private bool curvedLight = true;
        private float dopplerStrength = 1;
        public const float AssumedHeightRg = .3f;
        public MiniDiskCache Cache { get; private set; }
        public Material Material { get; private set; }
        public bool Ready => Cache != null && Material != null && surfaceRenderer != null;
        public bool IsCovered { get; private set; }
        public bool RendererVisible => surfaceRenderer != null && surfaceRenderer.enabled && surface.activeInHierarchy;
        public MeshRenderer SurfaceRenderer => surfaceRenderer;
        public long Generation => generation;
        public int LowerFrame => lowerFrame;
        public int UpperFrame => upperFrame;
        public double FrameBlend { get; private set; }
        public int TextureUploads { get; private set; }
        public int RayBudget => rayBudget;
        public long PlaybackPayloadBytes => Cache == null ? 0 : Cache.ResidentPayloadBytes
            + (long)Cache.GridSize * Cache.GridSize * (28 + (textureFormat == TextureFormat.RGBAHalf ? 16 : 32));
        public string Status { get; private set; } = "Evolved mini-disk cache has not been loaded.";
        public bool CurvedLight
        {
            get => curvedLight;
            set { curvedLight = value; opticalWeights.z = value ? 1 : 0; if (Material != null) Material.SetVector("_DiskWeights", opticalWeights); }
        }
        public float DopplerStrength
        {
            get => dopplerStrength;
            set { dopplerStrength = float.IsFinite(value) ? Mathf.Clamp01(value) : 0; if (Material != null) Material.SetFloat("_DopplerStrength", dopplerStrength); }
        }

        public void Initialize(Camera camera, int baseRaySteps)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Release(); view = camera;
            rayBudget = baseRaySteps <= 64 ? 192 : baseRaySteps <= 96 ? 256 : 384;
        }

        public void BindScenario(InteractiveBinaryScenario model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            scenario = model;
            initialSeparationRg = model.Evaluate(0).SeparationRg;
            double before = Math.Max(0, model.MergerTimeSeconds - Math.Max(1e-10, model.GravitationalTimeSeconds * 1e-7));
            var binary = model.Evaluate(before); var remnant = model.Evaluate(model.MergerTimeSeconds);
            endpointA = Lens(binary.BodyA); endpointB = Lens(binary.BodyB); endpointR = Lens(remnant.Remnant);
        }

        public bool Load(string path, long sourceGeneration)
        {
            ReleasePlayback();
            if (view == null) { Status = "Initialize the mini-disk player with the inspection camera first."; return false; }
            try
            {
                var shader = Resources.Load<Shader>("DTET/CurvedDiskVolume");
                if (shader == null || !shader.isSupported || !GeneratedFluidPlayer.TrySelectFilteredFloatFormat(out textureFormat))
                { Status = "Curved gas unavailable: shader or filtered floating-point texture support."; return false; }
                Cache = MiniDiskCache.Open(path); generation = sourceGeneration;
                if (scenario != null && !Matches(scenario.Evaluate(0)))
                    throw new InvalidOperationException("The cached scenario identity does not match the selected binary.");
                int count = checked(Cache.GridSize * Cache.GridSize);
                sigma = new float[count]; velocityX = new float[count]; velocityZ = new float[count]; pixels = new Color[count];
                frameA = CreateFrame("Evolved mini-disk lower frame"); frameB = CreateFrame("Evolved mini-disk upper frame");
                Material = new Material(shader) { name = "Evolved mini-disks / approximate curved light" };
                Material.SetFloat("_GeneratedOpacity", 8); Material.SetFloat("_GeneratedExposure", 1.6f);
                Material.SetFloat("_DopplerStrength", dopplerStrength);
                Material.SetVector("_DiskDomain", new Vector4((float)Cache.HalfExtentRg, AssumedHeightRg,
                    Mathf.Max(128, 6 * (float)Cache.HalfExtentRg), rayBudget));
                mesh = new Mesh { name = "Curved evolved gas screen surface" };
                mesh.vertices = new[] { new Vector3(-1,-1,0), new Vector3(-1,3,0), new Vector3(3,-1,0) };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000); mesh.UploadMeshData(true);
                surface = new GameObject("Evolved mini-disks and curved capture"); surface.transform.SetParent(transform, false);
                surface.layer = 0; surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                surfaceRenderer = surface.AddComponent<MeshRenderer>(); surfaceRenderer.sharedMaterial = Material;
                surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off; surfaceRenderer.receiveShadows = false;
                surfaceRenderer.lightProbeUsage = LightProbeUsage.Off; surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                surfaceRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; surfaceRenderer.enabled = false;
                Status = "Evolved Newtonian mini-disks; assumed thin vertical profile and approximate curved light.";
                return true;
            }
            catch (Exception error)
            {
                ReleasePlayback();
                Status = "Mini-disk cache unavailable: " + error.Message;
                return false;
            }
        }

        public void Present(InteractiveBinarySnapshot snapshot, bool gasVisible)
        {
            if (!Ready) return;
            IsCovered = snapshot.Generation == generation && Matches(snapshot)
                && Cache.TryGetFrames(snapshot.ElapsedSeconds, out int lower, out int upper, out double mix);
            if (IsCovered)
            {
                Cache.TryGetFrames(snapshot.ElapsedSeconds, out lower, out upper, out mix);
                try
                {
                    if (upperFrame == lower && lowerFrame != lower)
                    { var texture = frameA; frameA = frameB; frameB = texture; int index = lowerFrame; lowerFrame = upperFrame; upperFrame = index; }
                    if (lowerFrame != lower) { Upload(frameA, lower); lowerFrame = lower; }
                    if (upperFrame != upper) { Upload(frameB, upper); upperFrame = upper; }
                    FrameBlend = mix;
                }
                catch (Exception error)
                {
                    ReleasePlayback(); Status = "Mini-disk frame unavailable: " + error.Message;
                    return;
                }
            }
            BindTo(Material);
            Material.SetVector("_HorizonA", Horizon(snapshot.BodyA)); Material.SetVector("_HorizonB", Horizon(snapshot.BodyB));
            Material.SetVector("_HorizonR", Horizon(snapshot.Remnant));
            Vector4 a = Lens(snapshot.BodyA), b = Lens(snapshot.BodyB), remnant = Lens(snapshot.Remnant);
            float remnantWeight = snapshot.Remnant.HasValue ? 1 : 0;
            if (scenario != null)
            {
                double start = scenario.MergerTimeSeconds - MergerSurfaceView.LeadTg * scenario.GravitationalTimeSeconds;
                double end = scenario.MergerTimeSeconds + MergerSurfaceView.SettleTg * scenario.GravitationalTimeSeconds;
                if (snapshot.ElapsedSeconds >= start && snapshot.ElapsedSeconds <= end)
                {
                    if (a.w <= 0) a = endpointA; if (b.w <= 0) b = endpointB;
                    if (remnant.w <= 0) remnant = endpointR;
                    double u = (snapshot.ElapsedSeconds - start) / (end - start);
                    remnantWeight = (float)(u * u * u * (u * (u * 6 - 15) + 10));
                }
            }
            Material.SetVector("_DiskLensA", a); Material.SetVector("_DiskLensB", b); Material.SetVector("_DiskLensR", remnant);
            opticalWeights = new Vector4(1 - remnantWeight, remnantWeight, curvedLight ? 1 : 0, 1);
            Material.SetVector("_DiskWeights", opticalWeights);
            Material.SetFloat("_DopplerStrength", dopplerStrength);
            surfaceRenderer.enabled = IsCovered && gasVisible && isActiveAndEnabled;
        }

        public void BindTo(Material target)
        {
            if (target == null) return;
            target.SetTexture("_GeneratedFrameA", frameA); target.SetTexture("_GeneratedFrameB", frameB);
            target.SetVector("_GeneratedGrid", new Vector4(Cache == null ? 0 : (float)Cache.HalfExtentRg,
                AssumedHeightRg, (float)FrameBlend, Ready && IsCovered ? 1 : 0));
        }

        private bool Matches(InteractiveBinarySnapshot s) => Cache.SourceModelId == s.ScenarioId && Cache.ScenarioMode == s.Mode
            && Math.Abs(Cache.GravitationalRadiusMeters - s.GravitationalRadiusMeters)
            <= 1e-7 * Math.Max(1, s.GravitationalRadiusMeters)
            && Math.Abs(Cache.Mass1Solar - s.Mass1Solar) <= 1e-6 && Math.Abs(Cache.Mass2Solar - s.Mass2Solar) <= 1e-6
            && (scenario == null || (Math.Abs(Cache.MergerTimeSeconds - scenario.MergerTimeSeconds) <= 1e-8 * Math.Max(1, scenario.DurationSeconds)
                && Math.Abs(Cache.InitialSeparationRg - initialSeparationRg) <= 1e-7 * Math.Max(1, initialSeparationRg)));
        private Texture2D CreateFrame(string label) => new Texture2D(Cache.GridSize, Cache.GridSize, textureFormat, false, true)
        { name = label, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        private void Upload(Texture2D texture, int frame)
        {
            Cache.ReadFrame(frame, sigma, velocityX, velocityZ);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(sigma[i], velocityX[i], velocityZ[i], 0);
            texture.SetPixels(pixels); texture.Apply(false, false); TextureUploads++;
        }
        private static Vector4 Lens(InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return Vector4.zero; var b = body.Value;
            return new Vector4((float)b.PositionRg.X, (float)b.PositionRg.Y, (float)b.PositionRg.Z, (float)b.MassFraction);
        }
        private static Vector4 Horizon(InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return Vector4.zero; var b = body.Value;
            return new Vector4((float)b.PositionRg.X, (float)b.PositionRg.Y, (float)b.PositionRg.Z, (float)b.HorizonRadiusRg);
        }
        private void OnDisable() { if (surfaceRenderer != null) surfaceRenderer.enabled = false; }
        private void OnDestroy() => Release();
        public void Release()
        {
            ReleasePlayback(); view = null; scenario = null; endpointA = endpointB = endpointR = Vector4.zero;
        }
        private void ReleasePlayback()
        {
            if (surface != null) { surface.SetActive(false); Destroy(surface); }
            if (mesh != null) Destroy(mesh); if (Material != null) Destroy(Material);
            if (frameA != null) Destroy(frameA); if (frameB != null) Destroy(frameB);
            Cache?.Dispose(); Cache = null; Material = null; frameA = frameB = null; surface = null; surfaceRenderer = null; mesh = null;
            sigma = velocityX = velocityZ = null; pixels = null; lowerFrame = upperFrame = -1;
            IsCovered = false; FrameBlend = 0; TextureUploads = 0;
        }
    }
}
