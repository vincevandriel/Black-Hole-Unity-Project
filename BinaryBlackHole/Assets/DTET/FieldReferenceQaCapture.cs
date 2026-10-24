#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Opt-in built-player checks for the reference field.
    internal sealed class FieldReferenceQaCapture : MonoBehaviour
    {
        private const double DurationTg = 1024;
        private const long CacheLimitBytes = 4L * 1024 * 1024;
        private readonly List<string> checks = new List<string>();
        private readonly List<ImageMeasurement> images = new List<ImageMeasurement>();
        private ReferenceFlowLab lab;
        private string output;
        private bool passed = true;
        private long peakCacheBytes;
        private int peakCacheFrames;
        private GpuTransferMeasurement gpuTransfer;

        [Serializable] private sealed class ImageMeasurement
        {
            public string file;
            public double centralMean, centralMinimum, centralMaximum;
            public int width, height;
        }

        [Serializable] private sealed class Receipt
        {
            public bool success;
            public string unityVersion, graphicsDevice, scope;
            public string[] checks;
            public ImageMeasurement[] images;
            public long peakCacheBytes;
            public int peakCacheFrames, finalGenerationCount, textureUploadCount;
            public double tracerTotalWeight;
            public GpuTransferMeasurement gpuTransfer;
        }

        [Serializable] private sealed class GpuTransferMeasurement
        {
            public string targetFormat;
            public double expectedLinearRadiance, measured64, measured256, error64, error256, stepDifference;
            public double extinctionPerRg, opacityScale, lengthRg, sourceFunction, exposure;
            public double thinOpacityScale, thinExpectedLinearRadiance, thinMeasured256, thinError256;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_FIELD_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("DT-ET field reference integration receipt").AddComponent<FieldReferenceQaCapture>();
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
            if (!condition) { passed = false; Debug.LogError("DTET_FIELD_FAILED " + label); }
        }

        private void ObserveCache()
        {
            peakCacheBytes = Math.Max(peakCacheBytes, lab.CacheBytes);
            peakCacheFrames = Math.Max(peakCacheFrames, lab.CacheFrameCount);
        }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_FIELD_QA_PATH"));
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            lab = FindFirstObjectByType<ReferenceFlowLab>();
            if (lab == null) { Check(false, "reference laboratory is installed by its launch option"); Application.Quit(1); yield break; }
            lab.EmulatedInputEnabled = false;
            Check(FindFirstObjectByType<VisualPrototypeBootstrap>() == null,
                "reference laboratory is isolated from the M1 and M2 exhibit bootstrap");
            Check(!lab.Playing && lab.View != null, "laboratory starts paused with an independent camera");
            bool cameraHud = false;
            foreach (var canvas in lab.GetComponentsInChildren<Canvas>(true))
                cameraHud |= canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == lab.View;
            Check(cameraHud, "laboratory controls are rendered through the reference camera");
            lab.SetOpticalMode(false);
            lab.SetPlaying(false);
            lab.SetRate(64);
            Slider timeSlider = FindSlider(1024), opacitySlider = FindSlider(4);
            lab.SetPlaying(true);
            timeSlider.value = 128;
            opacitySlider.value = .5f;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Check(lab.TimeTg == 128 && !lab.Playing,
                "timeline slider selects reference time 128 and pauses playback");
            Check(SliderFillMatchesTrack(timeSlider),
                "timeline fill width matches its normalized value within one layout pixel");
            Check(lab.OpacityScale == .5f,
                "opacity slider updates the transfer opacity multiplier");
            Check(SliderFillMatchesTrack(opacitySlider),
                "opacity fill width matches its normalized value within one layout pixel");
            CaptureScreen("field-lab-controls.png");
            Check(images[images.Count - 1].centralMean > .001,
                "the integrated reference camera presents a nonblack laboratory view");
            opacitySlider.value = 1;
            lab.Seek(256);
            yield return new WaitForEndOfFrame();
            Check(lab.TimeTg == 256 && !lab.Playing && lab.PlaybackRate == 64,
                "seek selects the exact reference time and pauses");
            ObserveCache();
            Check(lab.CacheFrameCount > 0 && lab.CacheFrameCount <= 2 && lab.CacheBytes > 0 && lab.CacheBytes <= CacheLimitBytes,
                "field history uses at most two frames within its bounded CPU cache");
            ulong fieldAt256 = lab.FieldFingerprint;
            lab.SetTracers(true);
            lab.SetTracerCount(256);
            yield return new WaitForEndOfFrame();
            double weightAt256 = lab.TracerTotalWeight;
            ulong tracersAt256 = lab.TracerFingerprint;
            Check(lab.TracersVisible && lab.TracerCount == 256 && weightAt256 > 0 && double.IsFinite(weightAt256),
                "optional weighted tracers are finite and independently selectable");
            lab.SetTracerCount(1024);
            yield return new WaitForEndOfFrame();
            Check(lab.TracerCount == 1024 && Math.Abs(lab.TracerTotalWeight - weightAt256) <= Math.Max(1e-10, weightAt256 * 1e-6),
                "increasing tracer count preserves total represented weight");
            lab.SetTracerCount(256);
            lab.Seek(768);
            yield return new WaitForEndOfFrame();
            Check(lab.FieldFingerprint != fieldAt256 && lab.TracerFingerprint != tracersAt256,
                "a different recorded time changes both field and tracer states");
            lab.Seek(256);
            yield return new WaitForEndOfFrame();
            Check(lab.FieldFingerprint == fieldAt256 && lab.TracerFingerprint == tracersAt256,
                "repeated seek reconstructs identical field content and tracer positions");

            int cachedGeneration = lab.CacheGenerationCount;
            int cachedUploads = lab.TextureUploadCount;
            for (int i = 0; i < 12; i++) lab.Seek(256);
            yield return new WaitForEndOfFrame();
            Check(lab.CacheGenerationCount == cachedGeneration,
                "repeated seeks to the same time reuse cached field frames");
            Check(lab.TextureUploadCount == cachedUploads,
                "repeated seeks to the same time avoid redundant texture uploads");
            Vector3 positionBefore = lab.View.transform.position;
            lab.MoveCamera(new Vector3(.1f, .15f, -.2f));
            yield return new WaitForSecondsRealtime(.12f);
            Check(lab.View.transform.position != positionBefore && lab.TimeTg == 256 && !lab.Playing
                && lab.FieldFingerprint == fieldAt256 && lab.TracerFingerprint == tracersAt256,
                "camera traversal while paused leaves reference time and field content unchanged");
            Check(lab.TextureUploadCount == cachedUploads,
                "paused camera movement does not upload new field textures");

            int beforeSeeks = lab.CacheGenerationCount;
            for (int i = 0; i < 96; i++)
            {
                lab.Seek(((i * 397) % 1023) + .25);
                ObserveCache();
                if (i % 8 == 7) yield return null;
            }
            Check(peakCacheFrames <= 2 && peakCacheBytes <= CacheLimitBytes,
                "ninety-six distant seeks retain the two-frame cache and memory ceiling");
            Check(lab.CacheGenerationCount >= beforeSeeks && lab.CacheGenerationCount <= beforeSeeks + 192,
                "distant seeks generate at most two required frames per request");
            lab.Seek(256);
            yield return new WaitForEndOfFrame();
            Check(lab.FieldFingerprint == fieldAt256 && lab.TracerFingerprint == tracersAt256,
                "evicted history reconstructs exactly after repeated cache turnover");

            lab.SetRate(64);
            lab.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.15f);
            lab.SetPlaying(false);
            double afterForward = lab.TimeTg;
            Check(afterForward > 256 && !lab.Playing, "built-player update advances reference time at a positive rate");
            lab.SetRate(-64);
            lab.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.15f);
            lab.SetPlaying(false);
            Check(lab.TimeTg < afterForward && lab.PlaybackRate == -64,
                "negative playback revisits earlier reference states");
            lab.Seek(.01);
            lab.SetRate(-256);
            lab.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.12f);
            Check(lab.TimeTg == 0 && !lab.Playing, "reverse playback clamps and pauses at the first frame");
            lab.Seek(DurationTg - .01);
            lab.SetRate(256);
            lab.SetPlaying(true);
            yield return new WaitForSecondsRealtime(.12f);
            Check(lab.TimeTg == DurationTg && !lab.Playing, "forward playback clamps and pauses at the final frame");

            lab.Seek(256);
            lab.SetRate(64);
            lab.SetTracers(false);
            Check(!lab.TracersVisible && lab.FieldFingerprint == fieldAt256,
                "hiding tracers preserves the continuous flow field");
            lab.SetViewPreset(0);
            CheckGpuConstantSlab();
            lab.SetOpacity(0);
            yield return new WaitForEndOfFrame();
            Color32[] emptyPixels = CaptureCamera("field-opacity-zero.png");
            Check(lab.OpacityScale == 0, "zero-opacity control reaches the transfer renderer");
            lab.SetOpacity(1);
            yield return new WaitForEndOfFrame();
            Color32[] emittingPixels = CaptureCamera("field-overview.png");
            Check(MeanDifference(emptyPixels, emittingPixels) > .0001,
                "continuous emission visibly changes the central image from the zero-opacity control");
            Check(images[images.Count - 1].centralMean > .001,
                "the volume reference renders measurable central radiance");
            lab.Seek(700);
            lab.Seek(256);
            yield return new WaitForEndOfFrame();
            Color32[] repeatedPixels = CaptureCamera("field-repeat-seek.png");
            Check(MeanDifference(emittingPixels, repeatedPixels) < .002,
                "repeated field seek reproduces the camera image within display quantization tolerance");
            string[] viewNames = { "field-above.png", "field-edge.png", "field-below.png", "field-inside-volume.png" };
            for (int i = 0; i < viewNames.Length; i++)
            {
                lab.SetViewPreset(i + 1);
                yield return new WaitForEndOfFrame();
                CaptureCamera(viewNames[i]);
                Check(images[images.Count - 1].centralMean > .0001,
                    "continuous field is visible from reference viewpoint " + (i + 1));
            }
            lab.SetViewPreset(0);
            lab.SetTracers(true);
            yield return new WaitForEndOfFrame();
            CaptureCamera("field-weighted-tracers.png");

            lab.SetTracers(false);
            lab.SetOpticalMode(true);
            Check(lab.OpticalMode, "isolated-hole observer reference is independently selectable");
            Check(SlidersInteractable(false), "steady observer reference disables time and volume-opacity sliders");
            yield return WaitForOptical("first observer reference");
            if (lab.OpticalReady)
            {
                yield return new WaitForEndOfFrame();
                CaptureScreen("field-optical-overview.png");
                Check(images[images.Count - 1].centralMaximum - images[images.Count - 1].centralMinimum > .01,
                    "observer reference produces a nonuniform rendered image");
            }
            int priorOpticalRevision = lab.OpticalRevision;
            lab.Seek(640);
            Check(!lab.OpticalReady && lab.OpticalRevision != priorOpticalRevision,
                "seek invalidates the previous observer image before a new time can be shown");
            lab.Seek(256);
            yield return WaitForOptical("observer reference after a replacement seek");
            if (lab.OpticalReady)
            {
                yield return new WaitForEndOfFrame();
                CaptureScreen("field-optical-repeat-seek.png");
            }
            Check(lab.TimeTg == 256 && !lab.Playing,
                "observer rendering preserves the paused reference epoch");
            lab.SetOpticalMode(false);
            yield return new WaitForEndOfFrame();
            Check(!lab.OpticalMode && lab.FieldFingerprint == fieldAt256,
                "leaving observer reference restores the same coordinate field state");
            Check(SlidersInteractable(true), "returning to coordinate flow restores interactive sliders");
            ObserveCache();
            var receipt = new Receipt {
                success = passed, unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceName, checks = checks.ToArray(), images = images.ToArray(),
                peakCacheBytes = peakCacheBytes, peakCacheFrames = peakCacheFrames,
                finalGenerationCount = lab.CacheGenerationCount, textureUploadCount = lab.TextureUploadCount,
                tracerTotalWeight = lab.TracerTotalWeight,
                gpuTransfer = gpuTransfer,
                scope = "Built-player reference field replay, cache bounds, weighted tracers, time and camera controls, GPU transfer against a constant-slab analytic fixture, volume image changes and isolated-hole observer image lifecycle. Not GRMHD, binary optics, physical calibration, VR or laptop performance certification. Cache bytes cover the reported field arrays, not total process or GPU memory."
            };
            File.WriteAllText(Path.Combine(output, "field-reference-integration.json"), JsonUtility.ToJson(receipt, true));
            Debug.Log(passed ? "DTET_FIELD_REFERENCE_INTEGRATION_PASS" : "DTET_FIELD_REFERENCE_INTEGRATION_FAILED");
            Application.Quit(passed ? 0 : 1);
        }

        private IEnumerator WaitForOptical(string label)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!lab.OpticalReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(lab.OpticalReady, label + " completes within thirty seconds");
        }

        private Slider FindSlider(float maximum)
        {
            foreach (var slider in lab.GetComponentsInChildren<Slider>(true))
                if (slider.minValue == 0 && slider.maxValue == maximum) return slider;
            throw new InvalidOperationException("Missing reference slider with maximum " + maximum);
        }

        private bool SlidersInteractable(bool expected)
        {
            Slider[] sliders = lab.GetComponentsInChildren<Slider>(true);
            if (sliders.Length != 2) return false;
            foreach (var slider in sliders) if (slider.interactable != expected) return false;
            return true;
        }

        private static bool SliderFillMatchesTrack(Slider slider)
        {
            Canvas.ForceUpdateCanvases();
            if (slider.fillRect == null || !(slider.fillRect.parent is RectTransform track)) return false;
            float expected = track.rect.width * slider.normalizedValue;
            return track.rect.width > 0 && Math.Abs(slider.fillRect.rect.width - expected) <= 1;
        }

        private void CheckGpuConstantSlab()
        {
            // Test the shader transfer loop with a uniform field and linear readback, without post-processing.
            Material material = lab.VolumeMaterial;
            Camera camera = lab.View;
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            Color previousBackground = camera.backgroundColor;
            float previousAspect = camera.aspect;
            bool previousHdr = camera.allowHDR, previousMsaa = camera.allowMSAA, previousOrthographic = camera.orthographic;
            float previousOpacity = lab.OpacityScale;
            float previousExposure = material.GetFloat("_Exposure"), previousSteps = material.GetFloat("_Steps");
            float previousExtinction = material.GetFloat("_Extinction"), previousSlab = material.GetFloat("_SlabTest");
            bool fixturesEnabled = material.IsKeywordEnabled("DTET_QA_FIXTURES");
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool previousPostProcessing = cameraData != null && cameraData.renderPostProcessing;
            Canvas[] canvases = lab.GetComponentsInChildren<Canvas>(true);
            bool[] canvasStates = HideCanvases(canvases);
            try
            {
                camera.transform.SetPositionAndRotation(new Vector3(0, 0, -30), Quaternion.identity);
                camera.backgroundColor = Color.black; camera.aspect = 1; camera.orthographic = false;
                camera.allowHDR = true; camera.allowMSAA = false;
                if (cameraData != null) cameraData.renderPostProcessing = false;
                material.EnableKeyword("DTET_QA_FIXTURES");
                material.SetFloat("_SlabTest", 1); material.SetFloat("_Exposure", 1); material.SetFloat("_Extinction", .3f);
                lab.SetOpacity(.1f);
                RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)
                    ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf;
                Check(SystemInfo.SupportsRenderTextureFormat(format), "GPU transfer fixture supports a linear floating-point render target");
                material.SetFloat("_Steps", 64);
                Color at64 = ReadLinearCentralPixel(camera, format);
                material.SetFloat("_Steps", 256);
                Color at256 = ReadLinearCentralPixel(camera, format);
                // Along the exact central ray the box path is 40 rg. Constant B=1 gives
                // I = 1-exp(-tau), followed by the declared display map 1-exp(-I*exposure).
                double expected = 1 - Math.Exp(-(1 - Math.Exp(-.3 * .1 * 40)));
                double error64 = ChannelError(at64, expected), error256 = ChannelError(at256, expected);
                double difference = Math.Max(Math.Abs(at64.r - at256.r), Math.Max(Math.Abs(at64.g - at256.g), Math.Abs(at64.b - at256.b)));
                gpuTransfer = new GpuTransferMeasurement {
                    targetFormat = format + " / explicit Linear read-write", expectedLinearRadiance = expected,
                    measured64 = at64.r, measured256 = at256.r, error64 = error64, error256 = error256, stepDifference = difference,
                    extinctionPerRg = .3, opacityScale = .1, lengthRg = 40, sourceFunction = 1, exposure = 1
                };
                Check(error64 < .003, "64-step GPU slab transfer matches the analytic linear-radiance fixture within 0.003");
                Check(error256 < .003, "256-step GPU slab transfer matches the analytic linear-radiance fixture within 0.003");
                Check(difference < .001, "GPU constant-slab transfer is stable when ray steps increase from 64 to 256");
                lab.SetOpacity(1e-6f);
                Color thin = ReadLinearCentralPixel(camera, format);
                double thinExpected = 1 - Math.Exp(-(1 - Math.Exp(-.3 * 1e-6 * 40)));
                double thinError = ChannelError(thin, thinExpected);
                gpuTransfer.thinOpacityScale = 1e-6; gpuTransfer.thinExpectedLinearRadiance = thinExpected;
                gpuTransfer.thinMeasured256 = thin.r; gpuTransfer.thinError256 = thinError;
                Check(thinError < 2.5e-7 && thin.r > 0,
                    "very thin GPU slab preserves nonzero transfer within 0.00000025 linear radiance");
            }
            finally
            {
                material.SetFloat("_SlabTest", previousSlab); material.SetFloat("_Exposure", previousExposure);
                if (!fixturesEnabled) material.DisableKeyword("DTET_QA_FIXTURES");
                material.SetFloat("_Steps", previousSteps); material.SetFloat("_Extinction", previousExtinction);
                lab.SetOpacity(previousOpacity);
                camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                camera.backgroundColor = previousBackground; camera.aspect = previousAspect;
                camera.allowHDR = previousHdr; camera.allowMSAA = previousMsaa; camera.orthographic = previousOrthographic;
                if (cameraData != null) cameraData.renderPostProcessing = previousPostProcessing;
                RestoreCanvases(canvases, canvasStates);
            }
        }

        private static double ChannelError(Color color, double expected)
        {
            if (!float.IsFinite(color.r) || !float.IsFinite(color.g) || !float.IsFinite(color.b)) return double.PositiveInfinity;
            return Math.Max(Math.Abs(color.r - expected), Math.Max(Math.Abs(color.g - expected), Math.Abs(color.b - expected)));
        }

        private static Color ReadLinearCentralPixel(Camera camera, RenderTextureFormat format)
        {
            // Odd dimensions put one pixel center exactly on the camera's central ray.
            const int side = 65;
            var target = RenderTexture.GetTemporary(side, side, 24, format, RenderTextureReadWrite.Linear);
            var previousTarget = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The GPU fixture camera does not support a render request.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture = new Texture2D(side, side, TextureFormat.RGBAFloat, false, true);
                texture.ReadPixels(new Rect(0, 0, side, side), 0, 0);
                texture.Apply();
                return texture.GetPixel(side / 2, side / 2);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (texture != null) Destroy(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private Color32[] CaptureCamera(string file, int width = 640, int height = 360, bool includeInterface = false)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            float previousAspect = lab.View.aspect;
            Canvas[] canvases = includeInterface ? Array.Empty<Canvas>() : lab.GetComponentsInChildren<Canvas>(true);
            bool[] canvasStates = HideCanvases(canvases);
            Texture2D texture = null;
            try
            {
                lab.View.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(lab.View, request))
                    throw new InvalidOperationException("The reference camera does not support a render request.");
                RenderPipeline.SubmitRenderRequest(lab.View, request);
                RenderTexture.active = target;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return SaveAndMeasure(texture, file);
            }
            finally
            {
                lab.View.aspect = previousAspect;
                RenderTexture.active = previousTarget;
                if (texture != null) Destroy(texture);
                RenderTexture.ReleaseTemporary(target);
                RestoreCanvases(canvases, canvasStates);
            }
        }

        private void CaptureScreen(string file)
        {
            // Include the HUD in the offscreen camera capture; this does not read the window backbuffer.
            CaptureCamera(file, 1600, 900, true);
        }

        private static bool[] HideCanvases(Canvas[] canvases)
        {
            var states = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) { states[i] = canvases[i].enabled; canvases[i].enabled = false; }
            Canvas.ForceUpdateCanvases();
            return states;
        }

        private static void RestoreCanvases(Canvas[] canvases, bool[] states)
        {
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = states[i];
            Canvas.ForceUpdateCanvases();
        }

        private Color32[] SaveAndMeasure(Texture2D texture, string file)
        {
            if (texture == null) throw new InvalidOperationException("Reference frame capture returned no texture.");
            string path = Path.Combine(output, file);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Color32[] all = texture.GetPixels32();
            int left = texture.width / 4, right = texture.width * 3 / 4;
            // The center half excludes the reference UI's left panel and top/bottom bands.
            int bottom = texture.height / 4, top = texture.height * 3 / 4;
            var central = new Color32[(right - left) * (top - bottom)];
            double sum = 0, minimum = 1, maximum = 0;
            int cursor = 0;
            for (int y = bottom; y < top; y++) for (int x = left; x < right; x++)
            {
                Color32 pixel = all[y * texture.width + x];
                central[cursor++] = pixel;
                double intensity = (.2126 * pixel.r + .7152 * pixel.g + .0722 * pixel.b) / 255;
                sum += intensity; minimum = Math.Min(minimum, intensity); maximum = Math.Max(maximum, intensity);
            }
            images.Add(new ImageMeasurement { file = file, width = texture.width, height = texture.height,
                centralMean = sum / central.Length, centralMinimum = minimum, centralMaximum = maximum });
            Check(new FileInfo(path).Length > 500, "capture " + file);
            return central;
        }

        private static double MeanDifference(Color32[] left, Color32[] right)
        {
            if (left.Length != right.Length) return double.PositiveInfinity;
            double sum = 0;
            for (int i = 0; i < left.Length; i++)
                sum += Math.Abs(left[i].r - right[i].r) + Math.Abs(left[i].g - right[i].g) + Math.Abs(left[i].b - right[i].b);
            return sum / (left.Length * 3.0 * 255);
        }
    }
}
#endif
