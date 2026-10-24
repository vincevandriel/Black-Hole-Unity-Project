using System;
using BlackHole.Staging.Simulation;
using UnityEngine;

namespace DTET.VisualPrototype
{
    // Shared lens and retarded-wave uniforms for background stellar light.
    public sealed class InteractiveOptics : MonoBehaviour
    {
        private Material material;
        private Texture2D waveTexture;
        private readonly Color[] wavePixels = new Color[InteractiveOpticsReference.WaveSamples];
        private RenderTexture source;
        private Camera view;
        private InteractiveBinaryScenario model;
        private InteractiveBinarySnapshot snapshot;
        private GeneratedFluidPlayer generatedFluid;
        private bool active, gasVisible = true;
        private float lensGain, waveGain;
        private double sampledTime = double.NaN;
        private long sampledGeneration = long.MinValue;
        private double radialExtent;
        private bool hasActiveStatus, statusHasWaveTexture;
        private float statusLensGain, statusWaveGain;
        private int statusCoveredSamples;
        public RenderTexture Output => source;
        public bool Active => active;
        public bool Supported => material != null && material.shader.isSupported;
        public bool GasVisible { get => gasVisible; set => gasVisible = value; }
        public string Status { get; private set; } = "Optical approximation is off.";
        public int CoveredWaveSamples { get; private set; }
        public int WaveUploadCount { get; private set; }
        public ulong WaveFingerprint { get; private set; }
        public long ScenarioGeneration { get; private set; }
        public long ContextVersion { get; private set; }
        public double SnapshotTime { get; private set; }
        public double WaveRadiusRg => radialExtent;
        public double DisplayWaveRadiusRg => radialExtent / InteractiveOpticsReference.WaveRadialCompression;
        public Material WarpMaterial => material;
        public long AllocatedBytes => (waveTexture == null ? 0 : 2L * InteractiveOpticsReference.WaveSamples * (waveTexture.format == TextureFormat.RGBAHalf ? 8 : 16))
            + InteractiveOpticsReference.WaveSamples * 16;

        public RenderTexture Apply(RenderTexture input, Camera camera, InteractiveBinaryScenario scenario,
            InteractiveBinarySnapshot state, bool enabled, float requestedLensGain, float requestedWaveGain, bool gasIsVisible = true,
            GeneratedFluidPlayer generatedField = null)
        {
            Prepare(input, camera, scenario, state, enabled, requestedLensGain, requestedWaveGain, gasIsVisible, generatedField);
            RenderNow();
            return Output;
        }

        // Bind before drawing background stars, including explicit QA frames.
        public void Prepare(RenderTexture input, Camera camera, InteractiveBinaryScenario scenario,
            InteractiveBinarySnapshot state, bool enabled, float requestedLensGain, float requestedWaveGain, bool gasIsVisible = true,
            GeneratedFluidPlayer generatedField = null)
        {
            if (input == null || camera == null || scenario == null) throw new ArgumentNullException(nameof(input));
            if (!float.IsFinite(requestedLensGain) || !float.IsFinite(requestedWaveGain)) throw new ArgumentOutOfRangeException(nameof(requestedLensGain));
            source = input; view = camera; model = scenario; snapshot = state; gasVisible = gasIsVisible;
            generatedFluid = generatedField;
            ScenarioGeneration = state.Generation; SnapshotTime = state.ElapsedSeconds;
            ContextVersion = state.ContextVersion;
            lensGain = Mathf.Clamp(requestedLensGain, 0, 2); waveGain = Mathf.Clamp(requestedWaveGain, 0, 2);
            active = enabled && (lensGain > 0 || waveGain > 0);
            if (!active) { hasActiveStatus = false; Status = "Stellar lensing off: original background sources."; return; }
            if (!EnsureResources()) { active = false; hasActiveStatus = false; return; }
            BindSnapshot();
            BindCamera();
        }

        // Return the completed astronomical target, including hidden QA frames.
        public RenderTexture RenderNow()
        {
            // Source shaders apply optics; no intermediate target is needed.
            return source;
        }

        private bool EnsureResources()
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("DTET/InteractiveOptics");
                if (shader == null || !shader.isSupported) { Status = "Optical approximation unavailable: shader support."; return false; }
                material = new Material(shader) { name = "Bounded binary lens and retarded-wave approximation" };
            }
            if (waveTexture == null && GeneratedFluidPlayer.TrySelectFilteredFloatFormat(out var waveFormat))
            {
                waveTexture = new Texture2D(InteractiveOpticsReference.WaveSamples, 1, waveFormat, false, true)
                    { name = "Retarded signed waveform / coverage", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                material.SetTexture("_WaveHistory", waveTexture); sampledTime = double.NaN;
            }
            return true;
        }

