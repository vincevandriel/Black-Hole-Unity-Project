#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DTET.VisualPrototype
{
    internal sealed class InteractiveExhibitQaCapture : MonoBehaviour
    {
        private InteractiveBinaryExhibit exhibit;
        private string output;
        private bool passed = true;
        private double benchmarkSeconds = 3;
        private readonly List<string> checks = new List<string>();
        private readonly List<string> images = new List<string>();
        private readonly List<Window> windows = new List<Window>();
        private readonly List<Fixture> fixtures = new List<Fixture>();
        [Serializable] private sealed class Fixture
        {
            public string label;
            public double expectedX, expectedY, measuredX, measuredY, absoluteError;
        }
        [Serializable] private sealed class Window
        {
            public string profile, scenario;
            public int frames, completedReadbacks, width, height;
            public double seconds, meanMs, p95Ms, p99Ms, maximumMs;
        }
        [Serializable] private sealed class Receipt
        {
            public bool success, performanceOnly;
            public string unityVersion, graphicsDevice, graphicsApi, processor, scope, timingMethod;
            public string[] checks, images;
            public Window[] performance;
            public Fixture[] gpuFixtures;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_INTERACTIVE_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("Interactive exhibit validation").AddComponent<InteractiveExhibitQaCapture>();
        }
        private void OnEnable() => Application.logMessageReceived += Log;
        private void OnDisable() => Application.logMessageReceived -= Log;
        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) { passed = false; Application.Quit(1); }
        }
        private void Check(bool condition, string label)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) { passed = false; Debug.LogError("DTET_INTERACTIVE_FAILED " + label); }
        }
        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_INTERACTIVE_QA_PATH"));
            Directory.CreateDirectory(output);
            if (double.TryParse(Environment.GetEnvironmentVariable("DTET_INTERACTIVE_QA_BENCHMARK_SECONDS"), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
                benchmarkSeconds = Math.Max(2, Math.Min(15, seconds));
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(.75f);
            // Initialize the presentation surface before measuring ordinary camera frames.
            // A hidden Windows launch can defer that surface until the first resolution event.
            // This does not draw a camera or substitute a manual render for the pixel checks.
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.2f);
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.2f);
            exhibit = FindFirstObjectByType<InteractiveBinaryExhibit>();
            if (exhibit == null) { Check(false, "interactive launch installs the new exhibit"); Application.Quit(1); yield break; }
            exhibit.EmulatedInputEnabled = false;
            Check(!exhibit.Ready && exhibit.SceneTarget == null, "quality launcher precedes scene allocation");
            Check(FindFirstObjectByType<ReducedBinaryExhibit>() == null && FindFirstObjectByType<ReferenceFlowLab>() == null && FindFirstObjectByType<VisualPrototypeBootstrap>() == null,
                "interactive entry does not instantiate a competing simulation");
            bool performanceOnly = Environment.GetEnvironmentVariable("DTET_INTERACTIVE_PERFORMANCE_ONLY") == "1";
            if (!performanceOnly) Capture("launcher.png", true);
            if (performanceOnly)
            {
                exhibit.Launch(ExhibitQualityProfile.High); yield return null;
                exhibit.Wrist.SetRaised(true, true); exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 1, 1);
                foreach (var scenario in new[] { "replay-paused", "experiment-playback", "camera-motion", "repeated-seek" })
                    yield return Measure(scenario);
                exhibit.ReturnToLauncher(); yield return null; yield return null;
            }
            else
            {
                foreach (var p in new[] { ExhibitQualityProfile.Laptop, ExhibitQualityProfile.Balanced, ExhibitQualityProfile.High })
                    yield return Validate(p, false);
                yield return Validate(ExhibitQualityProfile.High, true);
            }
            Check(!exhibit.Ready && exhibit.SceneTarget == null && exhibit.Session == null && exhibit.Wrist == null && exhibit.Optics == null,
                "all paths release scene, session, optics and wrist references");
            var receipt = new Receipt
            {
                success = passed, performanceOnly = performanceOnly, unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), processor = SystemInfo.processorType,
                checks = checks.ToArray(), images = images.ToArray(), performance = windows.ToArray(), gpuFixtures = fixtures.ToArray(),
                scope = "Desktop recorded replay and bounded adjustable educational experiment, prescribed gas, illustrative lens/wave warp and emulated holographic wrist. No scientific GRMHD, exact binary metric, headset, human usability or laptop certification.",
                timingMethod = "Completed offscreen scene with source-level stellar optics and full interface. Stopwatch includes player-loop yield and one-pixel GPU readback completion fence. No PNG/full-image readback or file writes inside windows. Not native presentation FPS or standalone GPU timing."
            };
            File.WriteAllText(Path.Combine(output, "interactive-exhibit-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_INTERACTIVE_EXHIBIT_INTEGRATION_PASS" : "DTET_INTERACTIVE_EXHIBIT_INTEGRATION_FAILED");
            Application.Quit(passed ? 0 : 1);
        }

        private IEnumerator Validate(ExhibitQualityProfile profile, bool fallback)
        {
            exhibit.Launch(profile, fallback); yield return null;
            string prefix = fallback ? "compatibility" : profile.ToString().ToLowerInvariant();
            Check(exhibit.Ready && exhibit.Session != null && exhibit.View != null && exhibit.Wrist != null, prefix + " loads all exhibit bindings");
            Check(!exhibit.Session.IsPlaying && exhibit.Snapshot.ElapsedSeconds == 0, prefix + " starts paused at the recorded beginning");
            Check(!exhibit.Wrist.Raised, prefix + " initial binary view is unobstructed by the wrist");
            if (profile == ExhibitQualityProfile.Laptop && !fallback)
                Check(exhibit.LensGain == .35f && exhibit.WaveGain == 1.3f,
                    "first launch enables the declared mass and wave display gains");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
            if (!fallback) yield return ValidateAutomaticAccretion(prefix);
            if (!fallback) Capture(prefix + "-first-view.png");
            exhibit.Wrist.SetRaised(true, true); exhibit.Wrist.RefreshNow();
            Check(exhibit.Wrist.PresentedGeneration == exhibit.Snapshot.Generation, prefix + " wrist shares the scenario generation");
            Check(exhibit.BodyATransform.gameObject.activeSelf && exhibit.BodyBTransform.gameObject.activeSelf, prefix + " numerical binary proxies are visible");
            Check(Vector3.Distance(exhibit.BodyATransform.position, V(exhibit.Snapshot.BodyA.Value.PositionRg)) < 1e-5f,
                prefix + " body position is the authoritative snapshot position");
            Check(exhibit.GasVisible == exhibit.Profile.GasEnabled, prefix + " capability admission controls gas visibility");
            exhibit.SeekWrist(.45); exhibit.Wrist.RefreshNow();
            Check(Math.Abs(exhibit.Wrist.PresentedSourceTime - exhibit.Snapshot.ElapsedSeconds) < 1e-10, prefix + " wrist cursor shares the requested event time");
            var before = exhibit.Snapshot; var position = exhibit.View.transform.position;
            exhibit.MoveCamera(new Vector3(1, .25f, 0));
            Check(exhibit.View.transform.position != position && exhibit.Snapshot.ElapsedSeconds == before.ElapsedSeconds, prefix + " camera can move while paused");
            exhibit.SetViewPreset(0);
            var state = exhibit.CaptureWristState();
            var sampled = exhibit.SampleWristGraph(state.Progress, false);
            Check(sampled.HasValue == state.SignalAvailable && (!sampled.HasValue || Math.Abs(sampled.Value - state.Signal) < 1e-10), prefix + " numerical graph matches readout at the cursor");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 1, 1); exhibit.RenderFrame();
            Check(ReferenceEquals(exhibit.PresentedTexture, exhibit.SceneTarget), prefix + " optical off directly presents the original target");
            var originalStarlight = ReadPixels(exhibit.SceneTarget);
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 1, 1); exhibit.RenderFrame();
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 0, 0); exhibit.RenderFrame();
            Check(ReferenceEquals(exhibit.PresentedTexture, exhibit.SceneTarget) && !exhibit.Optics.Active
                && PixelDifference(originalStarlight, ReadPixels(exhibit.SceneTarget)) < 1e-6,
                prefix + " zero gains after active bending restore the actual optical-off stellar image");
            exhibit.Wrist.SetOpticalStyle(InteractiveOpticalStyle.Off);
            exhibit.ToggleStellarBending(); exhibit.RenderFrame();
            Check(exhibit.LensGain == .35f && exhibit.WaveGain == 1.3f
                && exhibit.OpticalStyle == InteractiveOpticalStyle.Enhanced && exhibit.Optics.Active == exhibit.Profile.GasEnabled,
                prefix + " transport bending restores visible defaults after wrist Off");
            exhibit.ToggleStellarBending(); exhibit.RenderFrame();
            Check(exhibit.OpticalStyle == InteractiveOpticalStyle.Off && !exhibit.Optics.Active
                && exhibit.Snapshot.ElapsedSeconds == before.ElapsedSeconds
                && PixelDifference(originalStarlight, ReadPixels(exhibit.SceneTarget)) < 1e-6,
                prefix + " transport bending Off restores source image without seeking");
            double sameSignal = exhibit.CaptureWristState().Signal;
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 1, 1); exhibit.RenderFrame();
            if (profile == ExhibitQualityProfile.Balanced && !fallback) ValidateWaveHistoryReadback("recorded replay");
            Check(exhibit.CaptureWristState().Signal == sameSignal && exhibit.Snapshot.ElapsedSeconds == before.ElapsedSeconds,
                prefix + " optical gain leaves event and signal unchanged");
            if (!fallback) Check(ReferenceEquals(exhibit.PresentedTexture, exhibit.SceneTarget) && exhibit.Optics.Active,
                prefix + " enabled stellar optics updates active uniforms without a duplicate scene target");
            else Check(ReferenceEquals(exhibit.PresentedTexture, exhibit.SceneTarget), prefix + " forced compatibility bypasses distortion");
            Capture(prefix + "-replay.png");
            var expectedPosition = exhibit.BodyATransform.position;
            double expectedSignal = exhibit.CaptureWristState().Signal;
            for (int i = 0; i < 48; i++) exhibit.SeekWrist((i * .61803398875) % 1);
            exhibit.SeekWrist(.45); exhibit.Wrist.RefreshNow();
            Check(Vector3.Distance(expectedPosition, exhibit.BodyATransform.position) < 1e-6f && expectedSignal == exhibit.CaptureWristState().Signal,
                prefix + " repeated seeks restore the same bodies and signal");
            exhibit.SeekWrist(1); exhibit.RenderFrame();
            Check(exhibit.RemnantTransform.gameObject.activeSelf && !exhibit.BodyATransform.gameObject.activeSelf, prefix + " recorded remnant switches proxies");
            Capture(prefix + "-recorded-remnant.png");

            // Exercise the actual wrist controls and their presenter callbacks.
            exhibit.Wrist.SelectScenario(false); exhibit.Wrist.RefreshNow();
            long generation = exhibit.Snapshot.Generation;
            exhibit.Wrist.SetPendingExperiment(50, 20, 35); exhibit.Wrist.ApplyExperiment(); exhibit.Wrist.RefreshNow();
            Check(exhibit.Snapshot.Generation > generation && exhibit.Snapshot.Mass1Solar == 50 && exhibit.Snapshot.Mass2Solar == 20,
                prefix + " wrist Apply replaces the experiment atomically");
            Check(Math.Abs(exhibit.Snapshot.SeparationRg - 35) < 1e-10 && !exhibit.Session.IsPlaying, prefix + " new separation applies at paused start");
            Check(exhibit.PresentedGeneration == exhibit.Wrist.PresentedGeneration && exhibit.PresentedGeneration == exhibit.Snapshot.Generation,
                prefix + " body, gas and wrist commit the same generation");
            Check(Math.Abs(exhibit.Snapshot.BodyA.Value.HorizonRadiusMeters / exhibit.Snapshot.BodyB.Value.HorizonRadiusMeters - 2.5) < 1e-10,
                prefix + " physical proxy sizes follow component masses");
            generation = exhibit.Snapshot.Generation;
            exhibit.Wrist.SetPendingExperiment(5, 80, 30); exhibit.Wrist.ApplyExperiment();
            Check(exhibit.Snapshot.Generation == generation && !string.IsNullOrWhiteSpace(exhibit.Wrist.ValidationText), prefix + " invalid mass ratio is rejected with feedback");
            exhibit.Wrist.SetPendingExperiment(35, 30, 30); exhibit.Wrist.ApplyExperiment(); exhibit.SeekWrist(.4);
            exhibit.Wrist.SetFrequencyPlot(true); exhibit.Wrist.RefreshNow();
            state = exhibit.CaptureWristState(); sampled = exhibit.SampleWristGraph(state.Progress, true);
            Check(sampled.HasValue && Math.Abs(sampled.Value - state.FrequencyHz) < 1e-9 && state.FrequencyAvailable,
                prefix + " frequency plot matches the bounded physical model");
            Check(state.SignalAvailable && !state.ReplayMode && state.SignalUnitLabel.Contains("strain"), prefix + " experiment and replay signals have distinct labels");
            if (profile == ExhibitQualityProfile.Balanced && !fallback)
            {
                exhibit.RenderFrame(); ValidateWaveHistoryReadback("adjustable physical signal");
            }
            exhibit.Wrist.SetFrequencyPlot(false); exhibit.SetViewPreset(0); Capture(prefix + "-experiment.png");
            exhibit.SeekWrist(exhibit.Session.Model.CutoffTimeSeconds / exhibit.Snapshot.DurationSeconds);
            Check(!exhibit.CaptureWristState().SignalAvailable && !exhibit.CaptureWristState().FrequencyAvailable,
                prefix + " quantitative waveform stops at the model cutoff");
            exhibit.SeekWrist(1); Capture(prefix + "-conceptual-remnant.png");
            Check(exhibit.Snapshot.IsConceptual && exhibit.RemnantTransform.gameObject.activeSelf, prefix + " adjustable remnant is explicitly conceptual");
            exhibit.SeekWrist(.35);
            double frozen = exhibit.Snapshot.ElapsedSeconds;
            exhibit.Wrist.SetReducedMotion(false); exhibit.Wrist.SetRaised(false, true); exhibit.Wrist.SetRaised(true);
            float initialReveal = exhibit.Wrist.ProjectionProgress;
            yield return new WaitForSecondsRealtime(.5f);
            Check(exhibit.Wrist.ProjectionProgress > initialReveal && exhibit.Snapshot.ElapsedSeconds == frozen,
                prefix + " hologram unfolds on UI time while the event remains paused");
            exhibit.Wrist.SetPinned(true); exhibit.Wrist.RefreshNow(); Capture(prefix + "-pinned.png");
            Check(exhibit.Wrist.Pinned && exhibit.Wrist.PresentedGeneration == exhibit.Snapshot.Generation, prefix + " pinned display keeps the same data source");
            exhibit.Wrist.SetPinned(false); exhibit.Wrist.SetReducedMotion(true); exhibit.Wrist.SetRaised(false, true); exhibit.Wrist.SetRaised(true);
            yield return null;
            Check(exhibit.Wrist.ProjectionProgress > .99f, prefix + " reduced-motion opening is immediate");
            exhibit.Wrist.SetReducedMotion(false);
            if (profile == ExhibitQualityProfile.Balanced && !fallback)
            {
                int callbacks = exhibit.AutomaticOpticalFrames;
                var automaticRequest = new RenderPipeline.StandardRequest { destination = exhibit.SceneTarget };
                RenderPipeline.SubmitRenderRequest(exhibit.View, automaticRequest);
                Check(exhibit.AutomaticOpticalFrames == callbacks + 1 && ReferenceEquals(exhibit.PresentedTexture, exhibit.Optics.Output),
                    "explicit render request invokes the shared end-camera callback");
                int uploads = exhibit.Optics.WaveUploadCount;
                exhibit.RenderFrame(); exhibit.RenderFrame();
                Check(exhibit.Optics.WaveUploadCount == uploads, "paused rendering reuses the waveform texture");
                exhibit.SetWristPlaying(true); yield return null;
                Check(exhibit.Wrist.PresentedSourceTime == exhibit.Snapshot.ElapsedSeconds,
                    "normal update order presents the same live scene and wrist epoch");
                exhibit.SetWristPlaying(false); exhibit.SeekWrist(.35); exhibit.RenderFrame();
                ValidatePointerTargets();
                Check(exhibit.Optics.ScenarioGeneration == exhibit.Snapshot.Generation && exhibit.Optics.SnapshotTime == exhibit.Snapshot.ElapsedSeconds,
                    "optics shares the active scenario generation and event time");
                exhibit.Wrist.SetPendingExperiment(8, 80, 50); exhibit.Wrist.ApplyExperiment(); exhibit.Wrist.RefreshNow();
                Check(exhibit.Wrist.HasDenseCycles, "long high-cycle experiment uses a labelled envelope band");
                bool envelopeBounds = true;
                for (int i = 0; i < 96; i++)
                {
                    double fraction = i / 96.0;
                    double? signal = exhibit.SampleWristGraph(fraction, false), envelope = exhibit.SampleWristEnvelope(fraction);
                    envelopeBounds &= !signal.HasValue || (envelope.HasValue && Math.Abs(signal.Value) <= envelope.Value + 1e-20);
                }
                Check(envelopeBounds, "phase-independent graph envelope bounds the instantaneous strain");
                exhibit.Wrist.SetPendingExperiment(80, 80, 25); exhibit.Wrist.ApplyExperiment();
                double nearCutoff = exhibit.Session.Model.CutoffTimeSeconds * .999999 / exhibit.Snapshot.DurationSeconds;
                Check(exhibit.SampleWristEnvelope(nearCutoff) < exhibit.CaptureWristState().WaveformAxisMaximum,
                    "fixed strain axis contains the largest admitted mass envelope");
                exhibit.Wrist.SetPendingExperiment(35, 30, 30); exhibit.Wrist.ApplyExperiment(); exhibit.SeekWrist(.35); exhibit.RenderFrame();
                ValidateGpuFixtures();
                for (int i = 1; i < 5; i++) { exhibit.SetViewPreset(i); Capture("view-" + i + ".png"); }
                exhibit.SetWristSamples(true); Capture("density-samples.png"); exhibit.SetWristSamples(false);
                exhibit.SetWristGas(false); Capture("gas-off.png"); exhibit.SetWristGas(true);
                exhibit.SetViewPreset(0); exhibit.Wrist.SetRaised(false, true);
                exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 0, 0); Capture("unobstructed-optics-off.png");
                exhibit.SetWristOptics(InteractiveOpticalStyle.Subtle, .35f, .25f); Capture("unobstructed-optics-subtle.png");
                exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 1, 1); exhibit.Wrist.SetRaised(true, true);
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.2f);
                exhibit.RenderFrame(); ValidatePointerTargets(); Capture("wrist-1280x720.png", width: 1280, height: 720);
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.2f);
                // Same-frame optical image determinism, independent of wall time.
                exhibit.SetViewPreset(0); exhibit.RenderFrame(); var one = ReadPixels((RenderTexture)exhibit.PresentedTexture);
                exhibit.SeekWrist(.8); exhibit.SeekWrist(.35); exhibit.RenderFrame(); var two = ReadPixels((RenderTexture)exhibit.PresentedTexture);
                Check(PixelDifference(one, two) < 1e-6, "rewind restores the same optical image without a shader clock");
                ValidateVisibleScene();
                ValidateStellarLight();
                if (Environment.GetEnvironmentVariable("DTET_STAR_MOVIE") == "1") yield return CaptureStellarSequence();
                if (Environment.GetEnvironmentVariable("DTET_DISK_MOVIE") == "1") yield return CaptureAccretionSequence();
                if (Environment.GetEnvironmentVariable("DTET_VISUAL_MOVIE") == "1") yield return CaptureMergerSequence();
            }
            foreach (var scenario in new[] { "replay-paused", "experiment-playback", "camera-motion", "repeated-seek" }) yield return Measure(scenario);
            exhibit.ReturnToLauncher(); yield return null; yield return null;
            Check(exhibit.SceneTarget == null && exhibit.Wrist == null && exhibit.Optics == null, prefix + " returning releases owned resources");
            bool noOwnedTargets = true;
            foreach (var texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (texture.name == "Bounded interactive scene" || texture.name == "Capped astronomical optics") noOwnedTargets = false;
            Check(noOwnedTargets, prefix + " no owned astronomical render targets survive unload");
        }

        private IEnumerator ValidateAutomaticAccretion(string profile)
        {
            // These images must come from ordinary enabled-camera frames. In particular,
            // do not call RenderFrame or SubmitRenderRequest anywhere in this measurement.
            // A batch/headless environment that suppresses normal rendering fails explicitly.
            exhibit.View.enabled = true; exhibit.CaptureCamera.enabled = true;
            Check(exhibit.GasVisible && exhibit.Snapshot.ElapsedSeconds == 0 && !exhibit.Session.IsPlaying,
                profile + " ordinary startup has gas enabled at the paused initial epoch");
            int initialCallbacks = exhibit.AutomaticOpticalFrames;
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.05f);
            int initialFrames = exhibit.AutomaticOpticalFrames - initialCallbacks;
            Check(initialFrames >= 2, profile + " initial disk capture receives genuine automatic camera frames / " + initialFrames);
            var gas = ReadPixels(exhibit.SceneTarget);
            CaptureAutomaticScene(profile + "-automatic-start-gas.png");

            exhibit.SetWristGas(false);
            int offCallbacks = exhibit.AutomaticOpticalFrames;
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.05f);
            int offFrames = exhibit.AutomaticOpticalFrames - offCallbacks;
            Check(offFrames >= 2, profile + " gas-off comparison receives genuine automatic camera frames / " + offFrames);
            var noGas = ReadPixels(exhibit.SceneTarget);
            CaptureAutomaticScene(profile + "-automatic-start-no-gas.png");
            ValidateAutomaticDiskPixels(profile, gas, noGas);

            exhibit.WatchStarlight(); exhibit.SetWristPlaying(false);
            Check(!exhibit.GasVisible, profile + " starlight inspection explicitly hides gas");
            exhibit.ShowAccretionView();
            Check(exhibit.GasVisible && !exhibit.Session.IsPlaying && exhibit.Snapshot.ElapsedSeconds == 0
                && !exhibit.Wrist.Raised && !exhibit.GeneratedFluidActive,
                profile + " accretion view restores illustrative disks, paused start and lowered wrist");
            int restoredCallbacks = exhibit.AutomaticOpticalFrames;
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.05f);
            int restoredFrames = exhibit.AutomaticOpticalFrames - restoredCallbacks;
            Check(restoredFrames >= 2, profile + " restored disks receive genuine automatic camera frames / " + restoredFrames);
            var restored = ReadPixels(exhibit.SceneTarget);
            double difference = PixelDifference(gas, restored);
            fixtures.Add(new Fixture { label = profile + " automatic accretion restoration / RGB difference", measuredX = difference });
            Check(difference < 1e-6, profile + " starlight to accretion restores the actual initial disk image");
            CaptureAutomaticScene(profile + "-automatic-restored-gas.png");
            Debug.Log("DTET_AUTOMATIC_DISKS " + profile + " / " + SystemInfo.graphicsDeviceType + " / "
                + exhibit.SceneTarget.width + "x" + exhibit.SceneTarget.height + " / automatic frames "
                + initialFrames + "," + offFrames + "," + restoredFrames + " / restore difference "
                + difference.ToString("R", CultureInfo.InvariantCulture));
        }

        private void ValidateAutomaticDiskPixels(string profile, Color32[] gas, Color32[] noGas)
        {
            int width = exhibit.SceneTarget.width, height = exhibit.SceneTarget.height, warm = 0;
            for (int i = 0; i < gas.Length; i++)
                if (gas[i].r - noGas[i].r > 20 && gas[i].r > gas[i].b * 1.15f) warm++;
            fixtures.Add(new Fixture { label = profile + " automatic initial warm gas pixels", measuredX = warm,
                expectedX = gas.Length * .003 });
            Check(warm > gas.Length * .003, profile + " automatic initial image contains resolved warm gas / " + warm);
            int bodyIndex = 0;
            foreach (var body in new[] { exhibit.Snapshot.GasState.BodyA, exhibit.Snapshot.GasState.BodyB })
            {
                Vector3 centre = exhibit.View.WorldToViewportPoint(V(body.PositionRg));
                Vector3 edge = exhibit.View.WorldToViewportPoint(V(body.PositionRg)
                    + exhibit.View.transform.right * (float)body.OuterRadiusRg);
                float radius = Mathf.Abs(edge.x-centre.x) * width;
                float cx = centre.x * width, cy = centre.y * height;
                bool visible = centre.z > 0 && radius >= 4 && cx-radius > 0 && cx+radius < width
                    && cy-radius > 0 && cy+radius < height;
                Check(visible, profile + " automatic initial compact disk is framed and resolved / body " + bodyIndex);
                int resolved = 0;
                for (int y = Mathf.Max(0, (int)(cy-radius)); y < Mathf.Min(height, cy+radius); y++)
                for (int x = Mathf.Max(0, (int)(cx-radius)); x < Mathf.Min(width, cx+radius); x++)
                {
                    if ((x-cx)*(x-cx)+(y-cy)*(y-cy) > radius*radius) continue;
                    int i = y * width + x;
                    if (gas[i].r-noGas[i].r > 20 && gas[i].r > gas[i].b*1.15f) resolved++;
                }
                // Require a fraction of each projected disk, not a few shared stream pixels.
                double minimum = Math.Max(16, .025 * Math.PI * radius * radius);
                fixtures.Add(new Fixture { label = profile + " automatic initial disk " + bodyIndex + " warm pixels / radius",
                    expectedX = minimum, measuredX = resolved, measuredY = radius });
                Check(resolved > minimum, profile + " automatic initial compact disk has warm emission / body "
                    + bodyIndex + " / " + resolved + " pixels, required " + minimum.ToString("F1", CultureInfo.InvariantCulture));
                bodyIndex++;
            }
        }

        private void CaptureAutomaticScene(string name)
        {
            // Direct diagnostic readback of the already rendered linear scene target;
            // no manual draw, alternate camera target, UI render, or display tone map.
            var active = RenderTexture.active; RenderTexture.active = exhibit.SceneTarget;
            var image = new Texture2D(exhibit.SceneTarget.width, exhibit.SceneTarget.height, TextureFormat.RGB24, false, true);
            try
            {
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); images.Add(name);
                Check(new FileInfo(Path.Combine(output, name)).Length > 100, "capture " + name);
            }
            finally { Destroy(image); RenderTexture.active = active; }
        }

        private void ValidateVisibleScene()
        {
            exhibit.SelectWristScenario(true); exhibit.Wrist.SetRaised(false, true);
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
            exhibit.SeekWrist(.45); exhibit.SetViewPreset(0); exhibit.RenderFrame();
            var a = exhibit.View.WorldToViewportPoint(exhibit.BodyATransform.position);
            var b = exhibit.View.WorldToViewportPoint(exhibit.BodyBTransform.position);
            Check(a.z > 0 && b.z > 0 && a.x > .1 && a.x < .9 && b.x > .1 && b.x < .9
                && a.y > .18 && a.y < .85 && b.y > .18 && b.y < .85
                && Vector2.Distance(a, b) > .10f, "binary framing separates both horizons within the visible scene");
            Check(exhibit.BodyATransform.GetComponentsInChildren<LineRenderer>().Length == 0,
                "horizon silhouettes have no luminous coordinate wireframes");
            ValidateRayProjection();
            for (int view = 0; view < 4; view++)
            {
                exhibit.SetViewPreset(view); Capture("revised-replay-view-" + view + ".png");
                exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 0); exhibit.RenderFrame();
                var noWave = ReadPixels((RenderTexture)exhibit.PresentedTexture);
                exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f); exhibit.RenderFrame();
                var wave = ReadPixels((RenderTexture)exhibit.PresentedTexture);
                double change = PixelDifference(noWave, wave);
                fixtures.Add(new Fixture { label = "rendered wave-only image difference / view " + view,
                    measuredX = change });
                // Background points occupy far fewer pixels than the former full gas warp.
                // Check actual nonzero stellar response here; the featured source is reviewed separately.
                Check(change > .000001, "wave distortion displaces background stellar pixels from viewpoint " + view);
            }
            exhibit.SetViewPreset(0); exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 0, 0);
            exhibit.SetWristGas(false); exhibit.RenderFrame(); var noGas = ReadPixels(exhibit.SceneTarget);
            exhibit.SetWristGas(true); exhibit.RenderFrame(); var gas = ReadPixels(exhibit.SceneTarget);
            int lit = 0;
            for (int i = 0; i < gas.Length; i++) if (gas[i].r - noGas[i].r > 35 && gas[i].r > gas[i].b * 1.15f) lit++;
            Check(lit > gas.Length * .003, "accretion emission produces a resolved warm structure in the rendered image");
            foreach (var body in new[] { exhibit.Snapshot.GasState.BodyA, exhibit.Snapshot.GasState.BodyB })
            {
                Vector3 centre = exhibit.View.WorldToViewportPoint(V(body.PositionRg));
                Vector3 edge = exhibit.View.WorldToViewportPoint(V(body.PositionRg) + exhibit.View.transform.right * (float)body.OuterRadiusRg);
                float rx = Mathf.Abs(edge.x - centre.x) * exhibit.SceneTarget.width;
                float cx = centre.x * exhibit.SceneTarget.width, cy = centre.y * exhibit.SceneTarget.height;
                int resolved = 0;
                for (int y = Mathf.Max(0, (int)(cy-rx)); y < Mathf.Min(exhibit.SceneTarget.height, cy+rx); y++)
                for (int x = Mathf.Max(0, (int)(cx-rx)); x < Mathf.Min(exhibit.SceneTarget.width, cx+rx); x++)
                {
                    if ((x-cx)*(x-cx)+(y-cy)*(y-cy)>rx*rx) continue;
                    int i = y * exhibit.SceneTarget.width + x;
                    if (gas[i].r-noGas[i].r > 35 && gas[i].r > gas[i].b*1.15f) resolved++;
                }
                Check(resolved > 30, "each compact disk has resolved warm emission around body " + body.MassFraction);
            }
            Capture("revised-disks-optics-off.png");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 0, 1.3f);
            Capture("revised-waves-only.png");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
            double initialSeparation = exhibit.Snapshot.SeparationRg;
            exhibit.WatchMerger(); exhibit.SetWristPlaying(false);
            Check(exhibit.Snapshot.SeparationRg < initialSeparation && !exhibit.Wrist.Raised
                && exhibit.Snapshot.ElapsedSeconds < exhibit.Session.Model.MergerTimeSeconds,
                "watch merger frames a later tighter binary before common horizon formation");
            Capture("revised-late-inspiral.png");
            var pose = exhibit.View.transform.position;
            exhibit.Session.Seek(exhibit.Session.Model.MergerTimeSeconds); exhibit.Present();
            ValidateGasFixtures();
            Capture("revised-common-horizon.png");
            Check(exhibit.View.transform.position == pose, "event playback preserves the user's inspection viewpoint");
            exhibit.Session.Seek(Math.Min(exhibit.Snapshot.DurationSeconds,
                exhibit.Session.Model.MergerTimeSeconds + 120 * exhibit.Snapshot.GravitationalTimeSeconds));
            exhibit.Present(); Capture("revised-post-merger-waves.png");
            exhibit.RestartPresentation();
            Check(exhibit.Snapshot.ElapsedSeconds == 0 && exhibit.Session.PlaybackRate == .05
                && !exhibit.Session.IsPlaying && !exhibit.Wrist.Raised,
                "restart restores the beginning, normal slow rate and unobstructed binary framing");
        }

        private IEnumerator CaptureMergerSequence()
        {
            Directory.CreateDirectory(Path.Combine(output, "movie"));
            exhibit.SelectWristScenario(true); exhibit.Wrist.SetRaised(false, true);
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
            exhibit.WatchMerger(); exhibit.SetWristPlaying(false);
            double centre = exhibit.Session.Model.MergerTimeSeconds;
            for (int i = 0; i < 120; i++)
            {
                exhibit.Session.Seek(Math.Max(0, Math.Min(exhibit.Snapshot.DurationSeconds, centre - .16 + .25 * i / 119)));
                exhibit.Present(); Capture("movie/frame-" + i.ToString("D3") + ".png");
                // Permit deferred texture disposal between frames; no 120-frame memory spike.
                yield return null;
            }
        }

        private void ValidateStellarLight()
        {
            exhibit.SelectWristScenario(true); exhibit.SeekWrist(.35); exhibit.SetViewPreset(0);
            exhibit.SetWristSamples(false); exhibit.SetWristGas(true); exhibit.Wrist.SetRaised(false, true);
            Check(exhibit.FeaturedStar != null, "extended background star is installed");
            ValidateFeaturedProjectionAndFormula();
            exhibit.BackgroundStarsVisible = false;
            exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 0, 0); exhibit.RenderFrame();
            var gasOnly = ReadPixels((RenderTexture)exhibit.PresentedTexture);
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 2, 2); exhibit.RenderFrame();
            Check(PixelDifference(gasOnly, ReadPixels((RenderTexture)exhibit.PresentedTexture)) < 1e-6,
                "mass and wave optics do not distort or illuminate foreground gas when stars are hidden");
            exhibit.SetWristGas(false); exhibit.FeaturedStar.Visibility = true;
            exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 0, 0); exhibit.RenderFrame();
            var unbent = ReadPixels(exhibit.SceneTarget);
            int lit = 0; foreach (var p in unbent) if (p.r > 45 || p.g > 45 || p.b > 45) lit++;
            Check(lit > 3 && lit < unbent.Length * .01, "unbent nearby background star is resolved and occupies less than one percent of the scene");
            Capture("featured-star-unbent.png");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 0); exhibit.RenderFrame();
            var lensed = ReadPixels(exhibit.SceneTarget);
            Check(PixelDifference(unbent, lensed) > .00001, "moving binary lens changes the extended background star image");
            Capture("featured-star-lensed.png");
            foreach (double fraction in new[] { 0.0, .15, .3, .45, .6, .8 })
            {
                exhibit.SeekWrist(fraction); Capture("featured-star-epoch-" + fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
            exhibit.SeekWrist(.35); exhibit.RenderFrame();
            Check(PixelDifference(lensed, ReadPixels(exhibit.SceneTarget)) < 1e-6,
                "extended star lens image is deterministic after rewind");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f); exhibit.RenderFrame();
            Check(PixelDifference(lensed, ReadPixels(exhibit.SceneTarget)) > .000001,
                "retarded waves distort the extended background star light");
            Capture("featured-star-waves.png");
            var source = exhibit.FeaturedStar.SourcePositionRg;
            double time = exhibit.Snapshot.ElapsedSeconds;
            exhibit.MoveCamera(new Vector3(4, 3, 0)); exhibit.RenderFrame();
            Check(exhibit.FeaturedStar.SourcePositionRg == source && exhibit.Snapshot.ElapsedSeconds == time,
                "featured source stays fixed when observer moves while paused");
            exhibit.BackgroundStarsVisible = true;
            exhibit.WatchStarlight(); exhibit.SetWristPlaying(false);
            Check(!exhibit.GasVisible && !exhibit.Wrist.Raised && exhibit.Snapshot.ElapsedSeconds == 0,
                "starlight view isolates background sources with a reset event");
            Capture("starlight-view.png");
            exhibit.RestartPresentation();
            Check(exhibit.GasVisible, "restart restores the accretion view after starlight inspection");
        }

        private void ValidateFeaturedProjectionAndFormula()
        {
            var source = exhibit.FeaturedStar;
            if (source == null || !source.Ready) { Check(false, "featured stellar shader is ready for independent projection checks"); return; }
            Camera view = exhibit.View;
            Vector3 previousPosition = view.transform.position; Quaternion previousRotation = view.transform.rotation;
            bool previousStars = exhibit.BackgroundStarsVisible, previousFeatured = source.Visibility;
            bool previousGas = exhibit.GasVisible, previousSamples = exhibit.SamplesVisible;
            InteractiveOpticalStyle previousStyle = exhibit.OpticalStyle;
            float previousLens = exhibit.LensGain, previousWave = exhibit.WaveGain;
            RenderTexture previousActive = RenderTexture.active;
            Material material = source.StarMaterial;
            float previousFixture = material.GetFloat("_StellarFixture");
            Vector4 previousInput = material.GetVector("_StellarFixtureInput"), previousLensInput = material.GetVector("_StellarFixtureLens");
            var ownedTargets = new HashSet<int>();
            foreach (var texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (texture.IsCreated() && (texture.name == "Bounded interactive scene" || texture.name == "Capped astronomical optics"))
                    ownedTargets.Add(texture.GetInstanceID());
            Renderer[] renderers = exhibit.GetComponentsInChildren<Renderer>(true);
            var previousEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) previousEnabled[i] = renderers[i].enabled;
            RenderTexture fixtureTarget = null; Texture2D fixtureImage = null; GameObject fixtureCameraObject = null;
            try
            {
                exhibit.BackgroundStarsVisible = false; exhibit.SetWristGas(false); exhibit.SetWristSamples(false);
                exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 0, 0); material.SetFloat("_StellarFixture", 0);
                // Compare real emitted pixels to Unity's independent world-space projection.
                // Both vertical signs matter: a centred source can hide a render-target Y flip.
                foreach (Vector2 rotation in new[] { new Vector2(-5, -5), new Vector2(-5, 5), new Vector2(5, -5), new Vector2(5, 5) })
                {
                    view.transform.rotation = previousRotation * Quaternion.Euler(rotation.y, rotation.x, 0);
                    source.Visibility = false; exhibit.RenderFrame(); var background = ReadPixels(exhibit.SceneTarget);
                    source.Visibility = true; exhibit.RenderFrame(); var foreground = ReadPixels(exhibit.SceneTarget);
                    Vector3 expected = view.WorldToViewportPoint(source.SourcePositionRg);
                    int width = exhibit.SceneTarget.width, height = exhibit.SceneTarget.height;
                    double flux = 0, momentX = 0, momentY = 0;
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        double weight = Math.Max(0, .2126 * (foreground[index].r-background[index].r)
                            + .7152 * (foreground[index].g-background[index].g) + .0722 * (foreground[index].b-background[index].b));
                        flux += weight; momentX += weight * (x+.5); momentY += weight * (y+.5);
                    }
                    double measuredX = flux > 0 ? momentX/flux : -10000, measuredY = flux > 0 ? momentY/flux : -10000;
                    double expectedX = expected.x*width, expectedY = expected.y*height;
                    double error = Math.Sqrt((measuredX-expectedX)*(measuredX-expectedX)+(measuredY-expectedY)*(measuredY-expectedY));
                    fixtures.Add(new Fixture { label = "featured source actual centroid vs Unity projection / camera yaw-pitch " + rotation,
                        expectedX = expectedX, expectedY = expectedY, measuredX = measuredX, measuredY = measuredY, absoluteError = error });
                    Check(expected.z > 0 && expected.x > .1 && expected.x < .9 && expected.y > .1 && expected.y < .9
                        && flux > 100 && error < 1.25, "unbent featured source matches world projection within 1.25 pixels / yaw-pitch " + rotation);
                    Capture("stellar-projection-yaw-" + rotation.x.ToString("0", CultureInfo.InvariantCulture)
                        + "-pitch-" + rotation.y.ToString("0", CultureInfo.InvariantCulture) + ".png");
                }

                // Render the actual production mesh and shader through an independent camera.
                // A Graphics.Blit quad has a different vertex-position convention and would
                // not validate this shader's explicit clip-space geometry.
                const int side = 65;
                fixtureTarget = new RenderTexture(side, side, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                    { name = "QA featured stellar reference target" };
                if (!fixtureTarget.Create()) throw new InvalidOperationException("Could not allocate the floating stellar reference target.");
                fixtureImage = new Texture2D(side, side, TextureFormat.RGBAFloat, false, true);
                fixtureCameraObject = new GameObject("QA actual stellar mesh reference camera");
                Camera fixtureCamera = fixtureCameraObject.AddComponent<Camera>(); fixtureCamera.CopyFrom(view);
                fixtureCamera.enabled = false; fixtureCamera.targetTexture = null; fixtureCamera.aspect = 1;
                fixtureCamera.clearFlags = CameraClearFlags.SolidColor; fixtureCamera.backgroundColor = Color.black;
                fixtureCamera.allowHDR = true; fixtureCamera.allowMSAA = false;
                fixtureCamera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                fixtureCameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = renderers[i].sharedMaterial == material;
                material.SetFloat("_StellarFixture", 1);
                foreach (Vector4 lens in new[] { new Vector4(100, .5f, 1, 1e8f), new Vector4(100, .5f, 1, 200),
                    new Vector4(100, .5f, 1, 99), new Vector4(1, .8f, 1.6f, 1e6f) })
                foreach (Vector2 angle in new[] { new Vector2(.2f, .1f), new Vector2(-.1f, .02f), new Vector2(.0001f, -.0002f) })
                {
                    material.SetVector("_Optics", new Vector4(1, 0, 1, 1));
                    material.SetVector("_StellarFixtureInput", new Vector4(angle.x, angle.y, 0, 0));
                    material.SetVector("_StellarFixtureLens", lens);
                    StellarLensReference.Deflection(angle.x, angle.y, lens.x, lens.y, lens.z, lens.w, 1, out double x, out double y);
                    var request = new RenderPipeline.StandardRequest { destination = fixtureTarget };
                    if (!RenderPipeline.SupportsRenderRequest(fixtureCamera, request))
                        throw new InvalidOperationException("URP stellar reference render requests are unavailable.");
                    RenderPipeline.SubmitRenderRequest(fixtureCamera, request);
                    RenderTexture.active = fixtureTarget;
                    fixtureImage.ReadPixels(new Rect(0, 0, side, side), 0, 0); fixtureImage.Apply();
                    RecordFixture("actual stellar shader / lens " + lens + " / angle " + angle, x, y,
                        fixtureImage.GetPixel(side/2, side/2), 3e-4);
                }
            }
            finally
            {
                material.SetFloat("_StellarFixture", previousFixture);
                material.SetVector("_StellarFixtureInput", previousInput); material.SetVector("_StellarFixtureLens", previousLensInput);
                RenderTexture.active = previousActive;
                if (fixtureCameraObject != null) { fixtureCameraObject.SetActive(false); Destroy(fixtureCameraObject); }
                if (fixtureImage != null) Destroy(fixtureImage);
                if (fixtureTarget != null)
                {
                    fixtureTarget.Release(); Check(!fixtureTarget.IsCreated(), "stellar formula check releases its floating render target");
                    Destroy(fixtureTarget);
                }
                view.transform.SetPositionAndRotation(previousPosition, previousRotation);
                exhibit.BackgroundStarsVisible = previousStars; source.Visibility = previousFeatured;
                exhibit.SetWristGas(previousGas); exhibit.SetWristSamples(previousSamples);
                exhibit.SetWristOptics(previousStyle, previousLens, previousWave);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = previousEnabled[i];
                exhibit.RenderFrame();
                bool noExtraTargets = true;
                foreach (var texture in Resources.FindObjectsOfTypeAll<RenderTexture>())
                    if (texture.IsCreated() && (texture.name == "Bounded interactive scene" || texture.name == "Capped astronomical optics"
                        || texture.name == "QA featured stellar reference target") && !ownedTargets.Contains(texture.GetInstanceID())) noExtraTargets = false;
                Check(noExtraTargets, "stellar projection and formula checks retain no additional owned render targets");
            }
        }

        private IEnumerator CaptureStellarSequence()
        {
            Directory.CreateDirectory(Path.Combine(output, "starlight-movie"));
            exhibit.SelectWristScenario(true); exhibit.WatchStarlight(); exhibit.SetWristPlaying(false);
            double end = Math.Min(.5, exhibit.Session.Model.MergerTimeSeconds * .7);
            for (int i = 0; i < 120; i++)
            {
                exhibit.Session.Seek(end * i / 119); exhibit.Present();
                Capture("starlight-movie/frame-" + i.ToString("D3") + ".png"); yield return null;
            }
            exhibit.RestartPresentation();
        }

        private IEnumerator CaptureAccretionSequence()
        {
            Directory.CreateDirectory(Path.Combine(output, "accretion-movie"));
            exhibit.SelectWristScenario(true); exhibit.ShowAccretionView();
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f); exhibit.SetViewPreset(0);
            double end = Math.Min(.5, exhibit.Session.Model.MergerTimeSeconds * .7);
            for (int i = 0; i < 120; i++)
            {
                exhibit.Session.Seek(end * i / 119); exhibit.Present();
                Capture("accretion-movie/frame-" + i.ToString("D3") + ".png"); yield return null;
            }
            exhibit.ShowAccretionView();
        }

        private IEnumerator Measure(string scenario)
        {
            if (scenario == "replay-paused") exhibit.SelectWristScenario(true); else exhibit.SelectWristScenario(false);
            exhibit.SeekWrist(.4); exhibit.SetViewPreset(scenario == "camera-motion" ? 2 : 0);
            exhibit.Wrist.SetRaised(true, true); exhibit.Wrist.SetPinned(false);
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 1, 1);
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { name = "QA completed interface target" }; target.Create();
            var pixel = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32); pixel.Create();
            var previous = exhibit.CaptureCamera.targetTexture; exhibit.CaptureCamera.targetTexture = target;
            var previousActive = RenderTexture.active;
            bool sceneEnabled = exhibit.View.enabled, interfaceEnabled = exhibit.CaptureCamera.enabled;
            exhibit.View.enabled = false; exhibit.CaptureCamera.enabled = false;
            var values = new List<double>(10000); int completed = 0, index = 0;
            var watch = Stopwatch.StartNew(); double warm = .5;
            while (watch.Elapsed.TotalSeconds < warm) { exhibit.RenderFrame(); Graphics.Blit(target, pixel); var read = AsyncGPUReadback.Request(pixel); read.WaitForCompletion(); yield return null; }
            if (scenario == "experiment-playback") exhibit.SetWristPlaying(true);
            watch.Restart(); double before = 0;
            while (watch.Elapsed.TotalSeconds < benchmarkSeconds)
            {
                yield return null;
                if (scenario == "camera-motion") exhibit.View.transform.RotateAround(Vector3.zero, Vector3.up, .2f);
                if (scenario == "repeated-seek") exhibit.SeekWrist((index * .61803398875) % 1);
                exhibit.RenderFrame(); Graphics.Blit(target, pixel);
                var read = AsyncGPUReadback.Request(pixel); read.WaitForCompletion(); if (!read.hasError) completed++;
                double now = watch.Elapsed.TotalSeconds; values.Add((now - before) * 1000); before = now; index++;
            }
            exhibit.SetWristPlaying(false); exhibit.CaptureCamera.targetTexture = previous;
            RenderTexture.active = previousActive;
            exhibit.View.enabled = sceneEnabled; exhibit.CaptureCamera.enabled = interfaceEnabled;
            target.Release(); pixel.Release(); Destroy(target); Destroy(pixel);
            values.Sort(); double sum = 0; foreach (double value in values) sum += value;
            windows.Add(new Window { profile = exhibit.Profile.EffectiveProfile + (exhibit.Profile.GasEnabled ? "" : " compatibility"), scenario = scenario,
                frames = values.Count, completedReadbacks = completed, seconds = watch.Elapsed.TotalSeconds, width = exhibit.SceneTarget.width, height = exhibit.SceneTarget.height,
                meanMs = sum / values.Count, p95Ms = Quantile(values, .95), p99Ms = Quantile(values, .99), maximumMs = values[values.Count - 1] });
            Check(values.Count > 10 && completed == values.Count, scenario + " completes every measured GPU frame");
        }
        private static double Quantile(List<double> v, double q)
        { double p = (v.Count - 1) * q; int lo = (int)p; return v[lo] + (v[Math.Min(v.Count - 1, lo + 1)] - v[lo]) * (p - lo); }

        private void ValidatePointerTargets()
        {
            RectTransform panel = null;
            foreach (var childRect in exhibit.Wrist.GetComponentsInChildren<RectTransform>(true))
                if (childRect.name == "Projected instrument panel") panel = childRect;
            bool inside = panel != null;
            if (panel != null)
            {
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(exhibit.CaptureCamera, corner);
                    inside &= p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1;
                }
            }
            Check(inside, "all wrist panel corners are inside the viewport");
            Button frequency = null;
            foreach (var button in exhibit.Wrist.GetComponentsInChildren<Button>(true))
                if (button.GetComponentInChildren<Text>().text == "FREQUENCY") frequency = button;
            if (frequency == null) { Check(false, "frequency pointer target exists"); return; }
            var rect = (RectTransform)frequency.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(exhibit.CaptureCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            bool hit = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == frequency;
            Check(hit && exhibit.Wrist.IsScreenPointOverPanel(point), "screen-space raycast reaches the frequency button above the scene");
            if (hit) ExecuteEvents.Execute(frequency.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Check(exhibit.Wrist.FrequencyPlot, "pointer dispatch selects the frequency graph");
            EventSystem.current.SetSelectedGameObject(frequency.gameObject);
            Check(exhibit.Wrist.InputFocused, "selected wrist control captures keyboard focus");
            exhibit.Wrist.SetRaised(false, true);
            Check(!exhibit.Wrist.InputFocused, "lowering the wrist releases flight input immediately");
            EventSystem.current.SetSelectedGameObject(null);
            exhibit.Wrist.SetRaised(true, true); exhibit.Wrist.SetFrequencyPlot(false);
        }

        private void ValidateWaveHistoryReadback(string label)
        {
            var material = exhibit.Optics.WarpMaterial;
            var wave = material == null ? null : material.GetTexture("_WaveHistory") as Texture2D;
            Check(wave != null && wave.width == InteractiveOpticsReference.WaveSamples && wave.height == 1,
                label + " has the actual uploaded wave history");
            if (wave == null) return;
            if (Environment.GetEnvironmentVariable("DTET_FORCE_HALF_TEXTURES_QA") == "1")
                Check(wave.format == TextureFormat.RGBAHalf, label + " forced Half wave texture is selected");
            Check(wave.filterMode == FilterMode.Bilinear, label + " wave texture uses linear filtering");
            var target = new RenderTexture(wave.width, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var image = new Texture2D(wave.width, 1, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                Check(target.Create(), label + " wave readback target allocates");
                if (!target.IsCreated()) return;
                GL.sRGBWrite = false;
                Graphics.Blit(wave, target); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, wave.width, 1), 0, 0); image.Apply();
                // Decode with the uniform used by the real distortion shader, then compare to
                // ideal CPU display magnitudes. This does not reproduce texture encoding.
                double displayScale = material.GetVector("_WaveParameters").y;
                double relativeTolerance = wave.format == TextureFormat.RGBAHalf ? .002 : 0;
                bool coverageMatches = true, valuesMatch = true, nonzeroSurvives = false;
                int covered = 0, worstIndex = 0;
                double maximumError = 0, worstExpectedPlus = 0, worstExpectedCross = 0;
                double worstMeasuredPlus = 0, worstMeasuredCross = 0;
                for (int i = 0; i < wave.width; i++)
                {
                    double radius = exhibit.Optics.WaveRadiusRg * i / (wave.width - 1);
                    var ideal = exhibit.Session.Model.EvaluateRetardedSignal(exhibit.Snapshot.ElapsedSeconds, radius);
                    double plus = ideal.IsAvailable ? (ideal.Kind == InteractiveSignalKind.QuadrupoleStrain
                        ? ideal.HPlus.GetValueOrDefault() / InteractiveBinaryScenario.ReferenceStrainAmplitude
                        : ideal.NormalizedRealH22.GetValueOrDefault()) : 0;
                    double cross = ideal.IsAvailable ? (ideal.Kind == InteractiveSignalKind.QuadrupoleStrain
                        ? ideal.HCross.GetValueOrDefault() / InteractiveBinaryScenario.ReferenceStrainAmplitude
                        : ideal.NormalizedImaginaryH22.GetValueOrDefault()) : 0;
                    var pixel = image.GetPixel(i, 0);
                    double measuredPlus = pixel.r * displayScale, measuredCross = pixel.g * displayScale;
                    coverageMatches &= float.IsFinite(pixel.b) && Math.Abs(pixel.b - (ideal.IsAvailable ? 1 : 0)) < 1e-6;
                    if (ideal.IsAvailable) covered++;
                    double error = Math.Max(Math.Abs(measuredPlus - plus), Math.Abs(measuredCross - cross));
                    valuesMatch &= double.IsFinite(measuredPlus) && double.IsFinite(measuredCross)
                        && Math.Abs(measuredPlus - plus) <= 2e-6 + relativeTolerance * Math.Abs(plus)
                        && Math.Abs(measuredCross - cross) <= 2e-6 + relativeTolerance * Math.Abs(cross);
                    nonzeroSurvives |= ideal.IsAvailable && Math.Max(Math.Abs(plus), Math.Abs(cross)) > 1e-4
                        && Math.Max(Math.Abs(measuredPlus), Math.Abs(measuredCross)) > 1e-4;
                    if (i == 0 || error > maximumError)
                    {
                        maximumError = error; worstIndex = i; worstExpectedPlus = plus; worstExpectedCross = cross;
                        worstMeasuredPlus = measuredPlus; worstMeasuredCross = measuredCross;
                    }
                }
                fixtures.Add(new Fixture { label = label + " decoded wave history / " + wave.format + " / worst sample " + worstIndex,
                    expectedX = worstExpectedPlus, expectedY = worstExpectedCross,
                    measuredX = worstMeasuredPlus, measuredY = worstMeasuredCross, absoluteError = maximumError });
                Check(coverageMatches && covered == exhibit.Optics.CoveredWaveSamples && covered > 0,
                    label + " GPU coverage agrees with retarded CPU history");
                Check(valuesMatch, label + " decoded GPU waveform agrees with CPU display magnitude");
                Check(nonzeroSurvives, label + " nonzero covered waveform survives GPU storage");
            }
            finally
            {
                GL.sRGBWrite = previousSrgbWrite; RenderTexture.active = previous;
                Destroy(image); target.Release(); Destroy(target);
            }
        }

        private void ValidateRayProjection()
        {
            var bodyMaterial = exhibit.BodyATransform.GetComponent<Renderer>().sharedMaterial;
            Color originalColor = bodyMaterial.GetColor("_Color");
            bool originalGas = exhibit.GasVisible, originalSamples = exhibit.SamplesVisible;
            var originalStyle = exhibit.OpticalStyle;
            float originalLens = exhibit.LensGain, originalWave = exhibit.WaveGain;
            var optics = exhibit.Optics.WarpMaterial;
            var target = new RenderTexture(exhibit.SceneTarget.width, exhibit.SceneTarget.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                if (!target.Create()) throw new InvalidOperationException("Cannot allocate real-camera ray projection fixture.");
                bodyMaterial.SetColor("_Color", Color.green);
                exhibit.SetWristGas(false); exhibit.SetWristSamples(false);
                exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
                for (int view = 0; view < 4; view++)
                {
                    exhibit.SetViewPreset(view); exhibit.RenderFrame();
                    var rendered = ReadPixels(exhibit.SceneTarget);
                    optics.SetFloat("_OpticsFixture", 3); GL.sRGBWrite = false;
                    Graphics.Blit(exhibit.SceneTarget, target, optics);
                    var analytic = ReadPixels(target);
                    int actualCount = 0, analyticCount = 0, union = 0, intersection = 0;
                    double actualX = 0, actualY = 0, analyticX = 0, analyticY = 0;
                    var overlay = new Color32[rendered.Length];
                    for (int i = 0; i < rendered.Length; i++)
                    {
                        bool actual = rendered[i].g > 180 && rendered[i].r < 30 && rendered[i].b < 30;
                        bool predicted = analytic[i].r > 180;
                        int x = i % target.width, y = i / target.width;
                        if (actual) { actualCount++; actualX += x; actualY += y; }
                        if (predicted) { analyticCount++; analyticX += x; analyticY += y; }
                        if (actual || predicted) union++;
                        if (actual && predicted) intersection++;
                        overlay[i] = new Color32(predicted ? (byte)255 : (byte)0, actual ? (byte)255 : (byte)0, 0, 255);
                    }
                    double overlap = union == 0 ? 0 : (double)intersection / union;
                    double centroidError = actualCount == 0 || analyticCount == 0 ? 1e6
                        : Math.Sqrt(Math.Pow(actualX / actualCount - analyticX / analyticCount, 2)
                            + Math.Pow(actualY / actualCount - analyticY / analyticCount, 2));
                    fixtures.Add(new Fixture { label = "actual rendered horizon versus optical ray mask / view " + view,
                        expectedX = 1, measuredX = overlap, measuredY = centroidError, absoluteError = 1 - overlap });
                    Check(actualCount > 20 && analyticCount > 20 && overlap > .90 && centroidError < 1.5,
                        "actual camera geometry and optical ray projection align from viewpoint " + view);
                    var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                    try
                    {
                        image.SetPixels32(overlay); image.Apply();
                        string name = "optics-ray-projection-view-" + view + ".png";
                        File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); images.Add(name);
                    }
                    finally { Destroy(image); }
                    optics.SetFloat("_OpticsFixture", 0);
                }
            }
            finally
            {
                optics.SetFloat("_OpticsFixture", 0); bodyMaterial.SetColor("_Color", originalColor);
                exhibit.SetWristGas(originalGas); exhibit.SetWristSamples(originalSamples);
                exhibit.SetWristOptics(originalStyle, originalLens, originalWave);
                GL.sRGBWrite = previousSrgb; RenderTexture.active = previousTarget;
                target.Release(); Destroy(target);
                exhibit.SetViewPreset(0); exhibit.RenderFrame();
            }
        }

        private void ValidateGpuFixtures()
        {
            var target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); target.Create();
            var image = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var optics = new Material(Resources.Load<Shader>("DTET/InteractiveOptics"));
            var active = RenderTexture.active;
            try
            {
                foreach (double sourceDepth in new[] { 50.0, 100, 200, 1e6 })
                {
                    optics.SetFloat("_OpticsFixture", 1); optics.SetVector("_Optics", new Vector4(1, 1, 1, 1));
                    optics.SetVector("_FixtureInput", new Vector4(.1f, .2f, 0, 0));
                    optics.SetVector("_FixtureLens", new Vector4(100, .5f, 1, (float)sourceDepth));
                    InteractiveOpticsReference.LensOffset(.1f, .2f, 100, .5, 1, sourceDepth, 1, out double x, out double y);
                    FixtureRead("lens source depth " + sourceDepth, x, y, optics, target, image, 2e-6);
                }
                foreach (Vector2 signal in new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(99, 99) })
                {
                    optics.SetFloat("_OpticsFixture", 2); optics.SetVector("_Optics", new Vector4(1, 2, 1, 1));
                    optics.SetVector("_FixtureInput", new Vector4(.2f, .4f, signal.x, signal.y));
                    InteractiveOpticsReference.WaveOffset(.2f, .4f, signal.x, signal.y, 2, out double x, out double y);
                    FixtureRead("wave plus/cross " + signal, x, y, optics, target, image, 2e-6);
                }
                foreach (Vector2 angle in new[] { new Vector2(.01f, .03f), new Vector2(.2f, .4f), new Vector2(.9f, -.7f) })
                foreach (float gain in new[] { 0, .35f, 2 })
                {
                    optics.SetFloat("_OpticsFixture", 4); optics.SetVector("_Optics", new Vector4(gain, 0, 1, 1));
                    optics.SetVector("_FixtureInput", new Vector4(angle.x, angle.y, 0, 0));
                    optics.SetVector("_FixtureLens", new Vector4(25, .5f, 1, 1e6f));
                    InteractiveOpticsReference.SkyApparentOffset(angle.x, angle.y, 25, .5, 1, gain, out double x, out double y);
                    FixtureRead("shared sky star-centre deflection " + angle + " / gain " + gain,
                        x, y, optics, target, image, 2e-6);
                }
                ValidateGasFixtures();
            }
            finally { RenderTexture.active = active; Destroy(optics); Destroy(image); target.Release(); Destroy(target); }
        }
        private void FixtureRead(string label, double x, double y, Material material, RenderTexture target, Texture2D image, double tolerance)
        {
            Graphics.Blit(Texture2D.blackTexture, target, material); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); image.Apply(); var measured = image.GetPixel(0, 0);
            RecordFixture(label, x, y, measured, tolerance);
        }
        private void RecordFixture(string label, double x, double y, Color measured, double tolerance)
        {
            double error = Math.Max(Math.Abs(measured.r - x), Math.Abs(measured.g - y));
            fixtures.Add(new Fixture { label = label, expectedX = x, expectedY = y, measuredX = measured.r, measuredY = measured.g, absoluteError = error });
            Check(double.IsFinite(error) && error <= tolerance, "GPU/reference " + label);
        }

        private void ValidateGasFixtures()
        {
            const int side = 65;
            Material gas = exhibit.GasMaterial;
            if (gas == null) throw new InvalidOperationException("The adjustable gas fixture requires an active gas material.");
            float previousTest = gas.GetFloat("_FieldTest"); Vector4 previousPoint = gas.GetVector("_FieldPoint");
            Renderer[] renderers = exhibit.GetComponentsInChildren<Renderer>(true);
            var enabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) enabled[i] = renderers[i].enabled;
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = null; Texture2D image = null; GameObject cameraObject = null;
            try
            {
                target = new RenderTexture(side, side, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                if (!target.Create()) throw new InvalidOperationException("Could not allocate the linear gas fixture target.");
                image = new Texture2D(side, side, TextureFormat.RGBAFloat, false, true);
                // The volume is a world-space, front-culled cube. A fullscreen blit would not
                // exercise its actual vertex/culling path. A dedicated camera also avoids the
                // exhibit's normal endCameraRendering optical composite callback.
                cameraObject = new GameObject("QA adjustable gas fixture camera");
                Camera camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(exhibit.View);
                camera.enabled = false; camera.targetTexture = null; camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.allowHDR = true; camera.allowMSAA = false;
                cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                camera.transform.position = new Vector3(0, (float)exhibit.Session.Model.HalfExtentY * 2,
                    (float)-exhibit.Session.Model.HalfExtentXZ * 2);
                camera.transform.LookAt(Vector3.zero);
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = renderers[i].sharedMaterial == gas;
                gas.SetFloat("_FieldTest", 1);
                foreach (Vector3 point in new[] { new Vector3((float)(exhibit.Snapshot.GasState.ReservoirInnerRadiusRg * 1.3), 0, 0),
                    V(exhibit.Snapshot.GasState.BodyA.PositionRg) + new Vector3(5, 0, 0), new Vector3(10, 0, 0), new Vector3(1000, 0, 0) })
                {
                    var expected = exhibit.Session.Model.SampleGas(exhibit.Snapshot.GasState, point.x, point.y, point.z);
                    gas.SetVector("_FieldPoint", point);
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    if (!RenderPipeline.SupportsRenderRequest(camera, request))
                        throw new InvalidOperationException("URP gas fixture render requests are unavailable.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, side, side), 0, 0); image.Apply();
                    RecordFixture("adjustable gas " + point, expected.RelativeDensity, expected.NormalizedBolometricEmission,
                        image.GetPixel(side / 2, side / 2), 3e-4);
                }
            }
            finally
            {
                gas.SetFloat("_FieldTest", previousTest); gas.SetVector("_FieldPoint", previousPoint);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                RenderTexture.active = previous;
                if (cameraObject != null) Destroy(cameraObject);
                if (image != null) Destroy(image);
                if (target != null) { target.Release(); Destroy(target); }
            }
        }

        private void Capture(string name, bool launcher = false, int width = 1600, int height = 900)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create();
            var previous = exhibit.CaptureCamera.targetTexture; exhibit.CaptureCamera.targetTexture = target;
            if (launcher)
            {
                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(exhibit.CaptureCamera, request))
                    throw new InvalidOperationException("URP launcher capture requests are unavailable.");
                RenderPipeline.SubmitRenderRequest(exhibit.CaptureCamera, request);
            }
            else exhibit.RenderFrame();
            var active = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); images.Add(name);
            Destroy(image); RenderTexture.active = active; exhibit.CaptureCamera.targetTexture = previous; target.Release(); Destroy(target);
            Check(new FileInfo(Path.Combine(output, name)).Length > 10000, "capture " + name);
        }
        private static Color32[] ReadPixels(RenderTexture target)
        {
            var active = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            var pixels = image.GetPixels32(); Destroy(image); RenderTexture.active = active; return pixels;
        }
        private static double PixelDifference(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return double.PositiveInfinity;
            double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return sum / (a.Length * 3 * 255.0);
        }
        private static Vector3 V(NumericalVector3 p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }
}
#endif
