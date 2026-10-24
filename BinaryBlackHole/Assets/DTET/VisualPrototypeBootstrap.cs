using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // SI inspiral lesson with a separate artistic storyboard; no merger, gas or observer-optics model.
    public sealed partial class VisualPrototypeBootstrap : MonoBehaviour, ILessonHudProvider
    {
        private static readonly Color Ink = Hex("#07101F");
        private static readonly Color Panel = Hex("#101B30");
        private static readonly Color Muted = Hex("#AABAD0");
        private static readonly Color Cyan = Hex("#63D9E8");
        private static readonly Color Violet = Hex("#A78BFA");
        private static readonly Color Gold = Hex("#F2BC72");
        private static readonly Color White = Hex("#F4F7FC");

        private Transform bodyA;
        private Transform bodyB;
        private GameObject remnantMarker;
        private LineRenderer orbit;
        private LineRenderer trailA;
        private LineRenderer trailB;
        private HudSignalGraphic learningSignal;
        private Button reverseButton;
        private Button applyStartButton;
        private Text modeBanner, motionNote, gasNote, storyExplanation, graphTitle, graphLegend, graphAxis, playbackCaption, eventContextNote;
        private readonly SimulationSession session = new SimulationSession();
        private PhysicalSceneSnapshot physicalFrame;
        private bool physicalMode = true;
        public bool PhysicalMode => physicalMode;
        public PhysicalSceneSnapshot PhysicalFrame => physicalFrame;
        public static double MetersPerDisplayUnit => StagingScenarios.Baseline.InitialSeparationMeters / 4.9;
        private static readonly Vector3 ExhibitCenter = new Vector3(-3.25f, 0, 0);
        private Text scenarioText;
        private Text separationText;
        private Text separationCaption;
        private Text playbackText;
        private Text seekText;
        private Text layerBody;
        private Text transportText;
        private Text endpointText;
        private Text checkpointFeedback;
        private Text viewModeText;
        private Image sceneDimmer;
        private ExhibitCameraRig observer;
        private ObserverPhysicsBridge observerPhysics;
        private StellarSourceField stellarField;
        public StellarSourceField StellarField => stellarField;

        public void FocusLensingDemo()
        {
            if (numericalMode) return; // This independent approximate-lens demo has no SXS metric.
            playing = false;
            session.Pause();
            RefreshPhysicalState();
            SetLearningVisible(false);
            stellarField.SetLensing(true);
            stellarField.SetRayPaths(true);
            observer.AlignForLensing(bodyA.gameObject.activeInHierarchy ? bodyA : remnantMarker.transform, stellarField.DemoSource.transform);
            transportText.text = "Approximate lens alignment / paused; move to change the view";
        }
        private GameObject learningOverlay;
        private GameObject observationOverlay;
        private PartnerLessonHud partnerHud;
        private bool learningVisible;
        private GameObject storyGroup;
        private GameObject graphGroup;
        private GameObject modelGroup;
        private Slider separationSlider;
        private Slider playbackSlider;
        private Slider seekSlider;
        private float visualPhase;
        private float visualProgress;
        private float visualSpeed = 1f;
        private float initialSeparation = 30f;
        private float pendingSeparation = 30f;
        private bool playing = true;
        private bool dimView;
        private int scenario;
        private Font font;

        public ExhibitCameraRig Observer => observer;
        public bool LearningVisible => learningVisible;
        public PartnerLessonHud PartnerHud => partnerHud;

        public LessonHudSnapshot CaptureHudSnapshot() => numericalMode
            ? new LessonHudSnapshot(numericalSession)
            : physicalMode
            ? new LessonHudSnapshot(physicalFrame ?? PhysicalSceneSnapshot.Capture(session, MetersPerDisplayUnit))
            : new LessonHudSnapshot(visualProgress, visualSpeed,
                Mathf.Lerp(2.45f, 1.05f, visualProgress) / 2.45f, playing);

        public void SetHudPlaybackRate(float rate)
        {
            if (!float.IsFinite(rate)) return;
            if (numericalMode)
            {
                numericalSession.SetPlaybackRate((rate < 0 ? -1 : 1) * Math.Max(.01, Math.Min(4, Math.Abs((double)rate))));
                playbackSlider.SetValueWithoutNotify((float)Math.Abs(numericalSession.PlaybackRate));
                RefreshNumericalState();
                return;
            }
            var magnitude = Mathf.Clamp(physicalMode ? Mathf.Abs(rate) : rate, 0.1f, 4f);
            visualSpeed = physicalMode && rate < 0 ? -magnitude : magnitude;
            if (physicalMode) session.SetPlaybackRate(visualSpeed);
            playbackSlider.SetValueWithoutNotify(magnitude);
            playbackText.text = physicalMode ? $"{visualSpeed:0.0}x playback / frequency stays physical"
                : $"{visualSpeed:0.0}x illustrative playback";
            RefreshPhysicalState();
        }

        public void SetPhysicsMode(bool enabled)
        {
            LeaveNumericalMode();
            physicalMode = enabled;
            if (reverseButton != null) reverseButton.interactable = enabled;
            session.Pause();
            playing = false;
            visualSpeed = 1;
            session.SetPlaybackRate(1);
            observerPhysics.InvalidateEpoch();
            SetArtisticGas(bodyA, !enabled);
            SetArtisticGas(bodyB, !enabled);
            SetArtisticGas(remnantMarker.transform, !enabled);
            SetScenario(0);
            SetHudPlaybackRate(1);
            modeBanner.text = enabled ? "SI INSPIRAL / BOUNDED APPROXIMATION" : "ARTISTIC STORYBOARD / NO ORBITAL PHYSICS";
            motionNote.text = enabled ? $"Source time in seconds / 1 scene unit = {MetersPerDisplayUnit / 1000:0} km." : "Orbit speed is authored; no physical time or Hz.";
            gasNote.text = enabled ? "Gas hidden. Enlarged silhouettes mark model positions." : "Luminous gas is artistic, not an inferred environment.";
            storyExplanation.text = enabled ? "A shared SI model drives both bodies, source time and the frequency graph. Energy loss shrinks the orbit; the dominant circular GW frequency is twice the orbital frequency." : "The paired markers follow an authored storyboard. Their orbit and gas are illustrative.";
            graphTitle.text = enabled ? "GRAPH / MODEL FREQUENCY (Hz)" : "GRAPH / ILLUSTRATIVE CHIRP";
            eventContextNote.text = "GW150914 was detected by LIGO. This mode is a simplified teaching example.";
            graphAxis.text = enabled ? "f_GW (Hz)" : "Illustrative";
            playbackCaption.text = enabled ? "PLAYBACK RATE / SOURCE TIME" : "PLAYBACK RATE / STORY POSITION";
            if (!enabled)
            {
                graphLegend.text = "Qualitative amplitude / story position 0–100%";
                UpdateStoryboard();
            }
            layerBody.text = enabled ? "Circular quadrupole decay: dr/dt = -K/r^3; f_GW = 2 f_orbit. SI units; stops at 10 GM/c². No calibrated detector strain or merger prediction. A/B: 30+30; C: 20+40 solar masses." : "Artistic orbit and graph; no physical equations drive this storyboard. Merger remains conceptual.";
            if (partnerHud != null) partnerHud.InvalidateNarration();
        }

        private static void SetArtisticGas(Transform body, bool visible)
        {
            foreach (Transform child in body)
                child.gameObject.SetActive(!child.name.StartsWith("Numerical", StringComparison.Ordinal)
                    && (visible || child.name.StartsWith("Central silhouette", StringComparison.Ordinal)));
        }

        public void ReversePlayback()
        {
            if (numericalMode) { SetHudPlaybackRate(-(float)numericalSession.PlaybackRate); return; }
            if (!physicalMode) return;
            SetHudPlaybackRate(-(float)session.PlaybackRate);
        }

        public void SelectPhysicalScenario(int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
            if (!physicalMode) SetPhysicsMode(true);
            SetScenario(index);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            bool interactiveExhibit = Array.IndexOf(Environment.GetCommandLineArgs(), "--interactive-exhibit") >= 0;
            bool reducedExhibit = Array.IndexOf(Environment.GetCommandLineArgs(), "--reduced-exhibit") >= 0;
            bool referenceLab = Array.IndexOf(Environment.GetCommandLineArgs(), "--reference-lab") >= 0;
#if DTET_DESKTOP_RELEASE
            interactiveExhibit |= !reducedExhibit && !referenceLab
                && Array.IndexOf(Environment.GetCommandLineArgs(), "--event-lessons") < 0;
#endif
#if UNITY_EDITOR
            int editorExhibit = UnityEditor.SessionState.GetInt("DTET.StartupExhibit", 0);
            interactiveExhibit |= editorExhibit == 3;
            reducedExhibit |= editorExhibit == 2;
            referenceLab |= editorExhibit == 1;
#endif
            if (interactiveExhibit)
            {
                if (FindFirstObjectByType<InteractiveBinaryExhibit>() == null)
                    new GameObject("DT-ET interactive suit observatory").AddComponent<InteractiveBinaryExhibit>();
#if DTET_DESKTOP_RELEASE
                Debug.Log("DTET_DESKTOP_OBSERVATORY_INSTALLED");
#endif
                return;
            }
            if (reducedExhibit)
            {
                if (FindFirstObjectByType<ReducedBinaryExhibit>() == null)
                    new GameObject("DT-ET reduced binary exhibit").AddComponent<ReducedBinaryExhibit>();
                return;
            }
            if (referenceLab)
            {
                if (FindFirstObjectByType<ReferenceFlowLab>() == null)
                    new GameObject("DT-ET isolated field reference lab").AddComponent<ReferenceFlowLab>();
                return;
            }
            if (FindFirstObjectByType<VisualPrototypeBootstrap>() != null) return;
            var root = new GameObject("DT-ET Storyboard / approximate thin-lens optics");
            DontDestroyOnLoad(root);
            root.AddComponent<VisualPrototypeBootstrap>();
        }

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildWorld();
            BuildHud();
            SetLayer(0);
            SetPhysicsMode(true);
            SetLearningVisible(false);
        }

        private void Update()
        {
            HandleKeyboard();
            if (numericalMode)
            {
                numericalSession.Tick(Time.unscaledDeltaTime);
                RefreshNumericalState();
            }
            else if (physicalMode)
            {
                session.Tick(Time.unscaledDeltaTime);
                RefreshPhysicalState();
            }
            else UpdateStoryboard();
        }

        private void UpdateStoryboard()
        {
            if (playing)
            {
                visualProgress = Mathf.Clamp01(visualProgress + Time.unscaledDeltaTime * visualSpeed * 0.025f);
                if (seekSlider != null) seekSlider.SetValueWithoutNotify(visualProgress);
                if (visualProgress >= 1f)
                {
                    playing = false;
                    transportText.text = "Conceptual endpoint reached; reset to compare";
                }
            }

            visualPhase = (float)StoryboardChoreography.Phase(visualProgress);
            Shader.SetGlobalFloat("_StoryDisplayTime", visualProgress * 40f);
            var displayScale = Mathf.Lerp(0.95f, 1.2f, Mathf.InverseLerp(25f, 50f, initialSeparation));
            var radius = Mathf.Lerp(2.45f, 1.05f, visualProgress) * displayScale;
            var center = new Vector3(-3.25f, 0, 0);
            var delta = new Vector3(Mathf.Cos(visualPhase), 0, Mathf.Sin(visualPhase)) * radius;
            bodyA.position = center + delta;
            bodyB.position = center - delta;
            var atEndpoint = visualProgress >= 0.98f;
            bodyA.gameObject.SetActive(!atEndpoint);
            bodyB.gameObject.SetActive(!atEndpoint);
            remnantMarker.SetActive(atEndpoint);
            orbit.enabled = !atEndpoint;
            trailA.enabled = !atEndpoint;
            trailB.enabled = !atEndpoint;
            if (endpointText != null) endpointText.text = atEndpoint
                ? "CONCEPTUAL MERGER / REMNANT: visual placeholder, no merger physics"
                : "MERGER / REMNANT: conceptual endpoint, not simulated here";
            DrawOrbit(radius);
            DrawTrail(trailA, radius, visualPhase, false);
            DrawTrail(trailB, radius, visualPhase, true);
            if (learningSignal != null) { learningSignal.SetModel(null); learningSignal.SetProgress(visualProgress); }
            if (seekText != null) seekText.text = $"Story position  {visualProgress * 100f:0}%";
        }

        private void RefreshPhysicalState()
        {
            if (!physicalMode || bodyA == null) return;
            physicalFrame = PhysicalSceneSnapshot.Capture(session, MetersPerDisplayUnit);
            var frame = physicalFrame;
            visualProgress = (float)frame.Progress;
            visualSpeed = (float)frame.PlaybackRate;
            playing = frame.Playing;
            bodyA.position = ExhibitCenter + new Vector3((float)frame.Body1X, 0, (float)frame.Body1Z);
            bodyB.position = ExhibitCenter + new Vector3((float)frame.Body2X, 0, (float)frame.Body2Z);
            bodyA.gameObject.SetActive(!frame.AtCutoff);
            bodyB.gameObject.SetActive(!frame.AtCutoff);
            remnantMarker.SetActive(frame.AtCutoff);
            orbit.enabled = trailA.enabled = trailB.enabled = !frame.AtCutoff;
            DrawOrbit((bodyA.position - ExhibitCenter).magnitude);
            DrawPhysicalTrail(trailA, frame, false);
            DrawPhysicalTrail(trailB, frame, true);
            Shader.SetGlobalFloat("_StoryDisplayTime", (float)frame.State.PhysicalTimeSeconds);
            if (seekSlider != null) seekSlider.SetValueWithoutNotify(visualProgress);
            if (seekText != null) seekText.text = $"Source t = {frame.State.PhysicalTimeSeconds:0.000} s / cutoff {frame.Model.CutoffTimeSeconds:0.000} s";
            if (endpointText != null) endpointText.text = frame.AtCutoff
                ? "MODEL CUTOFF / conceptual endpoint only; no merger or ringdown solution"
                : "Circular SI inspiral / 10 GM/c² cutoff / marker sizes enlarged";
            if (learningSignal != null) { learningSignal.SetModel(frame.Model); learningSignal.SetProgress((float)frame.GraphProgress); }
            if (graphLegend != null) graphLegend.text = $"0–{learningSignal.MaximumFrequencyHz:0.0} Hz / 0–{frame.Model.CutoffTimeSeconds:0.0} s";
            if (transportText != null) transportText.text = frame.AtCutoff ? "Approximation ended; endpoint is conceptual"
                : (frame.Playing ? "Playing" : "Paused") + $" / source t={frame.PlaybackTimeSeconds:0.000} s / {frame.PlaybackRate:0.0}x";
        }

        private static void DrawPhysicalTrail(LineRenderer line, PhysicalSceneSnapshot frame, bool second)
        {
            double frequency = frame.Model.Evaluate(Math.Min(frame.PlaybackTimeSeconds, frame.Model.CutoffTimeSeconds * (1 - 1e-12))).OrbitalFrequencyHertz.Value;
            double duration = 1.35 / (2 * Math.PI * frequency);
            for (int i = 0; i < line.positionCount; i++)
            {
                double t = Math.Max(0, Math.Min(frame.PlaybackTimeSeconds, frame.Model.CutoffTimeSeconds) - duration * (1 - i / (double)(line.positionCount - 1)));
                var state = frame.Model.Evaluate(t);
                double x = second ? state.Body2XMeters : state.Body1XMeters;
                double z = second ? state.Body2ZMeters : state.Body1ZMeters;
                line.SetPosition(i, ExhibitCenter + new Vector3((float)(x / frame.MetersPerDisplayUnit), 0.06f, (float)(z / frame.MetersPerDisplayUnit)));
            }
        }

        private void BuildWorld()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera");
                camera = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;

            var existingLight = FindFirstObjectByType<Light>();
            if (existingLight == null)
            {
                var lamp = new GameObject("Illustrative key light").AddComponent<Light>();
                lamp.type = LightType.Directional;
                lamp.transform.rotation = Quaternion.Euler(35, -25, 0);
                lamp.intensity = 1.5f;
            }

            orbit = VisualAssetFactory.CreateGlowLine(null, "Initial orbit / storyboard guide", new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f), 0.018f, 128, true);
            orbit.useWorldSpace = true;

            MakeReferenceRing(3.1f, 0.22f);
            MakeReferenceRing(3.7f, 0.12f);
            MakeRadialTicks();

            bodyA = VisualAssetFactory.CreateBody("Body A - illustrated black-hole marker", Cyan).transform;
            bodyB = VisualAssetFactory.CreateBody("Body B - illustrated black-hole marker", Violet).transform;
            remnantMarker = VisualAssetFactory.CreateBody("Conceptual remnant - not simulated", Gold, true);
            remnantMarker.transform.position = new Vector3(-3.25f, 0, 0);
            remnantMarker.transform.localScale = Vector3.one * 1.25f;
            remnantMarker.SetActive(false);
            trailA = MakeTrail("Body A / illustrative motion trail", Cyan);
            trailB = MakeTrail("Body B / illustrative motion trail", Violet);
            stellarField = VisualAssetFactory.CreateStarfield(camera).GetComponent<StellarSourceField>();
            stellarField.BindLenses(bodyA, bodyB, remnantMarker.transform);
            observer = camera.gameObject.AddComponent<ExhibitCameraRig>();
            observer.Initialize(camera, bodyA, bodyB);
            observerPhysics = camera.gameObject.AddComponent<ObserverPhysicsBridge>();
            observerPhysics.Initialize(observer);
        }

        private void MakeReferenceRing(float radius, float opacity)
        {
            var color = new Color(Cyan.r, Cyan.g, Cyan.b, opacity);
            var line = VisualAssetFactory.CreateGlowLine(null, "Static orbital reference / not a path", color, 0.012f, 128, true);
            line.useWorldSpace = true;
            for (var i = 0; i < line.positionCount; i++)
            {
                var t = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(-3.25f + Mathf.Cos(t) * radius, -0.04f, Mathf.Sin(t) * radius));
            }
        }

        private void MakeRadialTicks()
        {
            var color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f);
            for (var tick = 0; tick < 12; tick++)
            {
                var line = VisualAssetFactory.CreateGlowLine(null, "Orbital reference tick " + tick, color, 0.014f, 2, false);
                line.useWorldSpace = true;
                var angle = tick * Mathf.PI * 2f / 12f;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                line.SetPosition(0, new Vector3(-3.25f, -0.035f, 0) + direction * 3.64f);
                line.SetPosition(1, new Vector3(-3.25f, -0.035f, 0) + direction * 3.82f);
            }
        }

        private static LineRenderer MakeTrail(string name, Color color)
        {
            var line = VisualAssetFactory.CreateGlowLine(null, name, color, 0.058f, 52, false);
            line.useWorldSpace = true;
            line.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.7f, 1) }
            };
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0.04f), new Keyframe(1, 1f));
            return line;
        }

        private static void DrawTrail(LineRenderer line, float radius, float phase, bool opposite)
        {
            var sign = opposite ? -1f : 1f;
            for (var i = 0; i < line.positionCount; i++)
            {
                var fraction = i / (float)(line.positionCount - 1);
                var angle = phase - (1f - fraction) * 1.35f;
                var point = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius * sign;
                line.SetPosition(i, new Vector3(-3.25f + point.x, 0.06f, point.z));
            }
        }

        private void DrawOrbit(float radius)
        {
            for (var i = 0; i < orbit.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / orbit.positionCount;
                orbit.SetPosition(i, new Vector3(-3.25f + Mathf.Cos(angle) * radius, -0.06f, Mathf.Sin(angle) * radius));
            }
        }

        private void BuildHud()
        {
            var canvasObject = new GameObject("DT-ET world-space learning board", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 900);
            canvasObject.transform.position = ExhibitCameraRig.LearningBoardPosition;
            canvasObject.transform.localScale = Vector3.one * 0.004f;

            // Noninteractive world-space brightness filter, shared by the emulated/tracked view.
            var veil = new GameObject("View brightness filter", typeof(RectTransform), typeof(Canvas));
            veil.transform.SetParent(Camera.main.transform, false);
            veil.transform.localPosition = new Vector3(0, 0, 0.07f);
            veil.transform.localScale = Vector3.one * 0.001f;
            veil.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 500);
            veil.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            sceneDimmer = Block(veil.transform, "Brightness veil", new Color(0, 0, 0, 0), Anchor.Stretch, Vector2.zero, Vector2.zero).GetComponent<Image>();
            sceneDimmer.raycastTarget = false;

            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(canvasObject.transform);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            learningOverlay = new GameObject("Learning overlay", typeof(RectTransform));
            learningOverlay.transform.SetParent(canvasObject.transform, false);
            Place(learningOverlay.GetComponent<RectTransform>(), Anchor.Stretch, Vector2.zero, Vector2.zero);
            BuildLearningHud(learningOverlay.transform);
            observationOverlay = new GameObject("VR exhibit interface");
            observationOverlay.transform.SetParent(transform, false);
            BuildObservationHud(observationOverlay.transform);
            SetLearningVisible(false);
        }

        private void BuildLearningHud(Transform parent)
        {
            // World-space exhibit board.
            var reserved = Block(parent, "Learning reserved background", Ink, Anchor.Stretch, Vector2.zero, Vector2.zero);
            reserved.GetComponent<Image>().raycastTarget = false;
            TextAt(parent, "DT / ET", 34, White, new Vector2(42, -36), new Vector2(620, 50), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "BINARY BLACK HOLES  /  EINSTEIN TELESCOPE", 15, Cyan, new Vector2(42, -84), new Vector2(720, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "Two bodies. One evolving story.", 21, White, new Vector2(42, -125), new Vector2(700, 45), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "GOAL  Explain why smaller separation means a higher dominant GW frequency.", 15, White, new Vector2(42, -155), new Vector2(930, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(parent, "OBSERVE / H", new Vector2(820, -38), new Vector2(160, 42), Cyan, () => SetLearningVisible(false));
            ButtonAt(parent, "EXIT APP", new Vector2(820, -88), new Vector2(160, 38), Gold, ExitExperience);

            var banner = Block(parent, "Prototype status", Hex("#273148"), Anchor.TopLeft, new Vector2(42, -186), new Vector2(475, 40));
            modeBanner = TextAt(banner.transform, "SI INSPIRAL / BOUNDED APPROXIMATION", 14, Gold, Vector2.zero, new Vector2(455, 36), TextAnchor.MiddleCenter, Anchor.Center);
            motionNote = TextAt(parent, "Source time / physical frequency / fixed render scale", 15, Muted, new Vector2(42, -233), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);
            gasNote = TextAt(parent, "Gas disabled in the physical view", 15, Muted, new Vector2(42, -261), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "No ray-traced horizons or binary spacetime yet.", 15, Muted, new Vector2(42, -289), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(parent, "DIM / NORMAL", new Vector2(535, -186), new Vector2(160, 40), Cyan, ToggleDimView);
            viewModeText = TextAt(parent, "View: normal  |  B toggles dimming", 13, Muted, new Vector2(42, -321), new Vector2(700, 28), TextAnchor.MiddleLeft, Anchor.TopLeft);

            var card = Block(parent, "Learning controls", Panel, Anchor.TopRight, new Vector2(-40, -38), new Vector2(450, 812));
            TextAt(card.transform, "EXPLORE THE INSPIRAL", 22, White, new Vector2(20, -16), new Vector2(410, 40), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(card.transform, "Choose depth without changing the same story.", 14, Muted, new Vector2(20, -53), new Vector2(410, 32), TextAnchor.MiddleLeft, Anchor.TopLeft);

            ButtonAt(card.transform, "STORY", new Vector2(20, -96), new Vector2(128, 42), Cyan, () => SetLayer(0));
            ButtonAt(card.transform, "GRAPH", new Vector2(160, -96), new Vector2(128, 42), Violet, () => SetLayer(1));
            ButtonAt(card.transform, "MODEL", new Vector2(300, -96), new Vector2(128, 42), Gold, () => SetLayer(2));

            storyGroup = Block(card.transform, "Story content", Hex("#19263D"), Anchor.TopLeft, new Vector2(20, -155), new Vector2(410, 132));
            TextAt(storyGroup.transform, "STORY  /  WHAT TO NOTICE", 14, Cyan, new Vector2(15, -11), new Vector2(380, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            storyExplanation = TextAt(storyGroup.transform, "The paired markers orbit a shared centre. In the physical model, energy loss would draw them closer and raise the wave frequency. Here the movement is illustrative only.", 15, White, new Vector2(15, -41), new Vector2(380, 82), TextAnchor.UpperLeft, Anchor.TopLeft);

            graphGroup = Block(card.transform, "Graph content", Hex("#19263D"), Anchor.TopLeft, new Vector2(20, -155), new Vector2(410, 132));
            graphTitle = TextAt(graphGroup.transform, "GRAPH / MODEL FREQUENCY (Hz)", 14, Violet, new Vector2(15, -11), new Vector2(380, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            DrawGraph(graphGroup.transform);

            modelGroup = Block(card.transform, "Model content", Hex("#19263D"), Anchor.TopLeft, new Vector2(20, -155), new Vector2(410, 132));
            TextAt(modelGroup.transform, "MODEL  /  ASSUMPTIONS", 14, Gold, new Vector2(15, -11), new Vector2(380, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            layerBody = TextAt(modelGroup.transform, "Planned model: f_orbit ~ sqrt(GM/r^3) / (2pi); f_GW ~ 2 f_orbit. M is kg, r is metres, f is Hz. These equations are not driving this visual preview. Merger remains conceptual.", 14, White, new Vector2(15, -40), new Vector2(380, 82), TextAnchor.UpperLeft, Anchor.TopLeft);

            TextAt(card.transform, "COMPARE THREE CONFIGURATIONS", 14, Muted, new Vector2(20, -305), new Vector2(410, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(card.transform, "A  /  CLOSER", new Vector2(20, -335), new Vector2(128, 44), Cyan, () => SetScenario(0));
            ButtonAt(card.transform, "B  /  WIDER", new Vector2(160, -335), new Vector2(128, 44), Violet, () => SetScenario(1));
            ButtonAt(card.transform, "C / UNEQUAL", new Vector2(300, -335), new Vector2(128, 44), Gold, () => SelectPhysicalScenario(2));
            scenarioText = TextAt(card.transform, "", 15, White, new Vector2(20, -384), new Vector2(410, 28), TextAnchor.MiddleLeft, Anchor.TopLeft);

            separationCaption = TextAt(card.transform, "INITIAL SEPARATION / GM/c²", 14, Muted, new Vector2(20, -426), new Vector2(270, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            separationSlider = SliderAt(card.transform, new Vector2(20, -458), new Vector2(410, 24), 25, 50, 30, value =>
            {
                pendingSeparation = value;
                separationText.text = $"Next start: {value:0} GM/c²  |  press APPLY";
            });
            separationText = TextAt(card.transform, "", 14, Cyan, new Vector2(20, -485), new Vector2(410, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            applyStartButton = ButtonAt(card.transform, "APPLY START", new Vector2(295, -421), new Vector2(133, 33), Cyan, ApplyPendingSeparation);

            ButtonAt(card.transform, "PAUSE / PLAY", new Vector2(20, -532), new Vector2(194, 42), Cyan, TogglePlayback);
            ButtonAt(card.transform, "RESET", new Vector2(234, -532), new Vector2(194, 42), Violet, ResetPlayback);
            transportText = TextAt(card.transform, "Illustrative playback running", 13, Muted, new Vector2(20, -577), new Vector2(410, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);
            seekSlider = SliderAt(card.transform, new Vector2(20, -610), new Vector2(410, 24), 0, 1, 0, value =>
            {
                SeekStoryboard(value);
            });
            seekText = TextAt(card.transform, "Story position  0%", 13, Muted, new Vector2(20, -635), new Vector2(410, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);

            playbackCaption = TextAt(card.transform, "PLAYBACK RATE / SOURCE TIME", 13, Muted, new Vector2(20, -670), new Vector2(410, 23), TextAnchor.MiddleLeft, Anchor.TopLeft);
            playbackSlider = SliderAt(card.transform, new Vector2(20, -697), new Vector2(410, 24), 0.1f, 4f, 1f, value =>
            {
                SetHudPlaybackRate((numericalMode ? numericalSession.PlaybackRate < 0 : physicalMode && session.PlaybackRate < 0) ? -value : value);
            });
            playbackText = TextAt(card.transform, "", 13, Cyan, new Vector2(20, -725), new Vector2(410, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(card.transform, "TUTOR  /  OFFLINE STAGING ADAPTER ONLY", 12, Gold, new Vector2(20, -765), new Vector2(410, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);

            ButtonAt(parent, "PHYSICS", new Vector2(42, -370), new Vector2(180, 40), Cyan, () => SetPhysicsMode(true));
            ButtonAt(parent, "STORYBOARD", new Vector2(235, -370), new Vector2(180, 40), Violet, () => SetPhysicsMode(false));
            reverseButton = ButtonAt(parent, "REVERSE", new Vector2(428, -370), new Vector2(150, 40), Gold, ReversePlayback);
            ButtonAt(parent, "SXS REPLAY / N", new Vector2(591, -370), new Vector2(190, 40), Gold, SetNumericalMode);
            BuildNumericalNotes(parent);
            eventContextNote = TextAt(parent, "GW150914 was detected by LIGO. This is a simplified circular lesson.", 14, Muted, new Vector2(42, -430), new Vector2(800, 32), TextAnchor.MiddleLeft, Anchor.TopLeft);
            var checkpoint = Block(parent, "One-question formative check", Hex("#142138"), Anchor.BottomLeft, new Vector2(42, 106), new Vector2(820, 150));
            TextAt(checkpoint.transform, "QUICK CHECK  /  THINK BEFORE REVEALING", 14, Cyan, new Vector2(17, -10), new Vector2(780, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(checkpoint.transform, "If the two bodies move closer, should the dominant GW frequency rise, fall, or stay the same?", 15, White, new Vector2(17, -38), new Vector2(780, 33), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(checkpoint.transform, "RISE", new Vector2(17, -78), new Vector2(116, 35), Cyan, () => AnswerCheckpoint(true));
            ButtonAt(checkpoint.transform, "FALL", new Vector2(143, -78), new Vector2(116, 35), Violet, () => AnswerCheckpoint(false));
            ButtonAt(checkpoint.transform, "SAME", new Vector2(269, -78), new Vector2(116, 35), Violet, () => AnswerCheckpoint(false));
            checkpointFeedback = TextAt(checkpoint.transform, "Choose an answer, then compare A and B.", 13, Muted, new Vector2(400, -74), new Vector2(400, 63), TextAnchor.UpperLeft, Anchor.TopLeft);

            endpointText = TextAt(parent, "MERGER / REMNANT: conceptual endpoint, not simulated here", 14, Gold, new Vector2(42, 38), new Vector2(900, 28), TextAnchor.MiddleLeft, Anchor.BottomLeft);
            separationText.text = "Current start: 30 GM/c²  (visual scale only)";
            playbackText.text = "1.0x visual rate  /  does not change physical frequency";
        }

        private void BuildObservationHud(Transform parent)
        {
            var hudObject = new GameObject("Partner lesson HUD");
            hudObject.transform.SetParent(parent, false);
            partnerHud = hudObject.AddComponent<PartnerLessonHud>();
            partnerHud.Initialize(this, font, observer, ToggleDimView, ExitExperience, stellarField, FocusLensingDemo);
            observer.PoseRelocated += () => partnerHud.WristRig.SetRaised(partnerHud.WristRig.Raised, true);

            var welcome = new GameObject("Exhibit entrance instructions", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            welcome.transform.SetParent(parent, false);
            welcome.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            welcome.GetComponent<Canvas>().worldCamera = Camera.main;
            welcome.GetComponent<RectTransform>().sizeDelta = new Vector2(760, 214);
            welcome.transform.position = new Vector3(-3.25f, 2.0f, -3.5f);
            welcome.transform.localScale = Vector3.one * 0.004f;
            var plaque = Block(welcome.transform, "VR emulation welcome plaque", Panel, Anchor.Stretch, Vector2.zero, Vector2.zero);
            TextAt(plaque.transform, "DT / ET  ·  VR EXHIBIT / EMULATED HEAD AND HAND", 20, Cyan, new Vector2(18, -8), new Vector2(724, 35), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(plaque.transform, "V: raise / lower wrist   ·   Right-drag: look   ·   WASD: move   ·   Q/E: height", 17, White, new Vector2(18, -46), new Vector2(724, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(plaque.transform, "Wheel: move closer / farther   ·   F: exhibit   ·   T: information   ·   H: learning", 17, White, new Vector2(18, -77), new Vector2(724, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(plaque.transform, "J: lens alignment   ·   L: bending on/off   ·   G: ray guides   ·   O: input options", 16, Gold, new Vector2(18, -108), new Vector2(724, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(plaque.transform, "GOAL: Explain closer separation -> faster orbit -> higher GW frequency.", 16, White, new Vector2(18, -142), new Vector2(724, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(plaque.transform, "N: numerical replay   ·   P: SI / artwork   ·   Esc: pause   ·   Wrist: Exit / options", 15, Muted, new Vector2(18, -174), new Vector2(724, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
        }

        public void SetLearningVisible(bool visible)
        {
            learningVisible = visible;
            learningOverlay.SetActive(visible);
            partnerHud.SetLearningMode(visible);
            observer.SetLearningViewport(visible);
        }

        public void SeekStoryboard(float value)
        {
            if (!float.IsFinite(value)) return;
            if (numericalMode)
            {
                observerPhysics.InvalidateEpoch();
                numericalSession.Seek(Mathf.Clamp01(value) * numericalSession.Model.DurationSeconds);
                RefreshNumericalState();
                return;
            }
            observerPhysics.InvalidateEpoch();
            visualProgress = Mathf.Clamp01(value);
            playing = false;
            if (physicalMode)
            {
                session.Seek(visualProgress * session.Model.EndTimeSeconds);
                RefreshPhysicalState();
            }
            else visualPhase = (float)StoryboardChoreography.Phase(visualProgress);
            seekSlider.SetValueWithoutNotify(visualProgress);
            transportText.text = physicalMode ? "Source-time seek / paused" : "Illustrative storyboard seek / paused";
        }

        private void DrawGraph(Transform parent)
        {
            graphAxis = TextAt(parent, "f_GW (Hz)", 12, Muted, new Vector2(15, -38), new Vector2(90, 18), TextAnchor.MiddleLeft, Anchor.TopLeft);
            var graph = new GameObject("Shared model frequency curve", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudSignalGraphic));
            graph.transform.SetParent(parent, false);
            Place(graph.GetComponent<RectTransform>(), Anchor.TopLeft, new Vector2(102, -44), new Vector2(270, 62));
            learningSignal = graph.GetComponent<HudSignalGraphic>();
            learningSignal.color = Violet;
            learningSignal.raycastTarget = false;
            graphLegend = TextAt(parent, "", 11, Muted, new Vector2(102, -108), new Vector2(285, 20), TextAnchor.MiddleLeft, Anchor.TopLeft);
        }

        private void SetScenario(int value)
        {
            if (numericalMode) { SetPhysicsMode(true); }
            observerPhysics.InvalidateEpoch();
            scenario = value;
            if (physicalMode)
            {
                session.SelectScenario(value == 0 ? StagingScenarios.Baseline : value == 1 ? StagingScenarios.Wider : StagingScenarios.Unequal);
                initialSeparation = value == 1 ? 45 : 30;
                pendingSeparation = initialSeparation;
                separationSlider.SetValueWithoutNotify(initialSeparation);
                separationText.text = $"Current start: {initialSeparation:0} GM/c²";
                scenarioText.text = $"{session.Scenario.Mass1SolarMasses:0}+{session.Scenario.Mass2SolarMasses:0} solar masses / {initialSeparation:0} GM/c²";
                ResetCheckpoint();
                RefreshPhysicalState();
                return;
            }
            playing = false;
            ResetCheckpoint();
            initialSeparation = value == 0 ? 30 : 45;
            pendingSeparation = initialSeparation;
            if (separationSlider != null) separationSlider.SetValueWithoutNotify(initialSeparation);
            if (separationText != null) separationText.text = $"Current start: {initialSeparation:0} GM/c²  (visual scale only)";
            if (scenarioText != null) scenarioText.text = value == 0
                ? "Scenario A: 30 + 30 solar masses, closer start"
                : "Scenario B: 30 + 30 solar masses, wider start";
            visualProgress = 0;
            visualPhase = 0;
            if (seekSlider != null) seekSlider.SetValueWithoutNotify(0);
            if (transportText != null) transportText.text = "Scenario selected; paused for comparison";
        }

        private void ApplyPendingSeparation()
        {
            if (numericalMode) { SeekNumericalMerger(); return; }
            observerPhysics.InvalidateEpoch();
            if (physicalMode)
            {
                session.SetInitialSeparation(pendingSeparation * session.Scenario.GravitationalRadiusMeters);
                initialSeparation = pendingSeparation;
                separationText.text = $"Current start: {initialSeparation:0} GM/c²";
                scenarioText.text = $"Custom / {session.Scenario.Mass1SolarMasses:0}+{session.Scenario.Mass2SolarMasses:0} solar masses";
                ResetCheckpoint();
                RefreshPhysicalState();
                return;
            }
            initialSeparation = pendingSeparation;
            visualProgress = 0;
            visualPhase = 0;
            playing = false;
            ResetCheckpoint();
            seekSlider.SetValueWithoutNotify(0);
            separationText.text = $"Current start: {initialSeparation:0} GM/c²  (visual scale only)";
            scenarioText.text = $"Custom start: {initialSeparation:0} GM/c² / masses not connected";
            transportText.text = "New visual starting point applied; paused";
        }

        public void TogglePlayback()
        {
            if (numericalMode)
            {
                if (numericalSession.IsPlaying) numericalSession.Pause();
                else
                {
                    if (numericalSession.PlaybackRate > 0 && numericalSession.PlaybackTimeSeconds >= numericalSession.Model.DurationSeconds) numericalSession.Reset();
                    numericalSession.Play();
                }
                RefreshNumericalState();
                return;
            }
            if (physicalMode)
            {
                if (session.IsPlaying) session.Pause();
                else
                {
                    if (session.PlaybackRate > 0 && session.PlaybackTimeSeconds >= session.Model.EndTimeSeconds) session.Reset();
                    session.Play();
                }
                RefreshPhysicalState();
                return;
            }
            if (visualProgress >= 1f) ResetPlayback();
            playing = !playing;
            transportText.text = playing ? "Illustrative playback running" : "Illustrative playback paused";
        }

        public void ResetPlayback()
        {
            if (numericalMode)
            {
                observerPhysics.InvalidateEpoch();
                numericalSession.Reset();
                RefreshNumericalState();
                return;
            }
            if (physicalMode)
            {
                observerPhysics.InvalidateEpoch();
                session.Reset();
                RefreshPhysicalState();
                ResetCheckpoint();
                return;
            }
            observerPhysics.InvalidateEpoch();
            visualProgress = 0;
            visualPhase = 0;
            seekSlider.SetValueWithoutNotify(0);
            playing = false;
            ResetCheckpoint();
            transportText.text = "Illustrative playback reset and paused";
        }

        private void ToggleDimView()
        {
            dimView = !dimView;
            sceneDimmer.color = new Color(0, 0, 0, dimView ? 0.32f : 0f);
            viewModeText.text = dimView ? "View: dimmed  |  B returns to normal" : "View: normal  |  B toggles dimming";
        }

        private void AnswerCheckpoint(bool correct)
        {
            checkpointFeedback.text = correct
                ? "Yes. In the simplified inspiral relation, closer means faster orbit; dominant GW frequency follows at about twice the orbital rate."
                : "Try again: as separation shrinks, orbital motion speeds up. What should happen to the wave frequency?";
            checkpointFeedback.color = correct ? Cyan : Gold;
        }

        private void ResetCheckpoint()
        {
            if (checkpointFeedback == null) return;
            checkpointFeedback.text = "Choose an answer, then compare A and B.";
            checkpointFeedback.color = Muted;
        }

        private void ExitExperience()
        {
            playing = false;
            session.Pause();
            numericalSession?.Pause();
            Application.Quit();
            transportText.text = "Exit requested; in the Editor, use the Play toolbar to stop";
        }

        private void HandleKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame) TogglePlayback();
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                playing = false;
                session.Pause();
                numericalSession?.Pause();
                if (numericalMode) RefreshNumericalState();
                RefreshPhysicalState();
                transportText.text = "Paused with Esc; Space resumes, Exit closes the app";
            }
            if (keyboard.rKey.wasPressedThisFrame) ResetPlayback();
            if (keyboard.pKey.wasPressedThisFrame) SetPhysicsMode(!physicalMode);
            if (keyboard.nKey.wasPressedThisFrame) SetNumericalMode();
            if (keyboard.commaKey.wasPressedThisFrame) ReversePlayback();
            if (keyboard.fKey.wasPressedThisFrame || keyboard.homeKey.wasPressedThisFrame) SetLearningVisible(false);
            if (keyboard.bKey.wasPressedThisFrame) ToggleDimView();
            if (keyboard.hKey.wasPressedThisFrame)
            {
                SetLearningVisible(!learningVisible);
                if (learningVisible) observer.FocusLearning();
            }
            if (keyboard.tKey.wasPressedThisFrame) { SetLearningVisible(false); observer.FocusInformation(); }
            if (keyboard.kKey.wasPressedThisFrame) { SetLearningVisible(false); observer.FocusClockComparison(); }
            if (keyboard.lKey.wasPressedThisFrame) stellarField.ToggleLensing();
            if (keyboard.gKey.wasPressedThisFrame) stellarField.ToggleRayPaths();
            if (keyboard.jKey.wasPressedThisFrame) FocusLensingDemo();
            if (keyboard.oKey.wasPressedThisFrame) partnerHud.ToggleOptions();
            if (keyboard.uKey.wasPressedThisFrame) partnerHud.SetVisible(!partnerHud.Visible);
            if (keyboard.digit1Key.wasPressedThisFrame) SetLayer(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SetLayer(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SetLayer(2);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        public void SetQualityCheckView(int layer, float progress)
        {
            SetLearningVisible(true);
            SetLayer(Mathf.Clamp(layer, 0, 2));
            SeekStoryboard(progress);
        }
#endif

        private void SetLayer(int layer)
        {
            session.SelectLayer((InformationLayer)layer);
            if (observationOverlay != null) SetLearningVisible(true);
            storyGroup.SetActive(layer == 0);
            graphGroup.SetActive(layer == 1);
            modelGroup.SetActive(layer == 2);
        }

        private static GameObject Block(Transform parent, string name, Color color, Anchor anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, anchor, offset, size);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text TextAt(Transform parent, string content, int size, Color color, Vector2 offset, Vector2 dimensions, TextAnchor alignment, Anchor anchor)
        {
            var go = new GameObject(content.Length > 28 ? content.Substring(0, 28) : content, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchor, offset, dimensions);
            var label = go.GetComponent<Text>();
            label.font = font;
            label.text = content;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private Button ButtonAt(Transform parent, string label, Vector2 offset, Vector2 size, Color accent, Action action)
        {
            var go = Block(parent, label + " button", Hex("#24344B"), Anchor.TopLeft, offset, size);
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Hex("#24344B");
            colors.highlightedColor = Hex("#344D69");
            colors.pressedColor = accent;
            button.colors = colors;
            TextAt(go.transform, label, 14, accent, Vector2.zero, size, TextAnchor.MiddleCenter, Anchor.Center);
            button.onClick.AddListener(() => action());
            return button;
        }

        private Slider SliderAt(Transform parent, Vector2 offset, Vector2 size, float min, float max, float value, Action<float> changed)
        {
            var root = Block(parent, "Slider track", Hex("#35465B"), Anchor.TopLeft, offset, size);
            var slider = root.AddComponent<Slider>();
            var fill = Block(root.transform, "Fill", Cyan, Anchor.Stretch, Vector2.zero, Vector2.zero).GetComponent<RectTransform>();
            var handle = Block(root.transform, "Handle", White, Anchor.Center, Vector2.zero, new Vector2(14, 0)).GetComponent<RectTransform>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => changed(v));
            return slider;
        }

        private enum Anchor { TopLeft, TopRight, BottomLeft, Center, Stretch }

        private static void Place(RectTransform rect, Anchor anchor, Vector2 offset, Vector2 size)
        {
            switch (anchor)
            {
                case Anchor.TopLeft: rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); break;
                case Anchor.TopRight: rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one; break;
                case Anchor.BottomLeft: rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero; break;
                case Anchor.Stretch: rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 0.5f); break;
                default: rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f); break;
            }
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static Color Hex(string code)
        {
            ColorUtility.TryParseHtmlString(code, out var color);
            return color;
        }
    }
}