        private void BindSnapshot()
        {
            ReducedBinaryFlowBinding.Bind(material, snapshot.GasState);
            material.SetVector("_FlowBounds", new Vector4((float)model.HalfExtentXZ, (float)model.HalfExtentY, 0, 0));
            material.SetFloat("_GeneratedGas", generatedFluid == null ? 0 : 1);
            if (generatedFluid != null)
            {
                generatedFluid.BindTo(material);
                if (generatedFluid.History != null)
                    material.SetVector("_FlowBounds", new Vector4((float)generatedFluid.History.HalfExtentRg, 16, 0, 0));
            }
            material.SetVector("_HorizonA", Horizon(snapshot.BodyA));
            material.SetVector("_HorizonB", Horizon(snapshot.BodyB));
            material.SetVector("_HorizonR", Horizon(snapshot.Remnant));
            radialExtent = InteractiveOpticsReference.WaveHistoryRadius(2 * model.HalfExtentXZ);
            if (waveTexture != null && (sampledTime != snapshot.ElapsedSeconds || sampledGeneration != snapshot.Generation))
            {
                ulong hash = 14695981039346656037UL;
                CoveredWaveSamples = 0;
                // Half stores display-normalized strain; physical values and fingerprints stay unchanged.
                float storageScale = waveTexture.format == TextureFormat.RGBAHalf
                    && snapshot.Signal.Kind == InteractiveSignalKind.QuadrupoleStrain
                    ? (float)(1 / InteractiveBinaryScenario.ReferenceStrainAmplitude) : 1;
                for (int i = 0; i < wavePixels.Length; i++)
                {
                    double radius = radialExtent * i / (wavePixels.Length - 1);
                    var signal = model.EvaluateRetardedSignal(snapshot.ElapsedSeconds, radius);
                    float plus = 0, cross = 0;
                    if (signal.IsAvailable)
                    {
                        plus = (float)(signal.HPlus ?? signal.NormalizedRealH22 ?? 0);
                        cross = (float)(signal.HCross ?? signal.NormalizedImaginaryH22 ?? 0);
                        CoveredWaveSamples++;
                    }
                    wavePixels[i] = new Color(plus * storageScale, cross * storageScale, signal.IsAvailable ? 1 : 0, 0);
                    hash = Mix(hash, plus); hash = Mix(hash, cross); hash = Mix(hash, signal.IsAvailable ? 1 : 0);
                }
                waveTexture.SetPixels(wavePixels); waveTexture.Apply(false, false);
                WaveFingerprint = hash; WaveUploadCount++; sampledTime = snapshot.ElapsedSeconds; sampledGeneration = snapshot.Generation;
            }
            // Keep absolute strain separate from its fixed, declared display normalization.
            float scale = WaveDisplayScale;
            float effectiveWaveGain = waveTexture == null ? 0 : waveGain;
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            material.SetFloat("_OpticsFixture", 0);
            #endif
            material.SetVector("_WaveParameters", new Vector4((float)radialExtent, scale, gasVisible ? 1 : 0,
                (float)InteractiveOpticsReference.WaveRadialCompression));
            bool hasWaveTexture = waveTexture != null;
            if (!hasActiveStatus || statusLensGain != lensGain || statusWaveGain != effectiveWaveGain
                || statusCoveredSamples != CoveredWaveSamples || statusHasWaveTexture != hasWaveTexture)
            {
                Status = $"Background-star lensing {lensGain:0.00}; stellar wave shift {effectiveWaveGain:0.00}, distance compressed 12x; {CoveredWaveSamples}/{wavePixels.Length} covered radii."
                    + (hasWaveTexture ? " No binary metric or ray geodesics." : " Wave texture unavailable; wave distortion off.");
                hasActiveStatus = true; statusLensGain = lensGain; statusWaveGain = effectiveWaveGain;
                statusCoveredSamples = CoveredWaveSamples; statusHasWaveTexture = hasWaveTexture;
            }
        }

