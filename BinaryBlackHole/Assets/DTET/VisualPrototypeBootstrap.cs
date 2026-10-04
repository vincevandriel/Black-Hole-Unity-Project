using System;
using UnityEngine;
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
        private Text scenarioText;
        private Text separationText;
        private Text playbackText;
        private Text seekText;
        private Text layerBody;
        private Text transportText;
        private Text endpointText;
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
        private bool playing = true;
        private int scenario;
        private Font font;

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
        }

        private void Update()
        {
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

            var radius = Mathf.Lerp(2.45f, 1.05f, visualProgress) * initialSeparation / 30f;
            var center = new Vector3(-3.25f, 0, 0);
            var delta = new Vector3(Mathf.Cos(visualPhase), 0, Mathf.Sin(visualPhase)) * radius;
            bodyA.position = center + delta;
            bodyB.position = center - delta;
            var atEndpoint = visualProgress >= 0.98f;
            bodyA.gameObject.SetActive(!atEndpoint);
            bodyB.gameObject.SetActive(!atEndpoint);
            remnantMarker.SetActive(atEndpoint);
            orbit.enabled = !atEndpoint;
            if (endpointText != null) endpointText.text = atEndpoint
                ? "CONCEPTUAL MERGER / REMNANT: visual placeholder, no merger physics"
                : "MERGER / REMNANT: conceptual endpoint, not simulated here";
            DrawOrbit(radius);
            if (seekText != null) seekText.text = $"Story position  {visualProgress * 100f:0}%";
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
            camera.transform.position = new Vector3(-3.25f, 7.5f, -15.5f);
            camera.transform.LookAt(new Vector3(-3.25f, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
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

            var plane = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plane.name = "Orbital plane reference (not spacetime)";
            plane.transform.position = new Vector3(-3.25f, -0.15f, 0);
            plane.transform.localScale = new Vector3(5.3f, 0.012f, 5.3f);
            plane.GetComponent<Renderer>().material = Material("Orbital plane", Hex("#13243B"), false);
            Destroy(plane.GetComponent<Collider>());

            var orbitObject = new GameObject("Initial orbit / storyboard guide");
            orbit = orbitObject.AddComponent<LineRenderer>();
            orbit.useWorldSpace = true;
            orbit.loop = true;
            orbit.positionCount = 96;
            orbit.widthMultiplier = 0.025f;
            orbit.material = Material("Orbit guide", Cyan, true);
            orbit.startColor = Cyan;
            orbit.endColor = Cyan;

            bodyA = CreateBody("Body A - illustrative marker", Cyan).transform;
            bodyB = CreateBody("Body B - illustrative marker", Violet).transform;
            remnantMarker = CreateBody("Conceptual remnant - not simulated", Gold);
            remnantMarker.transform.position = new Vector3(-3.25f, 0, 0);
            remnantMarker.transform.localScale = Vector3.one * 1.25f;
            remnantMarker.SetActive(false);
            for (var i = 0; i < 36; i++)
            {
                var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = "Background star " + i;
                var x = Mathf.Sin(i * 12.9898f) * 9f - 3f;
                var y = Mathf.Cos(i * 5.398f) * 5f + 2f;
                var z = 6f + Mathf.Sin(i * 3.71f) * 2f;
                star.transform.position = new Vector3(x, y, z);
                star.transform.localScale = Vector3.one * (0.018f + (i % 4) * 0.009f);
                star.GetComponent<Renderer>().material = Material("Star", i % 3 == 0 ? Cyan : White, true);
                Destroy(star.GetComponent<Collider>());
            }
        }

        private GameObject CreateBody(string name, Color accent)
        {
            var root = new GameObject(name);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Dark central marker";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.85f;
            core.GetComponent<Renderer>().material = Material("Dark marker", Hex("#02040B"), false);
            Destroy(core.GetComponent<Collider>());

            var halo = new GameObject("Illustrative luminous ring");
            halo.transform.SetParent(root.transform, false);
            var line = halo.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 64;
            line.widthMultiplier = 0.075f;
            line.material = Material("Luminous ring", accent, true);
            for (var i = 0; i < 64; i++)
            {
                var theta = i * Mathf.PI * 2f / 64f;
                line.SetPosition(i, new Vector3(Mathf.Cos(theta) * 0.67f, 0.08f, Mathf.Sin(theta) * 0.67f));
            }
            return root;
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

            var eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(canvasObject.transform);
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            TextAt(canvasObject.transform, "DT / ET", 34, White, new Vector2(42, -36), new Vector2(620, 50), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(canvasObject.transform, "BINARY BLACK HOLES  /  EINSTEIN TELESCOPE", 15, Cyan, new Vector2(42, -84), new Vector2(720, 30), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(canvasObject.transform, "Two bodies. One evolving story.", 21, White, new Vector2(42, -125), new Vector2(700, 45), TextAnchor.MiddleLeft, Anchor.TopLeft);

            var banner = Block(canvasObject.transform, "Prototype status", Hex("#273148"), Anchor.TopLeft, new Vector2(42, -186), new Vector2(475, 40));
            TextAt(banner.transform, "VISUAL STAGING  |  NO LIVE PHYSICS", 15, Gold, Vector2.zero, new Vector2(455, 36), TextAnchor.MiddleCenter, Anchor.Center);
            TextAt(canvasObject.transform, "Body markers, ring and motion are conceptual illustrations.", 15, Muted, new Vector2(42, -233), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(canvasObject.transform, "Not a view of an event horizon or curved spacetime.", 15, Muted, new Vector2(42, -261), new Vector2(720, 34), TextAnchor.MiddleLeft, Anchor.TopLeft);

            var card = Block(canvasObject.transform, "Learning controls", Panel, Anchor.TopRight, new Vector2(-40, -38), new Vector2(450, 812));
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
            layerBody = TextAt(modelGroup.transform, "Equal-mass, circular binary. The future physics stage uses a leading-order inspiral approximation. The merger remains explicitly conceptual.", 15, White, new Vector2(15, -40), new Vector2(380, 82), TextAnchor.UpperLeft, Anchor.TopLeft);

            TextAt(card.transform, "COMPARE TWO STARTING POINTS", 14, Muted, new Vector2(20, -305), new Vector2(410, 26), TextAnchor.MiddleLeft, Anchor.TopLeft);
            ButtonAt(card.transform, "A  /  CLOSER", new Vector2(20, -335), new Vector2(194, 44), Cyan, () => SetScenario(0));
            ButtonAt(card.transform, "B  /  WIDER", new Vector2(234, -335), new Vector2(194, 44), Violet, () => SetScenario(1));
            scenarioText = TextAt(card.transform, "", 15, White, new Vector2(20, -384), new Vector2(410, 28), TextAnchor.MiddleLeft, Anchor.TopLeft);

            TextAt(card.transform, "INITIAL SEPARATION  /  VISUAL RANGE", 14, Muted, new Vector2(20, -426), new Vector2(410, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);
            separationSlider = SliderAt(card.transform, new Vector2(20, -458), new Vector2(410, 24), 25, 50, 30, value =>
            {
                initialSeparation = value;
                separationText.text = $"{value:0} GM/c²  (bounded preview; not metres to scale)";
            });
            separationText = TextAt(card.transform, "", 14, Cyan, new Vector2(20, -485), new Vector2(410, 25), TextAnchor.MiddleLeft, Anchor.TopLeft);

            ButtonAt(card.transform, "PAUSE / PLAY", new Vector2(20, -532), new Vector2(194, 42), Cyan, () =>
            {
                playing = !playing;
                transportText.text = playing ? "Illustrative playback running" : "Illustrative playback paused";
            });
            ButtonAt(card.transform, "RESET", new Vector2(234, -532), new Vector2(194, 42), Violet, () =>
            {
                visualProgress = 0;
                visualPhase = 0;
                seekSlider.SetValueWithoutNotify(0);
                playing = false;
                transportText.text = "Illustrative playback reset and paused";
            });
            transportText = TextAt(card.transform, "Illustrative playback running", 13, Muted, new Vector2(20, -577), new Vector2(410, 24), TextAnchor.MiddleLeft, Anchor.TopLeft);
            seekSlider = SliderAt(card.transform, new Vector2(20, -610), new Vector2(410, 24), 0, 1, 0, value =>
            {
                visualProgress = value;
                visualPhase = value * Mathf.PI * 8f;
                playing = false;
                transportText.text = "Illustrative storyboard seek (paused)";
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

            endpointText = TextAt(canvasObject.transform, "MERGER / REMNANT: conceptual endpoint, not simulated here", 14, Gold, new Vector2(42, 38), new Vector2(900, 28), TextAnchor.MiddleLeft, Anchor.BottomLeft);
            separationText.text = "30 GM/c²  (bounded preview; not metres to scale)";
            playbackText.text = "1.0x visual rate  /  does not change physical frequency";
        }

        private void DrawGraph(Transform parent)
        {
            TextAt(parent, "frequency", 12, Muted, new Vector2(15, -38), new Vector2(90, 18), TextAnchor.MiddleLeft, Anchor.TopLeft);
            TextAt(parent, "story time  ->", 12, Muted, new Vector2(270, -109), new Vector2(120, 18), TextAnchor.MiddleLeft, Anchor.TopLeft);
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
                rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg);
            }
            TextAt(parent, "No live data or Hz values yet", 12, Gold, new Vector2(108, -38), new Vector2(265, 20), TextAnchor.MiddleRight, Anchor.TopLeft);
        }

        private void SetScenario(int value)
        {
            scenario = value;
            initialSeparation = value == 0 ? 30 : 45;
            if (separationSlider != null) separationSlider.SetValueWithoutNotify(initialSeparation);
            if (separationText != null) separationText.text = $"{initialSeparation:0} GM/c²  (bounded preview; not metres to scale)";
            if (scenarioText != null) scenarioText.text = value == 0
                ? "Scenario A: 30 + 30 solar masses, closer start"
                : "Scenario B: 30 + 30 solar masses, wider start";
            visualProgress = 0;
            visualPhase = 0;
            if (seekSlider != null) seekSlider.SetValueWithoutNotify(0);
            if (transportText != null) transportText.text = "Illustrative playback reset for comparison";
        }

        private void SetLayer(int layer)
        {
            storyGroup.SetActive(layer == 0);
            graphGroup.SetActive(layer == 1);
            modelGroup.SetActive(layer == 2);
        }

        private static Material Material(string name, Color color, bool unlit)
        {
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
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
            var handle = Block(root.transform, "Handle", White, Anchor.Center, Vector2.zero, new Vector2(16, 28)).GetComponent<RectTransform>();
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
