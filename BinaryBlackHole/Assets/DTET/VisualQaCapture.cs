#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>Development-build visual receipt. Does nothing unless DTET_CAPTURE_PATH is set.</summary>
    internal sealed class VisualQaCapture : MonoBehaviour
    {
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
                CheckUiCommands();
                Debug.Log("DTET_UI_COMMANDS_PASS");
            }
            catch (Exception error)
            {
                Debug.LogError("DTET_UI_COMMANDS_FAILED " + error);
                Application.Quit(1);
                yield break;
            }
            var variant = Environment.GetEnvironmentVariable("DTET_CAPTURE_VARIANT") ?? "Story";
            switch (variant.ToLowerInvariant())
            {
                case "graph": prototype.SetQualityCheckView(1, 0.5f); break;
                case "model": prototype.SetQualityCheckView(2, 0.75f); break;
                case "endpoint": prototype.SetQualityCheckView(0, 0.99f); break;
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

            Click("DT-ET desktop exhibit HUD", "DIM / NORMAL button");
            Require(HasText(GameObject.Find("DT-ET desktop exhibit HUD"), "View: dimmed"), "Dim view");
            Click("DT-ET desktop exhibit HUD", "DIM / NORMAL button");
            Require(HasText(GameObject.Find("DT-ET desktop exhibit HUD"), "View: normal"), "Normal view");
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
