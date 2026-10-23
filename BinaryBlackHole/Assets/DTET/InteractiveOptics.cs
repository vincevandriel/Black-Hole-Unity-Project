using System;
using BlackHole.Staging.Simulation;
using UnityEngine;

namespace DTET.VisualPrototype
{
    /// <summary>A bounded astronomical-image approximation, composed before the suit and UI.</summary>
    public sealed class InteractiveOptics : MonoBehaviour
    {
        private Material material;
        private Texture2D waveTexture;
        private readonly Color[] wavePixels = new Color[InteractiveOpticsReference.WaveSamples];
        private RenderTexture source, target;
        private Camera view;
        private InteractiveBinaryScenario model;
        private InteractiveBinarySnapshot snapshot;
        private bool active, gasVisible = true;
        private float lensGain, waveGain;
        private double sampledTime = double.NaN;
        private long sampledGeneration = long.MinValue;
        private double radialExtent;
        private bool hasActiveStatus, statusHasWaveTexture;
        private float statusLensGain, statusWaveGain;
        private int statusCoveredSamples;
        public RenderTexture Output => active && target != null ? target : source;
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
        public Material WarpMaterial => material;
        public long AllocatedBytes => (target == null ? 0 : (long)target.width * target.height * (target.format == RenderTextureFormat.ARGBHalf ? 8 : 4))
            + (waveTexture == null ? 0 : 2L * InteractiveOpticsReference.WaveSamples * 16)
            + InteractiveOpticsReference.WaveSamples * 16;

        public RenderTexture Apply(RenderTexture input, Camera camera, InteractiveBinaryScenario scenario,
            InteractiveBinarySnapshot state, bool enabled, float requestedLensGain, float requestedWaveGain, bool gasIsVisible = true)
        {
            if (input == null || camera == null || scenario == null) throw new ArgumentNullException(nameof(input));
            if (!float.IsFinite(requestedLensGain) || !float.IsFinite(requestedWaveGain)) throw new ArgumentOutOfRangeException(nameof(requestedLensGain));
            source = input; view = camera; model = scenario; snapshot = state; gasVisible = gasIsVisible;
            ScenarioGeneration = state.Generation; SnapshotTime = state.ElapsedSeconds;
            ContextVersion = state.ContextVersion;
            lensGain = Mathf.Clamp(requestedLensGain, 0, 2); waveGain = Mathf.Clamp(requestedWaveGain, 0, 2);
            active = enabled && (lensGain > 0 || waveGain > 0);
            if (!active) { hasActiveStatus = false; Status = "Optical approximation off: original astronomical target."; return source; }
            if (!EnsureResources()) { active = false; hasActiveStatus = false; return source; }
            BindSnapshot();
            RenderNow();
            return Output;
        }

        /// <summary>Call after explicitly rendering the astronomical camera, including hidden QA captures.</summary>
        public RenderTexture RenderNow()
        {
            if (!active || source == null || target == null || material == null || view == null) return source;
            BindCamera();
            var previousTarget = RenderTexture.active;
            try { Graphics.Blit(source, target, material); }
            finally { RenderTexture.active = previousTarget; }
            return target;
        }

