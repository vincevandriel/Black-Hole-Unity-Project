using System;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>
    /// World-space exhibit cards and a wrist-mounted control canvas. No screen-space control dock.
    /// All playback commands go through the same provider used by the main learning controls.
    /// </summary>
    public sealed class PartnerLessonHud : MonoBehaviour
    {
        private static readonly Color Accent = new Color(0.37f, 0.88f, 0.83f);
        private static readonly Color Warning = new Color(0.95f, 0.70f, 0.36f);
        private static readonly Color White = new Color(0.9f, 0.94f, 0.97f);
        private static readonly Color Muted = new Color(0.62f, 0.70f, 0.78f);
        private static readonly Color Panel = new Color(0.07f, 0.10f, 0.15f, 0.94f);
        private ILessonHudProvider provider;
        private Font font;
        private Sprite rounded;
        private Texture2D roundedTexture;
        private Text separation, orbits, frequency, remaining, playLabel, speedLabel, tutor, status;
        private Text visitorClock, referenceClock, clockDifference, clockState;
        private ObserverPhysicsBridge observerPhysics;
        private StellarSourceField stellarField;
        private Text bendingStatus, lensButton, rayButton;
        private ExhibitCameraRig observerRig;
        private GameObject wristControls, inputOptions;
        private Slider lookSlider, scrollSlider;
        private Text lookValue, scrollValue, separationLabel, timeLabel, honestyLabel, signalTitle, signalSubtitle, signalLegend;
        private bool lastPhysicalMode;
        private long lastScenarioVersion = -1;
        public void InvalidateNarration() { lastStage = -1; lastScenarioVersion = -1; }
        public bool OptionsOpen => inputOptions != null && inputOptions.activeSelf;
        private HudSignalGraphic signal;
        private HudProgressGraphic ring;
        private readonly Image[] stageChips = new Image[3];
        private int lastStage = -1;
        private readonly System.Collections.Generic.List<GameObject> informationCards = new System.Collections.Generic.List<GameObject>();
        public VirtualWristRig WristRig { get; private set; }

        public bool Visible => gameObject.activeSelf;

        public void Initialize(ILessonHudProvider source, Font typeface, ExhibitCameraRig observer, Action dim, Action exit,
            StellarSourceField stars, Action align)
        {
            provider = source;
            stellarField = stars;
            observerRig = observer;
            observerPhysics = observer.GetComponent<ObserverPhysicsBridge>();
            font = typeface;
            rounded = CreateRoundedSprite();
            var readout = WorldCard("Partner readout card", new Vector3(-7.3f, 1.2f, 0), new Vector2(266, 354));
            Label(readout, "LESSON READOUTS", 13, Accent, 18, 12, 230, 24);
            separationLabel = Label(readout, "SEPARATION (VISUAL)", 12, Muted, 18, 48, 230, 20);
            separation = Label(readout, "", 29, White, 18, 70, 230, 36);
            Label(readout, "ORBITS PER SECOND", 12, Muted, 18, 113, 230, 20);
            orbits = Label(readout, "", 24, White, 18, 136, 230, 34);
            Label(readout, "WAVE FREQUENCY", 12, Muted, 18, 177, 230, 20);
            frequency = Label(readout, "", 24, Accent, 18, 200, 230, 34);
            timeLabel = Label(readout, "PLAYBACK LEFT / NOT PHYSICAL TIME", 11, Muted, 18, 241, 230, 20);
            remaining = Label(readout, "", 26, White, 18, 264, 230, 34);
            var honesty = Box(readout, "Readout honesty pill", new Vector2(14, -310), new Vector2(238, 30), new Color(1, 1, 1, 0.07f));
            honestyLabel = Label(honesty, "No orbital physics · approximate optics", 12, Warning, 4, 2, 230, 26, TextAnchor.MiddleCenter);

            var chirp = WorldCard("Partner chirp card", new Vector3(1.3f, 2.1f, 0), new Vector2(400, 216));
            signalTitle = Label(chirp, "Detector-like signal, not a picture", 17, White, 18, 12, 364, 30);
            signalSubtitle = Label(chirp, "QUALITATIVE CHIRP · NO Hz OR STRAIN DATA", 11, Warning, 18, 47, 364, 23);
            var graph = new GameObject("Illustrative chirp waveform", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudSignalGraphic));
            graph.transform.SetParent(chirp, false);
            Place(graph.GetComponent<RectTransform>(), new Vector2(18, -79), new Vector2(364, 92));
            signal = graph.GetComponent<HudSignalGraphic>();
            signal.color = Accent;
            signal.raycastTarget = false;
            signalLegend = Label(chirp, "Height scaled to fit · illustrative waveform", 11, Muted, 18, 176, 300, 26);
            Label(chirp, "time →", 12, Muted, 317, 176, 65, 26, TextAnchor.MiddleRight);

            var bubble = WorldCard("Partner tutor bubble", new Vector3(1.3f, 0.8f, 0), new Vector2(400, 172));
            var glow = Box(bubble, "Tutor orb glow", new Vector2(16, -14), new Vector2(38, 38), new Color(Accent.r, Accent.g, Accent.b, 0.18f));
            Box(glow, "Tutor orb core", new Vector2(9, -9), new Vector2(20, 20), Accent);
            Label(bubble, "OFFLINE STAGING GUIDE / NOT AI", 12, Warning, 66, 14, 315, 26);
            tutor = Label(bubble, "", 16, White, 18, 55, 364, 70, TextAnchor.UpperLeft);
            Button(bubble, "PREDICT / LEARN", 18, 132, 165, 28, () => { provider.SetLearningVisible(true); observer.FocusLearning(); });
            Label(bubble, "Optional · H opens all layers", 11, Muted, 196, 133, 186, 26);

            var clocks = WorldCard("Observer clock comparison", new Vector3(4.0f, 2.1f, 0), new Vector2(350, 254));
            Label(clocks, "TIME DILATION / FUTURE PHYSICS", 14, Accent, 18, 12, 314, 25);
            Label(clocks, "VISITOR PROPER TIME", 11, Muted, 18, 43, 314, 20);
            visitorClock = Label(clocks, "Not connected", 23, White, 18, 64, 314, 32);
            Label(clocks, "IDEAL FAR-AWAY REFERENCE CLOCK", 11, Muted, 18, 103, 314, 20);
            referenceClock = Label(clocks, "Not connected", 23, White, 18, 124, 314, 32);
            clockDifference = Label(clocks, "Difference: unavailable", 14, Accent, 18, 168, 314, 25);
            clockState = Label(clocks, "", 12, Warning, 18, 204, 314, 39);

            var bending = WorldCard("Approximate bending card", new Vector3(-7.3f, -1.25f, 0), new Vector2(350, 190));
            Label(bending, "LIGHT BENDING / APPROXIMATE", 14, Accent, 18, 12, 314, 25);
            bendingStatus = Label(bending, "", 13, Warning, 18, 45, 314, 43);
            Label(bending, "J aligns a fixed star behind a marker. Move to split/stretch its images. G shows thin-lens ray guides; not GR paths.",
                13, White, 18, 96, 314, 75, TextAnchor.UpperLeft);

            var wristCanvas = WorldCanvas("Wrist control canvas", new Vector2(404, 376));
            var wrist = Card(wristCanvas.transform, "Partner wrist controls", Vector2.zero, new Vector2(400, 372), false);
            wristControls = wrist.parent.gameObject;
            Label(wrist, "WRIST CONTROLS / V TO LOWER", 12, Accent, 18, 10, 364, 25);
            var ringObject = new GameObject("Partner playback progress ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudProgressGraphic));
            ringObject.transform.SetParent(wrist, false);
            Place(ringObject.GetComponent<RectTransform>(), new Vector2(142, -40), new Vector2(116, 116));
            ring = ringObject.GetComponent<HudProgressGraphic>();
            ring.color = Accent;
            ring.raycastTarget = false;
            Button(wrist, "REPLAY", 24, 65, 70, 66, provider.ResetPlayback);
            playLabel = Button(wrist, "PAUSE", 162, 60, 76, 76, provider.TogglePlayback).GetComponentInChildren<Text>();
            speedLabel = Button(wrist, "1×", 306, 65, 70, 66, CycleSpeed).GetComponentInChildren<Text>();
            speedLabel.fontSize = 24;
            var names = new[] { "Start", "Halfway", "End" };
            var starts = new[] { 0f, 0.5f, 1f };
            for (var i = 0; i < names.Length; i++)
            {
                var progress = starts[i];
                stageChips[i] = Button(wrist, names[i], 18 + i * 124, 168, 116, 34, () => provider.SeekStoryboard(progress)).GetComponent<Image>();
            }
            Button(wrist, "EXHIBIT / F", 18, 212, 116, 34, () => { provider.SetLearningVisible(false); observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview); });
            Button(wrist, "INFO / T", 142, 212, 116, 34, () => { provider.SetLearningVisible(false); observer.FocusInformation(); });
            Button(wrist, "LEARN / H", 266, 212, 116, 34, () => { provider.SetLearningVisible(true); observer.FocusLearning(); });
            Button(wrist, "DIM / B", 18, 254, 116, 34, dim);
            Button(wrist, "ORBIT / C", 142, 254, 116, 34, observer.ToggleFlight);
            Button(wrist, "EXIT", 266, 254, 116, 34, exit);
            lensButton = Button(wrist, "LENS / L", 18, 296, 116, 34, stars.ToggleLensing).GetComponentInChildren<Text>();
            rayButton = Button(wrist, "RAYS / G", 142, 296, 116, 34, stars.ToggleRayPaths).GetComponentInChildren<Text>();
            Button(wrist, "ALIGN / J", 266, 296, 116, 34, align);
            Button(wrist, "OPTIONS / O", 18, 338, 170, 26, ToggleOptions);
            status = Label(wrist, "", 11, Muted, 196, 338, 186, 26, TextAnchor.MiddleCenter);
            BuildInputOptions(wristCanvas.transform);
            WristRig = new GameObject("Virtual visitor wrist").AddComponent<VirtualWristRig>();
            WristRig.transform.SetParent(transform, false);
            WristRig.Initialize(Camera.main, wristCanvas);
            Refresh();
        }

        private void BuildInputOptions(Transform parent)
        {
            var options = Card(parent, "Observer input options", Vector2.zero, new Vector2(400, 372), false);
            inputOptions = options.parent.gameObject;
            Label(options, "INPUT OPTIONS / O", 14, Accent, 18, 12, 260, 28);
            Button(options, "BACK", 294, 12, 88, 30, ToggleOptions);
            Label(options, "MOUSE LOOK", 14, White, 18, 65, 230, 25);
            lookValue = Label(options, "", 14, Accent, 258, 65, 124, 25, TextAnchor.MiddleRight);
            Label(options, "Right-drag head / orbit look", 12, Muted, 18, 93, 364, 22);
            lookSlider = SensitivitySlider(options, "Look sensitivity slider", 125,
                ObserverInputSettings.MinimumLook, ObserverInputSettings.MaximumLook, observerRig.LookSensitivity, observerRig.SetLookSensitivity);
            Label(options, "SCROLL / CLOSER–FARTHER", 14, White, 18, 167, 270, 25);
            scrollValue = Label(options, "", 14, Accent, 292, 167, 90, 25, TextAnchor.MiddleRight);
            Label(options, "Visitor movement / orbit distance", 12, Muted, 18, 195, 364, 22);
            scrollSlider = SensitivitySlider(options, "Scroll sensitivity slider", 227,
                ObserverInputSettings.MinimumScroll, ObserverInputSettings.MaximumScroll, observerRig.ScrollSensitivity, observerRig.SetScrollSensitivity);
            Label(options, "Scroll default: 3× the original speed.\nEmulated inputs only; not tracked head motion.",
                12, Muted, 18, 268, 364, 40, TextAnchor.UpperLeft);
            Button(options, "RESET DEFAULTS", 18, 316, 180, 34, observerRig.ResetInputSensitivities);
            Label(options, "Saved on this computer", 11, Muted, 209, 316, 173, 34, TextAnchor.MiddleCenter);
            inputOptions.SetActive(false);
        }

        private Slider SensitivitySlider(Transform parent, string name, float y, float minimum, float maximum,
            float value, Action<float> change)
        {
            // A full-height raycast track is easier to acquire on the small wrist surface.
            var root = Box(parent, name, new Vector2(18, -y), new Vector2(364, 28), new Color(1, 1, 1, 0.12f));
            var fill = Box(root, "Fill", Vector2.zero, new Vector2(364, 28), new Color(Accent.r, Accent.g, Accent.b, 0.45f));
            var handleArea = new GameObject("Handle area", typeof(RectTransform));
            handleArea.transform.SetParent(root, false);
            Place(handleArea.GetComponent<RectTransform>(), new Vector2(8, 0), new Vector2(348, 28));
            var handle = Box(handleArea.transform, "Handle", Vector2.zero, new Vector2(16, 28), Accent);
            ((RectTransform)fill).sizeDelta = Vector2.zero;
            ((RectTransform)handle).sizeDelta = new Vector2(16, 0);
            ((RectTransform)handle).pivot = new Vector2(0.5f, 0.5f);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = (RectTransform)fill;
            slider.handleRect = (RectTransform)handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => change(v));
            return slider;
        }

        public void ToggleOptions()
        {
            var open = !OptionsOpen;
            SetVisible(true);
            inputOptions.SetActive(open);
            wristControls.SetActive(!open);
            if (open) WristRig.SetRaised(true, true);
            else observerRig.SaveInputSettings();
            Refresh();
        }

        public void SetLearningMode(bool learning)
        {
            foreach (var card in informationCards) card.SetActive(!learning);
        }

        private Canvas WorldCanvas(string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            root.GetComponent<RectTransform>().sizeDelta = size;
            return canvas;
        }

        private Transform WorldCard(string name, Vector3 location, Vector2 size)
        {
            var canvas = WorldCanvas(name + " world panel", size + Vector2.one * 4);
            canvas.transform.position = location;
            canvas.transform.localScale = Vector3.one * 0.006f;
            informationCards.Add(canvas.gameObject);
            return Card(canvas.transform, name, Vector2.zero, size, false);
        }

        public void SetVisible(bool value)
        {
            gameObject.SetActive(value);
            if (value && provider != null) Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (provider == null) return;
            var snapshot = provider.CaptureHudSnapshot();
            separationLabel.text = snapshot.PhysicalModel ? "CENTRE SEPARATION / MODEL" : "SEPARATION (VISUAL)";
            timeLabel.text = snapshot.PhysicalModel ? "SOURCE TIME / SECONDS" : "PLAYBACK LEFT / NOT PHYSICAL TIME";
            honestyLabel.text = snapshot.PhysicalModel ? "SI inspiral · optics approximate" : "No orbital physics · approximate optics";
            signalTitle.text = snapshot.PhysicalModel ? "Model GW frequency / Hz" : "Detector-like signal, not a picture";
            signalSubtitle.text = snapshot.PhysicalModel ? "CIRCULAR MODEL / NOT DETECTOR STRAIN" : "QUALITATIVE CHIRP / NO Hz OR STRAIN DATA";
            signal.SetModel(snapshot.Physics?.Model);
            signalLegend.text = snapshot.PhysicalModel ? $"0–{signal.MaximumFrequencyHz:0.0} Hz / {snapshot.Physics.Model.CutoffTimeSeconds:0.0} source s" : "Height scaled to fit / illustrative waveform";
            separation.fontSize = snapshot.ConceptualEndpoint ? 22 : 29;
            separation.text = snapshot.ConceptualEndpoint ? "Conceptual remnant"
                : snapshot.SeparationKm.HasValue ? $"{snapshot.SeparationKm.Value:0} km"
                : $"{snapshot.VisualSeparationRatio * 100f:0}% of start";
            orbits.text = snapshot.OrbitsPerSecond.HasValue ? $"{snapshot.OrbitsPerSecond.Value:0.0}" : "Not connected";
            frequency.text = snapshot.WaveFrequencyHz.HasValue ? $"{snapshot.WaveFrequencyHz.Value:0.0} Hz" : "Not connected";
            remaining.text = snapshot.PhysicalModel ? $"{snapshot.Physics.State.PhysicalTimeSeconds:0.000} s"
                : $"{snapshot.PlaybackSecondsLeft:0.0} s{(snapshot.Playing ? "" : " / paused")}";
            playLabel.text = snapshot.Progress >= 1f ? "PLAY" : snapshot.Playing ? "PAUSE" : "PLAY";
            speedLabel.text = $"{snapshot.PlaybackRate:0.#}×";
            status.text = $"{snapshot.Progress * 100f:0}% · V lowers wrist";
            lookSlider.SetValueWithoutNotify(observerRig.LookSensitivity);
            scrollSlider.SetValueWithoutNotify(observerRig.ScrollSensitivity);
            lookValue.text = $"{observerRig.LookSensitivity:0.00}×";
            scrollValue.text = $"{observerRig.ScrollSensitivity:0.00}×";
            bendingStatus.text = stellarField.OpticalStatus;
            lensButton.text = stellarField.LensingEnabled ? "LENS ON / L" : "LENS OFF / L";
            rayButton.text = stellarField.ShowRayPaths ? "RAYS ON / G" : "RAYS OFF / G";
            if (observerPhysics != null)
            {
                var clocks = observerPhysics.CaptureClocks();
                visitorClock.text = clocks.VisitorProperSeconds.HasValue ? $"{clocks.VisitorProperSeconds.Value:0.000} s" : "Not connected";
                referenceClock.text = clocks.ReferenceProperSeconds.HasValue ? $"{clocks.ReferenceProperSeconds.Value:0.000} s" : "Not connected";
                clockDifference.text = clocks.DifferenceSeconds.HasValue ? $"Visitor − reference: {clocks.DifferenceSeconds.Value:+0.000;-0.000;0.000} s"
                    : "Difference: unavailable";
                clockState.text = clocks.Fidelity == RelativityFidelity.Unavailable
                    ? "Clock physics not connected.\nApproximate bending adds no clock rate."
                    : clocks.Fidelity + " · " + clocks.ModelId;
            }
            signal.SetProgress(snapshot.PhysicalModel ? (float)snapshot.Physics.GraphProgress : snapshot.Progress);
            ring.SetProgress(snapshot.Progress);
            var stage = snapshot.ConceptualEndpoint ? 2 : snapshot.Progress < 0.5f ? 0 : 1;
            for (var i = 0; i < stageChips.Length; i++) stageChips[i].color = i == stage
                ? new Color(Accent.r, Accent.g, Accent.b, 0.35f) : new Color(1, 1, 1, 0.1f);
            // Local scripted narration only. Never sends a snapshot to a network provider.
            var narrationStage = snapshot.ConceptualEndpoint ? 4 : Mathf.Min(3, Mathf.FloorToInt(snapshot.Progress * 4f));
            if (snapshot.PhysicalModel)
            {
                if (narrationStage == lastStage && lastPhysicalMode && lastScenarioVersion == snapshot.Physics.ScenarioVersion) return;
                lastStage = narrationStage;
                lastPhysicalMode = true;
                lastScenarioVersion = snapshot.Physics.ScenarioVersion;
                tutor.text = snapshot.ConceptualEndpoint ? "The circular model reached its cutoff. This marker is a conceptual endpoint; merger and ringdown await numerical replay."
                    : "The SI model drives both bodies and this graph. Smaller separation raises orbital frequency; the dominant circular GW frequency is twice that. Pause or rewind to compare.";
                return;
            }
            if (narrationStage == lastStage && !lastPhysicalMode) return;
            lastPhysicalMode = false;
            lastStage = narrationStage;
            tutor.text = narrationStage == 0 ? "Two illustrated black holes share a centre. Move around the exhibit, or pause to inspect them."
                : narrationStage == 1 ? "A real inspiral loses orbital energy as gravitational waves. These moving markers are only a storyboard."
                : narrationStage == 2 ? "In a physical inspiral, smaller separation means faster orbital motion and a higher wave frequency."
                : narrationStage == 3 ? "The illustrated chirp tightens toward the end. Its height is scaled to fit: it is not measured strain."
                : "This remnant is a conceptual endpoint. Merger and ringdown physics are not simulated. Replay to compare viewpoints.";
        }

        private void CycleSpeed()
        {
            var currentRate = provider.CaptureHudSnapshot().PlaybackRate;
            var current = Mathf.Abs(currentRate);
            provider.SetHudPlaybackRate((currentRate < 0 ? -1 : 1) * (current < 0.99f ? 1f : current < 1.99f ? 2f : 0.5f));
            Refresh();
        }

        private Transform Card(Transform parent, string name, Vector2 position, Vector2 size, bool right, bool bottom = false)
        {
            var edge = Box(parent, name + " edge", position, size + Vector2.one * 4f, new Color(1, 1, 1, 0.16f));
            var rect = (RectTransform)edge;
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1 : 0, bottom ? 0 : 1);
            rect.pivot = rect.anchorMin;
            var card = Box(edge, name, new Vector2(right ? -2 : 2, bottom ? 2 : -2), size, Panel);
            var cardRect = (RectTransform)card;
            cardRect.anchorMin = cardRect.anchorMax = rect.anchorMin;
            cardRect.pivot = rect.pivot;
            return card;
        }

        private Transform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), position, size);
            var image = go.GetComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            return go.transform;
        }

        private Text Label(Transform parent, string text, int size, Color color, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), new Vector2(x, -y), new Vector2(width, height));
            var label = go.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private Transform Button(Transform parent, string text, float x, float y, float width, float height, Action action)
        {
            var root = Box(parent, text + " button", new Vector2(x, -y), new Vector2(width, height), new Color(1, 1, 1, 0.1f));
            var button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1);
            colors.pressedColor = Accent;
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Label(root, text, 14, White, 0, 0, width, height, TextAnchor.MiddleCenter);
            return root;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private Sprite CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 12;
            roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            roundedTexture.name = "Procedural partner HUD rounded corners";
            roundedTexture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var nearest = new Vector2(Mathf.Clamp(x + 0.5f, radius, size - radius), Mathf.Clamp(y + 0.5f, radius, size - radius));
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), nearest);
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance + 0.5f));
                }
            roundedTexture.SetPixels(pixels);
            roundedTexture.Apply();
            return Sprite.Create(roundedTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * radius);
        }

        private void OnDestroy()
        {
            if (rounded != null) Destroy(rounded);
            if (roundedTexture != null) Destroy(roundedTexture);
        }
    }
}
