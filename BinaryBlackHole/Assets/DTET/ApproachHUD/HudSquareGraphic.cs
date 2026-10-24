using System.Collections.Generic;
using UnityEngine;

namespace ApproachHUD
{
    /// <summary>Original vector HUD. All geometry is in local XY; no textures or video decoder.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class HudSquareGraphic : MonoBehaviour
    {
        [Min(0.01f)] public float size = 1f;
        [Range(0.001f, 0.02f)] public float lineWidth = 0.0035f;
        public Color tint = Color.white;
        [Min(0.1f)] public float loopDuration = 4f;

        readonly List<Vector3> vertices = new List<Vector3>(4096);
        readonly List<Color> colors = new List<Color>(4096);
        readonly List<int> triangles = new List<int>(8192);
        Mesh ownedMesh;
        Material ownedMaterial;
        float alpha;

        public Mesh GeneratedMesh => ownedMesh;

        void OnEnable()
        {
            if (Application.isPlaying) RenderSample(0, 0, 0);
        }

        /// <param name="opening">0 = absent, 1 = fully open.</param>
        /// <param name="idleSeconds">Periodic idle time; zero matches the opening's end.</param>
        public void RenderSample(float opening, float idleSeconds, float opacity = 1f, float idleMotion = 1f)
        {
            if (ownedMesh == null)
            {
                ownedMesh = new Mesh { name = "Approach HUD (runtime)", hideFlags = HideFlags.DontSave };
                ownedMesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = ownedMesh;
            }
            var renderer = GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null)
            {
                var shader = Resources.Load<Shader>("ApproachHUD/HudVertexAlpha");
                if (shader == null) throw new System.InvalidOperationException("Approach HUD shader resource is missing.");
                if (ownedMaterial == null) ownedMaterial = new Material(shader)
                    { name = "Approach HUD (runtime)", hideFlags = HideFlags.DontSave };
                renderer.sharedMaterial = ownedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            vertices.Clear(); colors.Clear(); triangles.Clear();
            float p = Mathf.Clamp01(opening);
            alpha = Mathf.Clamp01(opacity) * Smooth(p / 0.16f);
            if (alpha > 0.0001f)
            {
                float phase = Mathf.Repeat(idleSeconds / Mathf.Max(0.1f, loopDuration), 1f);
                float motion = Mathf.Clamp01(idleMotion);
                float wave = (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f)) * motion;
                float settle = 1f - Mathf.Pow(1f - Smooth((p - 0.05f) / 0.7f), 3f);
                float r = Mathf.Lerp(0.67f, 0.43f, settle) + 0.014f * wave;
                float leg = 0.118f * Smooth((p - 0.12f) / 0.6f);

                // Four open corner brackets and inset registration crosses.
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 c = new Vector2(sx * r, sy * r);
                    Line(c, c + new Vector2(-sx * leg, 0), 1f);
                    Line(c, c + new Vector2(0, -sy * leg), 1f);
                    Vector2 cross = c - new Vector2(sx, sy) * 0.036f;
                    Cross(cross, 0.013f * Smooth((p - 0.38f) / 0.35f), 0.55f);
                }

                // A compact inner frame assembles after the corner brackets.
                float inner = Smooth((p - 0.25f) / 0.65f);
                float q = 0.235f;
                float l = q * 2f * inner;
                Line(new Vector2(-q, q), new Vector2(-q + l, q), 0.55f);
                Line(new Vector2(q, -q), new Vector2(q - l, -q), 0.55f);
                Line(new Vector2(-q, q), new Vector2(-q, q - 0.09f * inner), 0.55f);
                Line(new Vector2(q, -q), new Vector2(q, -q + 0.09f * inner), 0.55f);
                // Steady lower and upper side accents; their alpha breathes gently.
                Line(new Vector2(-q, -q), new Vector2(-q, -q + 0.09f * inner), 0.4f + 0.2f * wave);
                Line(new Vector2(q, q), new Vector2(q, q - 0.09f * inner), 0.4f + 0.2f * wave);
                for (int i = 0; i < 25; i++)
                {
                    float x = Mathf.Lerp(-0.218f, 0.218f, i / 24f);
                    float h = i % 4 == 0 ? 0.016f : 0.008f;
                    float light = 0.24f + 0.48f * Mathf.Lerp(
                        Mathf.Pow(0.5f + 0.5f * Mathf.Cos(i * 0.27f), 6),
                        Mathf.Pow(0.5f + 0.5f * Mathf.Cos(i * 0.27f - phase * Mathf.PI * 2), 6), motion);
                    Line(new Vector2(x, q + 0.013f), new Vector2(x, q + 0.013f + h * inner), light * inner, 0.62f);
                }
                // Small data bars, deliberately decorative rather than fake labels.
                for (int i = 0; i < 8; i++)
                {
                    float x = -q + 0.008f + i * 0.009f;
                    float h = 0.006f + 0.014f * (0.5f + 0.5f * Mathf.Lerp(
                        Mathf.Sin(i * 1.7f), Mathf.Sin(i * 1.7f + phase * Mathf.PI * 2), motion));
                    Line(new Vector2(x, -q + 0.01f), new Vector2(x, -q + 0.01f + h * inner), inner * 0.65f, 0.7f);
                }

                // Central reticle and a periodic expanding square. No opening replay in idle.
                float lockIn = Smooth((p - 0.05f) / 0.75f);
                Cross(Vector2.zero, 0.033f * lockIn, 0.95f);
                float idleAngle = phase * 180f;
                float angle = (1f - lockIn) * 90f + Mathf.Lerp(Mathf.Round(idleAngle / 90f) * 90f, idleAngle, motion);
                float radius = (0.032f + 0.014f * wave) * lockIn;
                Square(radius, angle, 0.72f * lockIn);
                // sin^2 gives a zero-slope fade at both ends of the loop.
                float pulse = Mathf.Sin(phase * Mathf.PI);
                Square(Mathf.Lerp(0.065f, 0.19f, phase) * lockIn, 0f,
                    pulse * pulse * 0.48f * lockIn * motion);
            }
            ownedMesh.Clear();
            ownedMesh.SetVertices(vertices);
            ownedMesh.SetColors(colors);
            ownedMesh.SetTriangles(triangles, 0);
            ownedMesh.bounds = new Bounds(Vector3.zero, Vector3.one * size * 2f);
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void Square(float radius, float degrees, float opacity)
        {
            float a = degrees * Mathf.Deg2Rad;
            Vector2 prev = Vector2.zero;
            for (int i = 0; i <= 4; i++)
            {
                float x = i % 4 == 0 || i % 4 == 3 ? -radius : radius;
                float y = i % 4 < 2 ? radius : -radius;
                Vector2 v = new Vector2(x * Mathf.Cos(a) - y * Mathf.Sin(a), x * Mathf.Sin(a) + y * Mathf.Cos(a));
                if (i > 0) Line(prev, v, opacity, 0.8f);
                prev = v;
            }
        }

