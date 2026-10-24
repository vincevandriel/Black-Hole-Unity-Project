#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DTET.VisualPrototype
{
    // Generated-fluid integration checks.
    internal sealed class GeneratedFluidQaCapture : MonoBehaviour
    {
        private InteractiveBinaryExhibit exhibit;
        private string output;
        private bool passed = true;
        private readonly List<string> checks = new List<string>();
        private readonly List<string> images = new List<string>();
        private readonly List<double> renderMs = new List<double>();
        private double solveSeconds;
        private long historyBytes;
        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string unityVersion, graphicsDevice, scope;
            public string[] checks, images;
            public double solveSeconds, meanCompletedFrameMs, maximumCompletedFrameMs;
            public long historyBytes;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_GENERATED_FLUID_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("Generated fluid integration validation").AddComponent<GeneratedFluidQaCapture>();
        }
        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Exception) { passed = false; Application.Quit(1); } }
        private void Check(bool condition, string label)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) { passed = false; Debug.LogError("DTET_GENERATED_FLUID_FAILED " + label); }
        }
        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_GENERATED_FLUID_QA_PATH"));
            Directory.CreateDirectory(output);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(.75f);
            exhibit = FindFirstObjectByType<InteractiveBinaryExhibit>();
            if (exhibit == null) { Check(false, "interactive exhibit exists"); Application.Quit(1); yield break; }
            exhibit.EmulatedInputEnabled = false;
            exhibit.Launch(ExhibitQualityProfile.Laptop); yield return null;
            Check(!exhibit.GeneratedFluidReady && !exhibit.IsGeneratingFluid, "generation is opt-in after quality admission");
            exhibit.StartGeneratedFluid();
            Check(exhibit.IsGeneratingFluid, "solve starts asynchronously");
            var watch = Stopwatch.StartNew();
            while (exhibit.IsGeneratingFluid && watch.Elapsed.TotalSeconds < 300) yield return null;
            solveSeconds = watch.Elapsed.TotalSeconds;
            Check(exhibit.GeneratedFluidReady, "public-input fluid solve completes: " + exhibit.GeneratedFluidStatus);
            if (!exhibit.GeneratedFluidReady) { WriteReceipt(); Application.Quit(1); yield break; }
            var history = exhibit.GeneratedHistory;
            historyBytes = history.MemoryBytes;
            Check(history.GridSize == 96 && history.FrameCount == 129, "fixed physical grid and history cadence");
            Check(historyBytes > 0 && historyBytes < 24L * 1024 * 1024, "history memory below 24 MiB");
            Check(exhibit.GeneratedFluidActive && !exhibit.Session.IsPlaying, "finished solve selects generated gas and pauses");
            Check(Math.Abs(exhibit.Snapshot.ElapsedSeconds - history.StartSeconds) < 1e-9, "finished solve seeks covered history start");
            exhibit.SetWristSamples(true);
            Check(!exhibit.SamplesVisible, "prescribed density samples cannot accompany generated gas");
            exhibit.SetWristOptics(InteractiveOpticalStyle.Off, 0, 0);
            exhibit.Wrist.SetRaised(false, true);
            double t = history.StartSeconds + .37 * (history.EndSeconds - history.StartSeconds);
            exhibit.SeekWrist(t / exhibit.Snapshot.DurationSeconds); exhibit.SetViewPreset(1); exhibit.RenderFrame();
            var first = Pixels(exhibit.SceneTarget);
            exhibit.SetWristGas(false); exhibit.RenderFrame();
            Check(Difference(first, Pixels(exhibit.SceneTarget)) > .0001, "computed gas contributes nonblank scene radiance");
            exhibit.SetWristGas(true); exhibit.RenderFrame();
            history.TrySample(t, 12, 0, out var initialSample);
            foreach (double f in new[] { .9, .15, .7, 1.0, .0 })
            {
                exhibit.SeekWrist((history.StartSeconds + f * (history.EndSeconds - history.StartSeconds)) / exhibit.Snapshot.DurationSeconds);
                exhibit.RenderFrame();
            }
            exhibit.SeekWrist(t / exhibit.Snapshot.DurationSeconds); exhibit.RenderFrame();
            Check(Difference(first, Pixels(exhibit.SceneTarget)) < 1e-6, "reverse/seeks reproduce identical rendered fluid epoch");
            history.TrySample(t, 12, 0, out var restoredSample);
            Check(initialSample.SurfaceDensity == restoredSample.SurfaceDensity, "history sampling does not advance dissipative physics backward");
            double frozenTime = exhibit.Snapshot.ElapsedSeconds;
            var camera = exhibit.View.transform.position;
            exhibit.MoveCamera(new Vector3(4, 3, 0)); exhibit.RenderFrame();
            Check(exhibit.View.transform.position != camera && exhibit.Snapshot.ElapsedSeconds == frozenTime,
                "free inspection camera moves while fluid is paused");
            foreach (int view in new[] { 0, 1, 2, 3 })
            {
                exhibit.SetViewPreset(view); Capture("generated-view-" + view + ".png");
            }
            exhibit.SetViewPreset(1);
            foreach (double fraction in new[] { 0.0, .5, 1.0 })
            {
                exhibit.SeekWrist((history.StartSeconds + fraction * (history.EndSeconds - history.StartSeconds)) / exhibit.Snapshot.DurationSeconds);
                Capture("generated-epoch-" + fraction.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
            exhibit.SeekWrist(t / exhibit.Snapshot.DurationSeconds);
            exhibit.Wrist.SetRaised(true, true); exhibit.Wrist.SetFluidPanelOpen(true); Capture("generated-controls.png");
            ValidateGpuSamples(t);
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, 1, 1); exhibit.RenderFrame();
            Check(exhibit.Optics.Active && exhibit.Optics.ScenarioGeneration == exhibit.Snapshot.Generation,
                "illustrative optics and gravitational-wave display follow generated scene generation");
            int previousCallbacks = exhibit.AutomaticOpticalFrames;
            // Exercise camera completion; hidden players may skip automatic renders.
            var automaticRequest = new RenderPipeline.StandardRequest { destination = exhibit.SceneTarget };
            RenderPipeline.SubmitRenderRequest(exhibit.View, automaticRequest);
            Check(exhibit.AutomaticOpticalFrames > previousCallbacks, "camera completion callback updates the generated scene optics");
            Capture("generated-warp.png");
            for (int i = 0; i < 25; i++)
            {
                var frameWatch = Stopwatch.StartNew(); exhibit.RenderFrame();
                Fence(exhibit.SceneTarget); frameWatch.Stop(); renderMs.Add(frameWatch.Elapsed.TotalMilliseconds);
                yield return null;
            }
            exhibit.UseGeneratedFluid(false); exhibit.RenderFrame();
            Check(!exhibit.GeneratedFluidActive && exhibit.GeneratedFluidReady, "illustration toggle retains reusable solved history");
            exhibit.UseGeneratedFluid(true); exhibit.RenderFrame();
            Check(exhibit.GeneratedFluidActive, "solved history can be reselected");
            exhibit.WatchStarlight(); exhibit.SetWristPlaying(false);
            exhibit.UseGeneratedFluid(false); exhibit.RenderFrame();
            Check(exhibit.GasVisible && !exhibit.GeneratedFluidActive && exhibit.GeneratedFluidReady,
                "illustrative gas selection restores gas hidden by starlight and retains the solved cache");
            exhibit.SetWristGas(false); exhibit.UseGeneratedFluid(true); exhibit.RenderFrame();
            Check(exhibit.GasVisible && exhibit.GeneratedFluidPlayer.RendererVisible,
                "generated gas selection restores hidden gas inside its coverage");
            exhibit.ShowAccretionView(); exhibit.RenderFrame();
            Check(exhibit.GasVisible && !exhibit.GeneratedFluidActive && exhibit.GeneratedFluidReady
                && exhibit.Snapshot.ElapsedSeconds == 0 && !exhibit.Session.IsPlaying && !exhibit.Wrist.Raised,
                "accretion view exits generated mode into visible paused disks without discarding solved history");
            var restoredDisks = Pixels(exhibit.SceneTarget);
            exhibit.SetWristGas(false); exhibit.RenderFrame();
            Check(Difference(restoredDisks, Pixels(exhibit.SceneTarget)) > .0001,
                "restored accretion view emits visible gas after generated mode");
            exhibit.SetWristGas(true); Capture("accretion-restored-from-fluid.png");
            exhibit.UseGeneratedFluid(true); exhibit.RestartPresentation(); exhibit.RenderFrame();
            Check(!exhibit.GeneratedFluidActive && exhibit.GasVisible && exhibit.Snapshot.ElapsedSeconds == 0,
                "restart exits a selected computed history and restores the illustrative beginning");
            exhibit.UseGeneratedFluid(true); exhibit.WatchMerger(); exhibit.SetWristPlaying(false); exhibit.RenderFrame();
            Check(!exhibit.GeneratedFluidActive && exhibit.GasVisible && exhibit.Snapshot.ElapsedSeconds > 0,
                "watch merger selects visible illustrative merger gas after generated mode");
            exhibit.UseGeneratedFluid(true);
            exhibit.SelectWristScenario(false); yield return null;
            Check(!exhibit.GeneratedFluidReady && !exhibit.GeneratedFluidActive, "parameter generation invalidates prior fluid history");
            float expectedFocus = Mathf.Max(16, (float)exhibit.Snapshot.SeparationRg * 1.6f);
            var expectedCamera = new Vector3(0, expectedFocus * .85f, -expectedFocus * 1.3f);
            Check(Vector3.Distance(exhibit.View.transform.position, expectedCamera) < 1e-4f,
                "scenario switch frames the current binary after invalidating the wide generated reservoir");
            exhibit.StartGeneratedFluid(); watch.Restart();
            while (exhibit.IsGeneratingFluid && watch.Elapsed.TotalSeconds < 300) yield return null;
            Check(exhibit.GeneratedFluidReady, "adjustable binary generates its own matching fluid history");
            if (exhibit.GeneratedFluidReady)
            {
                Check(exhibit.GeneratedHistory.StartSeconds > 0, "long adjustable history uses bounded final interval");
                exhibit.SeekWrist(0); exhibit.RenderFrame();
                Check(!exhibit.GeneratedFluidPlayer.IsCovered && !exhibit.GeneratedFluidPlayer.RendererVisible,
                    "outside computed history no stale generated fluid is drawn");
                Check(exhibit.CaptureWristState().Status.Contains("Outside"), "uncovered epoch is reported in the suit");
                exhibit.SeekGeneratedFluidStart(); exhibit.RenderFrame();
                Check(exhibit.GeneratedFluidPlayer.IsCovered && exhibit.GeneratedFluidPlayer.RendererVisible,
                    "coverage-start control restores matching adjustable fluid");
                Capture("generated-adjustable.png");
                exhibit.RestartPresentation(); exhibit.RenderFrame();
                Check(exhibit.Snapshot.ElapsedSeconds == 0 && exhibit.GasVisible && !exhibit.GeneratedFluidActive
                    && exhibit.GeneratedFluidReady, "restart outside a bounded computed interval restores illustrative disks");
                exhibit.SetWristGas(false); exhibit.UseGeneratedFluid(true); exhibit.RenderFrame();
                Check(exhibit.GasVisible && exhibit.GeneratedFluidPlayer.RendererVisible
                    && Math.Abs(exhibit.Snapshot.ElapsedSeconds - exhibit.GeneratedHistory.StartSeconds) < 1e-9,
                    "reselecting generated fluid from an uncovered hidden epoch restores covered visible gas");
            }
            exhibit.StartGeneratedFluid(); exhibit.CancelGeneratedFluid(); yield return null;
            Check(!exhibit.IsGeneratingFluid && !exhibit.GeneratedFluidReady, "cancelled computation cannot publish stale data");
            exhibit.StartGeneratedFluid(); exhibit.ApplyWristExperiment(50, 20, 35);
            yield return new WaitForSecondsRealtime(.2f);
            Check(!exhibit.GeneratedFluidReady && !exhibit.GeneratedFluidActive, "changing masses during solve rejects its result");
            exhibit.ReturnToLauncher(); yield return null; yield return null;
            Check(exhibit.GeneratedHistory == null && !exhibit.IsGeneratingFluid && exhibit.GeneratedFluidMaterial == null,
                "return releases history and GPU textures");
            exhibit.Launch(ExhibitQualityProfile.High, true); yield return null;
            exhibit.StartGeneratedFluid(); yield return null;
            Check(!exhibit.IsGeneratingFluid && !exhibit.GeneratedFluidActive, "compatibility profile fails closed without fluid renderer");
            exhibit.ReturnToLauncher(); yield return null;
            WriteReceipt(); Application.Quit(passed ? 0 : 1);
        }
        private void ValidateGpuSamples(double time)
        {
            var history = exhibit.GeneratedHistory;
            var material = exhibit.GeneratedFluidMaterial;
            Check(material != null && material.shader.isSupported, "generated fluid shader supported");
            if (material == null) return;
            // Inspect the owned texture; sampler uniforms are not serialized Shader Properties.
            var storedFrame = exhibit.GeneratedFluidPlayer.LowerFrameTextureForQa;
            bool halfPrecision = storedFrame != null && storedFrame.format == TextureFormat.RGBAHalf;
            Check(storedFrame != null && (halfPrecision || storedFrame.format == TextureFormat.RGBAFloat),
                "generated field uses an admitted floating-point texture");
            if (Environment.GetEnvironmentVariable("DTET_FORCE_HALF_TEXTURES_QA") == "1")
                Check(halfPrecision, "forced compatibility fixture uses actual Half fluid storage");
            var target = new RenderTexture(65, 65, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); var image = new Texture2D(65, 65, TextureFormat.RGBAFloat, false, true);
            var go = new GameObject("Generated field fixture camera"); var camera = go.AddComponent<Camera>();
            camera.CopyFrom(exhibit.View); camera.enabled = false; camera.aspect = 1; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0); camera.allowHDR = true;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            camera.transform.position = new Vector3(0, 40, -80); camera.transform.LookAt(Vector3.zero);
            var renderers = exhibit.GetComponentsInChildren<Renderer>(true); var previousEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { previousEnabled[i] = renderers[i].enabled; renderers[i].enabled = renderers[i].sharedMaterial == material; }
            var previous = RenderTexture.active;
            bool fixturesEnabled = material.IsKeywordEnabled("DTET_QA_FIXTURES");
            try
            {
                material.EnableKeyword("DTET_QA_FIXTURES");
                material.SetFloat("_FieldTest", 1);
                foreach (var point in new[] { new Vector3(12, 0, 0), new Vector3(20, 2, 7), new Vector3(10000, 0, 0) })
                {
                    material.SetVector("_FieldPoint", point);
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 65, 65), 0, 0); image.Apply();
                    var actual = image.GetPixel(32, 32);
                    history.TrySample(time, point.x, point.z, out var sample);
                    double rho = sample.SurfaceDensity * Math.Exp(-point.y * point.y / 8.0) / (2 * Math.Sqrt(2 * Math.PI));
                    // Half texture tolerance includes 10-bit fractional quantization.
                    double relativeTolerance = halfPrecision ? .002 : 0;
                    bool Close(double measured, double expected) => Math.Abs(measured - expected) < 2e-4 + relativeTolerance * Math.Abs(expected);
                    Check(Close(actual.r, sample.SurfaceDensity) && Close(actual.g, rho)
                        && Close(actual.b, sample.VelocityXRgPerTg) && Close(actual.a, sample.VelocityZRgPerTg),
                        "CPU/GPU generated field and velocity " + point);
                }
            }
            finally
            {
                material.SetFloat("_FieldTest", 0);
                if (!fixturesEnabled) material.DisableKeyword("DTET_QA_FIXTURES");
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = previousEnabled[i];
                RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(image); Destroy(go);
            }
        }
        private void Capture(string name)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); target.Create();
            var previousTarget = exhibit.CaptureCamera.targetTexture; var active = RenderTexture.active;
            exhibit.CaptureCamera.targetTexture = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                exhibit.RenderFrame(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); images.Add(name);
                Check(new FileInfo(Path.Combine(output, name)).Length > 10000, "capture " + name);
            }
            finally { exhibit.CaptureCamera.targetTexture = previousTarget; RenderTexture.active = active; Destroy(image); target.Release(); Destroy(target); }
        }
        private static Color32[] Pixels(RenderTexture target)
        {
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            var result = image.GetPixels32(); Destroy(image); RenderTexture.active = old; return result;
        }
        private static double Difference(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return double.PositiveInfinity;
            double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return sum / (a.Length * 3 * 255.0);
        }
        private static void Fence(RenderTexture target)
        {
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1, 1, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); image.Apply(); Destroy(image); RenderTexture.active = old;
        }
        private void WriteReceipt()
        {
            double mean = 0, max = 0; foreach (double ms in renderMs) { mean += ms; max = Math.Max(max, ms); }
            var receipt = new Receipt { success = passed, checks = checks.ToArray(), images = images.ToArray(),
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                solveSeconds = solveSeconds, historyBytes = historyBytes,
                meanCompletedFrameMs = renderMs.Count == 0 ? 0 : mean / renderMs.Count, maximumCompletedFrameMs = max,
                scope = "Self-generated 2D isothermal Newtonian finite-volume fluid with prescribed binary forcing; assumed gas and Gaussian height, illustrative emission and optics. Completed offscreen render timings, not native presentation FPS or laptop/headset measurements. No GRMHD or binary metric validation." };
            File.WriteAllText(Path.Combine(output, "generated-fluid-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_GENERATED_FLUID_PASS" : "DTET_GENERATED_FLUID_FAILED");
        }
    }
}
#endif
