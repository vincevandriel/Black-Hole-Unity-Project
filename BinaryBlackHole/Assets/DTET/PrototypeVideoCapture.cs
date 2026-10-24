#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace DTET.VisualPrototype
{
    // Opt-in development recording; stream completed frames directly to the encoder.
    internal sealed class PrototypeVideoCapture : MonoBehaviour
    {
        private const int Fps = 30, Seconds = 36;
        private VisualPrototypeBootstrap prototype;
        private Process encoder;
        private Texture2D pixels;
        private readonly StringBuilder diagnostics = new StringBuilder();
        private Transform eye;
        private Vector3 fromPosition, toPosition;
        private Quaternion fromRotation, toRotation;
        private float moveStart, moveDuration;
        private Outline highlight;
        private float highlightUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_VIDEO_PATH"))) return;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            new GameObject("Development guided video capture").AddComponent<PrototypeVideoCapture>();
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            prototype = FindFirstObjectByType<VisualPrototypeBootstrap>();
            if (prototype == null) { Fail("Prototype did not initialize"); yield break; }
            prototype.Observer.PersistInputSettings = false;
            prototype.Observer.ResetInputSensitivities();
            prototype.Observer.EmulatedInputEnabled = false;
            prototype.Observer.SetPreset(ExhibitCameraRig.ViewPreset.Overview);
            prototype.SetLearningVisible(false);
            prototype.PartnerHud.SetVisible(false);
            var entrance = GameObject.Find("Exhibit entrance instructions");
            if (entrance != null) entrance.SetActive(false); // Tour framing only, not a product change.
            prototype.SeekStoryboard(0.12f);
            prototype.SetHudPlaybackRate(1.7f);
            prototype.TogglePlayback();
            eye = prototype.Observer.transform;
            fromPosition = toPosition = eye.position;
            fromRotation = toRotation = eye.rotation;
            var output = Environment.GetEnvironmentVariable("DTET_VIDEO_PATH");
            var executable = Environment.GetEnvironmentVariable("DTET_FFMPEG_PATH");
            if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            { Fail("Set DTET_FFMPEG_PATH to an existing ffmpeg executable"); yield break; }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            encoder = new Process { StartInfo = new ProcessStartInfo {
                FileName = executable,
                Arguments = $"-hide_banner -y -f rawvideo -pixel_format rgb24 -video_size {Screen.width}x{Screen.height} -framerate {Fps} -i pipe:0 -an -c:v libx264 -preset fast -crf 19 -pix_fmt yuv420p -movflags +faststart \"{output}\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardError = true
            }};
            encoder.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (diagnostics) diagnostics.AppendLine(e.Data); };
            try { encoder.Start(); encoder.BeginErrorReadLine(); }
            catch (Exception e) { Fail(e.ToString()); yield break; }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Fps;
            for (var frame = 0; frame < Seconds * Fps; frame++)
            {
                var t = frame / (float)Fps;
                try { DirectTour(frame, t); ApplyMove(t); }
                catch (Exception e) { Fail(e.ToString()); yield break; }
                yield return new WaitForEndOfFrame();
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
                    // ReadPixels returns bottom-up rows. ffmpeg's final presentation applies vflip.
                    var bytes = pixels.GetRawTextureData();
                    encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                }
                catch (Exception e) { Fail(e.ToString()); yield break; }
                if (frame % 150 == 0) Debug.Log($"DTET_VIDEO_FRAME {frame}/{Seconds * Fps}");
            }
            encoder.StandardInput.Close();
            if (!encoder.WaitForExit(20000) || encoder.ExitCode != 0)
            { Fail("Encoder failed: " + diagnostics); yield break; }
            File.WriteAllText(output + ".encoder.log", diagnostics.ToString());
            Debug.Log($"DTET_VIDEO_PASS frames={Seconds * Fps} fps={Fps} duration={Seconds} width={Screen.width} height={Screen.height} path={output}");
            Application.Quit();
        }

        private void DirectTour(int frame, float t)
        {
            if (highlight != null && t >= highlightUntil) { Destroy(highlight); highlight = null; }
            switch (frame)
            {
                case 0: MoveTo(new Vector3(-6.4f, 3.2f, -6.1f), new Vector3(-3.25f, 0, 0), t, 2.8f); break;
                case 84: MoveTo(new Vector3(-4.6f, 1.5f, -5.4f), new Vector3(-3.25f, 0, 0), t, 1.5f); break;
                case 129: MoveTo(new Vector3(-3.25f, 1.87f, -8.8f), new Vector3(-3.25f, 0, 0), t, 0.9f); break;
                case 156: prototype.PartnerHud.SetVisible(true); prototype.PartnerHud.WristRig.SetRaised(true); break;
                case 180: Click("Partner wrist controls", "PAUSE button", t); break;
                case 210: Click("Partner wrist controls", "Halfway button", t); break;
                case 240: Click("Partner wrist controls", "REPLAY button", t); break;
                case 255: prototype.PartnerHud.ToggleOptions(); break;
                case 282:
                    prototype.Observer.SetLookSensitivity(1.5f);
                    prototype.Observer.SetScrollSensitivity(5f);
                    break;
                case 312:
                    prototype.PartnerHud.ToggleOptions();
                    prototype.PartnerHud.WristRig.SetRaised(false);
                    prototype.SetLearningVisible(true);
                    MoveTo(ExhibitCameraRig.LearningBoardPosition + new Vector3(0, 0, -4.6f), ExhibitCameraRig.LearningBoardPosition, t, 1.3f);
                    break;
                case 351: Click("Learning controls", "A  /  CLOSER button", t); break;
                case 381: Click("Learning controls", "B  /  WIDER button", t); break;
                case 411:
                    GameObject.Find("Learning controls").GetComponentsInChildren<Slider>(true)[0].value = 38;
                    Click("Learning controls", "APPLY START button", t);
                    break;
                case 441:
                    prototype.SetHudPlaybackRate(2);
                    prototype.SeekStoryboard(0.4f);
                    prototype.TogglePlayback();
                    break;
                case 471: Click("One-question formative check", "RISE button", t); break;
                case 525: Click("Learning controls", "GRAPH button", t); break;
                case 600: Click("Learning controls", "MODEL button", t); break;
                case 618: Click("Learning overlay", "DIM / NORMAL button", t); break;
                case 636: Click("Learning overlay", "DIM / NORMAL button", t); break;
                case 660:
                    prototype.SetLearningVisible(false);
                    prototype.SeekStoryboard(0.25f);
                    prototype.PartnerHud.SetVisible(false);
                    break;
                case 661:
                    // Ask the real exhibit command for its alignment, then approach smoothly.
                    var position = eye.position; var rotation = eye.rotation;
                    prototype.FocusLensingDemo();
                    prototype.Observer.Zoom(1f); // The actual visitor scroll command approaches the marker.
                    var destination = eye.position; var orientation = eye.rotation;
                    eye.SetPositionAndRotation(position, rotation);
                    MoveTo(destination, orientation, t, 1f);
                    prototype.StellarField.SetLensing(false);
                    prototype.StellarField.SetRayPaths(false);
                    break;
                case 735:
                    prototype.StellarField.SetLensing(true);
                    prototype.StellarField.SetRayPaths(true);
                    break;
                case 759:
                    prototype.StellarField.SetRayPaths(true);
                    MoveTo(eye.position + eye.right * 0.5f, eye.rotation, t, 1.3f);
                    break;
                case 810:
                    prototype.StellarField.SetRayPaths(false);
                    MoveTo(new Vector3(1.9f, 1.5f, -3.6f), new Vector3(1.9f, 1.5f, 0), t, 1.3f);
                    break;
                case 849: prototype.PartnerHud.SetVisible(true); break;
                case 885: MoveTo(new Vector3(3.1f, 1.8f, -3.7f), new Vector3(3.1f, 1.8f, 0), t, 1f); break;
                case 915:
                    prototype.PartnerHud.SetVisible(false);
                    MoveTo(new Vector3(-3.25f, 2.5f, -7.5f), new Vector3(-3.25f, 0, 0), t, 1.3f);
                    break;
                case 954:
                    prototype.SeekStoryboard(0.97f);
                    prototype.SetHudPlaybackRate(0.5f);
                    prototype.TogglePlayback();
                    MoveTo(new Vector3(-4.4f, 3.6f, -6.5f), new Vector3(-3.25f, 0, 0), t, 4.1f);
                    break;
            }
        }

        private void Click(string parentName, string childName, float t)
        {
            var parent = GameObject.Find(parentName);
            Transform child = null;
            if (parent != null) foreach (Transform candidate in parent.transform)
                if (candidate.name == childName) { child = candidate; break; }
            var button = child == null ? null : child.GetComponent<Button>();
            if (button == null) throw new InvalidOperationException("Missing tour command " + parentName + "/" + childName);
            if (highlight != null) Destroy(highlight);
            highlight = child.gameObject.AddComponent<Outline>();
            highlight.effectColor = new Color(0.3f, 1, 1, 1);
            highlight.effectDistance = new Vector2(3, 3);
            highlightUntil = t + 0.75f;
            button.onClick.Invoke();
            Debug.Log($"DTET_VIDEO_ACTION {t:0.00}s {childName}");
        }

        private void MoveTo(Vector3 destination, Vector3 lookAt, float t, float duration)
            => MoveTo(destination, Quaternion.LookRotation(lookAt - destination, Vector3.up), t, duration);
        private void MoveTo(Vector3 destination, Quaternion orientation, float t, float duration)
        {
            fromPosition = eye.position; fromRotation = eye.rotation;
            toPosition = destination; toRotation = orientation;
            moveStart = t; moveDuration = duration;
        }
        private void ApplyMove(float t)
        {
            var blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - moveStart) / Mathf.Max(0.01f, moveDuration)));
            eye.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, blend), Quaternion.Slerp(fromRotation, toRotation, blend));
        }
        private void Fail(string message)
        {
            Debug.LogError("DTET_VIDEO_FAILED " + message);
            if (encoder != null && !encoder.HasExited) encoder.StandardInput.Close();
            Application.Quit(1);
        }
        private void OnDestroy()
        {
            if (pixels != null) Destroy(pixels);
            encoder?.Dispose();
        }
    }
}
#endif
