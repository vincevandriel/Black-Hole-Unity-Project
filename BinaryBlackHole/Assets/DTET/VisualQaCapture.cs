#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DTET.VisualPrototype
{
    // Development capture enabled by DTET_CAPTURE_PATH.
    internal sealed class VisualQaCapture : MonoBehaviour
    {
        private bool inputChecksPassed = true;
        private void OnEnable() => Application.logMessageReceived += QuitOnException;
        private void OnDisable() => Application.logMessageReceived -= QuitOnException;
        private static void QuitOnException(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) Application.Quit(1);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_CAPTURE_PATH"))) return;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            new GameObject("DT-ET development screenshot receipt").AddComponent<VisualQaCapture>();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            var prototype = FindFirstObjectByType<VisualPrototypeBootstrap>();
            if (prototype == null)
            {
                Debug.LogError("DTET_QA_FAILED: visual prototype did not start");
                Application.Quit(1);
                yield break;
            }
            try
            {
                // Captures exercise deterministic defaults without overwriting the visitor's preferences.
                prototype.Observer.PersistInputSettings = false;
                prototype.Observer.ResetInputSensitivities();
                prototype.SetPhysicsMode(false); // Deliberately audit the retained artistic mode here.
                prototype.SetLearningVisible(true);
                CheckUiCommands();
                CheckObservationCommands(prototype);
                Debug.Log("DTET_UI_COMMANDS_PASS");
            }
            catch (Exception error)
            {
                Debug.LogError("DTET_UI_COMMANDS_FAILED " + error);
                Application.Quit(1);
                yield break;
            }
            yield return CheckNavigationInput(prototype);
            yield return CheckPartnerHud(prototype);
            yield return CheckWorldInteraction(prototype);
            yield return CheckStarsAndClockBoundary(prototype);
            yield return CheckApproximateLensing(prototype);
            yield return CheckInputOptions(prototype);
            yield return CheckStoryboardAudit(prototype);
            if (!inputChecksPassed)
            {
                Application.Quit(1);
                yield break;
            }
            var variant = Environment.GetEnvironmentVariable("DTET_CAPTURE_VARIANT") ?? "Story";
            switch (variant.ToLowerInvariant())
            {
                case "graph": prototype.SetQualityCheckView(1, 0.5f); break;
                case "model": prototype.SetQualityCheckView(2, 0.75f); break;
                case "endpoint": prototype.SetQualityCheckView(0, 0.99f); break;
                case "observe":
                case "above":
                case "side":
                case "close":
                case "hud":
                case "hudend":
                case "wrist":
                    prototype.SetQualityCheckView(0, 0.25f);
                    if (variant.ToLowerInvariant() == "hudend") prototype.SetQualityCheckView(0, 1f);
                    prototype.SetLearningVisible(false);
                    prototype.Observer.SetPreset(variant.ToLowerInvariant() == "above" ? ExhibitCameraRig.ViewPreset.Above
                        : variant.ToLowerInvariant() == "side" ? ExhibitCameraRig.ViewPreset.Side : ExhibitCameraRig.ViewPreset.Overview);
                    if (variant.ToLowerInvariant() == "close") prototype.Observer.Focus(ExhibitCameraRig.ObservationTarget.BodyA);
                    if (variant.ToLowerInvariant() == "hud" || variant.ToLowerInvariant() == "hudend" || variant.ToLowerInvariant() == "wrist")
                        prototype.PartnerHud.WristRig.SetRaised(true, true);
                    break;
                case "scenariob":
                    Click("Learning controls", "B  /  WIDER button");
                    prototype.SetQualityCheckView(0, 0.25f);
                    break;
                case "clocks":
                    prototype.SetQualityCheckView(0, 0.25f);
                    prototype.SetLearningVisible(false);
                    prototype.Observer.FocusClockComparison();
                    break;
                case "options":
                    prototype.SetQualityCheckView(0, 0.25f);
                    prototype.SetLearningVisible(false);
                    prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
                    if (!prototype.PartnerHud.OptionsOpen) prototype.PartnerHud.ToggleOptions();
                    break;
                case "lensing":
                case "lensingoff":
                case "lensingrays":
                    prototype.SetQualityCheckView(0, 0.25f);
                    prototype.SetLearningVisible(false);
                    yield return new WaitForEndOfFrame();
                    prototype.FocusLensingDemo();
                    prototype.StellarField.SetLensing(variant.ToLowerInvariant() != "lensingoff");
                    prototype.StellarField.SetRayPaths(variant.ToLowerInvariant() == "lensingrays");
                    prototype.PartnerHud.WristRig.SetRaised(false, true);
                    break;
                default: prototype.SetQualityCheckView(0, 0.25f); break;
            }
            if (prototype.LearningVisible) prototype.Observer.FocusLearning();
            Debug.Log("DTET_QA_VIEW " + variant);
            if (variant.ToLowerInvariant() != "hud" && variant.ToLowerInvariant() != "hudend"
                && variant.ToLowerInvariant() != "wrist" && variant.ToLowerInvariant() != "options")
                prototype.PartnerHud.WristRig.SetRaised(false, true);
            yield return new WaitForSecondsRealtime(0.4f);
            var optics = new double[90];
            var frames = new double[90];
            double opticsSum = 0, frameSum = 0;
            for (var i = 0; i < optics.Length; i++)
            {
                yield return new WaitForEndOfFrame();
                optics[i] = prototype.StellarField.LastUpdateMilliseconds;
                frames[i] = Time.unscaledDeltaTime * 1000;
                opticsSum += optics[i]; frameSum += frames[i];
            }
            Array.Sort(optics); Array.Sort(frames);
            Debug.Log($"DTET_PERFORMANCE samples=90 opticsCPU_mean_ms={opticsSum / 90:0.000} opticsCPU_p95_ms={optics[85]:0.000} frame_mean_ms={frameSum / 90:0.000} frame_p95_ms={frames[85]:0.000} GPU_not_measured VR_not_tested");
            yield return new WaitForEndOfFrame();
            var path = Environment.GetEnvironmentVariable("DTET_CAPTURE_PATH");
            var captureStarted = DateTime.UtcNow;
            DevelopmentFrameCapture.WritePng(Camera.main, path);
            yield return new WaitForSecondsRealtime(2f);
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < captureStarted || new FileInfo(path).Length == 0)
            {
                Debug.LogError("DTET_CAPTURE_FAILED: no fresh screenshot at " + path);
                Application.Quit(1);
                yield break;
            }
            Debug.Log("DTET_CAPTURE_WRITTEN " + path);
            Application.Quit();
        }

        private IEnumerator CheckStarsAndClockBoundary(VisualPrototypeBootstrap prototype)
        {
            var field = FindFirstObjectByType<StellarSourceField>();
            CheckInput(field != null && field.Sources.Count == StellarSourceField.SourceCount, "1400 individual star source objects");
            if (field == null) yield break;
            field.SetLensing(false);
            field.SetRayPaths(false);
            CheckInput(field.GetComponentsInChildren<MeshRenderer>().Length == 1, "Star object images share one mesh renderer");
            var fixedPositions = new Vector3[field.Sources.Count];
            for (var i = 0; i < fixedPositions.Length; i++)
            {
                var source = field.Sources[i];
                fixedPositions[i] = source.transform.position;
                CheckInput(source.SourceId == i && source.transform.parent == field.transform, "Stable source identity/parent " + i);
                CheckInput(source.transform.localPosition.magnitude >= 84.99f && source.transform.localPosition.magnitude <= 125.01f,
                    "Finite distant source position " + i);
            }
            prototype.SetLearningVisible(false);
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            var rootPosition = field.transform.position;
            var directionBefore = (fixedPositions[0] - prototype.Observer.transform.position).normalized;
            prototype.Observer.MoveLocal(new Vector3(8, 0, 0));
            yield return new WaitForEndOfFrame();
            var directionAfter = (field.Sources[0].transform.position - prototype.Observer.transform.position).normalized;
            CheckInput(Vector3.Angle(directionBefore, directionAfter) > 0.1f, "Fixed-source parallax changes with visitor movement");
            CheckInput(field.transform.position == rootPosition, "Star field is not camera-following");
            var meshVertices = field.DisplayMesh.vertices;
            for (var i = 0; i < fixedPositions.Length; i++)
            {
                CheckInput(field.Sources[i].transform.position == fixedPositions[i], "Observer does not move source " + i);
                var vertex = i * ThinLensModel.MaximumImages * 4;
                var imageCentre = field.transform.TransformPoint((meshVertices[vertex] + meshVertices[vertex + 2]) * 0.5f);
                CheckInput(Vector3.Distance(imageCentre, fixedPositions[i]) < 0.0001f, "Unbent source image position " + i);
            }
            var bridge = prototype.Observer.GetComponent<ObserverPhysicsBridge>();
            var clocks = bridge.CaptureClocks();
            CheckInput(clocks.Fidelity == RelativityFidelity.Unavailable && !clocks.DifferenceSeconds.HasValue,
                "Default physics never invents proper-time readings");
            var epoch = bridge.Epoch;
            prototype.SeekStoryboard(0.5f);
            CheckInput(bridge.Epoch > epoch, "Seek invalidates physical clock epoch");
            epoch = bridge.Epoch;
            prototype.ResetPlayback();
            CheckInput(bridge.Epoch > epoch, "Reset invalidates physical clock epoch");
            var revision = bridge.WorldlineRevision;
            prototype.Observer.FocusInformation();
            CheckInput(bridge.WorldlineRevision > revision, "Teleport invalidates continuous observer worldline");
            revision = bridge.WorldlineRevision;
            prototype.Observer.Rotate(new Vector2(12, 4));
            CheckInput(bridge.WorldlineRevision == revision, "Head rotation does not reset a worldline");
            var redRay = new StellarRaySample(true, false, 0, 0.5);
            var blueRay = new StellarRaySample(true, false, 0, 2);
            CheckInput(redRay.Redshift == 1 && blueRay.Redshift == -0.5, "Redshift sign convention supports blueshift");
            CheckInput(HasText(GameObject.Find("Observer clock comparison"), "Not connected"), "Future clocks visibly labeled unavailable");
            var testProvider = new FixedTestClockProvider();
            bridge.BindProvider(testProvider);
            testProvider.Snapshot = new RelativityClockSnapshot(testProvider.ModelId, bridge.Epoch, bridge.WorldlineRevision,
                RelativityFidelity.Approximate, 8, 10, 0.8);
            CheckInput(bridge.CaptureClocks().DifferenceSeconds == -2, "Clock provider supplies complete totals");
            bridge.InvalidateWorldline();
            CheckInput(!bridge.CaptureClocks().DifferenceSeconds.HasValue, "Bridge rejects stale provider history");
            bridge.BindProvider(null);
            field.SetLensing(true);
            CheckInput(bridge.CaptureClocks().Fidelity == RelativityFidelity.Unavailable, "Test provider removed before rendering");
            if (inputChecksPassed) Debug.Log("DTET_STELLAR_CLOCK_BOUNDARY_PASS");
        }

        private sealed class FixedTestClockProvider : IObserverPhysicsProvider
        {
            public RelativityClockSnapshot Snapshot;
            public string ModelId => "QA-ONLY-NOT-PHYSICS";
            public string Status => "QA test values only";
            public RelativityClockSnapshot CaptureClocks(ObserverViewRequest observer) => Snapshot;
            public StellarRaySample TracePastDirectedRay(ObserverViewRequest observer, Vector3 direction) => default;
        }

        private IEnumerator CheckApproximateLensing(VisualPrototypeBootstrap prototype)
        {
            prototype.SeekStoryboard(0.25f);
            yield return new WaitForEndOfFrame();
            prototype.FocusLensingDemo();
            prototype.PartnerHud.SetVisible(true);
            prototype.PartnerHud.WristRig.SetRaised(true, true);
            yield return new WaitForEndOfFrame();
            var field = prototype.StellarField;
            var updates = field.ImageUpdateCount;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            CheckInput(field.ImageUpdateCount == updates, "Unchanged paused optics reuse the existing image mesh");
            var editedSource = field.Sources[1].transform;
            var savedSourcePosition = editedSource.position;
            editedSource.position += Vector3.right * 0.2f;
            yield return new WaitForEndOfFrame();
            CheckInput(field.ImageUpdateCount > updates, "External source movement invalidates optical images");
            editedSource.position = savedSourcePosition;
            yield return new WaitForEndOfFrame();
            var sourcePosition = field.DemoSource.transform.position;
            CheckInput(field.DemoImageCount >= 2, "Background demonstration star has multiple thin-lens images");
            var imagePosition = field.DemoImagePosition;
            CheckInput(Vector3.Angle(imagePosition - prototype.Observer.transform.position,
                sourcePosition - prototype.Observer.transform.position) > 0.1f, "Apparent star direction bends from direct direction");
            var restTint = field.DemoSource.RestTint;
            var colours = field.DisplayMesh.colors;
            CheckInput(colours[0] == restTint, "Lensing retains rest colour; redshift not implemented");
            var rays = field.GetComponentsInChildren<LineRenderer>();
            CheckInput(rays.Length >= 2 && rays[0].positionCount == 3, "Thin-lens broken-ray guides are present");
            prototype.Observer.MoveLocal(new Vector3(0.6f, 0, 0));
            yield return new WaitForEndOfFrame();
            CheckInput(field.ImageUpdateCount > updates, "Visitor movement invalidates the cached optical images");
            CheckInput(field.DemoSource.transform.position == sourcePosition, "Lensing does not move the source object");
            CheckInput(Vector3.Distance(field.DemoImagePosition, imagePosition) > 0.001f, "Apparent image updates with viewer movement");
            Click("Partner wrist controls", "LENS / L button");
            yield return new WaitForEndOfFrame();
            CheckInput(!field.LensingEnabled, "Wrist toggles bending off");
            var vertices = field.DisplayMesh.vertices;
            var directCentre = field.transform.TransformPoint((vertices[0] + vertices[2]) * 0.5f);
            CheckInput(Vector3.Distance(directCentre, sourcePosition) < 0.0001f, "Bending off restores direct source image");
            Click("Partner wrist controls", "LENS / L button");
            CheckInput(field.LensingEnabled, "Wrist toggles bending on");
            Click("Partner wrist controls", "RAYS / G button");
            CheckInput(!field.ShowRayPaths, "Wrist toggles ray guides off");
            Click("Partner wrist controls", "ALIGN / J button");
            CheckInput(field.ShowRayPaths && !prototype.CaptureHudSnapshot().Playing, "Wrist aligns and pauses demo");
            field.SetRayPaths(false);
            if (inputChecksPassed) Debug.Log("DTET_APPROXIMATE_LENSING_PASS");
        }

        private IEnumerator CheckStoryboardAudit(VisualPrototypeBootstrap prototype)
        {
            prototype.ResetPlayback();
            prototype.SetLearningVisible(true);
            Click("Learning controls", "A  /  CLOSER button");
            CheckInput(!prototype.CaptureHudSnapshot().Playing, "Selecting a scenario pauses for comparison");
            var beforeInvalid = prototype.CaptureHudSnapshot();
            prototype.SeekStoryboard(float.NaN);
            prototype.SetHudPlaybackRate(float.PositiveInfinity);
            CheckInput(prototype.CaptureHudSnapshot().Progress == beforeInvalid.Progress
                && prototype.CaptureHudSnapshot().PlaybackRate == beforeInvalid.PlaybackRate,
                "Non-finite public commands preserve recoverable state");
            prototype.SeekStoryboard(0.3f);
            yield return new WaitForEndOfFrame();
            var phaseBefore = (float)StoryboardChoreography.Phase(prototype.CaptureHudSnapshot().Progress);
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(0.15f);
            prototype.TogglePlayback();
            var progress = prototype.CaptureHudSnapshot().Progress;
            yield return new WaitForEndOfFrame();
            var firstObject = GameObject.Find("Body A - illustrated black-hole marker");
            var first = firstObject == null ? null : firstObject.transform;
            // Require the hierarchy name assigned by BuildWorld.
            CheckInput(first != null, "Audit locates the live first marker");
            if (first != null)
            {
                var playedPosition = first.position;
                prototype.SeekStoryboard(0.1f);
                yield return new WaitForEndOfFrame();
                prototype.SeekStoryboard(progress);
                yield return new WaitForEndOfFrame();
                CheckInput(Vector3.Distance(first.position, playedPosition) < 0.0001f, "Playback and seek reproduce the same marker pose");
            }
            CheckInput((float)StoryboardChoreography.Phase(progress) > phaseBefore, "Deterministic phase advances during playback");
            prototype.TogglePlayback();
            var activeOptics = new double[90];
            double activeSum = 0;
            for (var i = 0; i < activeOptics.Length; i++)
            {
                yield return new WaitForEndOfFrame();
                activeOptics[i] = prototype.StellarField.LastUpdateMilliseconds;
                activeSum += activeOptics[i];
            }
            prototype.TogglePlayback();
            Array.Sort(activeOptics);
            Debug.Log($"DTET_OPTICS_ACTIVE samples=90 CPU_mean_ms={activeSum / 90:0.000} CPU_p95_ms={activeOptics[85]:0.000} GPU_not_measured VR_not_tested");
            var controls = GameObject.Find("Learning controls");
            var initial = controls.GetComponentsInChildren<Slider>(true)[0];
            initial.value = 38;
            Click("Learning controls", "APPLY START button");
            CheckInput(HasText(controls, "Custom start:"), "Custom starts do not retain a false preset label");
            Click("One-question formative check", "RISE button");
            Click("Learning controls", "B  /  WIDER button");
            CheckInput(HasText(GameObject.Find("One-question formative check"), "Choose an answer"), "Scenario change clears old formative feedback");
            Click("Learning controls", "A  /  CLOSER button");
            prototype.SetLearningVisible(false);
            if (inputChecksPassed) Debug.Log("DTET_STORYBOARD_AUDIT_PASS");
        }

        private IEnumerator CheckInputOptions(VisualPrototypeBootstrap prototype)
        {
            var rig = prototype.Observer;
            var hud = prototype.PartnerHud;
            rig.ResetInputSensitivities();
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            hud.SetVisible(true);
            hud.WristRig.SetRaised(true, true);
            var originalMouse = Mouse.current;
            var originalKeyboard = Keyboard.current;
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var ui = UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            var devices = ui.actionsAsset.devices;
            ui.actionsAsset.devices = new InputDevice[] { mouse, keyboard };

            var optionsButton = DirectChild(GameObject.Find("Partner wrist controls"), "OPTIONS / O button").GetComponent<RectTransform>();
            var point = (Vector2)Camera.main.WorldToScreenPoint(optionsButton.TransformPoint(optionsButton.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return new WaitForSecondsRealtime(0.1f);
            CheckInput(hud.OptionsOpen && hud.WristRig.Menu.gameObject.activeInHierarchy, "Real pointer opens wrist input options");
            // Continue even if pointer routing failed, to report remaining independent checks.
            if (!hud.OptionsOpen) hud.ToggleOptions();
            var options = GameObject.Find("Observer input options");
            var look = options.transform.Find("Look sensitivity slider").GetComponent<Slider>();
            var scroll = options.transform.Find("Scroll sensitivity slider").GetComponent<Slider>();
            var rect = look.GetComponent<RectTransform>();
            point = (Vector2)Camera.main.WorldToScreenPoint(rect.TransformPoint(new Vector3(rect.rect.width * 0.75f, -rect.rect.height * 0.5f)));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            CheckInput(rig.LookSensitivity > 2.5f, "Real pointer changes look sensitivity slider");
            scroll.value = 4.5f;
            CheckInput(Mathf.Approximately(rig.ScrollSensitivity, 4.5f), "Scroll slider is independently wired");
            var before = rig.transform.position;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, scroll = new Vector2(0, 120) });
            yield return null;
            CheckInput(Vector3.Distance(before, rig.transform.position) < 0.001f, "Wheel over wrist options never moves the observer");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            Click("Observer input options", "RESET DEFAULTS button");
            yield return null;
            CheckInput(rig.LookSensitivity == 1 && rig.ScrollSensitivity == 3 && look.value == 1 && scroll.value == 3,
                "Reset defaults restores both sliders and tripled scroll speed");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.O));
            yield return null;
            CheckInput(!hud.OptionsOpen && GameObject.Find("Partner wrist controls") != null, "O returns to the normal wrist controls");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;

            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            before = rig.transform.position;
            rig.Zoom(1);
            var defaultStep = Vector3.Distance(before, rig.transform.position);
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            rig.SetScrollSensitivity(1);
            before = rig.transform.position;
            rig.Zoom(1);
            CheckInput(Mathf.Abs(defaultStep - 2.4f) < 0.001f
                && Mathf.Abs(defaultStep / Vector3.Distance(before, rig.transform.position) - 3) < 0.001f,
                "Default wheel movement is exactly three times original speed");
            rig.Focus(ExhibitCameraRig.ObservationTarget.Binary);
            rig.SetScrollSensitivity(3);
            rig.Zoom(1);
            CheckInput(Mathf.Abs(rig.Distance - ExhibitCameraRig.DefaultDistance * Mathf.Exp(-0.36f)) < 0.001f,
                "Scroll sensitivity applies to optional orbit distance too");
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            rig.SetLookSensitivity(1);
            var rotation = rig.transform.rotation;
            rig.Look(new Vector2(20, 0));
            var defaultAngle = Quaternion.Angle(rotation, rig.transform.rotation);
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            rig.SetLookSensitivity(2);
            rotation = rig.transform.rotation;
            rig.Look(new Vector2(20, 0));
            CheckInput(Mathf.Abs(Quaternion.Angle(rotation, rig.transform.rotation) / defaultAngle - 2) < 0.01f,
                "Look sensitivity scales angular movement independently");
            rig.ResetInputSensitivities();
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            ui.actionsAsset.devices = devices;
            if (originalMouse != null) originalMouse.MakeCurrent();
            if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            if (inputChecksPassed) Debug.Log("DTET_INPUT_OPTIONS_PASS");
        }

        private IEnumerator CheckPartnerHud(VisualPrototypeBootstrap prototype)
        {
            prototype.SetLearningVisible(false);
            prototype.PartnerHud.SetVisible(true);
            prototype.PartnerHud.WristRig.SetRaised(true, true);
            prototype.ResetPlayback();
            prototype.SetHudPlaybackRate(1f);
            Click("Partner wrist controls", "1× button");
            CheckInput(Mathf.Approximately(prototype.CaptureHudSnapshot().PlaybackRate, 2f), "HUD speed 1 -> 2");
            Click("Partner wrist controls", "1× button");
            CheckInput(Mathf.Approximately(prototype.CaptureHudSnapshot().PlaybackRate, 0.5f), "HUD speed 2 -> 0.5");
            Click("Partner wrist controls", "1× button");
            CheckInput(Mathf.Approximately(prototype.CaptureHudSnapshot().PlaybackRate, 1f), "HUD speed 0.5 -> 1");
            Click("Partner wrist controls", "Halfway button");
            var snapshot = prototype.CaptureHudSnapshot();
            CheckInput(Mathf.Approximately(snapshot.Progress, 0.5f) && !snapshot.Playing, "HUD stage seek pauses shared clock");
            CheckInput(!snapshot.SeparationKm.HasValue && !snapshot.OrbitsPerSecond.HasValue && !snapshot.WaveFrequencyHz.HasValue,
                "HUD does not invent physical measurements");
            CheckInput(Mathf.Approximately(snapshot.PlaybackSecondsLeft, 20f), "HUD reports playback duration, not physical time");
            yield return new WaitForEndOfFrame();
            CheckInput(HasText(GameObject.Find("Partner readout card"), "71% of start"), "HUD separation matches rendered storyboard radius");
            CheckInput(HasText(GameObject.Find("Partner readout card"), "Not connected"), "Missing physical readout labeled");
            Click("Partner wrist controls", "PAUSE button");
            CheckInput(prototype.CaptureHudSnapshot().Playing, "HUD play resumes main storyboard");
            yield return new WaitForSecondsRealtime(0.08f);
            CheckInput(prototype.CaptureHudSnapshot().Progress > 0.5f, "HUD playback advances shared state");
            Click("Partner wrist controls", "End button");
            yield return new WaitForEndOfFrame();
            CheckInput(HasText(GameObject.Find("Partner readout card"), "Conceptual remnant"), "HUD endpoint explicitly conceptual");
            CheckInput(HasText(GameObject.Find("Partner tutor bubble"), "This remnant is a conceptual endpoint"), "Offline guide follows endpoint");
            Click("Partner wrist controls", "REPLAY button");
            CheckInput(Mathf.Approximately(prototype.CaptureHudSnapshot().Progress, 0f) && !prototype.CaptureHudSnapshot().Playing, "HUD replay resets and pauses");
            prototype.SetHudPlaybackRate(100f);
            CheckInput(Mathf.Approximately(prototype.CaptureHudSnapshot().PlaybackRate, 4f), "HUD playback upper bound");
            prototype.SetHudPlaybackRate(-1f);
            CheckInput(Mathf.Approximately(prototype.CaptureHudSnapshot().PlaybackRate, 0.1f), "HUD playback lower bound");
            prototype.SetHudPlaybackRate(1f);
            prototype.SetLearningVisible(true);
            var sliders = GameObject.Find("Learning controls").GetComponentsInChildren<Slider>(true);
            CheckInput(Mathf.Approximately(sliders[2].value, 1f), "HUD rate synchronizes learning slider");
            sliders[2].value = 1.4f;
            prototype.SetLearningVisible(false);
            yield return new WaitForEndOfFrame();
            CheckInput(HasText(GameObject.Find("Partner wrist controls"), "1.4×"), "Learning rate synchronizes HUD");
            prototype.SetHudPlaybackRate(1f);
            Click("Partner tutor bubble", "PREDICT / LEARN button");
            CheckInput(prototype.LearningVisible && GameObject.Find("Partner chirp card world panel") == null
                && prototype.PartnerHud.WristRig.Menu.gameObject.activeInHierarchy, "Learning board replaces information panels but keeps wrist controls");
            prototype.SetLearningVisible(false);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            yield return null;
            CheckInput(!prototype.PartnerHud.Visible, "U hides partner HUD");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
            yield return null;
            CheckInput(prototype.PartnerHud.Visible, "U restores partner HUD");
            InputSystem.RemoveDevice(keyboard);
            if (inputChecksPassed) Debug.Log("DTET_PARTNER_HUD_PASS");
        }

        private IEnumerator CheckWorldInteraction(VisualPrototypeBootstrap prototype)
        {
            prototype.SetLearningVisible(false);
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            var wrist = prototype.PartnerHud.WristRig;
            wrist.SetRaised(false, true);
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var uiModule = UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            var originalDevices = uiModule.actionsAsset.devices;
            uiModule.actionsAsset.devices = new InputDevice[] { mouse, keyboard };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
            yield return new WaitForSecondsRealtime(0.35f);
            CheckInput(wrist.Raised && wrist.Menu.gameObject.activeInHierarchy, "V raises a visible wrist control canvas");
            CheckInput(wrist.Menu.renderMode == RenderMode.WorldSpace && wrist.Menu.transform.parent == wrist.Wrist, "Control canvas is physically parented to wrist pose");
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                CheckInput(canvas.renderMode == RenderMode.WorldSpace, "No desktop screen-space canvas: " + canvas.name);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            prototype.ResetPlayback();
            var button = GameObject.Find("Partner wrist controls").transform.Find("PAUSE button").GetComponent<RectTransform>();
            var point = (Vector2)Camera.main.WorldToScreenPoint(button.TransformPoint(button.rect.center));
            CheckInput(point.x > 0 && point.x < Screen.width && point.y > 0 && point.y < Screen.height, "Wrist play control projects into the viewport");
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = point };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Debug.Log("DTET_WRIST_POINTER " + point + " hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            CheckInput(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Pointer ray selects wrist play control rather than scene panels");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return new WaitForSecondsRealtime(0.1f);
            CheckInput(prototype.CaptureHudSnapshot().Playing, "Real pointer click on world-space wrist resumes playback");
            prototype.ResetPlayback();
            var wristRotation = wrist.Wrist.rotation;
            var menuPosition = wrist.Menu.transform.position;
            var headRotation = prototype.Observer.transform.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, delta = new Vector2(200, 0) }.WithButton(MouseButton.Right));
            yield return null;
            yield return new WaitForEndOfFrame();
            CheckInput(Quaternion.Angle(headRotation, prototype.Observer.transform.rotation) > 5f, "Head look works even with pointer over wrist controls");
            CheckInput(Quaternion.Angle(wristRotation, wrist.Wrist.rotation) < 0.1f
                && Vector3.Distance(menuPosition, wrist.Menu.transform.position) < 0.001f, "Looking away does not head-lock the wrist controls");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            var beforeMove = wrist.Wrist.position;
            prototype.Observer.MoveLocal(new Vector3(0.7f, 0, 0));
            yield return new WaitForSecondsRealtime(0.45f);
            CheckInput(Vector3.Distance(beforeMove, wrist.Wrist.position) > 0.6f, "Visitor locomotion carries the wrist with the body");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
            yield return null;
            CheckInput(!wrist.Raised && !wrist.Menu.gameObject.activeSelf, "V lowers wrist and hides controls");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            prototype.SetLearningVisible(true);
            CheckInput(wrist.gameObject.activeInHierarchy, "Wrist pose remains available beside learning board");
            prototype.SetLearningVisible(false);
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            uiModule.actionsAsset.devices = originalDevices;
            if (inputChecksPassed) Debug.Log("DTET_WORLD_WRIST_INPUT_PASS");
        }

        private IEnumerator CheckNavigationInput(VisualPrototypeBootstrap prototype)
        {
            var originalMouse = Mouse.current;
            var originalKeyboard = Keyboard.current;
            var testMouse = InputSystem.AddDevice<Mouse>();
            var testKeyboard = InputSystem.AddDevice<Keyboard>();
            var rig = prototype.Observer;
            prototype.SetLearningVisible(false);
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            prototype.PartnerHud.WristRig.SetRaised(true, true);
            var viewportPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var rotation = rig.transform.rotation;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint, delta = new Vector2(180f, 25f) }.WithButton(MouseButton.Right));
            yield return null;
            CheckInput(Quaternion.Angle(rotation, rig.transform.rotation) > 5f, "Right-drag changes observation angle");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint });
            yield return null;
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            var beforeZoom = rig.transform.position;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint, scroll = new Vector2(0, 120f) });
            yield return null;
            CheckInput(Mathf.Abs(Vector3.Distance(rig.transform.position, beforeZoom) - 2.4f) < 0.001f,
                "Injected wheel notch moves the visitor at the tripled default speed");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint });
            yield return null;
            beforeZoom = rig.transform.position;
            var wristButton = GameObject.Find("Partner wrist controls").transform.Find("PAUSE button").GetComponent<RectTransform>();
            var wristPoint = (Vector2)Camera.main.WorldToScreenPoint(wristButton.TransformPoint(wristButton.rect.center));
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = wristPoint, scroll = new Vector2(0, 120f) });
            yield return null;
            CheckInput(Vector3.Distance(beforeZoom, rig.transform.position) < 0.001f, "Wheel over world wrist UI leaves camera unchanged");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint });
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.C));
            yield return null;
            CheckInput(!rig.FreeFlight, "C key selects optional orbital inspection");
            var beforeMove = rig.transform.position;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.W, Key.E, Key.LeftShift));
            yield return new WaitForSecondsRealtime(0.15f);
            CheckInput(Vector3.Distance(beforeMove, rig.transform.position) > 0.1f, "W/E/Shift moves the observer");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
            yield return null;
            CheckInput(rig.FreeFlight && Mathf.Approximately(rig.Distance, ExhibitCameraRig.DefaultDistance), "F key recovers VR-style visitor view");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.H));
            yield return null;
            CheckInput(prototype.LearningVisible, "H opens learning panels");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.H));
            yield return null;
            CheckInput(!prototype.LearningVisible, "H returns to observation");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            prototype.SetLearningVisible(true);
            var seek = GameObject.Find("Learning controls").GetComponentsInChildren<Slider>(true)[1];
            seek.value = 0.25f;
            yield return null;
            rig.Focus(ExhibitCameraRig.ObservationTarget.BodyA);
            var trackedOffset = rig.transform.position - rig.FocusPoint;
            var firstPosition = rig.transform.position;
            seek.value = 0.40f;
            yield return null;
            CheckInput(Vector3.Distance(firstPosition, rig.transform.position) > 0.2f, "Focused camera follows the body's changed position");
            CheckInput(Vector3.Distance(trackedOffset, rig.transform.position - rig.FocusPoint) < 0.001f, "Body tracking retains observation offset");
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            InputSystem.RemoveDevice(testMouse);
            InputSystem.RemoveDevice(testKeyboard);
            if (originalMouse != null) originalMouse.MakeCurrent();
            if (originalKeyboard != null) originalKeyboard.MakeCurrent();
            if (inputChecksPassed) Debug.Log("DTET_NAVIGATION_INPUT_PASS");
        }

        private void CheckInput(bool condition, string label)
        {
            if (condition) return;
            inputChecksPassed = false;
            Debug.LogError("DTET_NAVIGATION_INPUT_FAILED " + label);
        }

        private static void CheckUiCommands()
        {
            var controls = GameObject.Find("Learning controls");
            if (controls == null) throw new InvalidOperationException("Learning controls missing");
            var story = controls.transform.Find("Story content");
            var graph = controls.transform.Find("Graph content");
            var model = controls.transform.Find("Model content");
            Click("Learning controls", "GRAPH button");
            Require(graph.gameObject.activeSelf && !story.gameObject.activeSelf, "Graph layer switch");
            Click("Learning controls", "MODEL button");
            Require(model.gameObject.activeSelf && !graph.gameObject.activeSelf, "Model layer switch");
            Click("Learning controls", "STORY button");
            Require(story.gameObject.activeSelf && !model.gameObject.activeSelf, "Story layer switch");

            var sliders = controls.GetComponentsInChildren<Slider>(true);
            Require(sliders.Length == 3, "Three controls should have sliders");
            sliders[0].value = 40f;
            Require(HasText(controls, "Next start: 40"), "Separation change remains pending");
            Click("Learning controls", "APPLY START button");
            Require(HasText(controls, "Current start: 40"), "Apply commits start");
            Require(Mathf.Approximately(sliders[1].value, 0f), "Apply resets storyboard seek");

            Click("Learning controls", "B  /  WIDER button");
            Require(Mathf.Approximately(sliders[0].value, 45f), "Wider preset selects 45 GM/c²");
            Click("Learning controls", "A  /  CLOSER button");
            Require(Mathf.Approximately(sliders[0].value, 30f), "Closer preset restores 30 GM/c²");
            Click("Learning controls", "RESET button");
            Require(HasText(controls, "Illustrative playback reset"), "Reset feedback");

            var check = GameObject.Find("One-question formative check");
            Text feedback = null;
            foreach (var label in check.GetComponentsInChildren<Text>(true))
                if (label.text.StartsWith("Choose an answer")) feedback = label;
            Require(feedback != null, "Checkpoint feedback label");
            var originalColor = feedback.color;
            Click("One-question formative check", "RISE button");
            Require(feedback.text.StartsWith("Yes."), "Correct-answer feedback");
            feedback.text = "Choose an answer, then compare A and B.";
            feedback.color = originalColor;

            Click("Learning overlay", "DIM / NORMAL button");
            Require(HasText(GameObject.Find("DT-ET world-space learning board"), "View: dimmed"), "Dim view");
            Click("Learning overlay", "DIM / NORMAL button");
            Require(HasText(GameObject.Find("DT-ET world-space learning board"), "View: normal"), "Normal view");
        }

        private static void CheckObservationCommands(VisualPrototypeBootstrap prototype)
        {
            var rig = prototype.Observer;
            Require(rig != null && !Camera.main.orthographic, "Perspective observation camera");
            prototype.SetLearningVisible(false);
            Require(Mathf.Approximately(Camera.main.rect.width, 1f), "Observation uses full viewport");
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Above);
            var above = rig.transform.position;
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Side);
            Require(Vector3.Distance(above, rig.transform.position) > 5f, "Above and side viewpoints differ");
            rig.Focus(ExhibitCameraRig.ObservationTarget.BodyA);
            Require(rig.Target == ExhibitCameraRig.ObservationTarget.BodyA && rig.Distance < 5f, "Focus follows first body at close range");
            rig.Zoom(20f);
            Require(Mathf.Approximately(rig.Distance, ExhibitCameraRig.MinimumDistance), "Close zoom bound");
            for (var i = 0; i < 4; i++) rig.Zoom(-20f);
            Require(Mathf.Approximately(rig.Distance, ExhibitCameraRig.MaximumDistance), "Far zoom bound");
            rig.Rotate(new Vector2(360f, 10000f));
            Require(float.IsFinite(rig.transform.position.y), "Extreme orbit input remains finite");
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            var home = rig.transform.position;
            Require(rig.FreeFlight, "VR-style first-person observation is the default");
            rig.MoveLocal(new Vector3(1f, 1f, 1f));
            Require(Vector3.Distance(home, rig.transform.position) > 0.5f, "Free-flight translation");
            rig.Rotate(new Vector2(30f, 10f));
            rig.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            Require(rig.FreeFlight && Vector3.Distance(home, rig.transform.position) < 0.001f, "Overview recovers home pose");
            prototype.SetLearningVisible(true);
            Require(Mathf.Approximately(Camera.main.rect.width, 1f), "Learning board does not shrink head view");
            var sliders = GameObject.Find("Learning controls").GetComponentsInChildren<Slider>(true);
            Require(Mathf.Approximately(sliders[1].value, 0f), "Camera navigation leaves storyboard state unchanged");
            Debug.Log("DTET_OBSERVATION_COMMANDS_PASS");
        }

        private static void Click(string parentName, string childName)
        {
            var parent = GameObject.Find(parentName);
            var child = DirectChild(parent, childName);
            var button = child == null ? null : child.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Missing button: " + parentName + "/" + childName);
            button.onClick.Invoke();
        }

        private static Transform DirectChild(GameObject parent, string name)
        {
            if (parent != null)
                foreach (Transform candidate in parent.transform)
                    if (candidate.name == name) return candidate;
            return null;
        }

        private static bool HasText(GameObject root, string start)
        {
            foreach (var label in root.GetComponentsInChildren<Text>(true))
                if (label.text.StartsWith(start)) return true;
            return false;
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }
    }
}
#endif
