#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DTET.VisualPrototype
{
    /// <summary>Opt-in M4 checks; completed off-screen rendering is timed separately from PNG captures.</summary>
    internal sealed class ReducedExhibitQaCapture : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private readonly List<ImageMeasurement> images = new List<ImageMeasurement>();
        private readonly List<PerformanceWindow> performance = new List<PerformanceWindow>();
        private readonly List<ProfileMeasurement> profiles = new List<ProfileMeasurement>();
        private readonly List<GpuFieldMeasurement> gpuField = new List<GpuFieldMeasurement>();
        private readonly List<RayConvergenceMeasurement> rayConvergence = new List<RayConvergenceMeasurement>();
        private GpuSkyMeasurement gpuSky;
        private ReducedBinaryExhibit exhibit;
        private string output;
        private bool passed = true;
        private double benchmarkSeconds = 3;
        private ulong sharedFieldFingerprint;
        private string sharedEventSignature;
        private int previousVsync, previousTargetRate;

        [Serializable] private sealed class Receipt
        {
            public bool success, frameTimingEnabled;
            public string unityVersion, graphicsDevice, graphicsApi, processor, operatingSystem, scope, timingMethod;
            public int systemMemoryMb, graphicsMemoryMb, screenWidth, screenHeight;
            public string[] checks;
            public ImageMeasurement[] images;
            public ProfileMeasurement[] profiles;
            public PerformanceWindow[] performance;
            public GpuFieldMeasurement[] gpuField;
            public RayConvergenceMeasurement[] rayConvergence;
            public GpuSkyMeasurement gpuSky;
        }
        [Serializable] private sealed class GpuFieldMeasurement
        {
            public string label;
            public double epochSeconds, xRg, yRg, zRg, cpuDensity, gpuDensity, cpuEmission, gpuEmission, densityError, emissionError;
        }
        [Serializable] private sealed class RayConvergenceMeasurement
        {
            public string profile, view, targetFormat;
            public int width, height, testedRaySteps, referenceRaySteps;
            public double epochSeconds, testedIntegratedRadiance, referenceIntegratedRadiance,
                relativeL1RadianceError, integratedFluxRelativeError, threshold;
        }
        [Serializable] private sealed class GpuSkyMeasurement
        {
            public int starIndex, width, height;
            public double expectedRed, expectedGreen, expectedBlue, measuredRed, measuredGreen, measuredBlue,
                maximumRelativeChannelError, maximumRelativeIntegratedFluxError,
                translationMeanAbsoluteDifference, rotationMeanAbsoluteDifference;
            public string method;
        }
        [Serializable] private sealed class ImageMeasurement
        {
            public string file;
            public int width, height;
            public double centralMean, centralMinimum, centralMaximum;
        }
        [Serializable] private sealed class ProfileMeasurement
        {
            public string requested, effective, admissionReason, fieldFingerprint, eventSignature;
            public bool forcedFallback, fallback;
            public int renderWidth, renderHeight, stars, tracers;
            public long renderAllocationBytes, analyticalCacheBytes, unityAllocatedBytesBefore, unityAllocatedBytesAfterUnload;
            public Distribution synchronousSeekMilliseconds;
        }
        [Serializable] private sealed class PerformanceWindow
        {
            public string profile, scenario, method, frameTimingStatus;
            public bool fallback, frameTimingEnabled;
            public int renderWidth, renderHeight, interfaceWidth, interfaceHeight, measuredFrames, successfulCompletionReadbacks;
            public double elapsedSeconds;
            public Distribution wallFrameMilliseconds, unityCpuFrameMilliseconds, unityGpuFrameMilliseconds;
        }
        [Serializable] private sealed class Distribution
        {
            public int count;
            public double minimum, mean, median, p95, p99, maximum;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_REDUCED_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("DT-ET reduced exhibit validation").AddComponent<ReducedExhibitQaCapture>();
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
            if (!condition) { passed = false; Debug.LogError("DTET_REDUCED_FAILED " + label); }
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_REDUCED_QA_PATH"));
            Directory.CreateDirectory(output);
            if (double.TryParse(Environment.GetEnvironmentVariable("DTET_REDUCED_QA_BENCHMARK_SECONDS"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)) benchmarkSeconds = Math.Max(2, Math.Min(15, seconds));
            previousVsync = QualitySettings.vSyncCount;
            previousTargetRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(1);
            exhibit = FindFirstObjectByType<ReducedBinaryExhibit>();
            if (exhibit == null) { Check(false, "reduced exhibit is installed by its launch option"); Application.Quit(1); yield break; }
            exhibit.EmulatedInputEnabled = false;
            Check(!exhibit.Ready && exhibit.SceneTarget == null && exhibit.RenderAllocationBytes == 0,
                "launcher is visible before scene render allocations");
            Check(FindFirstObjectByType<VisualPrototypeBootstrap>() == null && FindFirstObjectByType<ReferenceFlowLab>() == null,
                "reduced exhibit is isolated from the legacy and optical reference bootstraps");
            Capture("reduced-launcher.png", true);
            foreach (var profile in new[] { ExhibitQualityProfile.Laptop, ExhibitQualityProfile.Balanced, ExhibitQualityProfile.High })
                yield return ValidateProfile(profile, false);
            yield return ValidateProfile(ExhibitQualityProfile.High, true);
            Check(!exhibit.Ready && exhibit.SceneTarget == null && exhibit.RenderAllocationBytes == 0,
                "all validation paths return to the launcher without retained scene targets");
            QualitySettings.vSyncCount = previousVsync;
            Application.targetFrameRate = previousTargetRate;
            var receipt = new Receipt {
                success = passed, unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), processor = SystemInfo.processorType,
                operatingSystem = SystemInfo.operatingSystem, systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsMemoryMb = SystemInfo.graphicsMemorySize, screenWidth = Screen.width, screenHeight = Screen.height,
                frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), checks = checks.ToArray(), images = images.ToArray(),
                profiles = profiles.ToArray(), performance = performance.ToArray(), gpuField = gpuField.ToArray(),
                rayConvergence = rayConvergence.ToArray(), gpuSky = gpuSky,
                timingMethod = "Completed off-screen frames in a hidden development player: Stopwatch wall intervals include a player-loop yield (source update), requested camera/seek work, explicit scene and full interface rendering, Canvas update and a one-pixel AsyncGPUReadback.WaitForCompletion fence. Targets and requests are allocated before the half-second rendered warmup. No PNG encoding, file I/O or full-image readback occurs inside timed windows. Per-frame one-pixel readback/fence overhead is included. FrameTimingManager CPU/GPU samples are separately retained when available; zero samples explicitly mean unavailable. This is conservative completed-frame throughput, not native presentation FPS or a standalone GPU benchmark. Earlier hidden-player loop-only timing is invalid as rendered-frame evidence.",
                scope = "Launcher and profile admission, a shared recorded binary/radiation history with an explicitly prescribed gas field, deterministic seeking, pause/free inspection, common-horizon/remnant transition, bounded render allocations, richer synthetic sky, fallback and release of per-scene resources. Process working set/private memory are separately sampled by the runner. No fluid conservation, scientific GRMHD, binary observer optics, measured gas reconstruction, laptop/headset performance or complete GPU VRAM claim."
            };
            File.WriteAllText(Path.Combine(output, "reduced-exhibit-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_REDUCED_EXHIBIT_INTEGRATION_PASS" : "DTET_REDUCED_EXHIBIT_INTEGRATION_FAILED");
            Application.Quit(passed ? 0 : 1);
        }

        private IEnumerator ValidateProfile(ExhibitQualityProfile requested, bool forcedFallback)
        {
            string label = requested + (forcedFallback ? "-forced-fallback" : "");
            var row = new ProfileMeasurement { requested = requested.ToString(), forcedFallback = forcedFallback,
                unityAllocatedBytesBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() };
            FindButton(requested.ToString().ToUpperInvariant()).onClick.Invoke();
            Check(exhibit.SelectedProfile == requested && !exhibit.Ready && exhibit.SceneTarget == null,
                label + " launcher selection chooses the budget before scene allocation");
            FindButton(forcedFallback ? "COMPATIBILITY / GAS OFF" : "ENTER EXHIBIT").onClick.Invoke();
            yield return null;
            yield return new WaitForEndOfFrame();
            Check(exhibit.Ready && exhibit.View != null && exhibit.CaptureCamera != null && exhibit.SceneTarget != null,
                label + " admits a ready scene with scene and interface cameras");
            if (!exhibit.Ready) yield break;
            row.effective = exhibit.Profile.EffectiveProfile.ToString(); row.admissionReason = exhibit.Profile.Reason;
            row.fallback = exhibit.FallbackActive;
            row.renderWidth = exhibit.SceneTarget.width; row.renderHeight = exhibit.SceneTarget.height;
            row.renderAllocationBytes = exhibit.RenderAllocationBytes;
            row.analyticalCacheBytes = exhibit.CacheBytes;
            row.stars = exhibit.SkyCount; row.tracers = exhibit.TracerCount;
            Check(!exhibit.Session.IsPlaying && exhibit.Session.PlaybackTimeSeconds == 0,
                label + " opens paused at the first source epoch");
            CheckBodies(label + " first epoch");
            Check(exhibit.CacheBytes == 0 && exhibit.CacheFrameCount == 0,
                label + " prescribed field needs no decoded history cache");
            exhibit.Profile.RenderDimensions(Screen.width, Screen.height, out int expectedWidth, out int expectedHeight);
            Check(row.renderWidth == expectedWidth && row.renderHeight == expectedHeight && row.renderAllocationBytes > 0,
                label + " allocates the admitted scene dimensions and reports its render payload");
            Check(exhibit.SkyCount == exhibit.Profile.StarCount && exhibit.TracerCount == exhibit.Profile.TracerCount,
                label + " honors admitted sky and diagnostic sample counts");
            Check(exhibit.FallbackActive == !exhibit.Profile.GasEnabled && (!forcedFallback || exhibit.FallbackActive),
                label + " fallback admission agrees with the actual gas capability");
            if (forcedFallback) Check(AllTextContains("fallback") || AllTextContains("compatibility"), "forced fallback explicitly labels reduced visual capability");
            Check(AllTextContains("assumed") || AllTextContains("prescribed"),
                label + " visibly distinguishes the gas assumption from the source history");
            var model = exhibit.Session.Model;
            Slider timeline = exhibit.GetComponentInChildren<Slider>();
            exhibit.SetPlaying(true); timeline.value = .35f;
            Canvas.ForceUpdateCanvases();
            Check(Math.Abs(exhibit.Session.PlaybackTimeSeconds - .35f * model.DurationSeconds) < 1e-12 && !exhibit.Session.IsPlaying,
                label + " actual timeline slider selects the source epoch and pauses");
            Check(timeline.fillRect != null && timeline.fillRect.parent is RectTransform track
                && Math.Abs(timeline.fillRect.rect.width - track.rect.width * timeline.normalizedValue) <= 1,
                label + " timeline fill stays inside the track at its normalized value");
            double chosen = model.CommonHorizonElapsedSeconds * .42;
            exhibit.Seek(chosen);
            yield return new WaitForEndOfFrame();
            string eventAtChosen = StateSignature(exhibit.State);
            ulong fieldAtChosen = exhibit.FieldFingerprint;
            ulong skyAtChosen = exhibit.SkyFingerprint;
            row.fieldFingerprint = fieldAtChosen.ToString("x16"); row.eventSignature = eventAtChosen;
            if (sharedEventSignature == null) { sharedEventSignature = eventAtChosen; sharedFieldFingerprint = fieldAtChosen; }
            Check(eventAtChosen == sharedEventSignature && fieldAtChosen == sharedFieldFingerprint,
                label + " preserves the same binary radiation and prescribed field across quality settings");
            Check(exhibit.Session.PlaybackTimeSeconds == chosen && !exhibit.Session.IsPlaying,
                label + " exact seek pauses at the requested source time");
            CheckBodies(label + " chosen inspiral");
            CheckRadiationCursor(label + " chosen inspiral");
            exhibit.SetTracers(true);
            ulong tracerAtChosen = exhibit.TracerFingerprint;
            Check(double.IsFinite(exhibit.TracerTotalWeight) && exhibit.TracerTotalWeight >= 0,
                label + " diagnostic sampling reports a finite nonnegative field-weight estimate");
            exhibit.SetTracers(false);
            Check(exhibit.FieldFingerprint == fieldAtChosen && StateSignature(exhibit.State) == eventAtChosen,
                label + " hiding diagnostic samples does not alter the source or gas field");
            exhibit.SetViewPreset(0);
            if (!exhibit.FallbackActive)
            {
                exhibit.SetGasVisible(false); Color32[] gasOff = ReadNativeScene(null);
                exhibit.SetGasVisible(true); Color32[] gasOn = ReadNativeScene(null);
                Check(MeanDifference(gasOff, gasOn) > .00001,
                    label + " actual GPU gas emission changes the scene independently of density samples");
                exhibit.Seek(model.CommonHorizonElapsedSeconds); exhibit.Seek(chosen);
                Color32[] repeated = ReadNativeScene(null);
                Check(MeanDifference(gasOn, repeated) < .001,
                    label + " repeated seek reproduces the native scene image");
            }
            Capture("reduced-" + label.ToLowerInvariant() + "-overview.png", true);
            Check(images[images.Count - 1].centralMaximum > .01,
                label + " scene and interface render a nonblack image");
            Vector3 cameraBefore = exhibit.View.transform.position;
            exhibit.MoveCamera(new Vector3(.2f, .1f, -.2f));
            yield return new WaitForSecondsRealtime(.1f);
            Check(exhibit.View.transform.position != cameraBefore && exhibit.Session.PlaybackTimeSeconds == chosen
                && StateSignature(exhibit.State) == eventAtChosen && exhibit.FieldFingerprint == fieldAtChosen,
                label + " camera traversal while paused leaves binary radiation and gas unchanged");
            Check(exhibit.SkyFingerprint == skyAtChosen, label + " camera translation preserves synthetic stellar directions");

            var seekTimes = new List<double>(96);
            var clock = new Stopwatch();
            for (int i = 0; i < 96; i++)
            {
                double fraction = ((i * 397) % 1023 + .25) / 1024;
                clock.Restart(); exhibit.Seek(fraction * model.DurationSeconds); clock.Stop();
                seekTimes.Add(clock.Elapsed.TotalMilliseconds);
                if (i % 8 == 7) yield return null;
            }
            row.synchronousSeekMilliseconds = Summarize(seekTimes);
            exhibit.Seek(chosen); exhibit.SetTracers(true);
            yield return new WaitForEndOfFrame();
            Check(StateSignature(exhibit.State) == eventAtChosen && exhibit.FieldFingerprint == fieldAtChosen
                && exhibit.TracerFingerprint == tracerAtChosen,
                label + " ninety-six distant seeks reconstruct the identical binary field and diagnostic samples");
            Check(exhibit.CacheBytes == 0 && exhibit.RenderAllocationBytes == row.renderAllocationBytes,
                label + " repeated seeking keeps render allocation and history storage bounded");
            exhibit.SetTracers(false);
            exhibit.SetRate(.01); exhibit.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.15f); exhibit.SetPlaying(false);
            double forward = exhibit.Session.PlaybackTimeSeconds;
            Check(forward > chosen, label + " normal Update advances the source history");
            exhibit.SetRate(-.01); exhibit.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.15f); exhibit.SetPlaying(false);
            Check(exhibit.Session.PlaybackTimeSeconds < forward, label + " reverse playback moves to earlier source history");
            exhibit.Seek(0); exhibit.SetRate(-4); exhibit.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.08f);
            Check(exhibit.Session.PlaybackTimeSeconds == 0 && !exhibit.Session.IsPlaying,
                label + " reverse clamps and pauses at the first epoch");
            exhibit.Seek(model.DurationSeconds); exhibit.SetRate(4); exhibit.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.08f);
            Check(exhibit.Session.PlaybackTimeSeconds == model.DurationSeconds && !exhibit.Session.IsPlaying,
                label + " forward clamps and pauses at the final epoch");
            Check(exhibit.State.HasCommonHorizon && !exhibit.State.Radiation.HasValue,
                label + " final remnant does not extrapolate missing waveform coverage");
            CheckBodies(label + " final remnant");
            CheckRadiationCursor(label + " final remnant");
            Check(exhibit.RadiationText.IndexOf("coverage", StringComparison.OrdinalIgnoreCase) >= 0,
                label + " radiation readout labels unavailable waveform coverage");
            Capture("reduced-" + label.ToLowerInvariant() + "-remnant.png", true);
            exhibit.Seek(model.CommonHorizonElapsedSeconds);
            Check(exhibit.State.HasCommonHorizon && exhibit.State.SourceReferenceAlignedSeconds == 0
                && exhibit.State.Radiation.HasValue && exhibit.State.Radiation.Value.NativeRetardedTime == model.WaveformPeakNativeTime,
                label + " exact common-horizon command preserves the declared radiation epoch alignment");
            CheckBodies(label + " first common horizon");
            Capture("reduced-" + label.ToLowerInvariant() + "-merger.png", true);
            exhibit.Seek(Math.Max(0, model.CommonHorizonElapsedSeconds - .001));
            Check(!exhibit.State.HasCommonHorizon && exhibit.State.BodyA.HasValue && exhibit.State.BodyB.HasValue,
                label + " rewind across the transition restores both individual horizons");
            CheckBodies(label + " rewind before common horizon");
            exhibit.Seek(chosen);
            if (!exhibit.FallbackActive) CheckRayConvergence(label);
            if (requested == ExhibitQualityProfile.Balanced && !forcedFallback)
            {
                if (!exhibit.FallbackActive) CheckGpuField();
                CheckRenderedSky();
                string[] viewNames = { "above", "edge", "below", "close" };
                for (int i = 0; i < viewNames.Length; i++)
                {
                    exhibit.SetViewPreset(i + 1); yield return new WaitForEndOfFrame();
                    Capture("reduced-balanced-" + viewNames[i] + ".png", true);
                }
                exhibit.SetViewPreset(0); exhibit.SetTracers(true);
                Capture("reduced-balanced-diagnostic-samples.png", true); exhibit.SetTracers(false);
            }
            foreach (string scenario in new[] { "overview-paused", "edge-paused", "merger-playback", "camera-motion", "repeated-seek" })
                yield return MeasurePerformance(label, scenario, chosen);
            exhibit.SetPlaying(false);
            exhibit.ReturnToLauncher();
            yield return null; yield return null;
            row.unityAllocatedBytesAfterUnload = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Check(!exhibit.Ready && exhibit.SceneTarget == null && exhibit.RenderAllocationBytes == 0,
                label + " unload releases the render target and restores the launcher");
            int retainedTargets = 0;
            foreach (var target in Resources.FindObjectsOfTypeAll<RenderTexture>())
                if (target.name == "M4 bounded scene target") retainedTargets++;
            Check(retainedTargets == 0 && exhibit.GetComponentsInChildren<ReducedExhibitSky>(true).Length == 0,
                label + " deferred destruction removes the owned target and sky component");
            profiles.Add(row);
        }

        private IEnumerator MeasurePerformance(string label, string scenario, double chosen)
        {
            exhibit.SetPlaying(false); exhibit.SetTracers(false); exhibit.SetViewPreset(scenario == "edge-paused" ? 2 : 0);
            exhibit.Seek(scenario == "merger-playback" ? Math.Max(0, exhibit.Session.Model.CommonHorizonElapsedSeconds - .01) : chosen);
            if (scenario == "merger-playback") { exhibit.SetRate(.01); exhibit.SetPlaying(true); }
            Check(SystemInfo.supportsAsyncGPUReadback, label + " " + scenario + " supports a GPU completion readback");
            if (!SystemInfo.supportsAsyncGPUReadback) yield break;
            var fullInterfaceTarget = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var sceneRequest = new RenderPipeline.StandardRequest { destination = exhibit.SceneTarget };
            var interfaceRequest = new RenderPipeline.StandardRequest { destination = fullInterfaceTarget };
            if (!RenderPipeline.SupportsRenderRequest(exhibit.View, sceneRequest)
                || !RenderPipeline.SupportsRenderRequest(exhibit.CaptureCamera, interfaceRequest))
            {
                RenderTexture.ReleaseTemporary(fullInterfaceTarget);
                throw new InvalidOperationException("Completed-frame benchmark render requests are unavailable.");
            }
            var wall = new List<double>(2048); var cpu = new List<double>(2048); var gpu = new List<double>(2048);
            var timing = new FrameTiming[1];
            ulong lastTimestamp = 0;
            int frames = 0, completions = 0;
            Vector3 origin = exhibit.View.transform.position;
            var clock = new Stopwatch();
            try
            {
                var warmup = Stopwatch.StartNew();
                while (warmup.Elapsed.TotalSeconds < .5)
                {
                    yield return null;
                    CompleteOffscreenFrame(sceneRequest, interfaceRequest, fullInterfaceTarget);
                }
                clock.Start();
                double previous = clock.Elapsed.TotalMilliseconds;
                while (clock.Elapsed.TotalSeconds < benchmarkSeconds)
                {
                    // Include the ordinary player Update that advances playback. A hidden window
                    // may suppress its automatic render, so both cameras are explicitly rendered.
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    if (scenario == "camera-motion")
                    {
                        double phase = clock.Elapsed.TotalSeconds * 3;
                        Vector3 desired = origin + new Vector3((float)Math.Sin(phase) * 4, (float)Math.Cos(phase * .7) * 2, 0);
                        exhibit.MoveCamera(desired - exhibit.View.transform.position);
                    }
                    else if (scenario == "repeated-seek")
                        exhibit.Seek(((frames * 397) % 1023 + .25) / 1024 * exhibit.Session.Model.DurationSeconds);
                    CompleteOffscreenFrame(sceneRequest, interfaceRequest, fullInterfaceTarget); completions++;
                    double now = clock.Elapsed.TotalMilliseconds;
                    wall.Add(now - previous); previous = now; frames++;
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastTimestamp)
                    {
                        lastTimestamp = timing[0].frameStartTimestamp;
                        if (FinitePositive(timing[0].cpuFrameTime)) cpu.Add(timing[0].cpuFrameTime);
                        if (FinitePositive(timing[0].gpuFrameTime)) gpu.Add(timing[0].gpuFrameTime);
                    }
                }
                clock.Stop();
                performance.Add(new PerformanceWindow { profile = label, scenario = scenario, fallback = exhibit.FallbackActive,
                    frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(), renderWidth = exhibit.SceneTarget.width,
                    renderHeight = exhibit.SceneTarget.height, interfaceWidth = fullInterfaceTarget.width, interfaceHeight = fullInterfaceTarget.height,
                    measuredFrames = frames, successfulCompletionReadbacks = completions, elapsedSeconds = clock.Elapsed.TotalSeconds,
                    method = "Completed off-screen scene plus full interface render, including player update and one-pixel GPU completion readback; not native presentation FPS.",
                    frameTimingStatus = cpu.Count > 0 && gpu.Count > 0 ? "Unity CPU and GPU frame-timing samples available."
                        : "Unity frame-timing samples unavailable or incomplete; use the separately completed off-screen frame timings.",
                    wallFrameMilliseconds = Summarize(wall), unityCpuFrameMilliseconds = Summarize(cpu), unityGpuFrameMilliseconds = Summarize(gpu) });
                Check(frames >= 5 && completions == frames && wall.TrueForAll(FinitePositive),
                    label + " " + scenario + " supplies finite timings for completed off-screen frames");
            }
            finally
            {
                exhibit.SetPlaying(false);
                RenderTexture.ReleaseTemporary(fullInterfaceTarget);
            }
        }

        private void CompleteOffscreenFrame(RenderPipeline.StandardRequest sceneRequest,
            RenderPipeline.StandardRequest interfaceRequest, RenderTexture fullInterfaceTarget)
        {
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(exhibit.View, sceneRequest);
            RenderPipeline.SubmitRenderRequest(exhibit.CaptureCamera, interfaceRequest);
            var readback = AsyncGPUReadback.Request(fullInterfaceTarget, 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32);
            readback.WaitForCompletion();
            if (!readback.done || readback.hasError)
                throw new InvalidOperationException("Completed-frame benchmark GPU readback did not complete successfully.");
        }

        private static bool FinitePositive(double value) => double.IsFinite(value) && value > 0;
        private static Distribution Summarize(List<double> values)
        {
            var result = new Distribution { count = values.Count };
            if (values.Count == 0) return result;
            double[] sorted = values.ToArray(); Array.Sort(sorted);
            double total = 0; foreach (double value in sorted) total += value;
            result.minimum = sorted[0]; result.maximum = sorted[sorted.Length - 1]; result.mean = total / sorted.Length;
            result.median = Quantile(sorted, .5); result.p95 = Quantile(sorted, .95); result.p99 = Quantile(sorted, .99);
            return result;
        }
        private static double Quantile(double[] sorted, double fraction)
        {
            double index = (sorted.Length - 1) * fraction;
            int lower = (int)index, upper = Math.Min(sorted.Length - 1, lower + 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
        }
        private static string StateSignature(NumericalEventState state)
        {
            string text = state.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture) + "/" + state.NativeCoordinateTime.ToString("R", CultureInfo.InvariantCulture)
                + "/" + state.HasCommonHorizon;
            foreach (NumericalHorizonState? body in new[] { state.BodyA, state.BodyB, state.Remnant })
                text += body.HasValue ? "/" + body.Value.PositionMeters.X.ToString("R", CultureInfo.InvariantCulture)
                    + "/" + body.Value.PositionMeters.Y.ToString("R", CultureInfo.InvariantCulture)
                    + "/" + body.Value.PositionMeters.Z.ToString("R", CultureInfo.InvariantCulture)
                    + "/" + body.Value.ArealRadiusMeters.ToString("R", CultureInfo.InvariantCulture) : "/none";
            return text + (state.Radiation.HasValue ? "/" + state.Radiation.Value.RealH22.ToString("R", CultureInfo.InvariantCulture)
                + "/" + state.Radiation.Value.ImaginaryH22.ToString("R", CultureInfo.InvariantCulture) : "/no-radiation");
        }
        private bool AllTextContains(string value)
        {
            foreach (var text in exhibit.GetComponentsInChildren<Text>(true))
                if (text.isActiveAndEnabled && text.text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private Button FindButton(string name)
        {
            foreach (var button in exhibit.GetComponentsInChildren<Button>())
                if (button.name == name && button.isActiveAndEnabled) return button;
            throw new InvalidOperationException("Missing visible exhibit button " + name);
        }
        private void CheckBodies(string label)
        {
            Check(MatchesBody(exhibit.BodyATransform, exhibit.State.BodyA)
                && MatchesBody(exhibit.BodyBTransform, exhibit.State.BodyB)
                && MatchesBody(exhibit.RemnantTransform, exhibit.State.Remnant),
                label + " actual scene transforms and equal-area radii match the source state");
        }
        private bool MatchesBody(Transform marker, NumericalHorizonState? source)
        {
            if (marker == null || marker.gameObject.activeSelf != source.HasValue) return false;
            if (!source.HasValue) return true;
            double scale = exhibit.LengthMetersPerDisplayUnit;
            var p = source.Value.PositionMeters;
            var expected = new Vector3((float)(p.X / scale), (float)(p.Z / scale), (float)(p.Y / scale));
            double radius = marker.localScale.x * .5 * scale;
            return Vector3.Distance(marker.position, expected) < 1e-5
                && Math.Abs(radius - source.Value.ArealRadiusMeters) < .5
                && marker.localScale.x == marker.localScale.y && marker.localScale.y == marker.localScale.z;
        }
        private void CheckRadiationCursor(string label)
        {
            RectTransform cursor = null;
            foreach (var rect in exhibit.GetComponentsInChildren<RectTransform>())
                if (rect.name == "Radiation cursor") cursor = rect;
            double expectedX = 1110 + 450 * exhibit.Session.PlaybackTimeSeconds / exhibit.Session.Model.DurationSeconds;
            Check(cursor != null && Math.Abs(cursor.anchoredPosition.x - expectedX) < .01,
                label + " actual waveform cursor follows the selected source epoch");
        }

        private void CheckRenderedSky()
        {
            Renderer[] renderers = exhibit.GetComponentsInChildren<Renderer>(true);
            var enabled = new bool[renderers.Length];
            Vector3 position = exhibit.View.transform.position;
            Quaternion rotation = exhibit.View.transform.rotation;
            Color background = exhibit.View.backgroundColor;
            bool hdr = exhibit.View.allowHDR, msaa = exhibit.View.allowMSAA;
            var cameraData = exhibit.View.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post = cameraData != null && cameraData.renderPostProcessing;
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    enabled[i] = renderers[i].enabled;
                    // The sky controller is attached to the scene root, which also owns gas/bodies.
                    // Identify its actual draw material rather than accepting every descendant.
                    Material drawMaterial = renderers[i].sharedMaterial;
                    bool skyDraw = drawMaterial != null && drawMaterial.shader != null && drawMaterial.shader.name == "DTET/ExhibitSky";
                    if (!skyDraw) renderers[i].enabled = false;
                }
                // A deterministic bright reference avoids depending on whether an arbitrary
                // overview happens to contain a bright star. Point directly at catalogue entry 0.
                SyntheticSkyStar star = SyntheticSkyCatalog.Star(0);
                var direction = new Vector3((float)star.X, (float)star.Y, (float)star.Z);
                exhibit.View.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                exhibit.View.backgroundColor = Color.black;
                exhibit.View.allowHDR = true; exhibit.View.allowMSAA = false;
                if (cameraData != null) cameraData.renderPostProcessing = false;
                Quaternion referenceRotation = exhibit.View.transform.rotation;
                Color[] before = ReadNativeRadiance();
                ReadNativeScene("reduced-synthetic-sky.png");
                exhibit.View.transform.position = position + new Vector3(2, 1, -3);
                Color[] translated = ReadNativeRadiance();
                exhibit.View.transform.rotation = referenceRotation * Quaternion.Euler(0, 23, 0);
                Color[] rotated = ReadNativeRadiance();
                int width = exhibit.SceneTarget.width, height = exhibit.SceneTarget.height;
                // Admitted dimensions are even: the central star is between the four central
                // pixels, making the selected pixel center (+0.5,+0.5) from its projected center.
                double sigma = SyntheticSkyCatalog.PsfSigmaPixels(star.RelativeFlux, height);
                double flux = SyntheticSkyCatalog.IntegratedDisplayFlux(star.RelativeFlux, height)
                    * SyntheticSkyCatalog.PsfPixelMass(.5, .5, sigma);
                Color measured = before[(height / 2) * width + width / 2];
                double er = star.Red * flux, eg = star.Green * flux, eb = star.Blue * flux;
                double relativeError = Math.Max(Math.Abs(measured.r - er) / er,
                    Math.Max(Math.Abs(measured.g - eg) / eg, Math.Abs(measured.b - eb) / eb));
                int extent = (int)Math.Ceiling(3 * sigma + .5);
                double sumR = 0, sumG = 0, sumB = 0;
                for (int y = height / 2 - extent; y < height / 2 + extent; y++)
                    for (int x = width / 2 - extent; x < width / 2 + extent; x++)
                    { Color p = before[y * width + x]; sumR += p.r; sumG += p.g; sumB += p.b; }
                double integrated = SyntheticSkyCatalog.IntegratedDisplayFlux(star.RelativeFlux, height);
                double integratedError = Math.Max(Math.Abs(sumR - star.Red * integrated) / (star.Red * integrated),
                    Math.Max(Math.Abs(sumG - star.Green * integrated) / (star.Green * integrated),
                        Math.Abs(sumB - star.Blue * integrated) / (star.Blue * integrated)));
                double translationDifference = MeanDifference(before, translated), rotationDifference = MeanDifference(before, rotated);
                gpuSky = new GpuSkyMeasurement { starIndex = 0, width = width, height = height,
                    expectedRed = er, expectedGreen = eg, expectedBlue = eb, measuredRed = measured.r, measuredGreen = measured.g, measuredBlue = measured.b,
                    maximumRelativeChannelError = relativeError, maximumRelativeIntegratedFluxError = integratedError,
                    translationMeanAbsoluteDifference = translationDifference,
                    rotationMeanAbsoluteDifference = rotationDifference,
                    method = "Sky-only native linear HDR image with black background, aimed at synthetic catalogue star 0. Central pixel prediction uses integrated Gaussian PSF at (+0.5,+0.5) pixels for the even target dimensions, normalized catalogue colour and resolution-scaled flux. Relative channel tolerance 0.5 percent includes float projection and half-float target rounding." };
                Check(double.IsFinite(relativeError) && relativeError < .005,
                    "actual GPU bright-star central radiance matches the integrated PSF prediction within 0.5 percent");
                Check(double.IsFinite(integratedError) && integratedError < .01,
                    "actual GPU isolated bright-star kernel preserves integrated RGB flux within one percent");
                Check(translationDifference < .00001,
                    "actual GPU distant-star image is invariant under camera translation at fixed orientation");
                Check(rotationDifference > .00001,
                    "actual GPU distant-star directions respond to camera rotation");
            }
            finally
            {
                exhibit.View.transform.SetPositionAndRotation(position, rotation);
                exhibit.View.backgroundColor = background;
                exhibit.View.allowHDR = hdr; exhibit.View.allowMSAA = msaa;
                if (cameraData != null) cameraData.renderPostProcessing = post;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
            }
        }

        private void CheckGpuField()
        {
            double previousTime = exhibit.Session.PlaybackTimeSeconds;
            Camera camera = exhibit.View;
            bool hdr = camera.allowHDR, msaa = camera.allowMSAA;
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post = cameraData != null && cameraData.renderPostProcessing;
            Renderer[] renderers = exhibit.GetComponentsInChildren<Renderer>(true);
            var enabled = new bool[renderers.Length];
            Material material = exhibit.GasMaterial;
            float previousTest = material.GetFloat("_FieldTest");
            Vector4 previousPoint = material.GetVector("_FieldPoint");
            var model = new ReducedBinaryFlowModel(exhibit.Session.Model);
            try
            {
                Check(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat),
                    "GPU field fixture supports an explicit linear floating-point target");
                camera.allowHDR = true; camera.allowMSAA = false;
                if (cameraData != null) cameraData.renderPostProcessing = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    enabled[i] = renderers[i].enabled;
                    renderers[i].enabled = renderers[i].sharedMaterial == material;
                }
                material.SetFloat("_FieldTest", 1);
                double[] epochs = { 0, Math.Max(0, model.CommonHorizonElapsedSeconds - 60 * model.GravitationalTimeSeconds),
                    model.CommonHorizonElapsedSeconds, model.DurationSeconds };
                int epochIndex = 0;
                foreach (double epoch in epochs)
                {
                    exhibit.Seek(epoch);
                    ReducedBinaryFlowState state = exhibit.FieldState;
                    double reservoirRadius = (state.ReservoirInnerRadiusRg + state.ReservoirOuterRadiusRg) * .5;
                    var points = new List<Vector3> { new Vector3((float)reservoirRadius, 0, 0), Vector3.zero,
                        new Vector3((float)(model.HalfExtentXZ + 2), 0, 0) };
                    if (!state.IsCommonHorizon)
                    {
                        var body = state.BodyA;
                        points.Add(new Vector3((float)body.PositionRg.X, (float)body.PositionRg.Y,
                            (float)(body.PositionRg.Z + (body.InnerRadiusRg + body.OuterRadiusRg) * .5)));
                        var stream = state.StreamA;
                        points.Add(new Vector3((float)((stream.StartRg.X + stream.EndRg.X) * .5 + stream.BowNormal.X * ReducedBinaryFlowModel.StreamBowRg),
                            (float)((stream.StartRg.Y + stream.EndRg.Y) * .5),
                            (float)((stream.StartRg.Z + stream.EndRg.Z) * .5 + stream.BowNormal.Z * ReducedBinaryFlowModel.StreamBowRg)));
                    }
                    else
                    {
                        var body = state.Remnant;
                        points.Add(new Vector3((float)(body.PositionRg.X + (body.InnerRadiusRg + body.OuterRadiusRg) * .5),
                            (float)body.PositionRg.Y, (float)body.PositionRg.Z));
                    }
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector3 point = points[i];
                        var expected = model.Sample(state, point.x, point.y, point.z);
                        material.SetVector("_FieldPoint", new Vector4(point.x, point.y, point.z, 0));
                        Color measured = ReadLinearFieldPixel(camera);
                        double densityError = Math.Abs(measured.r - expected.RelativeDensity);
                        double emissionError = Math.Abs(measured.g - expected.NormalizedBolometricEmission);
                        string label = "epoch " + epochIndex + " point " + i;
                        gpuField.Add(new GpuFieldMeasurement { label = label, epochSeconds = epoch, xRg = point.x, yRg = point.y, zRg = point.z,
                            cpuDensity = expected.RelativeDensity, gpuDensity = measured.r, cpuEmission = expected.NormalizedBolometricEmission,
                            gpuEmission = measured.g, densityError = densityError, emissionError = emissionError });
                        Check(float.IsFinite(measured.r) && float.IsFinite(measured.g)
                            && densityError <= 5e-5 + 5e-4 * Math.Abs(expected.RelativeDensity)
                            && emissionError <= 5e-5 + 5e-4 * Math.Abs(expected.NormalizedBolometricEmission),
                            "actual GPU field matches the independent CPU value at " + label);
                    }
                    epochIndex++;
                }
            }
            finally
            {
                material.SetFloat("_FieldTest", previousTest); material.SetVector("_FieldPoint", previousPoint);
                camera.allowHDR = hdr; camera.allowMSAA = msaa;
                if (cameraData != null) cameraData.renderPostProcessing = post;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                exhibit.Seek(previousTime);
            }
        }

        private void CheckRayConvergence(string label)
        {
            double previousTime = exhibit.Session.PlaybackTimeSeconds;
            Camera camera = exhibit.View;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            Color background = camera.backgroundColor;
            bool hdr = camera.allowHDR, msaa = camera.allowMSAA;
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post = cameraData != null && cameraData.renderPostProcessing;
            Renderer[] renderers = exhibit.GetComponentsInChildren<Renderer>(true);
            var enabled = new bool[renderers.Length];
            Material material = exhibit.GasMaterial;
            float previousSteps = material.GetFloat("_Steps");
            var model = new ReducedBinaryFlowModel(exhibit.Session.Model);
            double threshold = exhibit.Profile.EffectiveProfile == ExhibitQualityProfile.Laptop ? .20
                : exhibit.Profile.EffectiveProfile == ExhibitQualityProfile.Balanced ? .10 : .05;
            try
            {
                Check(!exhibit.SceneTarget.sRGB && (exhibit.SceneTarget.format == RenderTextureFormat.ARGBHalf
                    || exhibit.SceneTarget.format == RenderTextureFormat.ARGBFloat),
                    label + " ray-convergence comparisons use the native linear HDR target");
                camera.backgroundColor = Color.black; camera.allowHDR = true; camera.allowMSAA = false;
                if (cameraData != null) cameraData.renderPostProcessing = false;
                for (int i = 0; i < renderers.Length; i++)
                {
                    enabled[i] = renderers[i].enabled;
                    renderers[i].enabled = renderers[i].sharedMaterial == material;
                }
                int[] presets = { 0, 2, 1, 4, 0 };
                string[] names = { "initial-overview", "initial-edge", "initial-above", "initial-close", "half-fade-overview" };
                for (int i = 0; i < presets.Length; i++)
                {
                    double epoch = i == presets.Length - 1
                        ? Math.Max(0, model.CommonHorizonElapsedSeconds - 60 * model.GravitationalTimeSeconds) : 0;
                    exhibit.Seek(epoch); exhibit.SetViewPreset(presets[i]);
                    material.SetFloat("_Steps", exhibit.Profile.RaySteps);
                    Color[] tested = ReadNativeRadiance();
                    material.SetFloat("_Steps", 256);
                    Color[] reference = ReadNativeRadiance();
                    double testSum = 0, referenceSum = 0, error = 0;
                    bool finite = true;
                    for (int p = 0; p < tested.Length; p++)
                    {
                        Color a = tested[p], b = reference[p];
                        finite &= float.IsFinite(a.r) && float.IsFinite(a.g) && float.IsFinite(a.b)
                            && float.IsFinite(b.r) && float.IsFinite(b.g) && float.IsFinite(b.b);
                        testSum += a.r + a.g + a.b; referenceSum += b.r + b.g + b.b;
                        error += Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);
                    }
                    double relativeL1 = referenceSum > 0 ? error / referenceSum : 1e30;
                    double fluxError = referenceSum > 0 ? Math.Abs(testSum - referenceSum) / referenceSum : 1e30;
                    rayConvergence.Add(new RayConvergenceMeasurement { profile = label, view = names[i],
                        targetFormat = exhibit.SceneTarget.format + " / Linear", width = exhibit.SceneTarget.width,
                        height = exhibit.SceneTarget.height, testedRaySteps = exhibit.Profile.RaySteps, referenceRaySteps = 256,
                        epochSeconds = epoch, testedIntegratedRadiance = testSum, referenceIntegratedRadiance = referenceSum,
                        relativeL1RadianceError = relativeL1, integratedFluxRelativeError = fluxError, threshold = threshold });
                    Check(finite && referenceSum > 1e-9 && relativeL1 <= threshold && fluxError <= threshold,
                        label + " " + names[i] + " radiance agrees with 256-ray-step numerical reference within "
                        + (threshold * 100).ToString("0", CultureInfo.InvariantCulture) + " percent relative L1 and integrated flux");
                }
            }
            finally
            {
                material.SetFloat("_Steps", previousSteps);
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.backgroundColor = background; camera.allowHDR = hdr; camera.allowMSAA = msaa;
                if (cameraData != null) cameraData.renderPostProcessing = post;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                exhibit.Seek(previousTime);
            }
        }

        private Color[] ReadNativeRadiance()
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = exhibit.SceneTarget };
                if (!RenderPipeline.SupportsRenderRequest(exhibit.View, request)) throw new InvalidOperationException("Ray-convergence render request is unavailable.");
                RenderPipeline.SubmitRenderRequest(exhibit.View, request);
                RenderTexture.active = exhibit.SceneTarget;
                texture = new Texture2D(exhibit.SceneTarget.width, exhibit.SceneTarget.height, TextureFormat.RGBAFloat, false, true);
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
                return texture.GetPixels();
            }
            finally { RenderTexture.active = previous; if (texture != null) Destroy(texture); }
        }

        private static Color ReadLinearFieldPixel(Camera camera)
        {
            const int side = 65;
            var target = RenderTexture.GetTemporary(side, side, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("GPU field fixture render request is unavailable.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture = new Texture2D(side, side, TextureFormat.RGBAFloat, false, true);
                texture.ReadPixels(new Rect(0, 0, side, side), 0, 0); texture.Apply();
                return texture.GetPixel(side / 2, side / 2);
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Destroy(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private Color32[] ReadNativeScene(string file)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = exhibit.SceneTarget };
                if (!RenderPipeline.SupportsRenderRequest(exhibit.View, request)) throw new InvalidOperationException("Reduced scene render request is unavailable.");
                RenderPipeline.SubmitRenderRequest(exhibit.View, request);
                RenderTexture.active = exhibit.SceneTarget;
                texture = new Texture2D(exhibit.SceneTarget.width, exhibit.SceneTarget.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
                Color32[] pixels = texture.GetPixels32();
                if (file != null)
                {
                    File.WriteAllBytes(Path.Combine(output, file), texture.EncodeToPNG());
                    images.Add(new ImageMeasurement { file = file, width = texture.width, height = texture.height });
                    Check(new FileInfo(Path.Combine(output, file)).Length > 500, "capture " + file);
                }
                return pixels;
            }
            finally { RenderTexture.active = previous; if (texture != null) Destroy(texture); }
        }

        private static double MeanDifference(Color32[] left, Color32[] right)
        {
            if (left.Length != right.Length) return double.PositiveInfinity;
            double sum = 0;
            for (int i = 0; i < left.Length; i++)
                sum += Math.Abs(left[i].r - right[i].r) + Math.Abs(left[i].g - right[i].g) + Math.Abs(left[i].b - right[i].b);
            return sum / (left.Length * 3.0 * 255);
        }
        private static double MeanDifference(Color[] left, Color[] right)
        {
            if (left.Length != right.Length) return double.PositiveInfinity;
            double sum = 0;
            for (int i = 0; i < left.Length; i++)
                sum += Math.Abs(left[i].r - right[i].r) + Math.Abs(left[i].g - right[i].g) + Math.Abs(left[i].b - right[i].b);
            return sum / (left.Length * 3.0);
        }

        private Color32[] Capture(string file, bool includeInterface)
        {
            const int width = 1600, height = 900;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                if (exhibit.Ready)
                {
                    var sceneRequest = new RenderPipeline.StandardRequest { destination = exhibit.SceneTarget };
                    if (!RenderPipeline.SupportsRenderRequest(exhibit.View, sceneRequest)) throw new InvalidOperationException("Reduced scene render request is unavailable.");
                    RenderPipeline.SubmitRenderRequest(exhibit.View, sceneRequest);
                }
                Camera camera = includeInterface ? exhibit.CaptureCamera : exhibit.View;
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("Reduced capture render request is unavailable.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                string path = Path.Combine(output, file); File.WriteAllBytes(path, texture.EncodeToPNG());
                Color32[] pixels = texture.GetPixels32(); double sum = 0, minimum = 1, maximum = 0; int count = 0;
                for (int y = height / 4; y < height * 3 / 4; y++) for (int x = width / 4; x < width * 3 / 4; x++)
                {
                    Color32 p = pixels[y * width + x]; double v = (.2126 * p.r + .7152 * p.g + .0722 * p.b) / 255;
                    sum += v; minimum = Math.Min(minimum, v); maximum = Math.Max(maximum, v); count++;
                }
                images.Add(new ImageMeasurement { file = file, width = width, height = height,
                    centralMean = sum / count, centralMinimum = minimum, centralMaximum = maximum });
                Check(new FileInfo(path).Length > 500, "capture " + file);
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Destroy(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
