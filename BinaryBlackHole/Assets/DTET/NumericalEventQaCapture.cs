#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Opt-in numerical replay checks against the actual built-player scene and HUD.
    internal sealed class NumericalEventQaCapture : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private bool passed = true;
        private string output;
        private VisualPrototypeBootstrap prototype;
        private Transform bodyA, bodyB, remnant;

        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string modelId, unityVersion, graphicsDevice, scope;
            public string[] checks;
            public double metersPerDisplayUnit, durationSeconds, commonHorizonElapsedSeconds;
            public int horizonARows, horizonBRows, commonHorizonRows, waveformRows;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_NUMERICAL_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("DT-ET numerical integration receipt").AddComponent<NumericalEventQaCapture>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) { passed = false; Application.Quit(1); }
        }

        private void Check(bool condition, string label)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) { passed = false; Debug.LogError("DTET_NUMERICAL_FAILED " + label); }
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_NUMERICAL_QA_PATH"));
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            prototype = FindFirstObjectByType<VisualPrototypeBootstrap>();
            if (prototype == null) { Check(false, "bootstrap available"); Application.Quit(1); yield break; }
            prototype.Observer.PersistInputSettings = false;
            prototype.Observer.EmulatedInputEnabled = false;
            bodyA = FindTransform("Body A - illustrated black-hole marker");
            bodyB = FindTransform("Body B - illustrated black-hole marker");
            remnant = FindTransform("Conceptual remnant - not simulated");
            prototype.SetLearningVisible(false);
            prototype.SetNumericalMode();
            yield return new WaitForEndOfFrame();
            Check(prototype.NumericalMode && !prototype.LearningVisible, "numerical shortcut path works with the learning board hidden");
            prototype.SetPhysicsMode(true);
            yield return new WaitForEndOfFrame();
            Check(prototype.PhysicalMode && !prototype.NumericalMode && !prototype.LearningVisible, "leaving numerical mode works with the board hidden");
            prototype.SetLearningVisible(true);
            FindTransform("SXS REPLAY / N button").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForEndOfFrame();
            var session = prototype.NumericalSession;
            var model = session.Model;
            Check(prototype.NumericalMode && !prototype.PhysicalMode && !session.IsPlaying && session.PlaybackTimeSeconds == 0,
                "SXS button selects a paused numerical history");
            Check(model.HorizonACount > 1000 && model.HorizonBCount > 1000 && model.CommonHorizonCount > 1000 && model.WaveformCount > 1000,
                "runtime asset contains horizon and radiation histories");
            Check(VisualPrototypeBootstrap.NumericalMetersPerDisplayUnit == 250000, "fixed numerical display scale is 250 km per unit");
            prototype.StellarField.ToggleLensing();prototype.StellarField.ToggleRayPaths();
            Check(!prototype.StellarField.ApproximationAvailable && !prototype.StellarField.LensingEnabled && !prototype.StellarField.ShowRayPaths,
                "unrelated approximate lens and ray controls stay unavailable in numerical mode");
            CheckBodies("relaxed start");
            var sliders = FindTransform("Learning controls").GetComponentsInChildren<Slider>(true);
            Check(!sliders[0].interactable && Math.Abs(sliders[2].minValue - .01) < 1e-8,
                "fixed event disables distance editing and allows slow playback");
            Check(!DirectChild(bodyA, "Layered accretion illustration / inner").gameObject.activeSelf
                && !DirectChild(bodyB, "Layered accretion illustration / inner").gameObject.activeSelf
                && !DirectChild(remnant, "Layered accretion illustration / inner").gameObject.activeSelf,
                "artistic gas is hidden on all numerical markers");

            sliders[1].value = .35f;
            yield return new WaitForEndOfFrame();
            Check(Math.Abs(session.PlaybackTimeSeconds / model.DurationSeconds - .35) < 1e-6 && !session.IsPlaying,
                "timeline slider seeks numerical source history and pauses");
            CheckBodies("timeline seek");
            var soughtTime = session.PlaybackTimeSeconds;
            var soughtA = bodyA.position;
            var soughtRadiation = session.State.Radiation;
            prototype.SetHudPlaybackRate(.01f);
            Check(session.PlaybackRate == .01 && session.PlaybackTimeSeconds == soughtTime
                && session.State.Radiation.HasValue == soughtRadiation.HasValue
                && (!soughtRadiation.HasValue || session.State.Radiation.Value.RealH22 == soughtRadiation.Value.RealH22),
                "0.01x rate boundary is accepted without changing recorded state");
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.2f);
            if (session.IsPlaying) prototype.TogglePlayback();
            Check(session.PlaybackTimeSeconds > soughtTime && !session.IsPlaying, "built-player Update advances numerical playback at 0.01x");
            var forwardTime = session.PlaybackTimeSeconds;
            prototype.ReversePlayback();
            Check(session.PlaybackRate == -.01, "reverse control preserves minimum rate magnitude");
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.2f);
            if (session.IsPlaying) prototype.TogglePlayback();
            Check(session.PlaybackTimeSeconds < forwardTime, "built-player reverse moves to earlier source time");
            prototype.SeekStoryboard(.8f);
            prototype.SeekStoryboard(.35f);
            yield return new WaitForEndOfFrame();
            Check(session.PlaybackTimeSeconds == soughtTime && bodyA.position == soughtA, "repeated timeline seek restores the exact scene pose");
            var heldTime = session.PlaybackTimeSeconds;
            var displayTime = Shader.GetGlobalFloat("_StoryDisplayTime");
            var cameraBefore = prototype.Observer.transform.position;
            prototype.SetLearningVisible(false);
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Above);
            yield return new WaitForSecondsRealtime(.15f);
            Check(prototype.Observer.transform.position != cameraBefore && session.PlaybackTimeSeconds == heldTime
                && bodyA.position == soughtA && Shader.GetGlobalFloat("_StoryDisplayTime") == displayTime,
                "pause freezes numerical history while the observer changes viewpoint");
            yield return Capture("numerical-above.png");
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            yield return Capture("numerical-overview.png");

            prototype.SetLearningVisible(true);
            // The exact native transition must be reached by its dedicated command, not a rounded float slider fraction.
            ClickLearning("APPLY START button");
            yield return new WaitForEndOfFrame();
            Check(session.PlaybackTimeSeconds == model.CommonHorizonElapsedSeconds
                && session.State.NativeCoordinateTime == model.CommonHorizonNativeTime && !session.IsPlaying,
                "TO MERGER button reaches the exact first common horizon sample");
            Check(session.State.HasCommonHorizon && !bodyA.gameObject.activeSelf && !bodyB.gameObject.activeSelf && remnant.gameObject.activeSelf,
                "common horizon replaces the two individual markers at the data boundary");
            CheckBodies("first common horizon");
            Check(session.State.Radiation.HasValue && session.State.SourceReferenceAlignedSeconds == 0
                && session.State.Radiation.Value.NativeRetardedTime == model.WaveformPeakNativeTime,
                "common-horizon and radiation-peak comparison epochs align explicitly");
            Check(prototype.CaptureHudSnapshot().NumericalModel && !prototype.CaptureHudSnapshot().ConceptualEndpoint
                && !prototype.CaptureHudSnapshot().WaveFrequencyHz.HasValue,
                "numerical HUD identifies recorded merger without inventing analytic Hz");
            CheckGraphs(model);
            Check(HasText(prototype.PartnerHud.transform, "RADIATION |H22| / DIMENSIONLESS")
                && HasText(prototype.PartnerHud.transform, $"{session.State.Radiation.Value.Amplitude:0.0000}")
                && HasText(prototype.PartnerHud.transform, "REMNANT MASS / SOLAR MASSES")
                && HasText(prototype.PartnerHud.transform, $"{session.State.Remnant.Value.MassSolar:0.00}"),
                "world readouts show current radiation amplitude and remnant mass");
            prototype.SetLearningVisible(false);
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            yield return Capture("numerical-merger.png");
            prototype.Observer.transform.position = new Vector3(-7.3f, 1.2f, -3.8f);
            prototype.Observer.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            yield return Capture("numerical-readouts.png");
            prototype.SetQualityCheckView(1, .65f);
            prototype.Observer.FocusLearning();
            yield return Capture("numerical-radiation-board.png");

            prototype.SeekStoryboard((float)((model.CommonHorizonElapsedSeconds - .001) / model.DurationSeconds));
            yield return new WaitForEndOfFrame();
            Check(!session.State.HasCommonHorizon && bodyA.gameObject.activeSelf && bodyB.gameObject.activeSelf && !remnant.gameObject.activeSelf,
                "seeking backwards across merger restores both individual horizons");
            CheckBodies("rewind before merger");
            prototype.SeekStoryboard(1);
            yield return new WaitForEndOfFrame();
            Check(session.PlaybackTimeSeconds == model.DurationSeconds && session.State.HasCommonHorizon && !session.IsPlaying,
                "timeline end clamps to the final recorded horizon");
            Check(!session.State.Radiation.HasValue
                && HasText(prototype.PartnerHud.transform, "Outside waveform coverage")
                && HasText(FindTransform("Learning controls"), "Outside waveform coverage / horizon replay continues"),
                "radiation outside coverage is unavailable in both board and world readouts");
            prototype.SetLearningVisible(false);
            prototype.Observer.transform.position = new Vector3(-7.3f, 1.2f, -3.8f);
            prototype.Observer.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            yield return Capture("numerical-after-waveform.png");
            prototype.SetHudPlaybackRate(-.01f);
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.1f);
            if (session.IsPlaying) prototype.TogglePlayback();
            Check(session.PlaybackTimeSeconds > 0 && session.PlaybackTimeSeconds < model.DurationSeconds,
                "reverse starts at final endpoint without resetting to start");
            prototype.SeekStoryboard(.001f);
            prototype.SetHudPlaybackRate(-4);
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.1f);
            Check(session.PlaybackTimeSeconds == 0 && !session.IsPlaying, "reverse clamps and pauses at start");
            prototype.SeekStoryboard(.999f);
            prototype.SetHudPlaybackRate(4);
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.1f);
            Check(session.PlaybackTimeSeconds == model.DurationSeconds && !session.IsPlaying, "forward playback clamps and pauses at final data sample");

            prototype.SetPhysicsMode(true);
            prototype.SetLearningVisible(true);
            yield return new WaitForEndOfFrame();
            CheckLegacyMode(true);
            prototype.SetNumericalMode();
            yield return new WaitForEndOfFrame();
            Check(ReferenceEquals(model, prototype.NumericalSession.Model) && !session.IsPlaying
                && session.PlaybackTimeSeconds == 0 && CountNumericalWires() == 9,
                "returning to replay reuses one data history and one set of proxy wires");
            prototype.SetPhysicsMode(false);
            yield return new WaitForEndOfFrame();
            CheckLegacyMode(false);
            var receipt = new Receipt {
                success = passed, modelId = NumericalEventHistory.ModelId, unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName, checks = checks.ToArray(),
                scope = "Built-player numerical history, scene transforms, area proxies, timeline, radiation coverage and HUD integration. Not horizon-shape, gas, observer optics, VR or laptop performance validation.",
                metersPerDisplayUnit = VisualPrototypeBootstrap.NumericalMetersPerDisplayUnit,
                durationSeconds = model.DurationSeconds, commonHorizonElapsedSeconds = model.CommonHorizonElapsedSeconds,
                horizonARows = model.HorizonACount, horizonBRows = model.HorizonBCount,
                commonHorizonRows = model.CommonHorizonCount, waveformRows = model.WaveformCount
            };
            File.WriteAllText(Path.Combine(output, "numerical-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_NUMERICAL_INTEGRATION_PASS" : "DTET_NUMERICAL_INTEGRATION_FAILED");
            Application.Quit(passed ? 0 : 1);
        }

        private void CheckBodies(string label)
        {
            var state = prototype.NumericalSession.State;
            bool poses = Matches(bodyA, state.BodyA, false) && Matches(bodyB, state.BodyB, false) && Matches(remnant, state.Remnant, false);
            bool radii = Matches(bodyA, state.BodyA, true) && Matches(bodyB, state.BodyB, true) && Matches(remnant, state.Remnant, true);
            Check(poses && radii, label + " marker positions and area radii match the SI history");
        }

        private static bool Matches(Transform body, NumericalHorizonState? state, bool radius)
        {
            if (body.gameObject.activeSelf != state.HasValue) return false;
            if (!state.HasValue) return true;
            if (!radius)
            {
                var p = state.Value.PositionMeters;
                var expected = new Vector3((float)(p.X / 250000 - 3.25), (float)(p.Z / 250000), (float)(p.Y / 250000));
                return Vector3.Distance(body.position, expected) < 1e-5;
            }
            double renderedRadius = body.localScale.x * .39 * VisualPrototypeBootstrap.NumericalMetersPerDisplayUnit;
            return Math.Abs(renderedRadius - state.Value.ArealRadiusMeters) < .1
                && body.localScale.x == body.localScale.y && body.localScale.y == body.localScale.z;
        }

        private void CheckGraphs(NumericalEventHistory model)
        {
            var graphs = FindObjectsByType<HudSignalGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool same = graphs.Length == 2;
            foreach (var graph in graphs) same &= ReferenceEquals(graph.EventModel, model) && graph.Model == null;
            Check(same, "both radiation graphs use the same numerical history");
        }

        private void CheckLegacyMode(bool physical)
        {
            var board = FindTransform("Learning controls");
            bool matchingGraphs = true;
            foreach (var graph in FindObjectsByType<HudSignalGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                matchingGraphs &= graph.EventModel == null && (physical ? ReferenceEquals(graph.Model, prototype.PhysicalFrame.Model) : graph.Model == null);
            bool wiresHidden = true;
            foreach (var body in new[] { bodyA, bodyB, remnant })
                foreach (Transform child in body)
                    if (child.name.StartsWith("Numerical", StringComparison.Ordinal)) wiresHidden &= !child.gameObject.activeSelf;
            Check(!prototype.NumericalMode && prototype.PhysicalMode == physical && matchingGraphs && wiresHidden
                && prototype.StellarField.ApproximationAvailable
                && HasText(board, physical ? "f_GW (Hz)" : "Illustrative")
                && HasText(board, physical ? "PLAYBACK RATE / SOURCE TIME" : "PLAYBACK RATE / STORY POSITION")
                && HasText(board, "APPLY START") && Math.Abs(bodyA.localScale.x - 1.08) < 1e-6,
                (physical ? "SI lesson" : "artistic storyboard") + " restores graph binding, labels, controls and marker scale");
            Check(DirectChild(bodyA, "Layered accretion illustration / inner").gameObject.activeSelf == !physical,
                (physical ? "SI lesson" : "artistic storyboard") + " restores its own gas visibility");
        }

        private int CountNumericalWires()
        {
            int count = 0;
            foreach (var body in new[] { bodyA, bodyB, remnant })
                foreach (Transform child in body)
                    if (child.name.StartsWith("Numerical areal proxy wire", StringComparison.Ordinal)) count++;
            return count;
        }

        private static bool HasText(Transform root, string exact)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true)) if (text.text == exact) return true;
            return false;
        }

        private static Transform FindTransform(string name)
        {
            foreach (var item in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.name == name) return item;
            throw new InvalidOperationException("Missing scene object: " + name);
        }

        private static Transform DirectChild(Transform parent, string name)
        {
            foreach (Transform child in parent) if (child.name == name) return child;
            throw new InvalidOperationException("Missing direct child: " + name);
        }

        private static void ClickLearning(string name)
        {
            DirectChild(FindTransform("Learning controls"), name).GetComponent<Button>().onClick.Invoke();
        }

        private IEnumerator Capture(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(output, name);
            DevelopmentFrameCapture.WritePng(Camera.main, path);
            Check(File.Exists(path) && new FileInfo(path).Length > 1000, "capture " + name);
        }
    }
}
#endif
