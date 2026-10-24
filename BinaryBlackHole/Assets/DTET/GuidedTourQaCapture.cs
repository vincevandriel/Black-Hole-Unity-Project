#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ApproachHUD;
using BlackHole.Staging.Simulation;
using UnityEngine;

namespace DTET.VisualPrototype
{
    internal sealed class GuidedTourQaCapture : MonoBehaviour
    {
        [Serializable] sealed class Receipt { public bool success; public string[] checks; public string unity, gpu; }
        readonly List<string> checks = new List<string>();
        bool passed = true;
        string output;
        InteractiveBinaryExhibit exhibit;
        GuidedObservationController guide;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DTET_GUIDED_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("Guided tour validation").AddComponent<GuidedTourQaCapture>();
        }
        void Check(bool ok, string label)
        { checks.Add((ok ? "PASS " : "FAIL ") + label); if (!ok) { passed = false; Debug.LogError("DTET_GUIDED_FAILED " + label); } }
        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnLog(string text, string stack, LogType type)
        { if (type == LogType.Exception) { passed = false; checks.Add("FAIL exception " + text); Finish(); } }

        IEnumerator Start()
        {
            output = Path.GetFullPath(Environment.GetEnvironmentVariable("DTET_GUIDED_QA_PATH")); Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.5f);
            exhibit = FindFirstObjectByType<InteractiveBinaryExhibit>();
            Check(exhibit != null, "interactive exhibit available");
            if (exhibit == null) { Finish(); yield break; }
            exhibit.EmulatedInputEnabled = false; exhibit.Launch(ExhibitQualityProfile.Laptop); exhibit.Session.Pause();
            guide = exhibit.Observations; guide.AutomaticTick = false;
            yield return new WaitForEndOfFrame();
            guide.Tick(0); exhibit.RenderFrame();
            Check(guide.Tour.Checkpoints.Count == 5 && guide.Tour.IsPausedAtCheckpoint && guide.Tour.CurrentCheckpoint.Id == "inspiral",
                "default tour begins paused at the first of five covered checkpoints");
            Check(exhibit.Snapshot.BodyA.HasValue && exhibit.Snapshot.BodyB.HasValue && exhibit.MiniDisks.IsCovered,
                "initial checkpoint has both black holes and evolved disk coverage");
            foreach (string id in new[] { "system", "black_hole_a", "black_hole_b", "accretion", "waves", "inspiral" })
                Check(guide.TryGetTarget(id, out _), "initial inspectable target " + id);
            Capture("01-initial-tags.png");
            exhibit.SetWristGas(false); guide.Tick(0);
            Check(!guide.TryGetTarget("accretion", out _), "hiding gas removes its inspection tag immediately");
            exhibit.SetWristGas(true); guide.Tick(0);
            Check(guide.TryGetTarget("accretion", out _), "restoring covered gas restores its tag");

            GuidedHudAnimationChecks.Run(Check);
            yield return null;
            GuidedHudAnimationChecks.VerifyReleased(Check);
            guide.FocusTarget("black_hole_a"); Step(2);
            Check(guide.FocusedId == "black_hole_a" && guide.Animation.State == HudSquareController.VisibilityState.Idle,
                "looking at a target opens the original HUD and enters its idle loop");
            double frozen = exhibit.Session.PlaybackTimeSeconds;
            guide.TryGetTarget("black_hole_a", out var anchor);
            Vector3 direction = (exhibit.View.transform.position - anchor).normalized;
            exhibit.View.transform.position = anchor + direction * 35; guide.FocusTarget("black_hole_a"); Step(.2f);
            Check(!guide.CanInspect && !guide.Interact(), "distant square cannot open its card");
            exhibit.View.transform.position = anchor + direction * 12; guide.FocusTarget("black_hole_a"); Step(.3f);
            Check(guide.CanInspect && guide.Interact() && guide.CardOpen, "approaching within 18 rg enables square interaction and text");
            Check(!guide.VoicePlaying, "opening a card does not autoplay narration");
            guide.ToggleVoice();
            yield return null;
            Check(guide.VoicePlaying, "opt-in replaceable narration starts in the player");
            Capture("02-near-target-explanation.png");
            float idle = guide.Animation.IdleSeconds;
            exhibit.MoveCamera(Vector3.up); Step(.4f);
            Check(exhibit.Session.PlaybackTimeSeconds == frozen && guide.Animation.IdleSeconds > idle,
                "camera and HUD remain active while the physical event clock is frozen");
            exhibit.View.transform.rotation = Quaternion.LookRotation(-direction);
            exhibit.View.transform.Rotate(0, 140, 0, Space.Self); Step(.3f);
            Check(!guide.CardOpen && !guide.VoicePlaying && guide.Animation.State == HudSquareController.VisibilityState.Closing,
                "looking away closes the explanation, stops voice and reverses the square");
            float closing = guide.Animation.OpeningProgress;
            guide.FocusTarget("black_hole_a"); Step(.2f);
            Check(guide.Animation.OpeningProgress >= closing && guide.Animation.State != HudSquareController.VisibilityState.Hidden,
                "returning gaze during closing resumes the current animation");
            Step(2); guide.Interact();
            guide.ToggleVoice(); guide.CloseCard();
            Check(!guide.VoicePlaying && !guide.CardOpen, "explicit close stops optional voice");