        private void BindCamera()
        {
            var pose = view.transform; Vector3 origin = pose.position, right = pose.right, up = pose.up, forward = pose.forward;
            material.SetVector("_CameraOrigin", origin); material.SetVector("_CameraRight", right);
            material.SetVector("_CameraUp", up); material.SetVector("_CameraForward", forward);
            Vector4 lensA = Lens(snapshot.BodyA), lensB = Lens(snapshot.BodyB), lensR = Lens(snapshot.Remnant);
            Vector4 radii = new Vector4((float)(snapshot.BodyA?.HorizonRadiusRg ?? 0), (float)(snapshot.BodyB?.HorizonRadiusRg ?? 0), (float)(snapshot.Remnant?.HorizonRadiusRg ?? 0), 0);
            if (!snapshot.Remnant.HasValue && snapshot.BodyA.HasValue)
            {
                // Blend presentation lenses only; retain the scenario's bodies, gas and signal.
                double blend = snapshot.Mode == InteractiveScenarioMode.AdjustableExperiment ? snapshot.MergerProgress
                    : Math.Max(0, Math.Min(1, 1 - (model.MergerTimeSeconds - snapshot.ElapsedSeconds) / (50 * snapshot.GravitationalTimeSeconds)));
                if (blend > 0)
                {
                    blend = blend * blend * (3 - 2 * blend);
                    var finalBody = model.Evaluate(model.MergerTimeSeconds).Remnant;
                    lensR = Lens(finalBody); lensR.w *= (float)blend;
                    radii.z = (float)(finalBody?.HorizonRadiusRg ?? 0);
                    lensA.w *= (float)(1 - blend); lensB.w *= (float)(1 - blend);
                }
            }
            material.SetVector("_LensA", lensA); material.SetVector("_LensB", lensB); material.SetVector("_LensR", lensR);
            material.SetVector("_LensRadii", radii);
            float tangent = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
            material.SetVector("_Optics", new Vector4(lensGain, waveTexture == null ? 0 : waveGain, tangent * source.width / source.height, tangent));
            material.SetVector("_WaveParameters", new Vector4((float)radialExtent,
                WaveDisplayScale,
                gasVisible ? 1 : 0, (float)InteractiveOpticsReference.WaveRadialCompression));
            Vector3 direction = origin.sqrMagnitude > 1e-8 ? origin.normalized : -forward;
            Vector3 e1 = Vector3.Cross(Vector3.up, direction);
            if (e1.sqrMagnitude < 1e-8) e1 = Vector3.right;
            e1.Normalize(); Vector3 e2 = Vector3.Cross(direction, e1).normalized;
            material.SetVector("_Polarization", new Vector4(Vector3.Dot(e1, right), Vector3.Dot(e1, up), Vector3.Dot(e2, right), Vector3.Dot(e2, up)));
            material.SetVector("_WaveBasisX", new Vector4(e1.x, e1.y, e1.z, origin.magnitude));
            material.SetVector("_WaveBasisY", new Vector4(e2.x, e2.y, e2.z, 0));
        }

        private float WaveDisplayScale => snapshot.Signal.Kind == InteractiveSignalKind.QuadrupoleStrain
            && (waveTexture == null || waveTexture.format != TextureFormat.RGBAHalf)
            ? (float)(1 / InteractiveBinaryScenario.ReferenceStrainAmplitude) : 1;

        public static void CopyStellarUniforms(Material destination, Material sourceMaterial, bool enabled)
        {
            if (destination == null) return;
            if (sourceMaterial == null || !enabled) { destination.SetVector("_Optics", Vector4.zero); return; }
            foreach (string name in StellarVectors) destination.SetVector(name, sourceMaterial.GetVector(name));
            var wave = sourceMaterial.GetTexture("_WaveHistory");
            destination.SetTexture("_WaveHistory", wave);
            destination.SetVector("_WaveHistory_TexelSize", wave == null ? Vector4.zero : new Vector4(1f / wave.width, 1f / wave.height, wave.width, wave.height));
        }
        private static readonly string[] StellarVectors = { "_CameraOrigin", "_CameraRight", "_CameraUp", "_CameraForward",
            "_LensA", "_LensB", "_LensR", "_LensRadii", "_Optics", "_WaveParameters", "_WaveBasisX", "_WaveBasisY", "_Polarization" };

        private Vector4 Lens(InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return Vector4.zero;
            Vector3 position = V(body.Value.PositionRg), delta = position - view.transform.position;
            float depth = Vector3.Dot(delta, view.transform.forward);
            if (depth <= .01f) return Vector4.zero;
            return new Vector4(Vector3.Dot(delta, view.transform.right) / depth, Vector3.Dot(delta, view.transform.up) / depth,
                depth, (float)(body.Value.MassSolar / snapshot.TotalMassSolar));
        }
        private static Vector4 Horizon(InteractiveBinaryBody? body)
        { if (!body.HasValue) return Vector4.zero; var p = body.Value.PositionRg; return new Vector4((float)p.X, (float)p.Y, (float)p.Z, (float)body.Value.HorizonRadiusRg); }
        private static Vector3 V(NumericalVector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        private static ulong Mix(ulong h, float v) => unchecked((h ^ (uint)BitConverter.SingleToInt32Bits(v)) * 1099511628211UL);
        public void Release()
        {
            active = false; source = null; view = null; model = null; generatedFluid = null;
            if (waveTexture != null) Destroy(waveTexture); waveTexture = null;
            if (material != null) Destroy(material); material = null;
            sampledTime = double.NaN; sampledGeneration = long.MinValue; CoveredWaveSamples = 0;
            WaveFingerprint = 0; hasActiveStatus = false; Status = "Optical approximation released.";
        }
        private void OnDestroy() { Release(); }
    }
}
