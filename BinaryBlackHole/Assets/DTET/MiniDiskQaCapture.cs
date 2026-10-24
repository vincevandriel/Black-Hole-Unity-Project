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
    // Opt-in development player integration and actual production-shader controls.
    internal sealed class MiniDiskQaCapture : MonoBehaviour
    {
        private InteractiveBinaryExhibit exhibit;
        private string output;
        private bool passed = true, written;
        private readonly List<string> checks = new List<string>();
        private readonly List<string> images = new List<string>();
        private readonly List<double> renderMs = new List<double>();
        private double densityChange, dopplerRatio, offPlaneRatio, shadowRelativeError;
        private long playbackBytes;
        private int movieFrames;
        private bool experimentEnabled;
        private double experimentSeconds;
        private readonly List<ProfileEvidence> profileEvidence = new List<ProfileEvidence>();

        [Serializable] private sealed class ProfileEvidence
        {
            public string requested, effective, fieldFingerprint, materialFingerprint;
            public int gridSize, frameCount, rayBudget, width, height;
            public double elapsedSeconds, meanCompletedReadbackMs;
        }

        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string unityVersion, graphicsDevice, scope;
            public string[] checks, images;
            public double relativeDensityChange, approachingToRecedingRadiance, curvedToStraightOffPlaneRadiance;
            public double singleHoleShadowRelativeRadiusError, meanCompletedFrameMs, p95CompletedFrameMs;
            public long playbackPayloadBytes;
            public int movieFrames, movieFramesPerSecond;
            public bool adjustedExperimentRequested;
            public double adjustedExperimentSeconds;
            public ProfileEvidence[] profiles;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_MINIDISK_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("Evolved mini-disk integration validation").AddComponent<MiniDiskQaCapture>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception) return;
            passed = false; checks.Add("FAIL exception: " + message);
            if (!string.IsNullOrEmpty(output)) WriteReceipt();
            Application.Quit(1);
        }
        private void Check(bool condition, string label)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) { passed = false; Debug.LogError("DTET_MINIDISK_FAILED " + label); }
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_MINIDISK_QA_PATH"));
            Directory.CreateDirectory(output);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(.5f);
            exhibit = FindFirstObjectByType<InteractiveBinaryExhibit>();
            Check(exhibit != null, "interactive exhibit exists");
            if (exhibit == null) { WriteReceipt(); Application.Quit(1); yield break; }
            exhibit.EmulatedInputEnabled = false;
            exhibit.Launch(ExhibitQualityProfile.Laptop);
            var watch = Stopwatch.StartNew();
            while ((exhibit.MiniDisks == null || !exhibit.MiniDisks.Ready) && watch.Elapsed.TotalSeconds < 30)
                yield return null;
            Check(exhibit.MiniDisks != null && exhibit.MiniDisks.Ready, "default numerical replay admits the prepared mini-disk cache");
            if (exhibit.MiniDisks == null || !exhibit.MiniDisks.Ready)
            { WriteReceipt(); Application.Quit(1); yield break; }
            exhibit.Session.Pause(); exhibit.Wrist.SetRaised(false, true);
            // Let the existing interface finish its first font/atlas updates before captures.
            yield return new WaitForEndOfFrame();
            exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
            exhibit.SetWristGas(true); exhibit.SetWristSamples(false); exhibit.RenderFrame();
            Check(exhibit.MiniDisks.RendererVisible && exhibit.MiniDisks.IsCovered && !exhibit.GeneratedFluidActive,
                "evolved mini-disk gas is the covered visible default without an interactive solve");
            playbackBytes = exhibit.MiniDisks.PlaybackPayloadBytes;
            Check(playbackBytes > 0, "playback reports allocated cache/texture payload");
            ValidateRecordedField();
            ValidateSharedScience();
            ValidateMaterialAndTiming();
            ValidateOptionalMaterialFallback();
            var diskCache = exhibit.MiniDisks.Cache;
            double gasStart = diskCache.StartSeconds, gasEnd = diskCache.EndSeconds;
            if (gasStart > 0)
            {
                exhibit.Session.Seek(0); exhibit.SetWristGas(true); exhibit.RenderFrame();
                Check(exhibit.Snapshot.ElapsedSeconds == 0 && !exhibit.MiniDisks.IsCovered && !exhibit.MiniDisks.RendererVisible,
                    "source history before late gas initialization remains seekable and shows no invented gas");
                Capture("minidisk-before-coverage.png");
                exhibit.ShowAccretionView(); exhibit.Session.Pause(); exhibit.RenderFrame();
                Check(exhibit.Snapshot.ElapsedSeconds == gasStart && exhibit.MiniDisks.IsCovered && exhibit.MiniDisks.RendererVisible,
                    "accretion view seeks the actual initialized gas epoch without resetting the scientific source clock");
            }
            exhibit.Session.Seek(gasStart); exhibit.SetViewPreset(0); exhibit.Wrist.SetRaised(false, true);
            exhibit.RenderFrame(); yield return new WaitForEndOfFrame();
            CaptureInterface("minidisk-default-interface.png");
            exhibit.Wrist.SetRaised(true, true); exhibit.Wrist.SetFluidPanelOpen(false);
            exhibit.RenderFrame(); yield return new WaitForEndOfFrame();
            CaptureInterface("minidisk-raised-wrist.png");
            if (gasStart > 0)
            {
                exhibit.Session.Seek(0); exhibit.RenderFrame(); yield return new WaitForEndOfFrame();
                Check(exhibit.CaptureWristState().Status.Contains("Gas covers"), "wrist explains the unavailable earlier gas coverage");
                CaptureInterface("minidisk-before-coverage-wrist.png");
            }
            exhibit.Wrist.SetRaised(false, true);

            double time = gasStart + (gasEnd - gasStart) * .31;
            exhibit.Session.Seek(time); exhibit.Session.Pause(); exhibit.SetViewPreset(0); exhibit.RenderFrame();
            var original = Pixels(exhibit.SceneTarget);
            exhibit.SetWristGas(false); exhibit.RenderFrame();
            Check(Difference(original, Pixels(exhibit.SceneTarget)) > .0001, "default evolved gas contributes actual radiance");
            exhibit.SetWristGas(true);
            foreach (double fraction in new[] { .91, .04, .66, 1.0, .0 })
            { exhibit.Session.Seek(gasStart + fraction * (gasEnd - gasStart)); exhibit.RenderFrame(); }
            exhibit.Session.Seek(time); exhibit.RenderFrame();
            Check(Difference(original, Pixels(exhibit.SceneTarget)) < 1e-6,
                "forward/backward cache seeks restore the same image at the same epoch");
            double frozen = exhibit.Snapshot.ElapsedSeconds;
            Vector3 previousPosition = exhibit.View.transform.position;
            exhibit.MoveCamera(new Vector3(3, 2, 0)); exhibit.RenderFrame();
            Check(exhibit.View.transform.position != previousPosition && exhibit.Snapshot.ElapsedSeconds == frozen,
                "viewpoint changes while the recorded fluid and scientific clock remain paused");
            foreach (int view in new[] { 1, 2, 3 })
            { exhibit.SetViewPreset(view); Capture("minidisk-view-" + view + ".png"); }
            exhibit.SetViewPreset(0);
            foreach (double fraction in new[] { 0.0, .5, .9, 1.0 })
            {
                exhibit.Session.Seek(gasStart + fraction * (gasEnd - gasStart));
                Capture("minidisk-epoch-" + fraction.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }

            ValidateProductionOpticalControls();
            yield return CaptureEmissionComparison();
            yield return CaptureMovie();
            // Cancellation must not later publish the abandoned experiment over the replay.
            exhibit.SelectWristScenario(false); yield return null;
            Check(exhibit.IsPreparingDisks, "adjustable scenario starts its own matching disk preparation");
            exhibit.SelectWristScenario(true); yield return null;
            Check(!exhibit.IsPreparingDisks && exhibit.MiniDisks.Ready
                && exhibit.MiniDisks.Cache.SourceModelId == NumericalEventHistory.ModelId,
                "return to replay cancels the abandoned solve and restores the correct cache identity");
            exhibit.Session.Pause();
            experimentEnabled = Environment.GetEnvironmentVariable("DTET_MINIDISK_EXPERIMENT_QA") == "1";
            if (experimentEnabled) yield return ValidateAdjustedCache();
            exhibit.Session.Seek(time); exhibit.SetViewPreset(0);
            for (int i = 0; i < 20; i++)
            {
                watch.Restart(); exhibit.RenderFrame(); Pixels(exhibit.SceneTarget); watch.Stop();
                renderMs.Add(watch.Elapsed.TotalMilliseconds); yield return null;
            }
            var releasedCache = exhibit.MiniDisks.Cache;
            var releasedMaterial = exhibit.MiniDisks.MaterialCache;
            exhibit.ReturnToLauncher(); yield return null;
            Check(!exhibit.Ready, "return to launcher releases the active scene");
            bool disposed = false;
            try { releasedCache.GetFrameTime(0); } catch (ObjectDisposedException) { disposed = true; }
            Check(disposed, "return to launcher closes the streamed cache rather than retaining its file handle");
            bool materialDisposed = false;
            if (releasedMaterial != null)
                try { releasedMaterial.GetFrameTime(0); } catch (ObjectDisposedException) { materialDisposed = true; }
            Check(materialDisposed, "return to launcher closes the transported-material stream");
            yield return ValidateRenderProfiles();
            WriteReceipt(); Application.Quit(passed ? 0 : 1);
        }

        private IEnumerator ValidateRenderProfiles()
        {
            string referenceFingerprint = null, referenceSource = null, referenceMaterial = null;
            int referenceGrid = 0, referenceFrames = 0;
            double selectedTime = 0;
            foreach (var profile in new[] { ExhibitQualityProfile.Laptop, ExhibitQualityProfile.Balanced, ExhibitQualityProfile.High })
            {
                exhibit.Launch(profile); exhibit.Session.Pause();
                Check(exhibit.MiniDisks != null && exhibit.MiniDisks.Ready && !exhibit.IsPreparingDisks,
                    profile + " loads the prepared replay without generating new physics");
                if (exhibit.MiniDisks == null || !exhibit.MiniDisks.Ready) { exhibit.ReturnToLauncher(); continue; }
                var cache = exhibit.MiniDisks.Cache;
                if (referenceFingerprint == null) selectedTime = cache.StartSeconds + .35 * (cache.EndSeconds - cache.StartSeconds);
                exhibit.Session.Seek(selectedTime); exhibit.SetViewPreset(0);
                exhibit.SetWristGas(true); exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
                exhibit.Wrist.SetRaised(false, true); exhibit.RenderFrame();
                cache.TryGetFrames(selectedTime, out int lower, out _, out _);
                int count = cache.GridSize * cache.GridSize;
                var density = new float[count]; var vx = new float[count]; var vz = new float[count];
                cache.ReadFrame(lower, density, vx, vz);
                ulong fingerprint = 14695981039346656037UL;
                unchecked
                {
                    for (int i = 0; i < count; i++)
                    {
                        fingerprint = (fingerprint ^ (uint)BitConverter.SingleToInt32Bits(density[i])) * 1099511628211UL;
                        fingerprint = (fingerprint ^ (uint)BitConverter.SingleToInt32Bits(vx[i])) * 1099511628211UL;
                        fingerprint = (fingerprint ^ (uint)BitConverter.SingleToInt32Bits(vz[i])) * 1099511628211UL;
                    }
                }
                string signature = fingerprint.ToString("x16");
                string materialSignature = "unavailable";
                if (exhibit.MiniDisks.MaterialDetailAvailable)
                {
                    var concentrations = new float[count]; exhibit.MiniDisks.MaterialCache.ReadFrame(lower, concentrations);
                    ulong materialHash = 14695981039346656037UL;
                    unchecked { foreach (float value in concentrations) materialHash = (materialHash ^ (uint)BitConverter.SingleToInt32Bits(value)) * 1099511628211UL; }
                    materialSignature = materialHash.ToString("x16");
                }
                if (referenceMaterial == null) referenceMaterial = materialSignature;
                Check(materialSignature != "unavailable" && materialSignature == referenceMaterial,
                    profile + " preserves the same transported material at the same source epoch");
                if (referenceFingerprint == null)
                {
                    referenceFingerprint = signature; referenceSource = cache.SourceSha256;
                    referenceGrid = cache.GridSize; referenceFrames = cache.FrameCount;
                }
                Check(signature == referenceFingerprint && cache.SourceSha256 == referenceSource
                    && cache.GridSize == referenceGrid && cache.FrameCount == referenceFrames
                    && exhibit.Snapshot.ElapsedSeconds == selectedTime && exhibit.MiniDisks.RendererVisible,
                    profile + " preserves the decoded physical field, grid, source identity and selected event epoch");
                double milliseconds = 0;
                for (int frame = 0; frame < 3; frame++)
                {
                    var watch = Stopwatch.StartNew(); exhibit.RenderFrame(); Pixels(exhibit.SceneTarget); watch.Stop();
                    milliseconds += watch.Elapsed.TotalMilliseconds; yield return null;
                }
                profileEvidence.Add(new ProfileEvidence { requested = profile.ToString(), effective = exhibit.Profile.EffectiveProfile.ToString(),
                    fieldFingerprint = signature, materialFingerprint = materialSignature, gridSize = cache.GridSize, frameCount = cache.FrameCount,
                    rayBudget = exhibit.MiniDisks.RayBudget, width = exhibit.SceneTarget.width, height = exhibit.SceneTarget.height,
                    elapsedSeconds = exhibit.Snapshot.ElapsedSeconds, meanCompletedReadbackMs = milliseconds / 3 });
                CaptureInterface("minidisk-profile-" + profile + ".png");
                exhibit.ReturnToLauncher(); yield return null;
            }
            if (profileEvidence.Count == 3)
                Check(profileEvidence[0].rayBudget < profileEvidence[1].rayBudget && profileEvidence[1].rayBudget < profileEvidence[2].rayBudget,
                    "the tested device admits distinct increasing curved-ray budgets for Laptop, Balanced and High");
        }

        private void ValidateMaterialAndTiming()
        {
            var player = exhibit.MiniDisks;
            Check(player.MaterialDetailAvailable, "matching transported-material history is loaded for the reference fluid");
            if (player.MaterialCache != null)
            {
                int count = player.Cache.GridSize * player.Cache.GridSize;
                var a = new float[count]; var b = new float[count];
                player.MaterialCache.ReadFrame(0, a);
                player.MaterialCache.ReadFrame(player.MaterialCache.FrameCount / 2, b);
                double variance = 0, change = 0; bool bounded = true;
                for (int i = 0; i < count; i++)
                {
                    bounded &= Finite(a[i]) && Finite(b[i]) && Math.Abs(a[i]) <= 1 && Math.Abs(b[i]) <= 1;
                    variance += a[i] * a[i]; change += Math.Abs(a[i] - b[i]);
                }
                Check(bounded && variance / count > 1e-5 && change / count > 1e-4,
                    "transported material is finite, bounded, structured and changes over the recorded fluid history");
                player.MaterialCache.ReadFrame(0, b);
                bool exact = true; for (int i = 0; i < count; i++) exact &= a[i] == b[i];
                Check(exact, "transported material rereads are immutable after seeking another epoch");
            }
            double start = player.Cache.StartSeconds;
            exhibit.Session.Seek(start); exhibit.SetViewPreset(0); exhibit.SetWristGas(true);
            player.MaterialDetailStrength = 1; exhibit.RenderFrame(); var detailed = Pixels(exhibit.SceneTarget);
            Capture("material-detail-on.png");
            var source = exhibit.Snapshot;
            player.MaterialDetailStrength = 0; exhibit.RenderFrame(); var smooth = Pixels(exhibit.SceneTarget);
            Capture("material-detail-off.png");
            Check(Difference(detailed, smooth) > 1e-5, "transported material changes actual emitted gas structure");
            Check(SameBody(source.BodyA, exhibit.Snapshot.BodyA) && source.Signal.DimensionlessRealH22 == exhibit.Snapshot.Signal.DimensionlessRealH22,
                "material appearance does not alter scientific bodies or radiation");
            player.MaterialDetailStrength = 1; exhibit.RenderFrame();
            Check(Difference(detailed, Pixels(exhibit.SceneTarget)) < 1e-6, "restoring material detail reproduces the paused image exactly");

            double merger = exhibit.Session.Model.MergerTimeSeconds;
            double tg = exhibit.Session.Model.GravitationalTimeSeconds;
            exhibit.Session.Seek(merger - 100 * tg); exhibit.SetMergerSlowMotion(true);
            double frozen = exhibit.Session.PlaybackTimeSeconds;
            exhibit.AdvancePlayback(1);
            Check(exhibit.Session.PlaybackTimeSeconds == frozen, "automatic merger timing respects pause");
            exhibit.Session.SetPlaybackRate(.02); exhibit.Session.Play(); exhibit.AdvancePlayback(1); exhibit.Session.Pause();
            double expected = MergerPlaybackTiming.Advance(frozen, 1, .02, merger, tg, exhibit.Session.Model.DurationSeconds);
            Check(Math.Abs(exhibit.Snapshot.ElapsedSeconds - expected) < 1e-12
                && exhibit.EffectivePlaybackRate < .02
                && exhibit.Snapshot.Signal.DimensionlessRealH22 == exhibit.Session.Model.Evaluate(expected).Signal.DimensionlessRealH22,
                "automatic slowdown advances the shared source clock and keeps the waveform at the same epoch");
            exhibit.SetWristRate(.05);
            Check(!exhibit.MergerSlowMotion, "manual speed selection disables automatic merger timing");
            double before = exhibit.Session.PlaybackTimeSeconds;
            exhibit.Session.Play(); exhibit.AdvancePlayback(.1); exhibit.Session.Pause();
            Check(Math.Abs(exhibit.Session.PlaybackTimeSeconds - before - .005) < 1e-12, "manual rate remains a constant source-time rate");
            exhibit.Session.SetPlaybackRate(.02); exhibit.SetMergerSlowMotion(true); exhibit.Session.Seek(start);
        }

        private void ValidateOptionalMaterialFallback()
        {
            string directory = Path.Combine(output, "material-fallback-fixture");
            Directory.CreateDirectory(directory);
            string source = Path.Combine(Application.streamingAssetsPath, "DTET", "MiniDisks", "sxs0305.mdf");
            string fluid = Path.Combine(directory, "fixture.mdf"), detail = MiniDiskMaterialAdvection.SidecarPath(fluid);
            var root = new GameObject("Optional material fallback control"); root.SetActive(false);
            var player = root.AddComponent<CachedMiniDiskPlayer>();
            try
            {
                File.Copy(source, fluid);
                File.WriteAllBytes(detail, new byte[] { 1, 2, 3 });
                player.Initialize(exhibit.View, exhibit.Profile.RaySteps); player.BindScenario(exhibit.Session.Model);
                bool headerFallback = player.Load(fluid, exhibit.Session.Generation);
                Check(headerFallback && player.Ready && !player.MaterialDetailAvailable,
                    "invalid material header falls back to intact fluid playback");
                player.Release();
                File.Copy(MiniDiskMaterialAdvection.SidecarPath(source), detail, true);
                long payload;
                using (var input = new BinaryReader(File.OpenRead(detail)))
                {
                    input.BaseStream.Position = 108; int modelLength = input.ReadInt32();
                    input.BaseStream.Position += modelLength + 8; payload = input.ReadInt64();
                }
                using (var changed = new FileStream(detail, FileMode.Open, FileAccess.Write))
                { changed.Position = payload; changed.WriteByte(0); changed.WriteByte(0); }
                player.Initialize(exhibit.View, exhibit.Profile.RaySteps); player.BindScenario(exhibit.Session.Model);
                bool opened = player.Load(fluid, exhibit.Session.Generation);
                player.Present(exhibit.Snapshot, true); player.MaterialDetailStrength = 1;
                Check(opened && player.Ready && player.IsCovered && !player.MaterialDetailAvailable
                    && player.Material.GetFloat("_MaterialDetailStrength") == 0,
                    "corrupt material payload preserves fluid and cannot re-enable stale detail");
            }
            finally
            {
                player.Release(); Destroy(root); File.Delete(detail); File.Delete(fluid); Directory.Delete(directory);
            }
        }

        private IEnumerator CaptureEmissionComparison()
        {
            var material = exhibit.MiniDisks.Material;
            float previousOpacity = material.GetFloat("_GeneratedOpacity"), previousExposure = material.GetFloat("_GeneratedExposure");
            Vector3 previousPosition = exhibit.View.transform.position; Quaternion previousRotation = exhibit.View.transform.rotation;
            double start = exhibit.MiniDisks.Cache.StartSeconds, end = exhibit.MiniDisks.Cache.EndSeconds;
            double merger = exhibit.Session.Model.MergerTimeSeconds, tg = exhibit.Snapshot.GravitationalTimeSeconds;
            double[] epochs = { start, merger - 250 * tg, merger, merger + 20 * tg, end };
            string[] names = { "seed", "pre-250tg", "common-horizon", "post-20tg", "end" };
            try
            {
                exhibit.Session.Pause(); exhibit.Session.Seek(start); exhibit.SetViewPreset(0);
                exhibit.SetWristGas(true); exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
                Vector3 fixedPosition = exhibit.View.transform.position; Quaternion fixedRotation = exhibit.View.transform.rotation;
                for (int epoch = 0; epoch < epochs.Length; epoch++)
                {
                    exhibit.Session.Seek(Math.Max(start, Math.Min(end, epochs[epoch])));
                    exhibit.View.transform.SetPositionAndRotation(fixedPosition, fixedRotation);
                    for (int choice = 0; choice < 2; choice++)
                    {
                        material.SetFloat("_GeneratedOpacity", choice == 0 ? 1.4f : 8);
                        material.SetFloat("_GeneratedExposure", choice == 0 ? 2.5f : 1.6f);
                        CaptureToneMappedScene("emission-" + (choice == 0 ? "baseline-" : "candidate-") + names[epoch] + ".png");
                        yield return null;
                    }
                }
                exhibit.Session.Seek(merger - 250 * tg); exhibit.SetViewPreset(2);
                for (int choice = 0; choice < 2; choice++)
                {
                    material.SetFloat("_GeneratedOpacity", choice == 0 ? 1.4f : 8);
                    material.SetFloat("_GeneratedExposure", choice == 0 ? 2.5f : 1.6f);
                    CaptureToneMappedScene("emission-" + (choice == 0 ? "baseline-" : "candidate-") + "edge-pre-250tg.png");
                    yield return null;
                }
                File.WriteAllText(Path.Combine(output, "emission-comparison.txt"),
                    "Paired real production renders, 1280x720. Baseline opacity/exposure=1.4/2.5; candidate=8/1.6.\n"
                    + "Identical evolved fields, epochs and fixed oblique camera; extra pair is edge-on at merger-250tg.\n"
                    + "Same display transfer as the movie. These are assumed gray display closures, not physical optical-depth/luminosity calibrations.\n");
            }
            finally
            {
                material.SetFloat("_GeneratedOpacity", previousOpacity); material.SetFloat("_GeneratedExposure", previousExposure);
                exhibit.View.transform.SetPositionAndRotation(previousPosition, previousRotation);
            }
        }

        private void CaptureToneMappedScene(string name)
        {
            const int width = 1280, height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); var hdr = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var png = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            var display = new Color32[width * height]; var previous = RenderTexture.active;
            try
            {
                exhibit.RenderFrame();
                RenderPipeline.SubmitRenderRequest(exhibit.View, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; hdr.ReadPixels(new Rect(0, 0, width, height), 0, 0); hdr.Apply();
                var pixels = hdr.GetPixelData<Color>(0);
                for (int i = 0; i < display.Length; i++)
                {
                    var p = pixels[i];
                    display[i] = new Color(Mathf.LinearToGammaSpace(1 - Mathf.Exp(-Mathf.Max(0, p.r))),
                        Mathf.LinearToGammaSpace(1 - Mathf.Exp(-Mathf.Max(0, p.g))),
                        Mathf.LinearToGammaSpace(1 - Mathf.Exp(-Mathf.Max(0, p.b))), 1);
                }
                png.SetPixels32(display); png.Apply();
                File.WriteAllBytes(Path.Combine(output, name), png.EncodeToPNG()); images.Add(name);
            }
            finally { RenderTexture.active = previous; Destroy(hdr); Destroy(png); target.Release(); Destroy(target); }
        }

        private IEnumerator ValidateAdjustedCache()
        {
            exhibit.ApplyWristExperiment(40, 25, 35);
            Check(exhibit.IsPreparingDisks && !exhibit.Session.IsPlaying,
                "40/25-solar-mass adjusted disk calculation starts with playback paused");
            var watch = Stopwatch.StartNew();
            while (exhibit.IsPreparingDisks && watch.Elapsed.TotalSeconds < 600)
            { exhibit.Present(); yield return null; }
            experimentSeconds = watch.Elapsed.TotalSeconds;
            Check(!exhibit.IsPreparingDisks && exhibit.MiniDisks.Ready,
                "adjusted physical parameters successfully produce and load their own evolved disk cache");
            if (exhibit.MiniDisks.Ready && !exhibit.IsPreparingDisks)
            {
                var cache = exhibit.MiniDisks.Cache;
                Check(exhibit.MiniDisks.MaterialDetailAvailable
                    && exhibit.MiniDisks.MaterialCache.FrameCount == cache.FrameCount,
                    "adjusted preparation also generates and admits matching transported material");
                Check(cache.Mass1Solar == 40 && cache.Mass2Solar == 25 && cache.InitialSeparationRg == 35
                    && cache.ScenarioMode == InteractiveScenarioMode.AdjustableExperiment,
                    "adjusted cache identity records the requested masses and initial separation");
                Check(exhibit.MiniDisks.Generation == exhibit.Snapshot.Generation && !exhibit.Session.IsPlaying
                    && exhibit.Snapshot.ElapsedSeconds == cache.StartSeconds && exhibit.MiniDisks.IsCovered,
                    "completed adjusted cache has the active generation and pauses at its actual coverage start");
                exhibit.SetViewPreset(0); exhibit.SetWristGas(true); exhibit.RenderFrame();
                var gas = Pixels(exhibit.SceneTarget);
                exhibit.SetWristGas(false); exhibit.RenderFrame();
                Check(Difference(gas, Pixels(exhibit.SceneTarget)) > .0001,
                    "successfully generated adjusted cache supplies visible gas rather than an empty completion state");
                exhibit.SetWristGas(true); exhibit.Wrist.SetRaised(false, true);
                yield return new WaitForEndOfFrame(); CaptureInterface("minidisk-adjusted-interface.png");
            }
            exhibit.SelectWristScenario(true); exhibit.Session.Pause(); exhibit.RenderFrame();
            Check(!exhibit.IsPreparingDisks && exhibit.MiniDisks.Ready
                && exhibit.MiniDisks.Cache.SourceModelId == NumericalEventHistory.ModelId,
                "the completed adjusted calculation can return to the original numerical replay cache");
        }

        private IEnumerator CaptureMovie()
        {
            string movie = Path.Combine(output, "movie"); Directory.CreateDirectory(movie);
            const int width = 1280, height = 720, count = 240;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create();
            var hdr = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var png = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            var display = new Color32[width * height];
            bool wasEnabled = exhibit.View.enabled;
            var previousActive = RenderTexture.active;
            try
            {
                double start = exhibit.MiniDisks.Cache.StartSeconds, end = exhibit.MiniDisks.Cache.EndSeconds;
                double merger = exhibit.Session.Model.MergerTimeSeconds, tg = exhibit.Session.Model.GravitationalTimeSeconds;
                double displayDuration = MergerPlaybackTiming.DisplayDuration(start, end, .02, merger, tg);
                exhibit.Session.Pause(); exhibit.Session.Seek(start);
                exhibit.SetWristGas(true); exhibit.SetWristSamples(false);
                exhibit.SetWristOptics(InteractiveOpticalStyle.Enhanced, .35f, 1.3f);
                exhibit.SetViewPreset(0); exhibit.Wrist.SetRaised(false, true);
                // The gas-initialization oblique camera is fixed across the available gas interval.
                Vector3 position = exhibit.View.transform.position; Quaternion rotation = exhibit.View.transform.rotation;
                exhibit.View.enabled = false;
                using (var times = new StreamWriter(Path.Combine(movie, "frames.csv"), false, System.Text.Encoding.UTF8))
                {
                    times.WriteLine("frame,video_seconds,elapsed_source_seconds,source_coordinate_seconds,phase");
                    for (int frame = 0; frame < count; frame++)
                    {
                        double seconds = frame == count - 1 ? end : MergerPlaybackTiming.Advance(start,
                            displayDuration * frame / (count - 1.0), .02, merger, tg, end);
                        exhibit.Session.Seek(seconds); exhibit.View.transform.SetPositionAndRotation(position, rotation);
                        exhibit.RenderFrame();
                        RenderPipeline.SubmitRenderRequest(exhibit.View, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; hdr.ReadPixels(new Rect(0, 0, width, height), 0, 0); hdr.Apply();
                        var pixels = hdr.GetPixelData<Color>(0);
                        for (int i = 0; i < display.Length; i++)
                        {
                            var p = pixels[i];
                            // Same 1-exp(-radiance) display transfer as ExhibitDisplay, then sRGB file encoding.
                            display[i] = new Color(Mathf.LinearToGammaSpace(1 - Mathf.Exp(-Mathf.Max(0, p.r))),
                                Mathf.LinearToGammaSpace(1 - Mathf.Exp(-Mathf.Max(0, p.g))),
                                Mathf.LinearToGammaSpace(1 - Mathf.Exp(-Mathf.Max(0, p.b))), 1);
                        }
                        png.SetPixels32(display); png.Apply();
                        File.WriteAllBytes(Path.Combine(movie, "frame-" + frame.ToString("0000") + ".png"), png.EncodeToPNG());
                        var s = exhibit.Snapshot;
                        times.WriteLine(frame + "," + (frame / 24.0).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                            + "," + seconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                            + "," + s.SourceTimeSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + s.Phase);
                        movieFrames++;
                        if (frame % 24 == 0) Debug.Log("DTET_MINIDISK_MOVIE " + frame + "/" + count);
                        yield return null;
                    }
                }
                Check(movieFrames == count && exhibit.Snapshot.ElapsedSeconds == end,
                    "240 actual default-scene frames cover the recorded gas interval at a fixed oblique viewpoint");
                File.WriteAllText(Path.Combine(movie, "README.txt"),
                    "240 actual scene renders, 1280x720, intended encoding 24 fps (10 seconds).\n"
                    + "The initialized gas interval uses the same automatic merger slowdown as playback, compressed to ten seconds; frames.csv records exact scientific source epochs.\n"
                    + "Earlier SXS source history is retained but has no gas evolution supplied by this finite-inventory cache.\n"
                    + "Laptop ray budget, production curved shader, default evolved cache; output resolution increased only for capture.\n"
                    + "Same display transfer as ExhibitDisplay. Scene only; no wrist, menus or external decorative overlays.\n"
                    + "Recorded reduced Newtonian gas and illustrative binary optics, not a measured gas movie or native FPS benchmark.\n");
            }
            finally
            {
                exhibit.View.enabled = wasEnabled; RenderTexture.active = previousActive;
                Destroy(hdr); Destroy(png); target.Release(); Destroy(target);
            }
        }

        private void ValidateRecordedField()
        {
            var cache = exhibit.MiniDisks.Cache;
            Check(cache.FrameCount >= 3 && cache.GridSize >= 64 && cache.EndSeconds > cache.StartSeconds,
                "cache has spatially resolved recorded epochs");
            Check(!cache.TryGetFrames(cache.StartSeconds - exhibit.Snapshot.GravitationalTimeSeconds, out _, out _, out _)
                && !cache.TryGetFrames(cache.EndSeconds + exhibit.Snapshot.GravitationalTimeSeconds, out _, out _, out _),
                "cache rejects epochs outside its actual recorded coverage");
            int count = checked(cache.GridSize * cache.GridSize);
            var initial = new float[count]; var later = new float[count];
            var vx = new float[count]; var vz = new float[count];
            cache.ReadFrame(0, initial, vx, vz);
            bool finite = true; double mass = 0, speedMass = 0;
            for (int i = 0; i < count; i++)
            {
                finite &= Finite(initial[i]) && initial[i] >= 0 && Finite(vx[i]) && Finite(vz[i]);
                mass += initial[i]; speedMass += initial[i] * Math.Sqrt(vx[i] * vx[i] + vz[i] * vz[i]);
            }
            Check(finite && mass > 0 && speedMass / mass > .02,
                "recorded initial fluid has finite positive density and nonzero mass-weighted motion");
            cache.ReadFrame(cache.FrameCount / 3, later, vx, vz);
            double changed = 0; for (int i = 0; i < count; i++) changed += Math.Abs(initial[i] - later[i]);
            densityChange = changed / Math.Max(mass, 1e-30);
            Check(densityChange > .01, "recorded density changes rather than replaying a static disk texture");
            cache.ReadFrame(0, later, vx, vz);
            bool identical = true; for (int i = 0; i < count; i++) identical &= initial[i] == later[i];
            Check(identical, "rereading the initial fluid frame is immutable after later-frame access");
        }

        private void ValidateSharedScience()
        {
            var data = Resources.Load<TextAsset>("Physics/SXS0305");
            Check(data != null, "independent numerical source asset available");
            if (data == null) return;
            InteractiveBinaryScenario reference;
            using (var stream = new MemoryStream(data.bytes, false)) reference = new InteractiveBinaryScenario(NumericalEventHistory.Load(stream));
            Resources.UnloadAsset(data);
            foreach (double fraction in new[] { 0.0, .37, .82, 1.0 })
            {
                double seconds = fraction * reference.DurationSeconds;
                exhibit.Session.Seek(seconds); exhibit.RenderFrame();
                var actual = exhibit.Snapshot; var expected = reference.Evaluate(seconds);
                bool same = actual.Mode == expected.Mode && actual.Phase == expected.Phase
                    && actual.TotalMassSolar == expected.TotalMassSolar
                    && actual.Mass1Solar == expected.Mass1Solar && actual.Mass2Solar == expected.Mass2Solar
                    && actual.SourceTimeSeconds == expected.SourceTimeSeconds
                    && SameBody(actual.BodyA, expected.BodyA) && SameBody(actual.BodyB, expected.BodyB)
                    && SameBody(actual.Remnant, expected.Remnant)
                    && actual.Signal.DimensionlessRealH22 == expected.Signal.DimensionlessRealH22
                    && actual.Signal.DimensionlessImaginaryH22 == expected.Signal.DimensionlessImaginaryH22;
                Check(same, "gas uses the unmodified scientific body/time/radiation history at fraction " + fraction);
                exhibit.SetWristGas(false); var hidden = exhibit.Snapshot;
                exhibit.SetWristGas(true); var restored = exhibit.Snapshot;
                Check(hidden.ElapsedSeconds == restored.ElapsedSeconds
                    && hidden.TotalMassSolar == restored.TotalMassSolar
                    && hidden.Signal.DimensionlessRealH22 == restored.Signal.DimensionlessRealH22,
                    "gas visibility does not change binary mass, clock or radiation");
            }
        }

        private static bool SameBody(InteractiveBinaryBody? actual, InteractiveBinaryBody? expected)
        {
            if (actual.HasValue != expected.HasValue) return false;
            if (!actual.HasValue) return true;
            return actual.Value.MassSolar == expected.Value.MassSolar
                && actual.Value.PositionRg.X == expected.Value.PositionRg.X
                && actual.Value.PositionRg.Y == expected.Value.PositionRg.Y
                && actual.Value.PositionRg.Z == expected.Value.PositionRg.Z;
        }

        private void ValidateProductionOpticalControls()
        {
            var shader = Shader.Find("DTET/CurvedDiskVolume");
            Check(shader != null && shader.isSupported, "production curved gas shader is supported");
            if (shader == null || !shader.isSupported) return;
            var root = new GameObject("Isolated mini-disk production-shader fixtures");
            int previousMask = exhibit.View.cullingMask;
            exhibit.View.cullingMask &= ~(1 << 30);
            var material = new Material(shader);
            // Use the production pass's clip-space triangle, not a world-space volume box.
            var mesh = new Mesh { name = "Controlled production optical screen" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 3, 0), new Vector3(3, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
            var screen = new GameObject("Controlled gas fields", typeof(MeshFilter), typeof(MeshRenderer));
            screen.layer = 30; screen.transform.SetParent(root.transform);
            screen.GetComponent<MeshFilter>().sharedMesh = mesh; screen.GetComponent<Renderer>().sharedMaterial = material;
            var cameraObject = new GameObject("Optical fixture camera"); cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.nearClipPlane = .01f; camera.farClipPlane = 200; camera.fieldOfView = 46; camera.aspect = 1;
            camera.allowHDR = true; camera.allowMSAA = false;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            var target = new RenderTexture(513, 513, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); var field = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            try
            {
                material.SetVector("_DiskDomain", new Vector4(18, .3f, 128, 512));
                material.SetVector("_GeneratedGrid", new Vector4(18, .3f, 0, 1));
                material.SetTexture("_GeneratedFrameA", field); material.SetTexture("_GeneratedFrameB", field);
                material.SetFloat("_GeneratedOpacity", .16f);
                material.SetFloat("_GeneratedExposure", 2.5f); material.SetFloat("_MergerActive", 0);
                material.SetVector("_HorizonA", Vector4.zero); material.SetVector("_HorizonB", Vector4.zero);
                material.SetVector("_HorizonR", Vector4.zero);
                material.SetVector("_DiskLensA", Vector4.zero); material.SetVector("_DiskLensB", Vector4.zero);
                material.SetVector("_DiskLensR", Vector4.zero); material.SetVector("_DiskWeights", new Vector4(1, 0, 0, 1));
                Check(material.HasProperty("_DopplerStrength"), "production shader exposes the Doppler control used in rendering");
                camera.transform.position = new Vector3(0, 6, -24); camera.transform.LookAt(Vector3.zero);
                FillField(field, (x, z) => Math.Abs(x) < 3 && Math.Abs(z) < 3 ? new Color(1, 0, -.2f, 0) : Color.clear);
                material.SetFloat("_DopplerStrength", 0); var offApproach = Render(camera, target);
                material.SetFloat("_DopplerStrength", 1); var approach = Render(camera, target);
                FillField(field, (x, z) => Math.Abs(x) < 3 && Math.Abs(z) < 3 ? new Color(1, 0, .2f, 0) : Color.clear);
                material.SetFloat("_DopplerStrength", 0); var offRecede = Render(camera, target);
                material.SetFloat("_DopplerStrength", 1); var recede = Render(camera, target);
                Check(FloatDifference(offApproach, offRecede) < 1e-6,
                    "opposite equal speeds have the same rendering when Doppler is disabled");
                dopplerRatio = Luminance(approach) / Math.Max(Luminance(recede), 1e-30);
                Check(Luminance(recede) > 1e-6 && dopplerRatio > 1.1,
                    "gas moving toward the camera is brighter than equal-speed receding gas");
                SaveFloatImage(approach, 513, "fixture-doppler-approaching.png");
                SaveFloatImage(recede, 513, "fixture-doppler-receding.png");

                material.SetFloat("_DopplerStrength", 0);
                camera.transform.position = new Vector3(0, .15f, -24); camera.transform.LookAt(Vector3.zero);
                material.SetVector("_DiskLensA", new Vector4(0, 0, 0, 1));
                material.SetVector("_HorizonA", new Vector4(0, 0, 0, .5f));
                FillField(field, (x, z) => Math.Abs(x) < 8 && z > 4 && z < 9 ? new Color(1, 0, 0, 0) : Color.clear);
                material.SetVector("_DiskWeights", new Vector4(1, 0, 0, 1)); var straight = Render(camera, target);
                material.SetVector("_DiskWeights", new Vector4(1, 0, 1, 1)); var curved = Render(camera, target);
                double straightOffPlane = OffPlaneLuminance(straight, 513), curvedOffPlane = OffPlaneLuminance(curved, 513);
                offPlaneRatio = curvedOffPlane / Math.Max(straightOffPlane, 1e-30);
                Check(curvedOffPlane > straightOffPlane * 1.2 + 1e-5,
                    "bent rays reveal the controlled far-side emitter away from the direct thin plane");
                SaveFloatImage(straight, 513, "fixture-far-side-straight.png");
                SaveFloatImage(curved, 513, "fixture-far-side-curved.png");
                FillField(field, (x, z) => Color.clear); var empty = Render(camera, target);
                Check(Luminance(empty) < 1e-8, "curvature produces no decorative gas light when the emitter field is empty");

                // Inside the integration box: include the finite-observer refractive index in the analytic impact parameter.
                camera.transform.position = new Vector3(0, 0, -16); camera.transform.LookAt(Vector3.zero);
                camera.fieldOfView = 60; camera.backgroundColor = Color.white;
                material.SetVector("_DiskDomain", new Vector4(.1f, .3f, 128, 512));
                var shadow = Render(camera, target); double darkArea = 0;
                foreach (var p in shadow) if (.2126 * p.r + .7152 * p.g + .0722 * p.b < .25) darkArea++;
                double radiusPixels = Math.Sqrt(darkArea / Math.PI), distance = 16;
                double n = Math.Pow(1 + .5 / distance, 3) / (1 - .5 / distance);
                double theta = Math.Asin(3 * Math.Sqrt(3) / (n * distance));
                double expectedRadius = .5 * 513 * Math.Tan(theta) / Math.Tan(Math.PI / 6);
                shadowRelativeError = Math.Abs(radiusPixels / expectedRadius - 1);
                double radiusTolerance = 2 / expectedRadius + .005;
                Check(darkArea > 100 && shadowRelativeError < radiusTolerance,
                    "single-hole isotropic shadow matches the analytic critical impact radius within two pixels plus 0.5 percent");
                SaveFloatImage(shadow, 513, "fixture-single-hole-reference.png");
                ValidateHomogeneousSlabs(camera, target, material, field);
            }
            finally
            {
                // Destroy is deferred; remove the full-screen optical fixture before the next real-scene render.
                root.SetActive(false); exhibit.View.cullingMask = previousMask;
                Destroy(field); Destroy(mesh); Destroy(material); target.Release(); Destroy(target); Destroy(root);
            }
        }

        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

        private void ValidateHomogeneousSlabs(Camera camera, RenderTexture target, Material material, Texture2D field)
        {
            camera.transform.position = new Vector3(0, 10, 0); camera.transform.LookAt(Vector3.zero, Vector3.forward);
            camera.backgroundColor = Color.clear;
            material.SetVector("_DiskLensA", Vector4.zero); material.SetVector("_HorizonA", Vector4.zero);
            material.SetVector("_DiskWeights", new Vector4(1, 0, 0, 0));
            material.SetVector("_DiskDomain", new Vector4(18, .3f, 128, 512));
            material.SetVector("_DiskSampling", new Vector4(36f / 128, 128, 513, 513));
            material.SetFloat("_GeneratedExposure", 1); material.SetFloat("_MaterialDetailStrength", 0);
            foreach (int verticalCase in new[] { 0, 1, 2 })
            foreach (float density in new[] { .02f, .2f, 1f }) foreach (float opacity in new[] { 1f, 8f })
            {
                float height = verticalCase == 0 ? .06f : .3f;
                material.SetVector("_GeneratedGrid", new Vector4(18, height, 0, 1));
                material.SetVector("_DiskDomain", new Vector4(18, height, 128, 512));
                material.SetVector("_DiskLensA", verticalCase == 2 ? new Vector4(0, 0, 0, 1) : Vector4.zero);
                material.SetVector("_DiskVertical", new Vector4(.04f, .25f, 0, 0));
                camera.transform.position = new Vector3(4, 10, 0); camera.transform.LookAt(new Vector3(4, 0, 0), Vector3.forward);
                FillField(field, (x, z) => new Color(density, 0, 0, 0));
                material.SetFloat("_GeneratedOpacity", opacity);
                var image = Render(camera, target); Color actual = image[(target.height / 2) * target.width + target.width / 2];
                double contrast = density / (density + .3);
                float heat = (float)(.35 + .55 * Math.Pow(contrast, .25));
                Color tint = Color.Lerp(new Color(1, .075f, .005f), new Color(1, .42f, .07f), Mathf.Clamp01((heat - .25f) / .4f));
                tint = Color.Lerp(tint, new Color(1, .84f, .52f), Mathf.Clamp01((heat - .65f) / .45f));
                tint = Color.Lerp(tint, new Color(.78f, .89f, 1), Mathf.Clamp01((heat - 1.1f) / .5f));
                float intensity = (float)((1 - Math.Exp(-opacity * density)) * (.08 + 1.4 * contrast * contrast) * .25);
                Color expected = tint * intensity;
                double error = Math.Max(Math.Abs(actual.r - expected.r), Math.Max(Math.Abs(actual.g - expected.g), Math.Abs(actual.b - expected.b)));
                Check(error <= 1e-5 + .005 * Math.Max(expected.r, Math.Max(expected.g, expected.b)),
                    "Gaussian column transfer oracle: vertical case " + verticalCase + " density " + density + " opacity " + opacity);
            }
        }
        private static void FillField(Texture2D texture, Func<float, float, Color> sample)
        {
            var pixels = new Color[texture.width * texture.height];
            for (int z = 0; z < texture.height; z++) for (int x = 0; x < texture.width; x++)
                pixels[z * texture.width + x] = sample(-18 + 36 * (x + .5f) / texture.width, -18 + 36 * (z + .5f) / texture.height);
            texture.SetPixels(pixels); texture.Apply(false, false);
        }
        private static Color[] Render(Camera camera, RenderTexture target)
        {
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            try { image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); return image.GetPixels(); }
            finally { Destroy(image); RenderTexture.active = old; }
        }
        private static double Luminance(Color[] pixels)
        {
            double sum = 0; foreach (var p in pixels) sum += .2126 * p.r + .7152 * p.g + .0722 * p.b;
            return sum / pixels.Length;
        }
        private static double OffPlaneLuminance(Color[] pixels, int size)
        {
            double sum = 0; int count = 0;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                if (Math.Abs(y - size / 2) > 16 && Math.Abs(x - size / 2) < 130)
                { var p = pixels[y * size + x]; sum += .2126 * p.r + .7152 * p.g + .0722 * p.b; count++; }
            return sum / count;
        }
        private static double FloatDifference(Color[] a, Color[] b)
        {
            double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return sum / (a.Length * 3);
        }
        private void SaveFloatImage(Color[] pixels, int size, string name)
        {
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            try { image.SetPixels(pixels); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); images.Add(name); }
            finally { Destroy(image); }
        }
        private void Capture(string name)
        {
            exhibit.RenderFrame(); var old = RenderTexture.active; RenderTexture.active = exhibit.SceneTarget;
            var image = new Texture2D(exhibit.SceneTarget.width, exhibit.SceneTarget.height, TextureFormat.RGB24, false);
            try { image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); images.Add(name); }
            finally { Destroy(image); RenderTexture.active = old; }
        }
        private void CaptureInterface(string name)
        {
            exhibit.RenderFrame(); Canvas.ForceUpdateCanvases();
            DevelopmentFrameCapture.WritePng(exhibit.CaptureCamera, Path.Combine(output, name));
            images.Add(name);
        }
        private static Color32[] Pixels(RenderTexture target)
        {
            var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            try { image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); return image.GetPixels32(); }
            finally { Destroy(image); RenderTexture.active = old; }
        }
        private static double Difference(Color32[] a, Color32[] b)
        {
            double sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return sum / (a.Length * 3 * 255.0);
        }
        private void WriteReceipt()
        {
            if (written) return; written = true;
            double total = 0; foreach (double ms in renderMs) total += ms;
            renderMs.Sort();
            var receipt = new Receipt { success = passed, checks = checks.ToArray(), images = images.ToArray(),
                unityVersion = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
                relativeDensityChange = densityChange, approachingToRecedingRadiance = dopplerRatio,
                curvedToStraightOffPlaneRadiance = offPlaneRatio, singleHoleShadowRelativeRadiusError = shadowRelativeError,
                playbackPayloadBytes = playbackBytes,
                movieFrames = movieFrames, movieFramesPerSecond = 24,
                adjustedExperimentRequested = experimentEnabled, adjustedExperimentSeconds = experimentSeconds,
                profiles = profileEvidence.ToArray(),
                meanCompletedFrameMs = renderMs.Count == 0 ? 0 : total / renderMs.Count,
                p95CompletedFrameMs = renderMs.Count == 0 ? 0 : renderMs[(int)Math.Ceiling(.95 * renderMs.Count) - 1],
                scope = "Recorded finite 2D Newtonian isothermal mini-disks; real production-shader texture fixtures and integrated numerical event checks. Single-hole optical control and illustrative binary index blend; no GRMHD, full binary spacetime validation, native presentation FPS, laptop or headset certification." };
            File.WriteAllText(Path.Combine(output, "minidisk-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_MINIDISK_PASS" : "DTET_MINIDISK_FAILED");
        }
    }
}
#endif
