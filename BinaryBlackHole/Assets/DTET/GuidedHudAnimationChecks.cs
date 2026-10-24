#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using ApproachHUD;
using UnityEngine;

namespace DTET.VisualPrototype
{
    internal static class GuidedHudAnimationChecks
    {
        static Mesh releasedMesh;
        static Material releasedMaterial;
        public static void Run(Action<bool, string> check)
        {
            var go = new GameObject("HUD animation test"); go.SetActive(false);
            var graphic = go.AddComponent<HudSquareGraphic>();
            var animation = go.AddComponent<HudSquareController>();
            animation.automaticTick = animation.automaticProximity = animation.showOnEnable = animation.faceObserver = false;
            animation.openingDuration = animation.hideDuration = 1.2f;
            go.SetActive(true); go.GetComponent<MeshRenderer>().enabled = false;
            try
            {
                check(animation.State == HudSquareController.VisibilityState.Hidden, "HUD starts hidden with manual control");
                animation.Show(); animation.Tick(.3f); var quarter = graphic.GeneratedMesh.vertices;
                animation.Tick(.6f); animation.Hide(); animation.Tick(.6f);
                check(Mathf.Abs(animation.OpeningProgress - .25f) < 1e-5f && Equal(quarter, graphic.GeneratedMesh.vertices),
                    "closing reverses the opening geometry at the same progress");
                check(animation.Opacity == 1, "closing keeps opacity instead of replacing assembly with a fade");
                var before = graphic.GeneratedMesh.vertices; animation.Show(); animation.Tick(0);
                check(Equal(before, graphic.GeneratedMesh.vertices) && animation.OpeningCount == 1,
                    "interrupted closing resumes without a mesh jump or second opening count");
                animation.Tick(2); check(animation.State == HudSquareController.VisibilityState.Idle && animation.IdleSeconds > 1,
                    "large manual tick preserves leftover time in the idle loop");
                before = graphic.GeneratedMesh.vertices; animation.Tick(graphic.loopDuration);
                check(Equal(before, graphic.GeneratedMesh.vertices), "idle geometry repeats after one complete loop");
                before = graphic.GeneratedMesh.vertices; animation.Hide(); animation.Tick(0);
                check(Equal(before, graphic.GeneratedMesh.vertices), "idle to closing begins at the exact current pose");
                animation.Tick(animation.idleReturnDuration);
                before = graphic.GeneratedMesh.vertices; graphic.RenderSample(1, 0, 1, 0);
                check(SamePoints(before, graphic.GeneratedMesh.vertices), "idle settles to the opening endpoint before reversal");
                animation.Tick(2);
                check(animation.State == HudSquareController.VisibilityState.Hidden && graphic.GeneratedMesh.vertexCount == 0,
                    "completed reversal leaves no visible geometry");
                animation.Show(); animation.Tick(.1f); check(animation.OpeningCount == 2, "fresh gaze after hiding starts a new opening");
                releasedMesh = graphic.GeneratedMesh; releasedMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
            }
            finally { UnityEngine.Object.Destroy(go); }
        }
        public static void VerifyReleased(Action<bool, string> check)
        { check(releasedMesh == null && releasedMaterial == null, "HUD destruction releases its owned mesh and material"); }
        static bool Equal(Vector3[] a, Vector3[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if ((a[i] - b[i]).sqrMagnitude > 1e-9f) return false; return true; }
        static bool SamePoints(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return false;
            var matched = new bool[b.Length];
            foreach (var p in a)
            {
                bool found = false;
                for (int j = 0; j < b.Length; j++) if (!matched[j] && (p - b[j]).sqrMagnitude < 1e-9f)
                    { matched[j] = true; found = true; break; }
                if (!found) return false;
            }
            return true;
        }
    }
}
#endif
