#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BlackHole.Staging.Simulation;
using UnityEngine;

namespace DTET.VisualPrototype
{
    public sealed class EditableSceneQaCapture : MonoBehaviour
    {
        [Serializable] private sealed class Receipt { public bool success; public string[] checks; }
        private readonly List<string> checks = new List<string>();
        private bool passed = true;
        private string output;
        private InteractiveBinaryExhibit exhibit;
        private SimulationBodyBinding[] bodies;
        private GameObject[] identities;
        private int updates;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DTET_EDITABLE_QA_PATH"))) return;
            Application.runInBackground = true;
            new GameObject("Editable scene checks").AddComponent<EditableSceneQaCapture>();
        }
        private void Check(bool value, string label)
        { checks.Add((value ? "PASS " : "FAIL ") + label); passed &= value; Debug.Log(checks[checks.Count - 1]); }
        private void OnEnable() { Application.logMessageReceived += Log; }
        private void OnDisable() { Application.logMessageReceived -= Log; }
        private void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception) { Check(false, message); Finish(); } }
        private IEnumerator Start()
        {
            output = Environment.GetEnvironmentVariable("DTET_EDITABLE_QA_PATH"); Directory.CreateDirectory(output);
            yield return null;
            exhibit = FindAnyObjectByType<InteractiveBinaryExhibit>();
            Check(exhibit != null && exhibit.SceneObjects != null, "saved scene installs the bound presenter");
            if (exhibit == null || exhibit.SceneObjects == null) { Finish(); yield break; }
            var objects = exhibit.SceneObjects;
            objects.ValidateBindings();
            bodies = new[] { objects.PrimaryBody, objects.SecondaryBody, objects.Remnant };
            identities = new GameObject[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                identities[i] = bodies[i].gameObject;
                bodies[i].StateUpdated += CountUpdate;
                var attachment = bodies[i].gameObject.AddComponent<AudioSource>(); attachment.playOnAwake = false;
            }
            Camera camera = objects.InspectionCamera;
            var tutor = objects.TutorRoot.GetComponent<GuidedObservationController>();
            var wrist = objects.WristRoot.GetComponent<InteractiveWristDisplay>();
            exhibit.EmulatedInputEnabled = false;
            exhibit.Launch(ExhibitQualityProfile.Laptop); exhibit.Session.Pause();
            yield return null;
            Check(exhibit.View == objects.InspectionCamera && exhibit.View == camera, "existing inspection camera is reused");
            Check(exhibit.Observations == tutor && exhibit.Wrist == wrist, "saved tutor and wrist controllers are reused");
            Check(exhibit.MiniDisks.Ready && exhibit.MiniDisks.IsCovered && exhibit.MiniDisks.RendererVisible, "existing evolved gas and rendering remain active");
            Check(exhibit.Observations.Tour.Checkpoints.Count == 5, "guided tutor retains five covered checkpoints");
            for (int pass = 0; pass < 2; pass++)
            {
                for (int step = 0; step <= 12; step++)
                {
                    int index = pass == 0 ? step : 12 - step;
                    exhibit.Session.Seek(exhibit.Session.Model.DurationSeconds * index / 12); exhibit.Present();
                    VerifyState("replay " + pass + "/" + index);
                }
            }
            Check(updates >= 3 * 24, "scripts attached to persistent bodies receive simulation updates");
            exhibit.Session.Seek(exhibit.MiniDisks.Cache.StartSeconds); exhibit.Present();
            Physics.SyncTransforms();
            float radius = (float)bodies[0].State.Value.HorizonRadiusRg;
            Ray ray = new Ray(bodies[0].transform.position + Vector3.up * (radius * 3), Vector3.down);
            Check(bodies[0].InteractionCollider.Raycast(ray, out _, radius * 5), "saved body collider supports interaction at the simulated position");
            exhibit.Wrist.SetRaised(true, true); exhibit.Wrist.RefreshNow();
            Check(exhibit.Wrist.PresentedGeneration == exhibit.Session.Generation, "wrist remains bound to the same physics generation");
            exhibit.ReturnToLauncher(); yield return null;
            VerifyIdentities("launcher");
            Check(objects.TutorRoot != null && objects.WristRoot != null && objects.InspectionCamera != null, "returning to launcher preserves authored attachment roots");
            exhibit.Launch(ExhibitQualityProfile.Laptop, true); exhibit.Session.Pause(); yield return null;
            VerifyIdentities("compatibility relaunch");
            Check(exhibit.Observations == tutor && exhibit.Wrist == wrist && exhibit.Observations.Tour.Checkpoints.Count == 5,
                "saved tutor and wrist reinitialize after return");
            exhibit.SelectWristScenario(false); exhibit.ApplyWristExperiment(30, 25, 35); exhibit.Session.Pause();
            for (int step = 0; step <= 8; step++)
            {
                exhibit.Session.Seek(exhibit.Session.Model.DurationSeconds * step / 8); exhibit.Present();
                VerifyState("experiment " + step);
            }
            exhibit.ResetWristScenario(); VerifyState("experiment reset");
            exhibit.ReturnToLauncher();
            exhibit.Launch(ExhibitQualityProfile.Laptop); exhibit.Session.Pause(); yield return null;
            VerifyIdentities("same-frame return and relaunch");
            Check(exhibit.Observations == tutor && exhibit.Observations.TryGetTarget("black_hole_a", out _), "tutor targets restored after repeated launch");
            Check(FindObjectsByType<SimulationBodyBinding>(FindObjectsInactive.Include).Length == 3, "no duplicate body objects are generated");
            exhibit.ReturnToLauncher(); yield return null;
            VerifyIdentities("final cleanup");
            Finish();
        }
        private void CountUpdate(SimulationBodyBinding body) { updates++; }
        private void VerifyIdentities(string label)
        {
            bool stable = true;
            for (int i = 0; i < bodies.Length; i++) stable &= bodies[i] != null && bodies[i].gameObject == identities[i]
                && bodies[i].GetComponent<AudioSource>() != null && bodies[i].InteractionCollider != null;
            Check(stable, label + " preserves body identity, attached components and colliders");
        }
        private void VerifyState(string label)
        {
            var state = exhibit.Snapshot;
            var expected = new[] { state.BodyA, state.BodyB, state.Remnant };
            bool equal = exhibit.BodyATransform == bodies[0].transform && exhibit.BodyBTransform == bodies[1].transform && exhibit.RemnantTransform == bodies[2].transform;
            for (int i = 0; i < bodies.Length; i++)
            {
                equal &= bodies[i].HasBody == expected[i].HasValue && bodies[i].gameObject.activeSelf == expected[i].HasValue;
                if (!expected[i].HasValue) continue;
                var p = expected[i].Value.PositionRg;
                equal &= Vector3.Distance(bodies[i].transform.position, new Vector3((float)p.X, (float)p.Y, (float)p.Z)) < 1e-5f;
                equal &= Math.Abs(bodies[i].transform.localScale.x - 2 * expected[i].Value.HorizonRadiusRg) < 1e-5;
            }
            Check(equal, label + " binds saved bodies to authoritative positions, radii and existence");
        }
        private void Finish()
        {
            if (string.IsNullOrEmpty(output)) output = Environment.GetEnvironmentVariable("DTET_EDITABLE_QA_PATH");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "editable-scene.json"), JsonUtility.ToJson(new Receipt { success = passed, checks = checks.ToArray() }, true));
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