        void Cross(Vector2 c, float radius, float opacity)
        {
            Line(c - Vector2.right * radius, c + Vector2.right * radius, opacity, 0.8f);
            Line(c - Vector2.up * radius, c + Vector2.up * radius, opacity, 0.8f);
        }

        void Line(Vector2 a, Vector2 b, float opacity, float widthScale = 1f)
        {
            if (opacity * alpha < 0.00001f || (b - a).sqrMagnitude < 0.00000001f) return;
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized;
            float core = lineWidth * widthScale * 0.5f;
            float feather = lineWidth * 0.45f;
            Strip(a, b, n, -core - feather, -core, 0, opacity);
            Strip(a, b, n, -core, core, opacity, opacity);
            Strip(a, b, n, core, core + feather, opacity, 0);
        }

        void Strip(Vector2 a, Vector2 b, Vector2 n, float d0, float d1, float o0, float o1)
        {
            int v = vertices.Count;
            vertices.Add((a + n * d0) * size); vertices.Add((b + n * d0) * size);
            vertices.Add((b + n * d1) * size); vertices.Add((a + n * d1) * size);
            Color c0 = tint; c0.a *= alpha * o0;
            Color c1 = tint; c1.a *= alpha * o1;
            colors.Add(c0); colors.Add(c0); colors.Add(c1); colors.Add(c1);
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
            triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 3);
        }

        void OnDestroy()
        {
            if (ownedMesh != null)
            { if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh); }
            if (ownedMaterial != null)
            { if (Application.isPlaying) Destroy(ownedMaterial); else DestroyImmediate(ownedMaterial); }
        }
    }
}
