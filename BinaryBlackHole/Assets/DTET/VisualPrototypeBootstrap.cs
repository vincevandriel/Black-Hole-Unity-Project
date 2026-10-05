using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>
    /// Desktop exhibit staging. Motion and graph artwork are illustrative, not a physical model.
    /// A later milestone will replace the storyboard state with SimulationSession outputs.
    /// </summary>
    public sealed class VisualPrototypeBootstrap : MonoBehaviour
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
        private RectTransform graphCursor;
        private Text scenarioText;
        private Text separationText;
        private Text playbackText;
        private Text seekText;
        private Text layerBody;
        private Text transportText;
        private Text endpointText;
        private Text checkpointFeedback;
        private Text viewModeText;
        private Image sceneDimmer;
        private ExhibitCameraRig observer;
        private GameObject learningOverlay;
        private GameObject observationOverlay;
        private Slider observationSeek;
        private Text observationTime;
        private Text observationCameraStatus;
        private Text observationEventStatus;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<VisualPrototypeBootstrap>() != null) return;
            var root = new GameObject("DT-ET Visual Staging (no physics)");
            DontDestroyOnLoad(root);
            root.AddComponent<VisualPrototypeBootstrap>();
        }

        private void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildWorld();
            BuildHud();
            SetLayer(0);
            SetScenario(0);
            SetLearningVisible(false);
        }

        private void Update()
        {
            HandleKeyboard();
            if (playing)
            {
                visualProgress = Mathf.Clamp01(visualProgress + Time.unscaledDeltaTime * visualSpeed * 0.025f);
                visualPhase += Time.unscaledDeltaTime * visualSpeed * 0.65f;
                if (seekSlider != null) seekSlider.SetValueWithoutNotify(visualProgress);
                if (visualProgress >= 1f)
                {
                    playing = false;
                    transportText.text = "Conceptual endpoint reached; reset to compare";
                }
            }

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
            if (graphCursor != null) graphCursor.anchoredPosition = new Vector2(102f + visualProgress * 270f, -106f);
            if (seekText != null) seekText.text = $"Story position  {visualProgress * 100f:0}%";
            if (observationSeek != null) observationSeek.SetValueWithoutNotify(visualProgress);
            if (observationTime != null) observationTime.text = $"{visualProgress * 100f:0}%  /  {(playing ? "playing" : "paused")}";
            if (observationCameraStatus != null) observationCameraStatus.text = observer.Status;
            if (observationEventStatus != null) observationEventStatus.text = atEndpoint
                ? "CONCEPTUAL REMNANT  /  reset or seek back to inspect the binary"
                : "VISUAL EXHIBIT  /  illustrative orbit, gas glow and chirp";
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
            var stars = VisualAssetFactory.CreateStarfield(camera);
            observer = camera.gameObject.AddComponent<ExhibitCameraRig>();
            observer.Initialize(camera, bodyA, bodyB, stars);
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
            var canvasObject = new GameObject("DT-ET desktop exhibit HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            sceneDimmer = Block(canvasObject.transform, "Scene-only brightness veil", new Color(0, 0, 0, 0), Anchor.Stretch, Vector2.zero, Vector2.zero).GetComponent<Image>();
            sceneDimmer.raycastTarget = false;

            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(canvasObject.transform);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            learningOverlay = new GameObject("Learning overlay", typeof(RectTransform));
            learningOverlay.transform.SetParent(canvasObject.transform, false);
            Place(learningOverlay.GetComponent<RectTransform>(), Anchor.Stretch, Vector2.zero, Vector2.zero);
            BuildLearningHud(learningOverlay.transform);
            observationOverlay = new GameObject("Observation overlay", typeof(RectTransform));
            observationOverlay.transform.SetParent(canvasObject.transform, false);
            Place(observationOverlay.GetComponent<RectTransform>(), Anchor.Stretch, Vector2.zero, Vector2.zero);
            BuildObservationHud(observationOverlay.transform);
            SetLearningVisible(false);
        }

        private void BuildLearningHud(Transform parent)
        {
            // A reduced camera viewport does not clear pixels outside its rectangle.
            // Cover the reserved area so opening panels cannot retain the previous frame's HUD.
            var reserved = Block(parent, "Learning reserved background", Ink, Anchor.Stretch, Vector2.zero, Vector2.zero);
            reserved.GetComponent<RectTransform>().anchorMin = new Vector2(0.68f, 0);
            reserved.GetComponent<Image>().raycastTarget = false;
            TextAt(parent, "DT / ET", 34, White, new Vector2(42, -36), new Vector2(620, 50), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "BINARY BLACK HOLES  /  EINSTEIN TELESCOPE", 15, Cyan, new Vector2(42, -84), new Vector2(720, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "Two bodies. One evolving story.", 21, White, new Vector2(42, -125), new Vector2(700, 45), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "GOAL  Explain why smaller separation means a higher dominant GW frequency.", 15, White, new Vector2(42, -155), new Vector2(930, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(parent, "OBSERVE / H", new Vector2(820, -38), new Vector2(160, 42), Cyan, () => SetLearningVisible(false));
            ButtonAt(parent, "EXIT APP", new Vector2(820, -88), new Vector2(160, 38), Gold, ExitExperience);

            var banner = Block(parent, "Prototype status", Hex("#273148"), Anchor.TopLeft, new Vector2(42, -186), new Vector2(475, 40));
            TextAt(banner.transform, "VISUAL STAGING  |  NO LIVE PHYSICS", 15, Gold, Vector2.zero, new Vector2(455, 36), TextAnchor.MiddleCenter, Anchor.Center);
            TextAt(parent, "Orbit, trails and disk glow are conceptual illustrations.", 15, Muted, new Vector2(42, -233), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "Luminous gas is artistic, not predicted for an isolated binary.", 15, Muted, new Vector2(42, -261), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);
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
            TextAt(storyGroup.transform, "The paired markers orbit a shared centre. In the physical model, energy loss would draw them closer and raise the wave frequency. Here the movement is illustrative only.", 15, White, new Vector2(15, -41), new Vector2(380, 82), TextAnchor.UpperLeft, Anchor.TopLeft);

            graphGroup = Block(card.transform, "Graph content", Hex("#19263D"), Anchor.TopLeft, new Vector2(20, -155), new Vector2(410, 132));
            TextAt(graphGroup.transform, "GRAPH  /  ILLUSTRATIVE CHIRP", 14, Violet, new Vector2(15, -11), new Vector2(380, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            DrawGraph(graphGroup.transform);

            modelGroup = Block(card.transform, "Model content", Hex("#19263D"), Anchor.TopLeft, new Vector2(20, -155), new Vector2(410, 132));
            TextAt(modelGroup.transform, "MODEL  /  ASSUMPTIONS", 14, Gold, new Vector2(15, -11), new Vector2(380, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            layerBody = TextAt(modelGroup.transform, "Planned model: f_orbit ~ sqrt(GM/r^3) / (2pi); f_GW ~ 2 f_orbit. M is kg, r is metres, f is Hz. These equations are not driving this visual preview. Merger remains conceptual.", 14, White, new Vector2(15, -40), new Vector2(380, 82), TextAnchor.UpperLeft, Anchor.TopLeft);

            TextAt(card.transform, "COMPARE TWO STARTING POINTS", 14, Muted, new Vector2(20, -305), new Vector2(410, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(card.transform, "A  /  CLOSER", new Vector2(20, -335), new Vector2(194, 44), Cyan, () => SetScenario(0));
            ButtonAt(card.transform, "B  /  WIDER", new Vector2(234, -335), new Vector2(194, 44), Violet, () => SetScenario(1));
            scenarioText = TextAt(card.transform, "", 15, White, new Vector2(20, -384), new Vector2(410, 28), TextAnchor.MiddleLeft, Anchor.TopLeft);

            TextAt(card.transform, "INITIAL SEPARATION  /  VISUAL RANGE", 14, Muted, new Vector2(20, -426), new Vector2(270, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            separationSlider = SliderAt(card.transform, new Vector2(20, -458), new Vector2(410, 24), 25, 50, 30, value =>
            {
                pendingSeparation = value;
                separationText.text = $"Next start: {value:0} GM/c²  |  press APPLY";
            });
            separationText = TextAt(card.transform, "", 14, Cyan, new Vector2(20, -485), new Vector2(410, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(card.transform, "APPLY START", new Vector2(295, -421), new Vector2(133, 33), Cyan, ApplyPendingSeparation);

            ButtonAt(card.transform, "PAUSE / PLAY", new Vector2(20, -532), new Vector2(194, 42), Cyan, TogglePlayback);
            ButtonAt(card.transform, "RESET", new Vector2(234, -532), new Vector2(194, 42), Violet, ResetPlayback);
            transportText = TextAt(card.transform, "Illustrative playback running", 13, Muted, new Vector2(20, -577), new Vector2(410, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);
            seekSlider = SliderAt(card.transform, new Vector2(20, -610), new Vector2(410, 24), 0, 1, 0, value =>
            {
                SeekStoryboard(value);
            });
            seekText = TextAt(card.transform, "Story position  0%", 13, Muted, new Vector2(20, -635), new Vector2(410, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);

            TextAt(card.transform, "VISUAL PLAYBACK RATE", 13, Muted, new Vector2(20, -670), new Vector2(410, 23), TextAnchor.MiddleLeft, Anchor.TopLeft);
            playbackSlider = SliderAt(card.transform, new Vector2(20, -697), new Vector2(410, 24), 0.1f, 4f, 1f, value =>
            {
                visualSpeed = value;
                playbackText.text = $"{value:0.0}x visual rate  /  does not change physical frequency";
            });
            playbackText = TextAt(card.transform, "", 13, Cyan, new Vector2(20, -725), new Vector2(410, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(card.transform, "TUTOR  /  OFFLINE STAGING ADAPTER ONLY", 12, Gold, new Vector2(20, -765), new Vector2(410, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);

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
            TextAt(parent, "DT / ET   /   BLACK HOLE OBSERVATORY", 23, White, new Vector2(28, -22), new Vector2(760, 36), TextAnchor.MiddleLeft, Anchor.TopLeft);
            observationEventStatus = TextAt(parent, "", 13, Gold, new Vector2(28, -61), new Vector2(880, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "GOAL  Explain why a closer binary has a higher gravitational-wave frequency.", 14, White, new Vector2(28, -89), new Vector2(1050, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);
            observationCameraStatus = TextAt(parent, "", 13, Muted, new Vector2(28, -117), new Vector2(1000, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);
            var actions = Block(parent, "Observation actions", new Color(0, 0, 0, 0), Anchor.TopRight, new Vector2(-24, -24), new Vector2(430, 40));
            actions.GetComponent<Image>().raycastTarget = false;
            ButtonAt(actions.transform, "LEARNING / H", Vector2.zero, new Vector2(160, 40), Cyan, () => SetLearningVisible(true));
            ButtonAt(actions.transform, "DIM / B", new Vector2(170, 0), new Vector2(120, 40), Cyan, ToggleDimView);
            ButtonAt(actions.transform, "EXIT", new Vector2(300, 0), new Vector2(130, 40), Gold, ExitExperience);

            var navigation = Block(parent, "Observation controls", new Color(0.06f, 0.10f, 0.18f, 0.92f), Anchor.BottomLeft, new Vector2(24, 24), new Vector2(1052, 132));
            ButtonAt(navigation.transform, "OVERVIEW / F", new Vector2(14, -12), new Vector2(138, 36), Cyan, () => observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview));
            ButtonAt(navigation.transform, "ABOVE", new Vector2(160, -12), new Vector2(100, 36), Cyan, () => observer.SetPreset(ExhibitCameraRig.ViewPreset.Above));
            ButtonAt(navigation.transform, "SIDE", new Vector2(268, -12), new Vector2(100, 36), Cyan, () => observer.SetPreset(ExhibitCameraRig.ViewPreset.Side));
            ButtonAt(navigation.transform, "FOCUS A", new Vector2(376, -12), new Vector2(116, 36), Cyan, () => observer.Focus(ExhibitCameraRig.ObservationTarget.BodyA));
            ButtonAt(navigation.transform, "FOCUS B", new Vector2(500, -12), new Vector2(116, 36), Violet, () => observer.Focus(ExhibitCameraRig.ObservationTarget.BodyB));
            ButtonAt(navigation.transform, "ORBIT / FLY / C", new Vector2(624, -12), new Vector2(174, 36), Gold, observer.ToggleFlight);
            ButtonAt(navigation.transform, "ZOOM +", new Vector2(806, -12), new Vector2(110, 36), Cyan, () => observer.Zoom(1));
            ButtonAt(navigation.transform, "ZOOM -", new Vector2(924, -12), new Vector2(110, 36), Cyan, () => observer.Zoom(-1));
            TextAt(navigation.transform, "Right-drag: orbit / look   |   Wheel: zoom   |   WASD: move   |   Q/E: height   |   Shift: faster   |   Middle-drag: pan", 13, Muted, new Vector2(14, -53), new Vector2(1020, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(navigation.transform, "PAUSE / SPACE", new Vector2(14, -87), new Vector2(148, 32), Cyan, TogglePlayback);
            ButtonAt(navigation.transform, "RESET / R", new Vector2(170, -87), new Vector2(116, 32), Violet, ResetPlayback);
            observationSeek = SliderAt(navigation.transform, new Vector2(304, -91), new Vector2(528, 22), 0, 1, 0, SeekStoryboard);
            observationTime = TextAt(navigation.transform, "", 14, White, new Vector2(852, -84), new Vector2(180, 36), TextAnchor.MiddleLeft, Anchor.TopLeft);
        }

        public void SetLearningVisible(bool visible)
        {
            learningVisible = visible;
            learningOverlay.SetActive(visible);
            observationOverlay.SetActive(!visible);
            observer.SetLearningViewport(visible);
        }

        private void SeekStoryboard(float value)
        {
            visualProgress = Mathf.Clamp01(value);
            visualPhase = visualProgress * Mathf.PI * 8f;
            playing = false;
            seekSlider.SetValueWithoutNotify(visualProgress);
            if (observationSeek != null) observationSeek.SetValueWithoutNotify(visualProgress);
            transportText.text = "Illustrative storyboard seek (paused)";
        }

        private void DrawGraph(Transform parent)
        {
            TextAt(parent, "frequency", 12, Muted, new Vector2(15, -38), new Vector2(90, 18), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "story time  ->", 12, Muted, new Vector2(270, -109), new Vector2(120, 18), TextAnchor.MiddleLeft, Anchor.TopLeft);
            Block(parent, "Illustrative horizontal axis", Hex("#4D617F"), Anchor.TopLeft, new Vector2(102, -106), new Vector2(270, 1)).GetComponent<Image>().raycastTarget = false;
            Block(parent, "Illustrative vertical axis", Hex("#4D617F"), Anchor.TopLeft, new Vector2(102, -43), new Vector2(1, 63)).GetComponent<Image>().raycastTarget = false;
            var points = new Vector2[20];
            for (var i = 0; i < points.Length; i++)
            {
                var t = i / (float)(points.Length - 1);
                points[i] = new Vector2(102 + t * 270, -105 + 54 * Mathf.Pow(t, 3));
            }
            for (var i = 0; i < points.Length - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var line = Block(parent, "Chirp preview segment", Violet, Anchor.TopLeft, a, new Vector2(Vector2.Distance(a, b), 3));
                var rect = line.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0, 0.5f);
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            }
            graphCursor = Block(parent, "Illustrative story-position cursor", Cyan, Anchor.TopLeft, new Vector2(102, -106), new Vector2(2, 54)).GetComponent<RectTransform>();
            graphCursor.pivot = new Vector2(0.5f, 0);
            TextAt(parent, "No live data or Hz values yet", 12, Gold, new Vector2(108, -38), new Vector2(265, 20), TextAnchor.MiddleRight, Anchor.TopLeft);
        }

        private void SetScenario(int value)
        {
            scenario = value;
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
            if (transportText != null) transportText.text = "Illustrative playback reset for comparison";
        }

        private void ApplyPendingSeparation()
        {
            initialSeparation = pendingSeparation;
            visualProgress = 0;
            visualPhase = 0;
            playing = false;
            seekSlider.SetValueWithoutNotify(0);
            separationText.text = $"Current start: {initialSeparation:0} GM/c²  (visual scale only)";
            transportText.text = "New visual starting point applied; paused";
        }

        private void TogglePlayback()
        {
            if (visualProgress >= 1f) ResetPlayback();
            playing = !playing;
            transportText.text = playing ? "Illustrative playback running" : "Illustrative playback paused";
        }

        private void ResetPlayback()
        {
            visualProgress = 0;
            visualPhase = 0;
            seekSlider.SetValueWithoutNotify(0);
            playing = false;
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

        private void ExitExperience()
        {
            playing = false;
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
                transportText.text = "Paused with Esc; Space resumes, Exit closes the app";
            }
            if (keyboard.rKey.wasPressedThisFrame) ResetPlayback();
            if (keyboard.bKey.wasPressedThisFrame) ToggleDimView();
            if (keyboard.hKey.wasPressedThisFrame) SetLearningVisible(!learningVisible);
            if (keyboard.digit1Key.wasPressedThisFrame) SetLayer(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SetLayer(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SetLayer(2);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        public void SetQualityCheckView(int layer, float progress)
        {
            SetLearningVisible(true);
            SetLayer(Mathf.Clamp(layer, 0, 2));
            visualProgress = Mathf.Clamp01(progress);
            visualPhase = visualProgress * Mathf.PI * 8f;
            seekSlider.SetValueWithoutNotify(visualProgress);
            playing = false;
            transportText.text = "Development visual check; illustrative storyboard paused";
        }
#endif

        private void SetLayer(int layer)
        {
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

        private void ButtonAt(Transform parent, string label, Vector2 offset, Vector2 size, Color accent, Action action)
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