        private bool EnsureResources()
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("DTET/InteractiveOptics");
                if (shader == null || !shader.isSupported) { Status = "Optical approximation unavailable: shader support."; return false; }
                material = new Material(shader) { name = "Bounded binary lens and retarded-wave approximation" };
            }
            if (waveTexture == null && SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
            {
                waveTexture = new Texture2D(InteractiveOpticsReference.WaveSamples, 1, TextureFormat.RGBAFloat, false, true)
                    { name = "Retarded signed waveform / coverage", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                material.SetTexture("_WaveHistory", waveTexture); sampledTime = double.NaN;
            }
            var format = source.format == RenderTextureFormat.ARGBHalf ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            if (target == null || target.width != source.width || target.height != source.height || target.format != format)
            {
                if (target != null) { target.Release(); Destroy(target); }
                target = new RenderTexture(source.width, source.height, 0, format, RenderTextureReadWrite.Linear)
                    { name = "Capped astronomical optics", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1 };
                if (!target.Create()) { Destroy(target); target = null; Status = "Optical approximation unavailable: target allocation."; return false; }
            }
            return true;
        }

        private void BindSnapshot()
        {
            ReducedBinaryFlowBinding.Bind(material, snapshot.GasState);
            material.SetVector("_FlowBounds", new Vector4((float)model.HalfExtentXZ, (float)model.HalfExtentY, 0, 0));
            material.SetVector("_HorizonA", Horizon(snapshot.BodyA));
            material.SetVector("_HorizonB", Horizon(snapshot.BodyB));
            material.SetVector("_HorizonR", Horizon(snapshot.Remnant));
            radialExtent = 4 * model.HalfExtentXZ;
            if (waveTexture != null && (sampledTime != snapshot.ElapsedSeconds || sampledGeneration != snapshot.Generation))
            {
                ulong hash = 14695981039346656037UL;
                CoveredWaveSamples = 0;
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
                    wavePixels[i] = new Color(plus, cross, signal.IsAvailable ? 1 : 0, 0);
                    hash = Mix(hash, plus); hash = Mix(hash, cross); hash = Mix(hash, signal.IsAvailable ? 1 : 0);
                }
                waveTexture.SetPixels(wavePixels); waveTexture.Apply(false, false);
                WaveFingerprint = hash; WaveUploadCount++; sampledTime = snapshot.ElapsedSeconds; sampledGeneration = snapshot.Generation;
            }
            bool strain = snapshot.Signal.Kind == InteractiveSignalKind.QuadrupoleStrain;
            // Keep absolute strain separate from its fixed, declared display normalization.
            float scale = strain ? (float)(1 / InteractiveBinaryScenario.ReferenceStrainAmplitude) : 1;
            float effectiveWaveGain = waveTexture == null ? 0 : waveGain;
            material.SetFloat("_OpticsFixture", 0);
            material.SetVector("_WaveParameters", new Vector4((float)radialExtent, scale, gasVisible ? 1 : 0, 0));
            bool hasWaveTexture = waveTexture != null;
            if (!hasActiveStatus || statusLensGain != lensGain || statusWaveGain != effectiveWaveGain
                || statusCoveredSamples != CoveredWaveSamples || statusHasWaveTexture != hasWaveTexture)
            {
                Status = $"Approximate softened lenses {lensGain:0.00}; retarded wave display {effectiveWaveGain:0.00}; {CoveredWaveSamples}/{wavePixels.Length} covered radii."
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
                // A presentation-only bridge of the summed lenses into the common lens. Proxies,
                // gas, orbit and waveform remain the exact scenario snapshot throughout.
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
            float centreDepth = Vector3.Dot(-origin, forward);
            material.SetVector("_SystemAngle", new Vector4(Vector3.Dot(-origin, right) / Math.Max(.01f, centreDepth),
                Vector3.Dot(-origin, up) / Math.Max(.01f, centreDepth), 0, 0));
            bool strain = snapshot.Signal.Kind == InteractiveSignalKind.QuadrupoleStrain;
            material.SetVector("_WaveParameters", new Vector4((float)radialExtent,
                strain ? (float)(1 / InteractiveBinaryScenario.ReferenceStrainAmplitude) : 1,
                gasVisible ? 1 : 0, centreDepth));
            Vector3 direction = origin.sqrMagnitude > 1e-8 ? origin.normalized : -forward;
            Vector3 e1 = Vector3.Cross(Vector3.up, direction);
            if (e1.sqrMagnitude < 1e-8) e1 = Vector3.right;
            e1.Normalize(); Vector3 e2 = Vector3.Cross(direction, e1).normalized;
            material.SetVector("_Polarization", new Vector4(Vector3.Dot(e1, right), Vector3.Dot(e1, up), Vector3.Dot(e2, right), Vector3.Dot(e2, up)));
        }

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
            active = false; source = null; view = null; model = null;
            if (target != null) { target.Release(); Destroy(target); } target = null;
            if (waveTexture != null) Destroy(waveTexture); waveTexture = null;
            if (material != null) Destroy(material); material = null;
            sampledTime = double.NaN; sampledGeneration = long.MinValue; CoveredWaveSamples = 0;
            WaveFingerprint = 0; hasActiveStatus = false; Status = "Optical approximation released.";
        }
        private void OnDestroy() { Release(); }
    }
}
