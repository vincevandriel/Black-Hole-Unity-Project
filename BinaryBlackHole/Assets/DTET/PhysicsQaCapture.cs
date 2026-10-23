#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    /// <summary>Opt-in built-player integration checks. Does nothing in ordinary sessions.</summary>
    internal sealed class PhysicsQaCapture : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private bool passed = true;
        private string output;
        private VisualPrototypeBootstrap prototype;
        private Transform bodyA, bodyB;

        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string modelId, unityVersion, graphicsDevice, scope;
            public string[] checks;
            public double metersPerDisplayUnit, initialSeparationKm, initialFrequencyHz, cutoffSeconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_PHYSICS_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("DT-ET physical integration receipt").AddComponent<PhysicsQaCapture>();
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
            if (!condition) { passed = false; Debug.LogError("DTET_PHYSICS_FAILED " + label); }
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_PHYSICS_QA_PATH"));
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            prototype = FindFirstObjectByType<VisualPrototypeBootstrap>();
            if (prototype == null) { Check(false, "bootstrap available"); Application.Quit(1); yield break; }
            prototype.Observer.PersistInputSettings = false;
            prototype.Observer.EmulatedInputEnabled = false;
            bodyA = GameObject.Find("Body A - illustrated black-hole marker").transform;
            bodyB = GameObject.Find("Body B - illustrated black-hole marker").transform;
            Check(prototype.PhysicalMode && !prototype.CaptureHudSnapshot().Playing, "physical mode starts paused");
            var initial = prototype.PhysicalFrame;
            Check(initial.Model.Parameters.Mass1Kilograms == initial.Model.Parameters.Mass2Kilograms, "original equal-mass lesson retained");
            Check(initial.Model.Parameters.InitialSeparationMeters == StagingScenarios.Baseline.InitialSeparationMeters, "baseline start selected");
            CheckSync("initial");

            prototype.SetLearningVisible(true);
            var sliders = GameObject.Find("Learning controls").GetComponentsInChildren<Slider>(true);
            sliders[1].value = .35f;
            yield return new WaitForEndOfFrame();
            Check(!prototype.CaptureHudSnapshot().Playing && Math.Abs(prototype.PhysicalFrame.Progress - .35) < 1e-6, "timeline slider seeks source session and pauses");
            CheckSync("seek");
            var sought = prototype.PhysicalFrame;
            prototype.SetHudPlaybackRate(4);
            Check(prototype.PhysicalFrame.State.GravitationalWaveFrequencyHertz == sought.State.GravitationalWaveFrequencyHertz, "rate leaves instantaneous physical Hz unchanged");
            prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.2f);
            prototype.TogglePlayback();
            Check(prototype.PhysicalFrame.PlaybackTimeSeconds > sought.PlaybackTimeSeconds, "built-player Update advances physical clock");
            CheckSync("playback");
            var held = prototype.PhysicalFrame;
            var heldA = bodyA.position;
            var displayTime = Shader.GetGlobalFloat("_StoryDisplayTime");
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Above);
            yield return new WaitForSecondsRealtime(.15f);
            Check(prototype.PhysicalFrame.PlaybackTimeSeconds == held.PlaybackTimeSeconds && bodyA.position == heldA, "pause freezes body and source time while camera moves");
            Check(Shader.GetGlobalFloat("_StoryDisplayTime") == displayTime, "paused display clock is frozen");
            prototype.SeekStoryboard(.8f);prototype.SeekStoryboard((float)sought.Progress);
            yield return new WaitForEndOfFrame();
            Check(Math.Abs(prototype.PhysicalFrame.State.SeparationMeters - sought.State.SeparationMeters) < .1, "seek replay restores separation");
            CheckSync("repeat seek");

            var version = prototype.PhysicalFrame.ScenarioVersion;
            Click("B  /  WIDER button");
            Check(prototype.PhysicalFrame.ScenarioVersion > version && prototype.PhysicalFrame.PlaybackTimeSeconds == 0, "scenario button publishes new generation at paused start");
            Check(prototype.PhysicalFrame.Model.Parameters.InitialSeparationMeters > initial.Model.Parameters.InitialSeparationMeters, "wider A/B comparison changes SI separation");
            Click("C / UNEQUAL button");
            prototype.SeekStoryboard(.42f);
            yield return new WaitForEndOfFrame();
            var unequal = prototype.PhysicalFrame;
            CheckSync("unequal masses");
            var centre = new Vector3(-3.25f, 0, 0);
            var weighted = 20 * (bodyA.position - centre) + 40 * (bodyB.position - centre);
            Check(weighted.magnitude < 1e-4, "rendered unequal-mass barycentre");
            Check(Math.Abs(Vector3.Distance(bodyA.position, centre) / Vector3.Distance(bodyB.position, centre) - 2) < 1e-4, "rendered orbit radius ratio");
            sliders[0].value = 40;
            Click("APPLY START button");
            Check(Math.Abs(prototype.PhysicalFrame.Model.Parameters.InitialSeparationMeters / StagingScenarios.GravitationalRadiusMeters - 40) < 1e-8
                && prototype.PhysicalFrame.Model.Parameters.Mass1Kilograms < prototype.PhysicalFrame.Model.Parameters.Mass2Kilograms, "initial-distance control preserves unequal masses");
            prototype.SeekStoryboard(.6f);prototype.SetHudPlaybackRate(-1);prototype.TogglePlayback();
            var reverseStart = prototype.PhysicalFrame.PlaybackTimeSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            prototype.TogglePlayback();
            Check(prototype.PhysicalFrame.PlaybackTimeSeconds < reverseStart, "built-player reverse advances toward earlier source time");
            CheckSync("reverse");
            foreach (var graph in FindObjectsByType<HudSignalGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Check(ReferenceEquals(graph.Model, prototype.PhysicalFrame.Model), "graph uses same model instance: " + graph.name);
            var texts = prototype.PartnerHud.GetComponentsInChildren<Text>(true);
            bool frequencyReadout = false;
            foreach (var text in texts)
                if (text.text == $"{prototype.PhysicalFrame.State.GravitationalWaveFrequencyHertz.Value:0.0} Hz") frequencyReadout = true;
            Check(frequencyReadout, "world HUD displays current model frequency");
            Check(!DirectChild(bodyA, "Layered accretion illustration / inner").gameObject.activeSelf, "artistic gas hidden in physical mode");

            prototype.SetQualityCheckView(1, .5f);prototype.Observer.FocusLearning();
            yield return Capture("physical-graph.png");
            prototype.SetLearningVisible(false);
            prototype.Observer.transform.position = new Vector3(-7.3f, 1.2f, -3.8f);
            prototype.Observer.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            yield return Capture("physical-readouts.png");
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Above);
            yield return Capture("physical-above.png");

            prototype.SeekStoryboard(1);
            yield return new WaitForEndOfFrame();
            var endpoint = prototype.CaptureHudSnapshot();
            Check(endpoint.ConceptualEndpoint && !endpoint.WaveFrequencyHz.HasValue && !endpoint.OrbitsPerSecond.HasValue, "cutoff removes physical frequency readouts");
            Check(!bodyA.gameObject.activeSelf && !bodyB.gameObject.activeSelf, "conceptual endpoint has separate marker presentation");
            prototype.SetQualityCheckView(2, 1);prototype.Observer.FocusLearning();
            yield return Capture("physical-cutoff.png");
            prototype.SetHudPlaybackRate(-1);prototype.TogglePlayback();
            yield return new WaitForSecondsRealtime(.1f);
            prototype.TogglePlayback();
            Check(prototype.PhysicalFrame.PlaybackTimeSeconds < prototype.PhysicalFrame.Model.EndTimeSeconds, "reverse starts at final endpoint without resetting");
            prototype.ResetPlayback();
            Check(prototype.PhysicalFrame.PlaybackTimeSeconds == 0 && !prototype.PhysicalFrame.Playing, "reset pauses at source start");
            prototype.SetPhysicsMode(false);prototype.SeekStoryboard(.3f);
            yield return new WaitForEndOfFrame();
            Check(!prototype.CaptureHudSnapshot().PhysicalModel && DirectChild(bodyA, "Layered accretion illustration / inner").gameObject.activeSelf, "artistic mode remains available with its gas illustration");
            CheckBoardLabels(false);
            prototype.SetQualityCheckView(1, .3f);prototype.Observer.FocusLearning();
            yield return Capture("artistic-graph-labels.png");
            prototype.SetHudPlaybackRate(-1);
            Check(Mathf.Approximately(prototype.CaptureHudSnapshot().PlaybackRate, .1f), "artistic lower playback bound preserved");
            prototype.SetPhysicsMode(true);
            yield return new WaitForEndOfFrame();
            CheckBoardLabels(true);
            CheckSync("return to physics");
            var receipt = new Receipt {
                success = passed, modelId = PhysicalSceneSnapshot.ModelId, unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName, checks = checks.ToArray(),
                scope = "Built-player physical model/transform/HUD/timeline integration. Not merger, gas, GR optics, VR or laptop performance validation.",
                metersPerDisplayUnit = initial.MetersPerDisplayUnit,
                initialSeparationKm = initial.State.SeparationMeters / 1000,
                initialFrequencyHz = initial.State.GravitationalWaveFrequencyHertz.Value,
                cutoffSeconds = initial.Model.CutoffTimeSeconds
            };
            File.WriteAllText(Path.Combine(output, "physical-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_PHYSICS_INTEGRATION_PASS" : "DTET_PHYSICS_INTEGRATION_FAILED");
            Application.Quit(passed ? 0 : 1);
        }

        private void CheckBoardLabels(bool physical)
        {
            var texts = GameObject.Find("Learning controls").GetComponentsInChildren<Text>(true);
            bool axis = false, legend = false, rate = false;
            foreach (var text in texts)
            {
                if (text.text == (physical ? "f_GW (Hz)" : "Illustrative")) axis = true;
                if (physical ? text.text.Contains(" Hz / 0–") : text.text == "Qualitative amplitude / story position 0–100%") legend = true;
                if (text.text == (physical ? "PLAYBACK RATE / SOURCE TIME" : "PLAYBACK RATE / STORY POSITION")) rate = true;
            }
            Check(axis && legend && rate, (physical ? "physical" : "artistic") + " board restores matching axis, legend and time labels");
            foreach (var graph in FindObjectsByType<HudSignalGraphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Check(physical ? ReferenceEquals(graph.Model, prototype.PhysicalFrame.Model) : graph.Model == null,
                    (physical ? "physical" : "artistic") + " graph binding restored: " + graph.name);
        }

        private void CheckSync(string label)
        {
            var f = prototype.PhysicalFrame;
            var centre = new Vector3(-3.25f, 0, 0);
            var expectedA = centre + new Vector3((float)f.Body1X, 0, (float)f.Body1Z);
            var expectedB = centre + new Vector3((float)f.Body2X, 0, (float)f.Body2Z);
            Check(Vector3.Distance(bodyA.position, expectedA) < 1e-5 && Vector3.Distance(bodyB.position, expectedB) < 1e-5, label + " transforms match SI snapshot");
            Check(Math.Abs(Vector3.Distance(bodyA.position, bodyB.position) * f.MetersPerDisplayUnit - f.State.SeparationMeters) < 1,
                label + " render separation within 1 metre");
        }

        private static void Click(string name)
        {
            // Labels contain '/', which Transform.Find treats as a hierarchy separator.
            foreach (Transform child in GameObject.Find("Learning controls").transform)
                if (child.name == name) { child.GetComponent<Button>().onClick.Invoke(); return; }
            throw new InvalidOperationException("Missing learning control: " + name);
        }

        private static Transform DirectChild(Transform parent, string name)
        {
            foreach (Transform child in parent) if (child.name == name) return child;
            throw new InvalidOperationException("Missing direct child: " + name);
        }

        private IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            var path = Path.Combine(output, name);
            DevelopmentFrameCapture.WritePng(Camera.main, path);
            for (int i = 0; i < 30 && !File.Exists(path); i++) yield return null;
            Check(File.Exists(path), "capture " + name);
        }
    }
}
#endif
