using System;
using System.Collections.Generic;
using ApproachHUD;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    public sealed class GuidedObservationController : MonoBehaviour
    {
        [Serializable] public sealed class Entry
        { public string id, title, body, narration, clip, titleExperiment, bodyExperiment, narrationExperiment, clipExperiment; }
        [Serializable] sealed class Catalog { public Entry[] entries; }
        sealed class Target
        {
            public string id, label;
            public Vector3 point;
            public GameObject tag;
            public Button shortcut;
        }

        public GuidedEventTour Tour { get; private set; }
        public HudSquareController Animation { get; private set; }
        public bool CardOpen => card != null && card.activeSelf;
        public bool VoicePlaying => audioSource != null && audioSource.isPlaying;
        public string FocusedId => focused == null ? null : focused.id;
        public float FocusDistance => focused == null ? float.PositiveInfinity : Vector3.Distance(exhibit.View.transform.position, focused.point);
        public bool CanInspect => focused != null && gazePresent && FocusDistance <= InteractionDistance
            && Animation.State == HudSquareController.VisibilityState.Idle && !exhibit.Session.IsPlaying;
        public const float InteractionDistance = 18f;
        public bool AutomaticTick { get; set; } = true;

        InteractiveBinaryExhibit exhibit;
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly List<Target> targets = new List<Target>();
        GameObject root, card, indexPanel, markerObject, sourceObject;
        Text heading, instruction, toggleText, markerLabel, cardTitle, cardBody, voiceText, continueText;
        Button voiceButton, markerButton;
        GuidedHudGraphic markerGraphic;
        AudioSource audioSource;
        AudioClip loadedClip;
        Target focused, pinned;
        string cardId, stageId;
        long context = -1;
        double coverage = double.NaN;
        bool enabledStops = true, gazePresent, released;
        float dwell, away;

        public void Initialize(InteractiveBinaryExhibit owner, Transform parent)
        {
            released = false; enabledStops = true; context = -1; coverage = double.NaN;
            focused = pinned = null; stageId = cardId = null; gazePresent = false; dwell = away = 0;
            exhibit = owner;
            var asset = Resources.Load<TextAsset>("DTET/GuidedTour");
            if (asset == null) throw new InvalidOperationException("Guided tour content is missing.");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            foreach (var entry in catalog.entries) entries.Add(entry.id, entry);
            Resources.UnloadAsset(asset);
            root = new GameObject("Guided observations", typeof(RectTransform));
            root.transform.SetParent(parent, false); root.layer = 5;
            var rect = root.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            BuildInterface();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.loop = false; audioSource.spatialBlend = 0;
            if (FindFirstObjectByType<AudioListener>() == null) exhibit.CaptureCamera.gameObject.AddComponent<AudioListener>();
            Sync();
        }

        void BuildInterface()
        {
            var p = root.transform;
            indexPanel = ReducedExhibitUi.Panel(p, "Exploration guide", 28, 240, 288, 360, ReducedExhibitUi.Ink);
            heading = ReducedExhibitUi.Label(indexPanel.transform, "", 19, 16, 14, 256, 62, Color.white);
            instruction = ReducedExhibitUi.Label(indexPanel.transform, "", 14, 16, 82, 256, 74, ReducedExhibitUi.Muted);
            continueText = ReducedExhibitUi.Button(p, "CONTINUE", 28, 715, 288, 36,
                () => { if (exhibit.Session.IsPlaying) exhibit.SetWristPlaying(false); else Continue(); }).GetComponentInChildren<Text>();
            toggleText = ReducedExhibitUi.Button(p, "GUIDED STOPS: ON", 28, 760, 288, 32,
                () => SetGuided(!enabledStops)).GetComponentInChildren<Text>();

            sourceObject = new GameObject("Approach HUD geometry"); sourceObject.SetActive(false);
            sourceObject.transform.SetParent(transform, false);
            var graphic = sourceObject.AddComponent<HudSquareGraphic>();
            graphic.lineWidth = .006f;
            Animation = sourceObject.AddComponent<HudSquareController>();
            Animation.automaticTick = Animation.automaticProximity = Animation.showOnEnable = Animation.faceObserver = false;
            Animation.useUnscaledTime = true;
            sourceObject.SetActive(true); sourceObject.GetComponent<MeshRenderer>().enabled = false;
            markerObject = new GameObject("Interactive approach square", typeof(RectTransform), typeof(CanvasRenderer), typeof(GuidedHudGraphic), typeof(Button));
            markerObject.transform.SetParent(p, false); markerObject.layer = 5;
            markerGraphic = markerObject.GetComponent<GuidedHudGraphic>(); markerGraphic.Source = graphic;
            markerButton = markerObject.GetComponent<Button>(); markerButton.targetGraphic = markerGraphic;
            markerButton.onClick.AddListener(() => Interact());
            markerLabel = ReducedExhibitUi.Label(p, "", 15, 0, 0, 360, 64, Color.white);
            markerLabel.alignment = TextAnchor.UpperCenter;
            var outline = markerLabel.gameObject.AddComponent<Outline>(); outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);
            card = ReducedExhibitUi.Panel(p, "Observation explanation", 1064, 398, 500, 394, ReducedExhibitUi.Ink);
            cardTitle = ReducedExhibitUi.Label(card.transform, "", 23, 22, 20, 456, 62, Color.white);
            cardBody = ReducedExhibitUi.Label(card.transform, "", 18, 22, 90, 456, 206, ReducedExhibitUi.Muted);
            voiceButton = ReducedExhibitUi.Button(card.transform, "PLAY VOICE", 22, 310, 210, 38, ToggleVoice);
            voiceText = voiceButton.GetComponentInChildren<Text>();
            ReducedExhibitUi.Button(card.transform, "CLOSE", 254, 310, 224, 38, CloseCard);
            ReducedExhibitUi.Label(card.transform, "Optional machine voice • replaceable recording", 12, 22, 360, 456, 22, ReducedExhibitUi.Muted);
            card.SetActive(false);
        }

        public void Sync()
        {
            if (released || exhibit == null || !exhibit.Ready) return;
            var s = exhibit.Session;
            double start = exhibit.GeneratedFluidActive && exhibit.GeneratedHistory != null ? exhibit.GeneratedHistory.StartSeconds
                : exhibit.EvolvedDisksSelected && exhibit.MiniDisks.Ready ? exhibit.MiniDisks.Cache.StartSeconds : 0;
            if (Tour == null || Tour.Generation != s.Generation || coverage != start)
            {
                coverage = start;
                Tour = new GuidedEventTour(start, s.Model.MergerTimeSeconds, s.Model.GravitationalTimeSeconds, s.Model.DurationSeconds, s.Generation);
                Tour.SetEnabled(enabledStops, s.PlaybackTimeSeconds, s.Generation);
                Tour.NotifySeek(s.PlaybackTimeSeconds, s.Generation);
                context = s.ContextVersion; stageId = null; CloseCard(); HideFocus();
            }
            else if (context != s.ContextVersion)
            {
                Tour.NotifySeek(s.PlaybackTimeSeconds, s.Generation);
                context = s.ContextVersion; CloseCard(); HideFocus();
            }
        }

        public double ClampAdvance(double from, double proposed, out bool stop)
        {
            Sync();
            double target = Tour.ClampForward(from, proposed, exhibit.Session.Generation, out var checkpoint);
            stop = checkpoint != null;
            if (stop) CloseCard();
            return target;
        }

        public void RecordCheckpointSeek() { context = exhibit.Session.ContextVersion; }
        public void Continue()
        {
            Sync(); Tour.Continue(exhibit.Session.Generation); CloseCard(); HideFocus();
            exhibit.SetWristPlaying(true);
        }
        public void BeforePlay()
        { Sync(); Tour.Continue(exhibit.Session.Generation); CloseCard(); HideFocus(); }
        public void SetGuided(bool enabled)
        {
            enabledStops = enabled; Sync();
            Tour.SetEnabled(enabled, exhibit.Session.PlaybackTimeSeconds, exhibit.Session.Generation);
            CloseCard(); RefreshGuide();
        }

        public void Tick(float dt)
        {
            if (released || exhibit == null || !exhibit.Ready) return;
            Sync(); UpdateTargets();
            if (!exhibit.Session.IsPlaying && !exhibit.IsPreparingDisks && !exhibit.IsGeneratingFluid)
                Tour.ClampForward(exhibit.Session.PlaybackTimeSeconds, exhibit.Session.PlaybackTimeSeconds, exhibit.Session.Generation, out _);
            bool explore = !exhibit.Session.IsPlaying && !exhibit.IsPreparingDisks && !exhibit.IsGeneratingFluid
                && (exhibit.Wrist == null || !exhibit.Wrist.InputFocused);
            indexPanel.SetActive(explore);
            Target candidate = null; float best = 16;
            foreach (var target in targets)
            {
                Vector3 view = exhibit.View.WorldToViewportPoint(target.point);
                bool visible = explore && view.z > 0 && view.x > .02f && view.x < .98f && view.y > .16f && view.y < .85f;
                target.tag.SetActive(visible);
                if (visible) Position(target.tag, view, 28, 28, 0, 0);
                float angle = Vector3.Angle(exhibit.View.transform.forward, target.point - exhibit.View.transform.position);
                if (visible && angle < best) { best = angle; candidate = target; }
            }
            if (pinned != null && explore && Angle(pinned) < 23) candidate = pinned;
            else pinned = null;
            if (pinned == null && focused != null && explore && Angle(focused) < 20 && (candidate == null || Angle(focused) < best + 3)) candidate = focused;
            if (!explore) candidate = null;
            if (candidate == focused && candidate != null)
            { away = 0; dwell += dt; if (dwell >= .15f) { gazePresent = true; Animation.Show(); } }
            else
            {
                away += dt;
                if (focused == null || away >= .16f)
                {
                    gazePresent = false; Animation.Hide(); CloseCard();
                    if (Animation.State == HudSquareController.VisibilityState.Hidden)
                    { focused = candidate; dwell = away = 0; }
                }
            }
            Animation.Tick(dt); markerGraphic.SetVerticesDirty();
            bool drawn = Animation.State != HudSquareController.VisibilityState.Hidden && focused != null;
            markerObject.SetActive(drawn); markerLabel.gameObject.SetActive(drawn);
            if (drawn)
            {
                var view = exhibit.View.WorldToViewportPoint(focused.point);
                if (view.z > 0 && gazePresent)
                {
                    float size = Mathf.Lerp(124, 184, Mathf.InverseLerp(45, 8, FocusDistance));
                    Position(markerObject, view, size, size, 0, 0);
                    Position(markerLabel.gameObject, view, 360, 64, 0, size * .55f + 36);
                }
                markerLabel.text = focused.label + "\n" + (CanInspect ? "F / CLICK SQUARE  •  INSPECT" : $"APPROACH  •  {FocusDistance:0.0} rg / {InteractionDistance:0} rg");
            }
            markerButton.interactable = CanInspect;
            if (CardOpen && (!gazePresent || FocusDistance > InteractionDistance + 2 || !explore)) CloseCard();
            if (exhibit.EmulatedInputEnabled && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Interact();
            voiceText.text = VoicePlaying ? "STOP VOICE" : "PLAY VOICE";
            RefreshGuide();
        }

        void LateUpdate() { if (AutomaticTick) Tick(Time.unscaledDeltaTime); }
        float Angle(Target target) => Vector3.Angle(exhibit.View.transform.forward, target.point - exhibit.View.transform.position);
        void Position(GameObject go, Vector3 view, float width, float height, float dx, float dy)
        {
            var size = root.GetComponent<RectTransform>().rect.size;
            ReducedExhibitUi.Place(go, view.x * size.x - width / 2 + dx, (1 - view.y) * size.y - height / 2 + dy, width, height);
        }

        void UpdateTargets()
        {
            var snapshot = exhibit.Snapshot;
            string stage = Tour.CurrentCheckpoint?.Id;
            if (stage == null)
            {
                stage = "inspiral";
                foreach (var point in Tour.Checkpoints) if (point.Seconds <= snapshot.ElapsedSeconds) stage = point.Id;
            }
            bool binary = snapshot.BodyA.HasValue && snapshot.BodyB.HasValue;
            bool gas = exhibit.GasVisible && (exhibit.GeneratedFluidActive ? exhibit.GeneratedFluidPlayer.IsCovered
                : exhibit.EvolvedDisksSelected ? exhibit.MiniDisks.IsCovered : exhibit.Profile.GasEnabled);
            string key = stage + ":" + binary + ":" + gas;
            if (key != stageId)
            {
                stageId = key; CloseCard(); HideFocus();
                foreach (var target in targets) { Destroy(target.tag); Destroy(target.shortcut.gameObject); }
                targets.Clear();
                AddTarget("system", snapshot.Mode == InteractiveScenarioMode.RepresentativeReplay ? "GW150914 / SYSTEM" : "ADJUSTABLE BINARY SYSTEM");
                if (binary) { AddTarget("black_hole_a", "PRIMARY BLACK HOLE"); AddTarget("black_hole_b", "SECONDARY BLACK HOLE"); }
                else AddTarget("remnant", "MERGED BLACK HOLE");
                if (gas) AddTarget("accretion", "ACCRETION GAS");
                if (exhibit.Profile.GasEnabled) AddTarget("waves", "OUTGOING WAVES");
                if (stage != "remnant") AddTarget(stage, StageTitle(stage));
            }
            Vector3 a = snapshot.BodyA.HasValue ? V(snapshot.BodyA.Value.PositionRg) : Vector3.zero;
            Vector3 b = snapshot.BodyB.HasValue ? V(snapshot.BodyB.Value.PositionRg) : Vector3.zero;
            float span = Mathf.Max(6, (float)snapshot.SeparationRg);
            foreach (var target in targets)
            {
                switch (target.id)
                {
                    case "system": target.point = Vector3.up * (span * .5f + 3); break;
                    case "black_hole_a": target.point = a; break;
                    case "black_hole_b": target.point = b; break;
                    case "accretion": target.point = a + (a.sqrMagnitude > .01f ? a.normalized : Vector3.right) * 2.4f; break;
                    case "waves": target.point = Vector3.right * (span * 1.1f + 4); break;
                    case "remnant": target.point = snapshot.Remnant.HasValue ? V(snapshot.Remnant.Value.PositionRg) : Vector3.zero; break;
                    default: target.point = Vector3.down * 3; break;
                }
            }
        }

        void AddTarget(string id, string label)
        {
            if (!entries.ContainsKey(id)) return;
            var target = new Target { id = id, label = label };
            string symbol = id == "black_hole_a" ? "A" : id == "black_hole_b" ? "B" : id == "accretion" ? "GAS"
                : id == "waves" ? "GW" : id == "system" ? "SYS" : id == "remnant" ? "BH" : "i";
            target.tag = ReducedExhibitUi.Button(root.transform, symbol, 0, 0, 28, 28, () => FocusTarget(id)).gameObject;
            target.tag.transform.SetAsFirstSibling();
            target.tag.GetComponentInChildren<Text>().fontSize = 11;
            var labelRect = target.tag.GetComponentInChildren<Text>().rectTransform;
            labelRect.anchoredPosition = Vector2.zero; labelRect.sizeDelta = new Vector2(28, 28);
            target.shortcut = ReducedExhibitUi.Button(indexPanel.transform, label, 16, 164 + targets.Count * 27, 256, 24, () => FocusTarget(id));
            target.shortcut.GetComponentInChildren<Text>().fontSize = 12;
            targets.Add(target);
        }

        public bool FocusTarget(string id)
        {
            if (exhibit.Session.IsPlaying) return false;
            foreach (var target in targets)
                if (target.id == id)
                {
                    pinned = target;
                    exhibit.View.transform.LookAt(target.point);
                    return true;
                }
            return false;
        }
        public bool TryGetTarget(string id, out Vector3 point)
        { foreach (var target in targets) if (target.id == id) { point = target.point; return true; } point = default; return false; }

        void RefreshGuide()
        {
            if (Tour == null) return;
            string stage = Tour.CurrentCheckpoint?.Id;
            int index = 0;
            for (int i = 0; i < Tour.Checkpoints.Count; i++) if (Tour.Checkpoints[i].Id == stage) index = i + 1;
            heading.text = stage == null ? "FREE EXPLORATION" : $"STOP {index} / {Tour.Checkpoints.Count}\n{StageTitle(stage)}";
            instruction.text = "Look at a tag, then approach its square.\nF / click opens the explanation.\nSelect a name below to face its location.";
            toggleText.text = enabledStops ? "GUIDED STOPS: ON" : "GUIDED STOPS: OFF";
            continueText.text = Tour.IsPausedAtCheckpoint ? (index == Tour.Checkpoints.Count ? "CONTINUE TO END" : "CONTINUE TO NEXT STOP")
                : exhibit.Session.IsPlaying ? "PAUSE TO EXPLORE" : "RESUME EVENT";
            if (exhibit.Session.IsPlaying && focused == null) markerLabel.gameObject.SetActive(false);
        }
        static string StageTitle(string id)
        {
            switch (id)
            {
                case "inspiral": return "THE INTACT BINARY";
                case "gas_tides": return "INSPIRAL & TIDAL GAS";
                case "final_approach": return "FINAL APPROACH";
                case "coalescence": return "COALESCENCE";
                default: return "THE REMNANT";
            }
        }

        public bool Interact()
        {
            if (!CanInspect || !entries.TryGetValue(focused.id, out var entry)) return false;
            CloseCard(); cardId = entry.id; cardTitle.text = entry.title;
            bool experiment = exhibit.Snapshot.Mode != InteractiveScenarioMode.RepresentativeReplay;
            if (experiment && !string.IsNullOrEmpty(entry.titleExperiment)) cardTitle.text = entry.titleExperiment;
            cardBody.text = experiment && !string.IsNullOrEmpty(entry.bodyExperiment) ? entry.bodyExperiment : entry.body;
            voiceButton.interactable = !string.IsNullOrEmpty(entry.clip); card.SetActive(true); return true;
        }
        public void ToggleVoice()
        {
            if (!CardOpen) return;
            if (VoicePlaying) { StopVoice(); return; }
            if (!entries.TryGetValue(cardId, out var entry) || string.IsNullOrEmpty(entry.clip)) return;
            StopVoice();
            bool experiment = exhibit.Snapshot.Mode != InteractiveScenarioMode.RepresentativeReplay;
            loadedClip = Resources.Load<AudioClip>(experiment && !string.IsNullOrEmpty(entry.clipExperiment) ? entry.clipExperiment : entry.clip);
            if (loadedClip == null) { voiceText.text = "VOICE UNAVAILABLE"; voiceButton.interactable = false; return; }
            audioSource.clip = loadedClip; audioSource.Play(); voiceText.text = "STOP VOICE";
        }
        void StopVoice()
        {
            if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }
            if (loadedClip != null) Resources.UnloadAsset(loadedClip);
            loadedClip = null;
            if (voiceText != null) voiceText.text = "PLAY VOICE";
        }
        public void CloseCard() { StopVoice(); cardId = null; if (card != null) card.SetActive(false); }
        void HideFocus() { focused = pinned = null; gazePresent = false; dwell = away = 0; if (Animation != null) Animation.Hide(); }
        public void Release()
        {
            released = true; CloseCard(); entries.Clear(); targets.Clear();
            if (sourceObject != null) { sourceObject.SetActive(false); Destroy(sourceObject); }
            if (root != null) { root.SetActive(false); Destroy(root); }
            root = sourceObject = null; focused = pinned = null;
        }
        void OnDestroy() { Release(); }
        static Vector3 V(NumericalVector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
    }
}
