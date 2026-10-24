using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    public enum InteractiveOpticalStyle { Off, Subtle, Enhanced }

    // Display values from one scenario generation.
    public struct InteractiveWristState
    {
        public long Generation;
        public bool ReplayMode, Playing, GasEnabled, SamplesEnabled, FrequencyAvailable, SignalAvailable;
        public double Progress, SourceTimeSeconds, DurationSeconds, PlaybackRate;
        public double Mass1Solar, Mass2Solar, SeparationRg, SeparationKm, FrequencyHz, Signal;
        public double CameraDistanceRg, WaveformAxisMaximum, FrequencyAxisMaximum;
        public string ScenarioLabel, SignalLabel, SignalUnitLabel, Status, StageLabel, ValidationMessage;
        public InteractiveOpticalStyle OpticalStyle;
        public float LensGain, WaveGain;
    }

    // Route display commands to the shared exhibit session.
    public interface IInteractiveExhibitPresenter
    {
        InteractiveWristState CaptureWristState();
        double? SampleWristGraph(double normalizedTime, bool frequency);
        double? SampleWristEnvelope(double normalizedTime);
        void SelectWristScenario(bool replay);
        void ApplyWristExperiment(double mass1Solar, double mass2Solar, double separationRg);
        void ResetWristScenario();
        void SetWristPlaying(bool playing);
        void SeekWrist(double normalizedTime);
        void SetWristRate(double rate);
        void SetWristOptics(InteractiveOpticalStyle style, float lensGain, float waveGain);
        void SetWristGas(bool enabled);
        void SetWristSamples(bool enabled);
        void SetWristView(int preset);
        void LeaveWristExhibit();
    }

    public struct GeneratedFluidUiState
    {
        public bool Supported, Generating, Ready, Selected, Covered;
        public double Progress;
        public string Status, Coverage, Metrics;
    }

    // Optional fluid controls.
    public interface IGeneratedFluidPresenter
    {
        GeneratedFluidUiState CaptureGeneratedFluidState();
        void StartGeneratedFluid();
        void CancelGeneratedFluid();
        void UseGeneratedFluid(bool value);
        void SeekGeneratedFluidStart();
    }

    // Desktop wrist display drawn at interface resolution; its pose is emulated.
    [DefaultExecutionOrder(200)]
    public sealed class InteractiveWristDisplay : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(.35f, .91f, 1f);
        private static readonly Color White = new Color(.90f, .96f, 1f);
        private static readonly Color Muted = new Color(.54f, .68f, .78f);
        private static readonly Color Gold = new Color(1f, .76f, .39f);
        private IInteractiveExhibitPresenter presenter;
        private Camera interfaceCamera;
        private Canvas canvas;
        private Font font;
        private Sprite rounded;
        private Texture2D roundedTexture;
        private RectTransform panel, suitRect;
        private CanvasGroup projectionGroup;
        private InteractiveSuitGraphic suitGraphic;
        private InteractiveWristPlotGraphic plot;
        private Slider massA, massB, separation, timeline, lensGain, waveGain;
        private Button replayButton, customButton, applyButton, signalTab, frequencyTab;
        private Text scenario, status, stateLine, measurements, signalTitle, signalUnits, graphValues;
        private Text massAText, massBText, separationText, modeHint, playText, rateText, opticalText;
        private Text gasText, samplesText, pinText, motionText, openText, validation, timeText;
        private readonly Button[] opticsButtons = new Button[3];
        private InteractiveWristState lastState;
        private long plottedGeneration = long.MinValue;
        private bool frequencyPlot, refreshing, pendingEdits, hasState;
        private float reveal;
        private string localValidation;
        private float nextTextRefresh;
        private RectTransform fluidPanel;
        private Text fluidStatus, fluidCoverage, fluidMetrics;
        private Button fluidSolve, fluidCancel, fluidGenerated, fluidIllustrative, fluidStart;
        private IGeneratedFluidPresenter fluidPresenter;

        public bool Raised { get; private set; } = true;
        public bool Pinned { get; private set; }
        public bool ReducedMotion { get; private set; }
        public float Reveal => reveal;
        public float ProjectionProgress => reveal;
        public string TitleText => scenario == null ? "" : scenario.text;
        public string StatusText => status == null ? "" : status.text;
        public bool FrequencyPlot => frequencyPlot;
        public int DenseCycleBins => plot == null ? 0 : plot.DenseCycleBins;
        public bool HasDenseCycles => DenseCycleBins > 0;
        public double PresentedSourceTime => lastState.SourceTimeSeconds;
        public long PresentedGeneration => lastState.Generation;
        public string ValidationText => validation == null ? localValidation ?? "" : validation.text;
        public float PendingMass1 => massA == null ? 35 : massA.value;
        public float PendingMass2 => massB == null ? 30 : massB.value;
        public float PendingSeparationRg => separation == null ? 30 : separation.value;
        public bool InputFocused
        {
            get
            {
                var selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                if (fluidPanel != null && fluidPanel.gameObject.activeInHierarchy
                    && ((Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(fluidPanel, Mouse.current.position.ReadValue(), interfaceCamera))
                    || (selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(fluidPanel)))) return true;
                if (!Raised || reveal < .1f || panel == null) return false;
                if (Mouse.current != null && IsScreenPointOverPanel(Mouse.current.position.ReadValue())) return true;
                return selected != null && selected.activeInHierarchy && selected.transform.IsChildOf(panel);
            }
        }
        public bool IsScreenPointOverPanel(Vector2 screenPoint) => Raised && reveal >= .1f && panel != null
            && RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint, interfaceCamera);

        public void Initialize(Camera presentationCamera, IInteractiveExhibitPresenter source)
        {
            if (presenter != null) throw new InvalidOperationException("The wrist display is already initialized.");
            interfaceCamera = presentationCamera != null ? presentationCamera : throw new ArgumentNullException(nameof(presentationCamera));
            presenter = source ?? throw new ArgumentNullException(nameof(source));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateRoundedSprite();
            var root = new GameObject("Suit hologram / full resolution presentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.layer = 5;
            // Fill the parent HUD; Unity's default 100x100 canvas would offset the instruments.
            var canvasRect = root.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero; canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one; canvasRect.localRotation = Quaternion.identity;
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = interfaceCamera;
            canvas.planeDistance = .08f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 40;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            BuildHeader(root.transform);
            var suit = new GameObject("Illustrated suit cuff and projection", typeof(RectTransform), typeof(CanvasRenderer), typeof(InteractiveSuitGraphic));
            suit.transform.SetParent(root.transform, false); suit.layer = 5;
            suitRect = suit.GetComponent<RectTransform>(); Place(suitRect, 960, 754, 610, 215);
            suitGraphic = suit.GetComponent<InteractiveSuitGraphic>(); suitGraphic.raycastTarget = false;
            BuildPanel(root.transform);
            fluidPresenter = presenter as IGeneratedFluidPresenter;
            if (fluidPresenter != null) BuildFluidPanel(root.transform);
            var open = Button(root.transform, "V  /  WRIST", 1405, 852, 164, 31, () => SetRaised(!Raised));
            openText = open.GetComponentInChildren<Text>();
            SetReducedMotion(PlayerPrefs.GetInt("DTET.Wrist.ReduceMotion", 0) == 1);
            SetRaised(true, true);
            Refresh(true);
        }

        private void BuildHeader(Transform root)
        {
            scenario = TextAt(root, "BINARY OBSERVATORY", 20, White, 36, 112, 835, 32);
            stateLine = TextAt(root, "", 12, Muted, 36, 148, 825, 27);
        }

        private void BuildPanel(Transform root)
        {
            panel = Box(root, "Projected instrument panel", 930, 108, 640, 715, new Color(.018f, .050f, .078f, .97f));
            projectionGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Box(panel, "Top emission edge", 0, 0, 640, 2, Cyan * .85f);
            Box(panel, "Left emission edge", 0, 0, 2, 715, new Color(Cyan.r, Cyan.g, Cyan.b, .25f));
            Box(panel, "Right emission edge", 638, 0, 2, 715, new Color(Cyan.r, Cyan.g, Cyan.b, .25f));
            TextAt(panel, "SUITLINK   /   WAVE LAB", 17, Cyan, 20, 14, 330, 27);
            pinText = Button(panel, "PIN / ENLARGE", 380, 14, 124, 27, () => SetPinned(!Pinned)).GetComponentInChildren<Text>();
            motionText = Button(panel, "MOTION: FULL", 512, 14, 110, 27, () => SetReducedMotion(!ReducedMotion)).GetComponentInChildren<Text>();
            replayButton = Button(panel, "EVENT REPLAY", 20, 52, 181, 31, () => SelectScenario(true));
            customButton = Button(panel, "YOUR EXPERIMENT", 209, 52, 199, 31, () => SelectScenario(false));
            Button(panel, "RESET TIME", 416, 52, 206, 31, ResetScenario);
            measurements = TextAt(panel, "", 14, White, 20, 93, 602, 49);
            signalTab = Button(panel, "WAVEFORM", 20, 148, 130, 27, () => SetFrequencyPlot(false));
            frequencyTab = Button(panel, "FREQUENCY", 158, 148, 130, 27, () => SetFrequencyPlot(true));
            signalTitle = TextAt(panel, "", 11, Muted, 302, 150, 320, 23); signalTitle.alignment = TextAnchor.MiddleRight;
            var graphBox = Box(panel, "Scientific data plotting area", 20, 182, 602, 122, new Color(.008f, .025f, .039f, 1));
            var graph = new GameObject("Generation-pinned graph", typeof(RectTransform), typeof(CanvasRenderer), typeof(InteractiveWristPlotGraphic));
            graph.transform.SetParent(graphBox, false); graph.layer = 5; Place(graph.GetComponent<RectTransform>(), 40, 7, 552, 105);
            plot = graph.GetComponent<InteractiveWristPlotGraphic>(); plot.raycastTarget = false;
            signalUnits = TextAt(graphBox, "", 10, Muted, 5, 5, 200, 15);
            graphValues = TextAt(panel, "", 11, Cyan, 20, 307, 602, 21);
            timeline = Slider(panel, "Recorded / model event time", 20, 334, 602, 16, 0, 1, value =>
            { if (!refreshing) { presenter.SetWristPlaying(false); presenter.SeekWrist(value); Refresh(true); } });
            timeText = TextAt(panel, "", 11, Muted, 20, 354, 602, 19);
            playText = Button(panel, "PLAY", 20, 379, 112, 31, () => presenter.SetWristPlaying(!presenter.CaptureWristState().Playing)).GetComponentInChildren<Text>();
            Button(panel, "REVERSE", 140, 379, 112, 31, () =>
            { var state = presenter.CaptureWristState(); presenter.SetWristRate(-state.PlaybackRate); }).GetComponentInChildren<Text>();
            rateText = Button(panel, "0.05x", 260, 379, 100, 31, CycleRate).GetComponentInChildren<Text>();
            Button(panel, "START", 368, 379, 112, 31, () => { presenter.SetWristPlaying(false); presenter.SeekWrist(0); Refresh(true); });
            Button(panel, "LAUNCHER", 488, 379, 134, 31, () => presenter.LeaveWristExhibit());
            TextAt(panel, "EXPERIMENT INPUTS", 11, Muted, 20, 423, 220, 20);
            modeHint = TextAt(panel, "", 10, Muted, 240, 423, 382, 20); modeHint.alignment = TextAnchor.MiddleRight;
            massAText = TextAt(panel, "M1  /  35.0 solar", 12, White, 20, 446, 290, 21);
            massBText = TextAt(panel, "M2  /  30.0 solar", 12, White, 332, 446, 290, 21);
            massA = Slider(panel, "Primary mass / solar masses", 20, 473, 290, 14, 5, 80, _ => InputsChanged());
            massB = Slider(panel, "Secondary mass / solar masses", 332, 473, 290, 14, 5, 80, _ => InputsChanged());
            massA.SetValueWithoutNotify(35); massB.SetValueWithoutNotify(30);
            separationText = TextAt(panel, "INITIAL SEPARATION  /  30.0 rg", 12, White, 20, 496, 382, 21);
            separation = Slider(panel, "Initial binary separation / total rg", 20, 523, 382, 14, 25, 50, _ => InputsChanged());
            separation.SetValueWithoutNotify(30);
            applyButton = Button(panel, "APPLY & PAUSE", 422, 498, 200, 39, ApplyExperiment);
            TextAt(panel, "LIGHT WARP", 11, Muted, 20, 551, 95, 21);
            var styles = new[] { "OFF", "SUBTLE", "ENHANCED" };
            for (int i = 0; i < 3; i++)
            { int style = i; opticsButtons[i] = Button(panel, styles[i], 124 + i * 112, 548, 104, 28, () => SetOpticalStyle((InteractiveOpticalStyle)style)); }
            opticalText = TextAt(panel, "ILLUSTRATIVE", 9, Gold, 470, 552, 152, 20); opticalText.alignment = TextAnchor.MiddleRight;
            TextAt(panel, "LENS GAIN", 10, Muted, 20, 589, 85, 18);
            lensGain = Slider(panel, "Mass bending visual gain", 111, 592, 190, 12, 0, 1, _ => OpticsChanged());
            TextAt(panel, "WAVE GAIN", 10, Muted, 332, 589, 90, 18);
            waveGain = Slider(panel, "Magnified wave visual gain", 432, 592, 190, 12, 0, 2, _ => OpticsChanged());
            gasText = Button(panel, "GAS ON", 20, 620, 145, 29, () => presenter.SetWristGas(!presenter.CaptureWristState().GasEnabled)).GetComponentInChildren<Text>();
            samplesText = Button(panel, "SAMPLES OFF", 173, 620, 145, 29, () => presenter.SetWristSamples(!presenter.CaptureWristState().SamplesEnabled)).GetComponentInChildren<Text>();
            Button(panel, "ABOVE", 326, 620, 92, 29, () => presenter.SetWristView(1));
            Button(panel, "EDGE", 426, 620, 92, 29, () => presenter.SetWristView(2));
            Button(panel, "BINARY", 526, 620, 96, 29, () => presenter.SetWristView(0));
            validation = TextAt(panel, "", 10, Gold, 20, 656, 602, 23);
            status = TextAt(panel, "", 10, Muted, 20, 682, 602, 22);
        }

        private void BuildFluidPanel(Transform root)
        {
            Button(root, "FLUID LAB  /  SOLVE GAS", 36, 196, 300, 32,
                () => fluidPanel.gameObject.SetActive(!fluidPanel.gameObject.activeSelf));
            fluidPanel = Box(root, "Generated fluid laboratory", 36, 240, 392, 386, new Color(.018f, .050f, .078f, .97f));
            TextAt(fluidPanel, "EXPERIMENTAL 2D FLUID", 15, Cyan, 12, 10, 368, 22);
            TextAt(fluidPanel, "Assumed gas / Newtonian gravity / false colour", 11, Gold, 12, 34, 368, 20);
            fluidSolve = Button(fluidPanel, "SOLVE GAS", 12, 63, 178, 30, () => fluidPresenter.StartGeneratedFluid());
            fluidCancel = Button(fluidPanel, "CANCEL", 202, 63, 178, 30, () => fluidPresenter.CancelGeneratedFluid());
            fluidIllustrative = Button(fluidPanel, "ILLUSTRATIVE GAS", 12, 102, 178, 30, () => fluidPresenter.UseGeneratedFluid(false));
            fluidGenerated = Button(fluidPanel, "GENERATED FLUID", 202, 102, 178, 30, () => fluidPresenter.UseGeneratedFluid(true));
            fluidStart = Button(fluidPanel, "GO TO COVERAGE START", 12, 141, 368, 30, () => fluidPresenter.SeekGeneratedFluidStart());
            fluidStatus = TextAt(fluidPanel, "", 12, White, 12, 181, 368, 60);
            fluidCoverage = TextAt(fluidPanel, "", 11, Gold, 12, 247, 368, 40);
            fluidMetrics = TextAt(fluidPanel, "", 11, Muted, 12, 297, 368, 80);
            fluidPanel.gameObject.SetActive(false);
        }

        private void RefreshFluidPanel()
        {
            if (fluidPresenter == null || fluidPanel == null) return;
            var state = fluidPresenter.CaptureGeneratedFluidState();
            fluidSolve.interactable = state.Supported && !state.Generating;
            fluidCancel.interactable = state.Generating;
            fluidIllustrative.interactable = !state.Generating;
            fluidGenerated.interactable = fluidStart.interactable = state.Ready && !state.Generating;
            fluidStatus.text = state.Generating ? $"SOLVING {100 * state.Progress:0.0}%\n{state.Status}" : state.Status;
            fluidCoverage.text = state.Coverage; fluidMetrics.text = state.Metrics;
            SetSelected(fluidGenerated, state.Selected); SetSelected(fluidIllustrative, !state.Selected);
        }

        public void SetRaised(bool value, bool immediate = false)
        {
            Raised = value;
            if (immediate || ReducedMotion) reveal = value ? 1 : 0;
            UpdatePose();
        }
        public void SetPinned(bool value) { Pinned = value; UpdatePose(); }
        public void SetReducedMotion(bool value)
        {
            ReducedMotion = value;
            PlayerPrefs.SetInt("DTET.Wrist.ReduceMotion", value ? 1 : 0);
            if (value) reveal = Raised ? 1 : 0;
            UpdatePose();
        }
        public void SetFrequencyPlot(bool value)
        { frequencyPlot = value; plottedGeneration = long.MinValue; Refresh(true); }
        public void RefreshNow() => Refresh(true);
        public void SetFluidPanelOpen(bool value)
        { if (fluidPanel != null) { fluidPanel.gameObject.SetActive(value); RefreshFluidPanel(); } }
        public void SetPendingExperiment(float m1, float m2, float initialSeparationRg)
        {
            if (massA == null) return;
            massA.SetValueWithoutNotify(m1); massB.SetValueWithoutNotify(m2); separation.SetValueWithoutNotify(initialSeparationRg);
            InputsChanged();
        }
        public void SelectScenario(bool replay)
        {
            localValidation = null; pendingEdits = false;
            presenter.SelectWristScenario(replay); Refresh(true);
        }
        public void ResetScenario()
        {
            localValidation = null; pendingEdits = false;
            presenter.ResetWristScenario(); Refresh(true);
        }
        public void ApplyExperiment()
        {
            if (lastState.ReplayMode) { localValidation = "Choose YOUR EXPERIMENT to change physical parameters."; Refresh(true); return; }
            float m1 = massA.value, m2 = massB.value, a = separation.value;
            if (Math.Min(m1, m2) / Math.Max(m1, m2) < .1f)
            { localValidation = "Use a mass ratio of at least 0.10. Adjust either mass before applying."; Refresh(true); return; }
            if (m1 < 5 || m1 > 80 || m2 < 5 || m2 > 80 || a < 25 || a > 50)
            { localValidation = "Allowed inputs: 5-80 solar masses each; separation 25-50 total rg."; Refresh(true); return; }
            try
            {
                presenter.ApplyWristExperiment(m1, m2, a);
                localValidation = null; pendingEdits = false; Refresh(true);
            }
            catch (ArgumentException error) { localValidation = error.Message; Refresh(true); }
        }
        public void SetOpticalStyle(InteractiveOpticalStyle style)
        {
            float lens = style == InteractiveOpticalStyle.Off ? 0 : style == InteractiveOpticalStyle.Subtle ? .2f : .35f;
            float wave = style == InteractiveOpticalStyle.Off ? 0 : style == InteractiveOpticalStyle.Subtle ? .45f : 1.3f;
            presenter.SetWristOptics(style, lens, wave); Refresh(true);
        }
        public void SetOpticalGains(float lens, float wave)
        { presenter.SetWristOptics(lastState.OpticalStyle, Mathf.Clamp01(lens), Mathf.Clamp(wave, 0, 2)); Refresh(true); }
        private void InputsChanged()
        { if (refreshing) return; pendingEdits = true; localValidation = null; UpdateInputLabels(); }
        private void OpticsChanged()
        {
            if (refreshing || presenter == null) return;
            var style = lastState.OpticalStyle == InteractiveOpticalStyle.Off ? InteractiveOpticalStyle.Subtle : lastState.OpticalStyle;
            presenter.SetWristOptics(style, lensGain.value, waveGain.value);
        }
        private void CycleRate()
        {
            var state = presenter.CaptureWristState(); double speed = Math.Abs(state.PlaybackRate);
            double next = speed < .049 ? .05 : speed < .099 ? .1 : speed < .199 ? .2
                : speed < .499 ? .5 : speed < .999 ? 1 : speed < 1.999 ? 2 : speed < 3.999 ? 4 : .01;
            presenter.SetWristRate(state.PlaybackRate < 0 ? -next : next);
        }
        private void Update()
        {
            if (presenter == null) return;
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) SetRaised(!Raised);
            float target = Raised ? 1 : 0;
            reveal = ReducedMotion ? target : Mathf.MoveTowards(reveal, target, Time.unscaledDeltaTime * 3.8f);
            UpdatePose();
            Refresh(Time.unscaledTime >= nextTextRefresh);
        }
        private void UpdatePose()
        {
            if (panel == null) return;
            float t = reveal * reveal * (3 - 2 * reveal);
            panel.anchoredPosition = Pinned ? new Vector2(458, -95 - (1 - t) * 40) : new Vector2(930, -108 - (1 - t) * 80);
            float scale = Pinned ? 1.07f : 1;
            panel.localScale = new Vector3(scale, scale * Mathf.Lerp(.86f, 1, t), 1);
            projectionGroup.alpha = t;
            projectionGroup.interactable = Raised && t > .9f;
            projectionGroup.blocksRaycasts = Raised && t > .9f;
            if (suitGraphic != null) { suitGraphic.SetReveal(t, Pinned); }
            if (pinText != null) pinText.text = Pinned ? "UNPIN" : "PIN / ENLARGE";
            if (motionText != null) motionText.text = ReducedMotion ? "MOTION: LOW" : "MOTION: FULL";
            if (openText != null) openText.text = Raised ? "V  /  LOWER WRIST" : "V  /  RAISE WRIST";
        }
        private void Refresh(bool text)
        {
            if (presenter == null || plot == null) return;
            var state = presenter.CaptureWristState();
            bool generationChanged = !hasState || state.Generation != lastState.Generation;
            hasState = true;
            lastState = state;
            if (plottedGeneration != state.Generation)
            {
                double max = frequencyPlot ? state.FrequencyAxisMaximum : state.WaveformAxisMaximum;
                plot.Rebuild(presenter, frequencyPlot, max > 0 ? max : 1, state.DurationSeconds);
                plottedGeneration = state.Generation;
            }
            plot.SetProgress((float)state.Progress, frequencyPlot
                ? (state.FrequencyAvailable ? (double?)state.FrequencyHz : null)
                : (state.SignalAvailable ? (double?)state.Signal : null));
            refreshing = true;
            timeline.SetValueWithoutNotify((float)state.Progress);
            if (generationChanged && !pendingEdits)
            {
                massA.SetValueWithoutNotify((float)state.Mass1Solar); massB.SetValueWithoutNotify((float)state.Mass2Solar);
                if (!state.ReplayMode) separation.SetValueWithoutNotify((float)state.SeparationRg);
            }
            if (text)
            {
                nextTextRefresh = Time.unscaledTime + .075f;
                scenario.text = state.ScenarioLabel ?? "BINARY OBSERVATORY";
                stateLine.text = $"{state.StageLabel}  /  {(state.Playing ? "PLAYING" : "PAUSED")}  /  click sky or lower wrist for flight";
                measurements.text = $"M1 {state.Mass1Solar:0.0}    M2 {state.Mass2Solar:0.0} solar    |    a {state.SeparationRg:0.00} rg / {state.SeparationKm:0.0} km\nCAMERA DISTANCE  {state.CameraDistanceRg:0.0} total rg   /   independent inspection position";
                signalTitle.text = frequencyPlot ? "MODEL FREQUENCY / Hz" : state.SignalLabel ?? "ACTIVE WAVEFORM";
                signalUnits.text = frequencyPlot ? $"{plot.AxisMaximum:0.#} Hz" : $"+/- {plot.AxisMaximum:G3}";
                graphValues.text = frequencyPlot
                    ? (state.FrequencyAvailable ? $"NOW {state.FrequencyHz:0.00} Hz  |  fixed axis; unavailable regions are gaps" : "Frequency unavailable at this epoch; no invented continuation")
                    : (state.SignalAvailable ? $"NOW {state.Signal:G4}  |  {(plot.DenseCycleBins > 0 ? "dense cycles: amplitude envelope band" : "fixed signal axis / resolved trace")}" : "Signal unavailable at this comparison epoch; gray gaps mark coverage");
                timeText.text = $"SOURCE t {state.SourceTimeSeconds:0.0000} s   /   {(state.Progress * 100):0.0}%   /   {state.PlaybackRate:+0.00;-0.00;0}x   /   {state.DurationSeconds:0.000} s record";
                playText.text = state.Playing ? "PAUSE" : "PLAY"; rateText.text = $"{Math.Abs(state.PlaybackRate):0.00}x";
                gasText.text = state.GasEnabled ? "GAS ON" : "GAS OFF";
                samplesText.text = state.SamplesEnabled ? "SAMPLES ON" : "SAMPLES OFF";
                opticalText.text = state.OpticalStyle == InteractiveOpticalStyle.Off ? "WARP OFF" : "AMPLIFIED / SPACING 1:12";
                lensGain.SetValueWithoutNotify(state.LensGain); waveGain.SetValueWithoutNotify(state.WaveGain);
                massA.interactable = massB.interactable = separation.interactable = !state.ReplayMode;
                applyButton.interactable = !state.ReplayMode;
                modeHint.text = state.ReplayMode ? "Recorded q/spins / use timeline for separation" : "5-80 solar each / q >= 0.10 / pending until Apply";
                validation.text = localValidation ?? state.ValidationMessage ?? (pendingEdits ? "Pending inputs: APPLY & PAUSE starts a new scenario." : "Assumed gas / illustrative optics / source-frame timeline");
                status.text = state.Status ?? "";
                RefreshFluidPanel();
                SetSelected(replayButton, state.ReplayMode); SetSelected(customButton, !state.ReplayMode);
                SetSelected(signalTab, !frequencyPlot); SetSelected(frequencyTab, frequencyPlot);
                for (int i = 0; i < opticsButtons.Length; i++) SetSelected(opticsButtons[i], i == (int)state.OpticalStyle);
                UpdateInputLabels();
            }
            refreshing = false;
        }
        private void UpdateInputLabels()
        {
            if (massA == null) return;
            massAText.text = $"M1  /  {massA.value:0.0} solar"; massBText.text = $"M2  /  {massB.value:0.0} solar";
            separationText.text = $"INITIAL SEPARATION  /  {separation.value:0.0} total rg";
        }
        private void SetSelected(Button button, bool selected)
        { if (button != null) button.GetComponent<Image>().color = selected ? new Color(.08f, .27f, .34f) : new Color(.04f, .11f, .16f); }
        private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); go.layer = 5;
            var image = go.GetComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced; image.color = color;
            var rect = go.GetComponent<RectTransform>(); Place(rect, x, y, w, h); return rect;
        }
        private Text TextAt(Transform parent, string text, int size, Color color, float x, float y, float w, float h)
        {
            var go = new GameObject("Instrument label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false); go.layer = 5; Place(go.GetComponent<RectTransform>(), x, y, w, h);
            var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size; label.color = color; label.text = text;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private Button Button(Transform parent, string text, float x, float y, float w, float h, Action action)
        {
            var rect = Box(parent, text, x, y, w, h, new Color(.04f, .11f, .16f));
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.3f, 1.4f, 1.4f); colors.pressedColor = new Color(.55f, 1, 1);
            colors.disabledColor = new Color(.45f, .52f, .56f, .6f); button.colors = colors;
            var label = TextAt(rect, text, 11, Cyan, 3, 0, w - 6, h); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => { action(); Refresh(true); }); return button;
        }
        private Slider Slider(Transform parent, string name, float x, float y, float w, float h, float min, float max, Action<float> action)
        {
            var rect = Box(parent, name, x, y - 5, w, h + 10, new Color(.03f, .09f, .13f));
            var track = Box(rect, "Instrument track", 0, 5 + h * .3f, w, h * .4f, new Color(.08f, .21f, .27f));
            var fill = Box(track, "Filled track", 0, 0, w, h * .4f, new Color(.21f, .71f, .84f));
            var handle = Box(rect, "Instrument handle", 0, 1, 13, h + 8, Cyan);
            var slider = rect.gameObject.AddComponent<Slider>(); slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill; slider.handleRect = handle; slider.minValue = min; slider.maxValue = max;
            fill.sizeDelta = Vector2.zero;
            // Keep a 20px handle inside the larger hit area despite the slider's stretched anchors.
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(13, 20 - (h + 10));
            slider.onValueChanged.AddListener(value => action(value)); return slider;
        }
        private static void Place(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
        private void CreateRoundedSprite()
        {
            const int side = 32;
            roundedTexture = new Texture2D(side, side, TextureFormat.RGBA32, false) { name = "Suit instrument corner mask" };
            var pixels = new Color[side * side];
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 10), dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 10);
                pixels[y * side + x] = new Color(1, 1, 1, Mathf.Clamp01(6 - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            roundedTexture.SetPixels(pixels); roundedTexture.Apply(false, true);
            rounded = Sprite.Create(roundedTexture, new Rect(0, 0, side, side), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        }
        private void OnDestroy()
        {
            if (rounded != null) Destroy(rounded);
            if (roundedTexture != null) Destroy(roundedTexture);
        }
    }
}
