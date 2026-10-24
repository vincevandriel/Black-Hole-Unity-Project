using System;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>Desktop experiment and recorded replay, with one model and presentation-only optics.</summary>
    public sealed class InteractiveBinaryExhibit : MonoBehaviour, IInteractiveExhibitPresenter, IGeneratedFluidPresenter
    {
        public bool Ready { get; private set; }
        public ExhibitQualitySettings Profile { get; private set; }
        public ExhibitQualityProfile SelectedProfile { get; private set; } = ExhibitQualityProfile.Auto;
        public InteractiveBinarySession Session { get; private set; }
        public InteractiveBinarySnapshot Snapshot => Session.Snapshot;
        public Camera View { get; private set; }
        public Camera CaptureCamera { get; private set; }
        public InteractiveWristDisplay Wrist { get; private set; }
        public InteractiveOptics Optics { get; private set; }
        public FeaturedBackgroundStar FeaturedStar { get; private set; }
        public bool BackgroundStarsVisible { get => sky != null && sky.Visible; set { if (sky != null) sky.Visible = value; if (FeaturedStar != null) FeaturedStar.Visibility = value; } }
        public RenderTexture SceneTarget { get; private set; }
        public Texture PresentedTexture => sceneImage == null ? null : sceneImage.texture;
        public bool EmulatedInputEnabled { get; set; } = true;
        public bool GasVisible { get; private set; }
        public bool SamplesVisible { get; private set; }
        public InteractiveOpticalStyle OpticalStyle { get; private set; } = InteractiveOpticalStyle.Enhanced;
        public float LensGain { get; private set; } = .35f;
        public float WaveGain { get; private set; } = 1.3f;
        public Transform BodyATransform => bodyA;
        public Transform BodyBTransform => bodyB;
        public Transform RemnantTransform => remnant;
        public Material GasMaterial => gasMaterial;
        public string ValidationMessage { get; private set; } = "";
        public double SampleWeight { get; private set; }
        public long PresentedGeneration { get; private set; }
        public int AutomaticOpticalFrames { get; private set; }
        public GeneratedFluidPlayer GeneratedFluidPlayer { get; private set; }
        public bool IsGeneratingFluid => GeneratedFluidPlayer != null && GeneratedFluidPlayer.IsGenerating;
        public bool GeneratedFluidReady => GeneratedFluidPlayer != null && GeneratedFluidPlayer.Ready;
        public bool GeneratedFluidActive => GeneratedFluidPlayer != null && GeneratedFluidPlayer.Selected;
        public GeneratedFluidHistory GeneratedHistory => GeneratedFluidPlayer == null ? null : GeneratedFluidPlayer.History;
        public Material GeneratedFluidMaterial => GeneratedFluidPlayer == null ? null : GeneratedFluidPlayer.Material;
        public string GeneratedFluidStatus => Ready && !Profile.GasEnabled ? "Generated fluid is disabled in compatibility mode; choose a supported rendering profile."
            : GeneratedFluidPlayer == null ? "Enter the observatory to generate fluid." : GeneratedFluidPlayer.Status;

        private GameObject launcher, sceneRoot, hud, sampleObject;
        private Text launchDetail, phaseLabel, statusLabel, transportPlay, bendingToggle;
        private RawImage sceneImage;
        private Transform bodyA, bodyB, remnant, gasBox;
        private Material bodyMaterial, gasMaterial, sampleMaterial, displayMaterial;
        private Mesh sampleMesh;
        private MeshRenderer gasRenderer, sampleRenderer;
        private ReducedExhibitSky sky;
        private Vector3[] samplePositions;
        private Color[] sampleColours;
        private int lastWidth, lastHeight;
        private long lastGeneration = -1;
        private double lastTime = double.NaN, nextLabels;
        private bool disposed, manualRendering;
        private readonly RenderPipeline.StandardRequest sceneRequest = new RenderPipeline.StandardRequest();
        private readonly RenderPipeline.StandardRequest interfaceRequest = new RenderPipeline.StandardRequest();
        private static readonly Color Gold = new Color(1, .72f, .35f);

        private void Awake()
        {
            RenderPipelineManager.endCameraRendering += AfterSceneRender;
            CaptureCamera = Camera.main;
            if (CaptureCamera == null)
            {
                var camera = new GameObject("Suit presentation camera");
                camera.tag = "MainCamera";
                CaptureCamera = camera.AddComponent<Camera>();
            }
            CaptureCamera.cullingMask = 1 << 5;
            CaptureCamera.clearFlags = CameraClearFlags.SolidColor;
            CaptureCamera.backgroundColor = new Color(.008f, .015f, .028f);
            CaptureCamera.nearClipPlane = .01f; CaptureCamera.farClipPlane = 10;
            CaptureCamera.depth = 10; CaptureCamera.allowHDR = false; CaptureCamera.allowMSAA = false;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Suit input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            BuildLauncher();
        }

        private ExhibitCapabilities Capabilities(bool fallback) => new ExhibitCapabilities(
            fallback ? 0 : SystemInfo.graphicsShaderLevel, SystemInfo.graphicsMemorySize,
            SystemInfo.systemMemorySize, SystemInfo.supports3DTextures, SystemInfo.supportsComputeShaders);

        private void BuildLauncher()
        {
            launcher = ReducedExhibitUi.Canvas(transform, CaptureCamera, "Interactive exhibit launcher");
            var p = launcher.transform;
            ReducedExhibitUi.Label(p, "DT / ET     SPACESUIT OBSERVATORY", 18, 100, 85, 1400, 45, ReducedExhibitUi.Accent);
            ReducedExhibitUi.Label(p, "BINARY EXPLORER", 64, 95, 155, 1400, 95, Color.white);
            ReducedExhibitUi.Label(p, "A real event to explore. A laboratory to experiment in.", 26, 100, 275, 1400, 50, ReducedExhibitUi.Muted);
            ReducedExhibitUi.Label(p, "Numerical replay  /  adjustable experiment  /  illustrative gas and light warping", 18, 100, 340, 1400, 50, Gold);
            var choices = new[] { ExhibitQualityProfile.Auto, ExhibitQualityProfile.Laptop, ExhibitQualityProfile.Balanced, ExhibitQualityProfile.High };
            for (int i = 0; i < choices.Length; i++)
            {
                var selected = choices[i];
                ReducedExhibitUi.Button(p, selected.ToString().ToUpperInvariant(), 100 + i * 350, 430, 325, 60, () => SelectProfile(selected));
            }
            launchDetail = ReducedExhibitUi.Label(p, "", 19, 100, 520, 1390, 100, ReducedExhibitUi.Muted);
            ReducedExhibitUi.Button(p, "ENTER OBSERVATORY", 100, 665, 390, 64, () => Launch(SelectedProfile));
            ReducedExhibitUi.Button(p, "COMPATIBILITY", 520, 665, 300, 64, () => Launch(SelectedProfile, true));
            ReducedExhibitUi.Button(p, "EXIT", 850, 665, 160, 64, () => Application.Quit());
            ReducedExhibitUi.Label(p, "Select before loading. The simulation starts paused; V raises your wrist display.\nLower rendering budgets preserve event and experiment parameters. Compatibility disables gas and distortion.", 17, 100, 775, 1390, 75, ReducedExhibitUi.Muted);
            SelectProfile(SelectedProfile);
        }

        public void SelectProfile(ExhibitQualityProfile value)
        {
            SelectedProfile = value;
            var profile = ExhibitQuality.Resolve(value, Capabilities(false));
            launchDetail.text = $"SELECTED {value} → {profile.EffectiveProfile}\nScene up to {profile.MaxRenderWidth} × {profile.MaxRenderHeight} · {profile.RaySteps} base gas samples + inner refinement\n{profile.Reason}";
        }

        public void Launch(ExhibitQualityProfile profile, bool fallback = false)
        {
            try { LaunchInternal(profile, fallback); }
            catch { ReturnToLauncher(); throw; }
        }

        private void LaunchInternal(ExhibitQualityProfile profile, bool fallback)
        {
            if (Ready) ReturnToLauncher();
            var gasShader = Resources.Load<Shader>("DTET/ReducedBinaryVolume");
            Profile = ExhibitQuality.Resolve(profile, Capabilities(fallback || gasShader == null || !gasShader.isSupported));
            SelectedProfile = profile;
            var data = Resources.Load<TextAsset>("Physics/SXS0305");
            if (data == null) throw new InvalidOperationException("The licensed SXS replay asset is missing.");
            try
            {
                using (var stream = new MemoryStream(data.bytes, false))
                    Session = new InteractiveBinarySession(NumericalEventHistory.Load(stream));
            }
            finally { Resources.UnloadAsset(data); }
            Session.SetPlaybackRate(.05);
            sceneRoot = new GameObject("Interactive astronomical scene"); sceneRoot.transform.SetParent(transform);
            var camera = new GameObject("Astronomical inspection camera"); camera.transform.SetParent(sceneRoot.transform);
            View = camera.AddComponent<Camera>(); View.cullingMask = ~(1 << 5);
            View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = new Color(.001f, .002f, .004f);
            View.nearClipPlane = .04f; View.farClipPlane = 20000; View.fieldOfView = 52;
            View.depth = -10; View.allowHDR = true; View.allowMSAA = false;
            ResizeTarget();
            sky = sceneRoot.AddComponent<ReducedExhibitSky>(); sky.Initialize(View, Profile.StarCount);
            bodyMaterial = new Material(Resources.Load<Shader>("DTET/SolidSurface")); bodyMaterial.SetColor("_Color", Color.black);
            bodyA = BuildMarker("Primary horizon proxy");
            bodyB = BuildMarker("Secondary horizon proxy");
            remnant = BuildMarker("Remnant horizon proxy");
            if (Profile.GasEnabled) BuildGas();
            BuildHud();
            Optics = sceneRoot.AddComponent<InteractiveOptics>();
            FeaturedStar = sceneRoot.AddComponent<FeaturedBackgroundStar>(); FeaturedStar.Initialize(View);
            GeneratedFluidPlayer = sceneRoot.AddComponent<GeneratedFluidPlayer>(); GeneratedFluidPlayer.Initialize(Profile.RaySteps);
            Ready = true; GasVisible = Profile.GasEnabled; SamplesVisible = false;
            Wrist = hud.AddComponent<InteractiveWristDisplay>(); Wrist.Initialize(CaptureCamera, this);
            lastGeneration = -1; lastTime = double.NaN;
            launcher.SetActive(false); SetViewPreset(0); Present(); Wrist.SetRaised(false, true);
        }

        private void ResizeTarget()
        {
            Profile.RenderDimensions(Math.Max(320, Screen.width), Math.Max(180, Screen.height), out int w, out int h);
            if (SceneTarget != null) { View.targetTexture = null; SceneTarget.Release(); Destroy(SceneTarget); }
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            SceneTarget = new RenderTexture(w, h, 24, format, RenderTextureReadWrite.Linear) { name = "Bounded interactive scene", filterMode = FilterMode.Bilinear, antiAliasing = 1 };
            if (!SceneTarget.Create()) throw new NotSupportedException("Scene target allocation failed. Select a smaller rendering budget.");
            View.targetTexture = SceneTarget; lastWidth = Screen.width; lastHeight = Screen.height;
            if (sceneImage != null) sceneImage.texture = SceneTarget;
        }

        private Transform BuildMarker(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(sceneRoot.transform);
            Destroy(go.GetComponent<Collider>()); go.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
            return go.transform;
        }

        private void BuildGas()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = "Prescribed emitting gas";
            box.transform.SetParent(sceneRoot.transform); Destroy(box.GetComponent<Collider>()); gasBox = box.transform;
            gasMaterial = new Material(Resources.Load<Shader>("DTET/ReducedBinaryVolume"));
            gasMaterial.SetFloat("_Steps", Profile.RaySteps); gasMaterial.SetFloat("_Exposure", 2.5f);
            gasMaterial.SetFloat("_Opacity", (float)ReducedBinaryFlowModel.GrayExtinctionPerRg);
            gasRenderer = box.GetComponent<MeshRenderer>(); gasRenderer.sharedMaterial = gasMaterial;
        }

        private void BuildHud()
        {
            hud = ReducedExhibitUi.Canvas(transform, CaptureCamera, "Observatory presentation");
            sceneImage = ReducedExhibitUi.Image(hud.transform, "Scene", SceneTarget, 0, 0, 1600, 900);
            displayMaterial = new Material(Resources.Load<Shader>("DTET/ExhibitDisplay")); sceneImage.material = displayMaterial;
            var r = sceneImage.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            ReducedExhibitUi.Panel(hud.transform, "Title strip", 0, 0, 1600, 88, ReducedExhibitUi.Ink);
            ReducedExhibitUi.Label(hud.transform, "DT / ET     BINARY EXPLORER", 26, 28, 12, 1450, 36, Color.white);
            phaseLabel = ReducedExhibitUi.Label(hud.transform, "", 15, 30, 53, 1500, 30, Gold);
            statusLabel = ReducedExhibitUi.Label(hud.transform, "", 14, 25, 866, 1540, 32, ReducedExhibitUi.Muted);
            transportPlay = ReducedExhibitUi.Button(hud.transform, "PLAY INSPIRAL", 36, 812, 185, 36,
                () => SetWristPlaying(!Session.IsPlaying)).GetComponentInChildren<Text>();
            ReducedExhibitUi.Button(hud.transform, "RESTART", 231, 812, 120, 36, RestartPresentation);
            ReducedExhibitUi.Button(hud.transform, "WATCH MERGER", 361, 812, 185, 36, WatchMerger);
            ReducedExhibitUi.Button(hud.transform, "STARLIGHT VIEW", 556, 812, 185, 36, WatchStarlight);
            bendingToggle = ReducedExhibitUi.Button(hud.transform, "BENDING: ON", 751, 812, 170, 36, ToggleStellarBending).GetComponentInChildren<Text>();
        }

        private void RebuildSamples()
        {
            if (sampleObject != null) { sampleObject.SetActive(false); Destroy(sampleObject); }
            if (sampleMesh != null) Destroy(sampleMesh);
            if (sampleMaterial == null) sampleMaterial = new Material(Resources.Load<Shader>("DTET/ReducedDensitySamples"));
            int count = Profile.TracerCount;
            samplePositions = new Vector3[count]; var vertices = new Vector3[count * 4]; var uv = new Vector2[count * 4];
            sampleColours = new Color[count * 4]; var indices = new int[count * 6];
            double x = Session.Model.HalfExtentXZ, y = Session.Model.HalfExtentY;
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3((float)((Radical(i + 1, 2) * 2 - 1) * x), (float)((Radical(i + 1, 3) * 2 - 1) * y), (float)((Radical(i + 1, 5) * 2 - 1) * x));
                samplePositions[i] = p;
                for (int j = 0; j < 4; j++) vertices[4 * i + j] = p;
                uv[4 * i] = new Vector2(-1, -1); uv[4 * i + 1] = new Vector2(-1, 1); uv[4 * i + 2] = new Vector2(1, 1); uv[4 * i + 3] = new Vector2(1, -1);
                int k = 4 * i, t = 6 * i; indices[t] = k; indices[t + 1] = k + 1; indices[t + 2] = k + 2; indices[t + 3] = k; indices[t + 4] = k + 2; indices[t + 5] = k + 3;
            }
            sampleMesh = new Mesh { name = "Interactive weighted spatial samples" }; sampleMesh.MarkDynamic();
            sampleMesh.vertices = vertices; sampleMesh.uv = uv; sampleMesh.colors = sampleColours; sampleMesh.triangles = indices;
            sampleMesh.bounds = new Bounds(Vector3.zero, new Vector3((float)x * 2, (float)y * 2, (float)x * 2));
            sampleObject = new GameObject("Weighted density overlay", typeof(MeshFilter), typeof(MeshRenderer)); sampleObject.transform.SetParent(sceneRoot.transform);
            sampleObject.GetComponent<MeshFilter>().sharedMesh = sampleMesh; sampleRenderer = sampleObject.GetComponent<MeshRenderer>();
            sampleRenderer.sharedMaterial = sampleMaterial; sampleRenderer.enabled = SamplesVisible;
        }

        public void Present()
        {
            if (!Ready) return;
            if (GeneratedFluidPlayer != null && GeneratedFluidPlayer.Poll(Session.Model, Session.Generation))
            {
                Session.Seek(GeneratedHistory.StartSeconds); Session.Pause();
                SamplesVisible = false; if (sampleRenderer != null) sampleRenderer.enabled = false;
                GasVisible = Profile.GasEnabled; lastTime = double.NaN;
                SetViewPreset(0);
            }
            var s = Snapshot;
            if (s.Generation != lastGeneration)
            {
                lastGeneration = s.Generation; lastTime = double.NaN; RebuildSamples();
                if (gasMaterial != null)
                {
                    float x = (float)Session.Model.HalfExtentXZ, y = (float)Session.Model.HalfExtentY;
                    gasBox.localScale = new Vector3(x * 2, y * 2, x * 2);
                    gasMaterial.SetVector("_HalfExtent", new Vector4(x, y, x, 0)); gasMaterial.SetVector("_FlowBounds", new Vector4(x, y, 0, 0));
                }
            }
            if (GeneratedFluidPlayer != null) GeneratedFluidPlayer.Present(s, GasVisible);
            if (gasRenderer != null) gasRenderer.enabled = GasVisible && !GeneratedFluidActive;
            if (sampleRenderer != null) sampleRenderer.enabled = SamplesVisible && !GeneratedFluidActive;
            if (s.ElapsedSeconds == lastTime) return;
            lastTime = s.ElapsedSeconds; PresentedGeneration = s.Generation;
            PresentBody(bodyA, s.BodyA); PresentBody(bodyB, s.BodyB); PresentBody(remnant, s.Remnant);
            MoveCamera(Vector3.zero);
            if (gasMaterial != null)
            {
                ReducedBinaryFlowBinding.Bind(gasMaterial, s.GasState);
                gasMaterial.SetVector("_HorizonA", Horizon(s.BodyA)); gasMaterial.SetVector("_HorizonB", Horizon(s.BodyB)); gasMaterial.SetVector("_HorizonR", Horizon(s.Remnant));
            }
            SampleWeight = 0;
            if (GeneratedFluidActive) return;
            double cell = 8 * Session.Model.HalfExtentXZ * Session.Model.HalfExtentXZ * Session.Model.HalfExtentY / samplePositions.Length;
            for (int i = 0; i < samplePositions.Length; i++)
            {
                var p = samplePositions[i]; var field = Session.Model.SampleGas(s.GasState, p.x, p.y, p.z);
                SampleWeight += field.RelativeDensity * cell;
                var c = new Color(.25f, .95f, 1, (float)Math.Min(.95, field.RelativeDensity * 1.5));
                for (int j = 0; j < 4; j++) sampleColours[4 * i + j] = c;
            }
            sampleMesh.colors = sampleColours;
        }

        private void PresentBody(Transform target, InteractiveBinaryBody? body)
        {
            target.gameObject.SetActive(body.HasValue); if (!body.HasValue) return;
            target.position = V(body.Value.PositionRg); target.localScale = Vector3.one * (float)(2 * body.Value.HorizonRadiusRg);
        }
        private static Vector3 V(NumericalVector3 p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
        private static Vector4 Horizon(InteractiveBinaryBody? h) => h.HasValue ? new Vector4((float)h.Value.PositionRg.X, (float)h.Value.PositionRg.Y, (float)h.Value.PositionRg.Z, (float)h.Value.HorizonRadiusRg) : Vector4.zero;
        private static double Radical(int n, int b) { double f = 1.0 / b, v = 0; while (n > 0) { v += n % b * f; n /= b; f /= b; } return v; }

        public void SetViewPreset(int index)
        {
            if (View == null || Session == null) return;
            float r = (float)(GeneratedFluidActive ? GeneratedHistory.HalfExtentRg : Session.Model.HalfExtentXZ);
            // Frame the binary rather than the much larger reservoir. This is a view command,
            // never an automatic camera track: navigation and paused inspection stay independent.
            float focus = GeneratedFluidActive ? r : Mathf.Max(16, (float)Snapshot.SeparationRg * 1.6f);
            Vector3[] points = { new Vector3(0, focus * .85f, -focus * 1.3f), new Vector3(0, focus * 1.55f, -.01f), new Vector3(0, focus * .14f, -focus * 1.55f), new Vector3(0, -focus * .85f, -focus * 1.3f), new Vector3(0, r * .95f, -r * 1.8f) };
            if (index < 0 || index >= points.Length) throw new ArgumentOutOfRangeException(nameof(index));
            View.transform.position = points[index]; View.transform.LookAt(Vector3.zero); MoveCamera(Vector3.zero);
        }
        public void MoveCamera(Vector3 delta)
        {
            if (!Ready || !float.IsFinite(delta.x) || !float.IsFinite(delta.y) || !float.IsFinite(delta.z)) return;
            var p = Vector3.ClampMagnitude(View.transform.position + delta, 10000);
            var s = Snapshot; KeepOutside(ref p, s.BodyA); KeepOutside(ref p, s.BodyB); KeepOutside(ref p, s.Remnant);
            View.transform.position = p;
        }
        private static void KeepOutside(ref Vector3 p, InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return; var h = body.Value; var d = p - V(h.PositionRg); float radius = (float)h.HorizonRadiusRg * 1.12f;
            if (d.sqrMagnitude < radius * radius) p = V(h.PositionRg) + (d.sqrMagnitude < 1e-8 ? Vector3.back : d.normalized) * radius;
        }

        private void Update()
        {
            if (!Ready) return;
            if (lastWidth != Screen.width || lastHeight != Screen.height) ResizeTarget();
            if (EmulatedInputEnabled) HandleInput();
            if (!Ready) return;
            if (IsGeneratingFluid) Session.Pause();
            Session.Tick(Time.unscaledDeltaTime); Present();
            BindSkyLensing();
            if (Time.unscaledTimeAsDouble >= nextLabels) { RefreshLabels(); nextLabels = Time.unscaledTimeAsDouble + .1; }
        }
        private void AfterSceneRender(ScriptableRenderContext context, Camera camera)
        {
            if (Ready && !manualRendering && camera == View) { RenderOptics(); AutomaticOpticalFrames++; }
        }
        public void RenderOptics()
        {
            if (!Ready) return;
            sceneImage.texture = Optics.Apply(SceneTarget, View, Session.Model, Snapshot,
                Profile.GasEnabled && OpticalStyle != InteractiveOpticalStyle.Off, LensGain, WaveGain, GasVisible,
                GeneratedFluidActive ? GeneratedFluidPlayer : null);
        }
        public void RenderFrame()
        {
            if (!Ready) return;
            Present(); RefreshLabels(); Wrist.RefreshNow(); Canvas.ForceUpdateCanvases();
            BindSkyLensing();
            manualRendering = true;
            try
            {
                sceneRequest.destination = SceneTarget;
                if (!RenderPipeline.SupportsRenderRequest(View, sceneRequest)) throw new NotSupportedException("Astronomical render requests are unavailable.");
                RenderPipeline.SubmitRenderRequest(View, sceneRequest);
                RenderOptics();
                if (CaptureCamera.targetTexture != null)
                {
                    interfaceRequest.destination = CaptureCamera.targetTexture;
                    if (!RenderPipeline.SupportsRenderRequest(CaptureCamera, interfaceRequest)) throw new NotSupportedException("Interface render requests are unavailable.");
                    RenderPipeline.SubmitRenderRequest(CaptureCamera, interfaceRequest);
                }
            }
            finally { manualRendering = false; }
        }
        private void BindSkyLensing()
        {
            bool enabled = Profile.GasEnabled && OpticalStyle != InteractiveOpticalStyle.Off;
            if (Optics != null) Optics.Prepare(SceneTarget, View, Session.Model, Snapshot, enabled, LensGain, WaveGain);
            // A selected style can still have both gains at zero. In that state Prepare
            // intentionally skips binding; never copy its previous active frame's uniforms.
            enabled = enabled && Optics != null && Optics.Active;
            if (sky != null)
            {
                sky.SetLensing(Snapshot, enabled ? LensGain : 0);
                sky.SetStellarWaves(Optics == null ? null : Optics.WarpMaterial, enabled);
            }
            if (FeaturedStar != null) FeaturedStar.Bind(Snapshot, Optics == null ? null : Optics.WarpMaterial, enabled);
        }
        private void HandleInput()
        {
            var k = Keyboard.current; var m = Mouse.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) SetWristPlaying(false);
            if (Wrist != null && Wrist.InputFocused) return;
            if (k != null)
            {
                if (k.spaceKey.wasPressedThisFrame) SetWristPlaying(!Session.IsPlaying);
                if (k.commaKey.wasPressedThisFrame) SetWristRate(-Session.PlaybackRate);
                if (k.rKey.wasPressedThisFrame) ResetWristScenario();
                if (k.mKey.wasPressedThisFrame) { Session.Pause(); Session.Seek(Session.Model.MergerTimeSeconds); Present(); RefreshLabels(); }
                if (k.gKey.wasPressedThisFrame) SetWristGas(!GasVisible);
                if (k.tKey.wasPressedThisFrame) SetWristSamples(!SamplesVisible);
                if (k.digit1Key.wasPressedThisFrame) SetViewPreset(0); if (k.digit2Key.wasPressedThisFrame) SetViewPreset(1);
                if (k.digit3Key.wasPressedThisFrame) SetViewPreset(2); if (k.digit4Key.wasPressedThisFrame) SetViewPreset(3); if (k.digit5Key.wasPressedThisFrame) SetViewPreset(4);
                var movement = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.eKey.isPressed ? 1 : 0) - (k.qKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                if (movement.sqrMagnitude > 0) MoveCamera(View.transform.TransformDirection(movement.normalized) * Time.unscaledDeltaTime * (k.leftShiftKey.isPressed ? 40 : 12));
            }
            if (m != null)
            {
                if (m.rightButton.isPressed) { var d = m.delta.ReadValue(); View.transform.Rotate(Vector3.up, d.x * .12f, Space.World); View.transform.Rotate(Vector3.right, -d.y * .12f, Space.Self); }
                float wheel = m.scroll.ReadValue().y; if (wheel != 0) MoveCamera(View.transform.forward * wheel * .02f);
            }
        }

        private void RefreshLabels()
        {
            if (!Ready) return;
            var s = CaptureWristState();
            phaseLabel.text = $"{s.ScenarioLabel}  ·  {s.StageLabel}  ·  {(GeneratedFluidActive ? "2D ISOTHERMAL FLUID / NEWTONIAN GRAVITY" : "ASSUMED GAS / ILLUSTRATIVE OPTICS")}";
            statusLabel.text = $"V wrist · WASD + Q/E move · right-drag look · 1–5 views · Space pause · {Profile.EffectiveProfile} {SceneTarget.width}×{SceneTarget.height} · {OpticalStyle}";
            transportPlay.text = Session.IsPlaying ? "PAUSE" : "PLAY INSPIRAL";
            bendingToggle.text = Optics != null && Optics.Active ? "BENDING: ON" : "BENDING: OFF";
        }

        public void WatchMerger()
        {
            if (!Ready || IsGeneratingFluid) return;
            SetWristGas(true);
            Session.Seek(Math.Max(0, Session.Model.MergerTimeSeconds - 600 * Snapshot.GravitationalTimeSeconds));
            Session.SetPlaybackRate(.01); Present(); SetViewPreset(0);
            Wrist.SetRaised(false, true); Session.Play(); RefreshLabels();
        }

        public void RestartPresentation()
        {
            if (!Ready || IsGeneratingFluid) return;
            SetWristGas(true);
            ResetWristScenario(); Session.SetPlaybackRate(.05); SetViewPreset(0);
            Wrist.SetRaised(false, true); RefreshLabels();
        }

        public void WatchStarlight()
        {
            if (!Ready || IsGeneratingFluid) return;
            Session.Reset(); Session.SetPlaybackRate(.03);
            SetWristGas(false); SetWristSamples(false); Present(); SetViewPreset(0);
            Wrist.SetRaised(false, true); Session.Play(); RefreshLabels();
        }

        public void ToggleStellarBending()
        {
            if (!Ready) return;
            bool hasGain = LensGain > 0 || WaveGain > 0;
            bool enabled = OpticalStyle != InteractiveOpticalStyle.Off && hasGain;
            SetWristOptics(enabled ? InteractiveOpticalStyle.Off : InteractiveOpticalStyle.Enhanced,
                hasGain ? LensGain : .35f, hasGain ? WaveGain : 1.3f);
        }

        public InteractiveWristState CaptureWristState()
        {
            if (!Ready) return default;
            var s = Snapshot; var signal = s.Signal;
            bool replay = s.Mode.ToString().Contains("Replay");
            double? value = replay ? signal.NormalizedRealH22 : signal.HPlus;
            return new InteractiveWristState
            {
                Generation = s.Generation, ReplayMode = replay, Playing = Session.IsPlaying, GasEnabled = GasVisible, SamplesEnabled = SamplesVisible,
                FrequencyAvailable = signal.FrequencyHertz.HasValue, SignalAvailable = value.HasValue,
                Progress = s.ElapsedSeconds / s.DurationSeconds, SourceTimeSeconds = s.ElapsedSeconds, DurationSeconds = s.DurationSeconds,
                PlaybackRate = Session.PlaybackRate, Mass1Solar = s.Mass1Solar, Mass2Solar = s.Mass2Solar,
                SeparationRg = s.SeparationRg, SeparationKm = s.SeparationMeters / 1000,
                FrequencyHz = signal.FrequencyHertz.GetValueOrDefault(), Signal = value.GetValueOrDefault(),
                ScenarioLabel = replay ? "GW150914-inspired / SXS0305" : "ADJUSTABLE EDUCATIONAL EXPERIMENT",
                StageLabel = PhaseLabel(s.Phase), SignalLabel = replay ? "Normalized numerical Re(H22)" : "Leading-order h+ at D = 10^12 m, i = 60 deg",
                Status = !Profile.GasEnabled ? "Compatibility: gas and optical distortion are disabled."
                    : GeneratedFluidActive ? (GeneratedFluidPlayer.IsCovered ? "2D isothermal fluid / assumed gas / Newtonian gravity" : "Outside generated fluid coverage: no fluid is displayed.")
                    : Optics != null && OpticalStyle != InteractiveOpticalStyle.Off && !Optics.Active ? "Optical effect unavailable or gains zero; original scene shown."
                    : replay ? "Recorded binary; comparison-epoch alignment" : "Quantitative inspiral ends at 10 rg; merger is illustrative",
                OpticalStyle = !Profile.GasEnabled || (Optics != null && !Optics.Active) ? InteractiveOpticalStyle.Off : OpticalStyle,
                LensGain = LensGain, WaveGain = WaveGain,
                CameraDistanceRg = View.transform.position.magnitude, WaveformAxisMaximum = replay ? 1 : 2e-8,
                FrequencyAxisMaximum = replay ? 500 : 250, SignalUnitLabel = replay ? "Re(H22) / peak |H22|" : "strain (local far-field model)", ValidationMessage = ValidationMessage
            };
        }
        private static string PhaseLabel(InteractiveBinaryPhase phase)
        {
            switch (phase)
            {
                case InteractiveBinaryPhase.NumericalInspiral: return "RECORDED INSPIRAL";
                case InteractiveBinaryPhase.NumericalRemnant: return "COMMON HORIZON / REMNANT";
                case InteractiveBinaryPhase.BoundedInspiral: return "BOUNDED PHYSICAL INSPIRAL";
                case InteractiveBinaryPhase.ConceptualMerger: return "ILLUSTRATIVE MERGER";
                default: return "ILLUSTRATIVE REMNANT";
            }
        }
        public double? SampleWristGraph(double fraction, bool frequency)
        {
            if (!Ready) return null;
            var s = Session.Model.Evaluate(Math.Max(0, Math.Min(1, fraction)) * Snapshot.DurationSeconds);
            if (frequency) return s.Signal.FrequencyHertz;
            return s.Mode.ToString().Contains("Replay") ? s.Signal.NormalizedRealH22 : s.Signal.HPlus;
        }
        public double? SampleWristEnvelope(double fraction)
        {
            if (!Ready) return null;
            var signal = Session.Model.EvaluateSignal(Math.Max(0, Math.Min(1, fraction)) * Snapshot.DurationSeconds);
            if (!signal.IsAvailable) return null;
            if (signal.NormalizedRealH22.HasValue && signal.NormalizedImaginaryH22.HasValue)
            {
                double re = signal.NormalizedRealH22.Value, im = signal.NormalizedImaginaryH22.Value;
                return Math.Sqrt(re * re + im * im);
            }
            if (!signal.StrainEnvelope.HasValue) return null;
            double inclination = Math.Cos(signal.InclinationRadians), polarization = 2 * signal.PolarizationRadians;
            double a = .5 * (1 + inclination * inclination) * Math.Cos(polarization);
            double b = inclination * Math.Sin(polarization);
            return signal.StrainEnvelope.Value * Math.Sqrt(a * a + b * b);
        }
        public void SelectWristScenario(bool replay)
        {
            if (!Ready) return;
            if (replay) Session.SelectReplay(); else Session.SelectExperiment(new InteractiveBinaryParameters(35, 30, 30));
            ValidationMessage = ""; SetViewPreset(0); Present(); RefreshLabels();
        }
        public void ApplyWristExperiment(double m1, double m2, double separationRg)
        {
            if (!Ready) return;
            try
            {
                var parameters = new InteractiveBinaryParameters(m1, m2, separationRg);
                double oldRg = Snapshot.GravitationalRadiusMeters;
                Session.SelectExperiment(parameters);
                View.transform.position *= (float)(oldRg / Snapshot.GravitationalRadiusMeters);
                ValidationMessage = "Applied together; paused at the new scenario's start.";
                Present(); RefreshLabels();
            }
            catch (ArgumentException e) { ValidationMessage = e.Message; }
        }
        public void ResetWristScenario() { if (!Ready) return; Session.Reset(); Present(); RefreshLabels(); }
        public void SetWristPlaying(bool playing) { if (!Ready) return; if (playing && !IsGeneratingFluid) Session.Play(); else Session.Pause(); RefreshLabels(); }
        public void SeekWrist(double fraction) { if (!Ready) return; Session.Seek(Math.Max(0, Math.Min(1, fraction)) * Snapshot.DurationSeconds); Present(); RefreshLabels(); }
        public void SetWristRate(double rate) { if (!Ready) return; Session.SetPlaybackRate(rate); RefreshLabels(); }
        public void SetWristOptics(InteractiveOpticalStyle style, float lensGain, float waveGain)
        {
            if (!Enum.IsDefined(typeof(InteractiveOpticalStyle), style) || !float.IsFinite(lensGain) || !float.IsFinite(waveGain)) return;
            OpticalStyle = style; LensGain = Mathf.Clamp(lensGain, 0, 2); WaveGain = Mathf.Clamp(waveGain, 0, 2);
            if (Ready) { RenderOptics(); RefreshLabels(); }
        }
        public void SetWristGas(bool value) { GasVisible = value && Ready && Profile.GasEnabled; if (Ready) Present(); }
        public void SetWristSamples(bool value) { SamplesVisible = value && Ready && !GeneratedFluidActive; if (sampleRenderer != null) sampleRenderer.enabled = SamplesVisible; }
        public void SetWristView(int index) => SetViewPreset(index);
        public void LeaveWristExhibit() => ReturnToLauncher();

        public void StartGeneratedFluid()
        {
            if (!Ready || !Profile.GasEnabled) return;
            Session.Pause(); GeneratedFluidPlayer.StartGeneration(Session.Model, Session.Generation);
            lastTime = double.NaN; Present(); RefreshLabels();
        }
        public void CancelGeneratedFluid()
        { if (GeneratedFluidPlayer == null) return; GeneratedFluidPlayer.CancelGeneration(); if (Ready) RefreshLabels(); }
        public void UseGeneratedFluid(bool value)
        {
            if (!Ready || GeneratedFluidPlayer == null) return;
            GeneratedFluidPlayer.Select(value);
            if (GeneratedFluidActive) { SamplesVisible = false; if (sampleRenderer != null) sampleRenderer.enabled = false; }
            SetViewPreset(0);
            lastTime = double.NaN; Present(); RefreshLabels();
        }
        public void SeekGeneratedFluidStart()
        { if (!Ready || !GeneratedFluidReady) return; Session.Seek(GeneratedHistory.StartSeconds); Present(); RefreshLabels(); }
        public GeneratedFluidUiState CaptureGeneratedFluidState()
        {
            var history = GeneratedHistory;
            var state = new GeneratedFluidUiState
            {
                Supported = Ready && Profile.GasEnabled, Generating = IsGeneratingFluid, Ready = GeneratedFluidReady,
                Selected = GeneratedFluidActive, Covered = GeneratedFluidPlayer != null && GeneratedFluidPlayer.IsCovered,
                Status = GeneratedFluidStatus, Progress = GeneratedFluidPlayer == null ? 0 : GeneratedFluidPlayer.GenerationProgress,
                Coverage = "No generated timeline. One physical grid is shared by all render profiles.",
                Metrics = "96 × 96 cells / up to 129 cached frames / final ≤ 4000 tg\nBackground CPU solve; deterministic cached playback."
            };
            if (history != null)
            {
                bool covered = history.TryGetFrames(Snapshot.ElapsedSeconds, out int lower, out int upper, out double blend);
                var d = covered ? history.FrameDiagnostics[lower] : history.Diagnostics;
                var e = covered ? history.FrameDiagnostics[upper] : history.Diagnostics;
                double Mass(double a, double b) => a + (b - a) * blend;
                double mass = Mass(d.FinalMass, e.FinalMass), sink = Mass(d.RemovedSinkMass, e.RemovedSinkMass);
                double outflow = Mass(d.NetOutflowMass, e.NetOutflowMass), floor = Mass(d.AddedFloorMass, e.AddedFloorMass);
                double residual = mass - (d.InitialMass - sink - outflow + floor);
                state.Coverage = $"COVERAGE {history.StartSeconds:0.0000} – {history.EndSeconds:0.0000} s\n{(state.Covered ? "Current epoch is covered" : "Outside coverage: generated fluid is hidden")}";
                state.Metrics = $"{history.GridSize}² cells / {history.FrameCount} frames / {GeneratedFluidPlayer.PlaybackPayloadBytes / 1048576.0:0.0} MiB payload\n"
                    + $"{(covered ? "M(t)" : "M(end)")} [relative] {mass:G4} / sink {sink:G3}\n"
                    + $"out {outflow:G3} / floor {floor:G3} / residual {residual:G3}\n"
                    + $"{d.StepCount} steps / max CFL {d.MaxCourantNumber:0.000}";
            }
            return state;
        }

        public void ReturnToLauncher()
        {
            Ready = false;
            if (GeneratedFluidPlayer != null) GeneratedFluidPlayer.Release(); GeneratedFluidPlayer = null;
            if (sceneRoot != null) sceneRoot.SetActive(false);
            if (hud != null) hud.SetActive(false);
            if (SceneTarget != null) { if (View != null) View.targetTexture = null; SceneTarget.Release(); Destroy(SceneTarget); }
            foreach (var owned in new UnityEngine.Object[] { sceneRoot, hud, bodyMaterial, gasMaterial, sampleMaterial, displayMaterial, sampleMesh })
                if (owned != null) Destroy(owned);
            sceneRoot = hud = sampleObject = null; SceneTarget = null; sceneImage = null;
            bodyMaterial = gasMaterial = sampleMaterial = displayMaterial = null; sampleMesh = null;
            samplePositions = null; sampleColours = null; sampleRenderer = gasRenderer = null;
            Session = null; Wrist = null; Optics = null; FeaturedStar = null; View = null; sky = null;
            lastGeneration = -1; lastTime = double.NaN; GasVisible = SamplesVisible = false;
            if (launcher != null && !disposed) { launcher.SetActive(true); SelectProfile(SelectedProfile); }
        }
        private void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= AfterSceneRender;
            disposed = true; ReturnToLauncher();
        }
    }
}
