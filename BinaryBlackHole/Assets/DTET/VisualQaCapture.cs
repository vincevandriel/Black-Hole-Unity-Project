#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DTET.VisualPrototype
{
    /// <summary>Development-build visual receipt. Does nothing unless DTET_CAPTURE_PATH is set.</summary>
    internal sealed class VisualQaCapture : MonoBehaviour
    {
        private bool inputChecksPassed = true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_CAPTURE_PATH"))) return;
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
                    prototype.SetQualityCheckView(0, 0.25f);
                    prototype.SetLearningVisible(false);
                    prototype.Observer.SetPreset(variant.ToLowerInvariant() == "above" ? ExhibitCameraRig.ViewPreset.Above
                        : variant.ToLowerInvariant() == "side" ? ExhibitCameraRig.ViewPreset.Side : ExhibitCameraRig.ViewPreset.Overview);
                    if (variant.ToLowerInvariant() == "close") prototype.Observer.Focus(ExhibitCameraRig.ObservationTarget.BodyA);
                    break;
                case "scenariob":
                    Click("Learning controls", "B  /  WIDER button");
                    prototype.SetQualityCheckView(0, 0.25f);
                    break;
                default: prototype.SetQualityCheckView(0, 0.25f); break;
            }
            Debug.Log("DTET_QA_VIEW " + variant);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return new WaitForEndOfFrame();
            var path = Environment.GetEnvironmentVariable("DTET_CAPTURE_PATH");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("DTET_CAPTURE_WRITTEN " + path);
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
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
            var viewportPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var rotation = rig.transform.rotation;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint, delta = new Vector2(180f, 25f) }.WithButton(MouseButton.Right));
            yield return null;
            CheckInput(Quaternion.Angle(rotation, rig.transform.rotation) > 5f, "Right-drag changes observation angle");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint });
            yield return null;
            var beforeZoom = rig.Distance;
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint, scroll = new Vector2(0, 120f) });
            yield return null;
            CheckInput(rig.Distance < beforeZoom, "Wheel moves closer in viewport");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint });
            yield return null;
            beforeZoom = rig.Distance;
            // Observation controls occupy the lower-left corner; wheel input there belongs to UI.
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = new Vector2(80f, 65f), scroll = new Vector2(0, 120f) });
            yield return null;
            CheckInput(Mathf.Approximately(beforeZoom, rig.Distance), "Wheel over UI leaves camera unchanged");
            InputSystem.QueueStateEvent(testMouse, new MouseState { position = viewportPoint });
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.C));
            yield return null;
            CheckInput(rig.FreeFlight, "C key selects free flight");
            var beforeMove = rig.transform.position;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.W, Key.E, Key.LeftShift));
            yield return new WaitForSecondsRealtime(0.15f);
            CheckInput(Vector3.Distance(beforeMove, rig.transform.position) > 0.1f, "W/E/Shift moves the observer");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
            yield return null;
            CheckInput(!rig.FreeFlight && Mathf.Approximately(rig.Distance, 13.5f), "F key recovers overview");
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
            Require(HasText(GameObject.Find("DT-ET desktop exhibit HUD"), "View: dimmed"), "Dim view");
            Click("Learning overlay", "DIM / NORMAL button");
            Require(HasText(GameObject.Find("DT-ET desktop exhibit HUD"), "View: normal"), "Normal view");
        }

        private static void CheckObservationCommands(VisualPrototypeBootstrap prototype)
        {
            var rig = prototype.Observer;
            Require(rig != null && !Camera.main.orthographic, "Perspective observation camera");
            prototype.SetLearningVisible(false);
            Require(Mathf.Approximately(Camera.main.rect.width, 1f), "Observation uses full viewport");
            Click("Observation controls", "ABOVE button");
            var above = rig.transform.position;
            Click("Observation controls", "SIDE button");
            Require(Vector3.Distance(above, rig.transform.position) > 5f, "Above and side viewpoints differ");
            Click("Observation controls", "FOCUS A button");
            Require(rig.Target == ExhibitCameraRig.ObservationTarget.BodyA && rig.Distance < 5f, "Focus follows first body at close range");
            rig.Zoom(20f);
            Require(Mathf.Approximately(rig.Distance, ExhibitCameraRig.MinimumDistance), "Close zoom bound");
            for (var i = 0; i < 4; i++) rig.Zoom(-20f);
            Require(Mathf.Approximately(rig.Distance, ExhibitCameraRig.MaximumDistance), "Far zoom bound");
            rig.Rotate(new Vector2(360f, 10000f));
            Require(float.IsFinite(rig.transform.position.y), "Extreme orbit input remains finite");
            Click("Observation controls", "OVERVIEW / F button");
            var home = rig.transform.position;
            Click("Observation controls", "ORBIT / FLY / C button");
            Require(rig.FreeFlight, "Free-flight mode selected");
            rig.MoveLocal(new Vector3(1f, 1f, 1f));
            Require(Vector3.Distance(home, rig.transform.position) > 0.5f, "Free-flight translation");
            rig.Rotate(new Vector2(30f, 10f));
            Click("Observation controls", "OVERVIEW / F button");
            Require(!rig.FreeFlight && Vector3.Distance(home, rig.transform.position) < 0.001f, "Overview recovers home pose");
            prototype.SetLearningVisible(true);
            Require(Mathf.Approximately(Camera.main.rect.width, 0.68f), "Learning viewport reserves panel space");
            var sliders = GameObject.Find("Learning controls").GetComponentsInChildren<Slider>(true);
            Require(Mathf.Approximately(sliders[1].value, 0f), "Camera navigation leaves storyboard state unchanged");
            Debug.Log("DTET_OBSERVATION_COMMANDS_PASS");
        }

        private static void Click(string parentName, string childName)
        {
            var parent = GameObject.Find(parentName);
            Transform child = null;
            if (parent != null)
                foreach (Transform candidate in parent.transform)
                    if (candidate.name == childName) child = candidate;
            var button = child == null ? null : child.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Missing button: " + parentName + "/" + childName);
            button.onClick.Invoke();
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