            for (int i = 1; i < guide.Tour.Checkpoints.Count; i++)
            {
                string expected = guide.Tour.Checkpoints[i].Id;
                guide.Continue(); exhibit.AdvancePlayback(100000); exhibit.Present(); guide.Tick(0);
                Check(!exhibit.Session.IsPlaying && guide.Tour.CurrentCheckpoint?.Id == expected
                    && exhibit.Snapshot.ElapsedSeconds == guide.Tour.Checkpoints[i].Seconds,
                    "large playback step stops exactly at " + expected);
                exhibit.SetViewPreset(i == 1 ? 1 : 0); Step(.2f);
                Capture("0" + (i + 2) + "-" + expected + ".png");
                Check(exhibit.MiniDisks.IsCovered && exhibit.CaptureWristState().SourceTimeSeconds == exhibit.Snapshot.ElapsedSeconds,
                    expected + " shares gas and wrist source time");
                if (i == 1) Check(exhibit.Snapshot.BodyA.HasValue && exhibit.Snapshot.BodyB.HasValue,
                    "dedicated inspiral stop retains both orbiting black holes");
                if (expected == "coalescence")
                {
                    exhibit.SetViewPreset(1); guide.FocusTarget("remnant"); Step(3);
                    guide.FocusTarget("coalescence"); Step(3);
                    Check(guide.FocusedId == "coalescence", "explicit topic selection wins over collinear target hysteresis");
                }
            }
            Check(exhibit.Snapshot.Remnant.HasValue && guide.TryGetTarget("remnant", out _), "final stop exposes the remnant target");
            guide.Continue(); exhibit.AdvancePlayback(100000); exhibit.Present(); guide.Tick(0);
            Check(guide.Tour.IsComplete && exhibit.Session.PlaybackTimeSeconds == exhibit.Session.Model.DurationSeconds,
                "continuing after the final stop reaches the timeline end");

            exhibit.ResetWristScenario(); guide.Tick(0);
            Check(guide.Tour.CurrentCheckpoint?.Id == "inspiral", "restart rearms the initial stop");
            guide.SetGuided(false); exhibit.SetWristPlaying(true); exhibit.AdvancePlayback(100000); exhibit.Present(); guide.Tick(0);
            Check(!guide.Tour.IsPausedAtCheckpoint && exhibit.Session.PlaybackTimeSeconds == exhibit.Session.Model.DurationSeconds,
                "free playback crosses all bookmarks when guided stops are disabled");
            exhibit.ResetWristScenario(); guide.Tick(0); guide.SetGuided(true); guide.Tick(0);
            guide.Continue(); exhibit.AdvancePlayback(100000); exhibit.Present(); guide.Tick(0);
            exhibit.Session.Seek(guide.Tour.Checkpoints[0].Seconds); guide.Tick(0);
            Check(guide.Tour.CurrentCheckpoint?.Id == "inspiral", "rewinding rearms earlier exploration stops");
            exhibit.Session.Seek(0); exhibit.Present(); guide.Tick(0);
            Check(!guide.TryGetTarget("accretion", out _), "uncovered source epochs have no accretion inspection tag");
            long generation = guide.Tour.Generation; exhibit.SelectWristScenario(true); guide.Tick(0);
            Check(guide.Tour.Generation > generation && guide.Tour.Generation == exhibit.Session.Generation && !guide.CardOpen,
                "scenario replacement clears stale cards and binds a fresh tour generation");

            exhibit.Session.Pause(); exhibit.SetViewPreset(0); guide.Tick(0);
            guide.TryGetTarget("system", out anchor); direction = (exhibit.View.transform.position - anchor).normalized;
            exhibit.View.transform.position = anchor + direction * 12; guide.FocusTarget("system"); Step(3.5f);
            Check(guide.FocusedId == "system" && guide.Interact(), "system topic opens after the previous square has closed");
            guide.ToggleVoice();
            Check(guide.VoicePlaying, "system topic has its own narration clip");
            exhibit.ReturnToLauncher();
            Check(!guide.VoicePlaying && !guide.CardOpen && exhibit.Observations == null, "leaving the exhibit releases tour UI and audio");
            yield return null;
            exhibit.Launch(ExhibitQualityProfile.Laptop, true); exhibit.Session.Pause();
            guide = exhibit.Observations; guide.AutomaticTick = false; guide.Tick(0);
            Check(guide.Tour.Checkpoints.Count == 5 && !guide.TryGetTarget("accretion", out _) && !guide.TryGetTarget("waves", out _),
                "compatibility tour omits disabled gas and optical targets");
            exhibit.ReturnToLauncher(); Finish();
        }

        void Step(float seconds)
        { for (int i = 0, n = Mathf.Max(1, Mathf.CeilToInt(seconds * 60)); i < n; i++) guide.Tick(seconds / n); }
        void Capture(string name)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); target.Create();
            var old = exhibit.CaptureCamera.targetTexture; var active = RenderTexture.active;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                guide.Tick(0); exhibit.CaptureCamera.targetTexture = target; exhibit.RenderFrame(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
            }
            finally { exhibit.CaptureCamera.targetTexture = old; RenderTexture.active = active; target.Release(); Destroy(target); Destroy(image); }
        }
        void Finish()
        {
            if (output != null) File.WriteAllText(Path.Combine(output, "guided-tour.json"), JsonUtility.ToJson(new Receipt
                { success = passed, checks = checks.ToArray(), unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName }, true));
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
