using UnityEngine;

namespace DTET.VisualPrototype
{
    /// <summary>
    /// Procedural exhibit art. None of these shapes perform lensing or predict emission.
    /// Runtime generation keeps the prototype editable and avoids third-party asset provenance.
    /// </summary>
    public static class VisualAssetFactory
    {
        private static Mesh diskMesh;

        public static GameObject CreateBody(string name, Color accent, bool remnant = false)
        {
            var root = new GameObject(name);
            var inner = remnant ? new Color(1f, 0.78f, 0.43f) : new Color(1f, 0.62f, 0.31f);

            CreateDisk(root.transform, "Layered accretion illustration / inner", inner, accent, 0.42f, 0.42f, new Vector3(-24, -16, 11));
            CreateDisk(root.transform, "Layered accretion illustration / outer haze", accent, inner, 0.14f, -0.16f, new Vector3(-22, -13, 11), 1.18f);

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Central silhouette / not an event-horizon render";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.78f;
            core.GetComponent<Renderer>().sharedMaterial = CreateSolidMaterial("Dark core", new Color(0.006f, 0.009f, 0.019f));
            Object.Destroy(core.GetComponent<Collider>());

            var photonGuide = CreateGlowLine(root.transform, "Illustrative bright inner rim", inner, 0.043f, 128, true);
            for (var i = 0; i < photonGuide.positionCount; i++)
            {
                var t = i * Mathf.PI * 2f / photonGuide.positionCount;
                photonGuide.SetPosition(i, new Vector3(Mathf.Cos(t) * 0.45f, 0.045f, Mathf.Sin(t) * 0.45f));
            }
            photonGuide.transform.localRotation = Quaternion.Euler(-24, -16, 11);

            var outerGuide = CreateGlowLine(root.transform, "Soft outer rim / visual guide", accent, 0.012f, 128, true);
            for (var i = 0; i < outerGuide.positionCount; i++)
            {
                var t = i * Mathf.PI * 2f / outerGuide.positionCount;
                outerGuide.SetPosition(i, new Vector3(Mathf.Cos(t) * 1.08f, 0, Mathf.Sin(t) * 1.08f));
            }
            outerGuide.transform.localRotation = Quaternion.Euler(-24, -16, 11);
            root.transform.localScale = Vector3.one * 1.08f;
            return root;
        }

        public static LineRenderer CreateGlowLine(Transform parent, string name, Color color, float width, int points, bool loop)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            line.numCapVertices = loop ? 0 : 4;
            line.sharedMaterial = new Material(LoadShader("DTET/GlowLine", "Universal Render Pipeline/Unlit")) { name = name + " material" };
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        public static Transform CreateStarfield(Camera camera)
        {
            const int count = 1400;
            var mesh = new Mesh { name = "Deterministic spherical starfield quads" };
            var vertices = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var colors = new Color[count * 4];
            var triangles = new int[count * 6];
            uint state = 0xD7E7A203u;
            for (var i = 0; i < count; i++)
            {
                var y = Next(ref state) * 2f - 1f;
                var angle = Next(ref state) * Mathf.PI * 2f;
                var horizontal = Mathf.Sqrt(1f - y * y);
                var direction = new Vector3(Mathf.Cos(angle) * horizontal, y, Mathf.Sin(angle) * horizontal);
                var size = Mathf.Lerp(0.035f, 0.20f, Mathf.Pow(Next(ref state), 3f));
                var temperature = Next(ref state);
                var tint = temperature < 0.16f
                    ? new Color(1f, 0.76f, 0.57f, 0.72f)
                    : temperature < 0.55f
                        ? new Color(0.65f, 0.82f, 1f, 0.65f)
                        : new Color(0.94f, 0.97f, 1f, 0.9f);
                var p = direction * 90f;
                var tangent = Vector3.Cross(direction, Mathf.Abs(y) > 0.95f ? Vector3.right : Vector3.up).normalized;
                var right = tangent * size;
                var up = Vector3.Cross(direction, tangent) * size;
                var v = i * 4;
                vertices[v] = p - right - up;
                vertices[v + 1] = p + right - up;
                vertices[v + 2] = p + right + up;
                vertices[v + 3] = p - right + up;
                uv[v] = Vector2.zero;
                uv[v + 1] = Vector2.right;
                uv[v + 2] = Vector2.one;
                uv[v + 3] = Vector2.up;
                for (var j = 0; j < 4; j++) colors[v + j] = tint;
                var t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            var stars = new GameObject("Single-mesh deterministic background stars", typeof(MeshFilter), typeof(MeshRenderer));
            stars.transform.position = camera.transform.position;
            stars.GetComponent<MeshFilter>().sharedMesh = mesh;
            stars.GetComponent<MeshRenderer>().sharedMaterial = new Material(LoadShader("DTET/Starfield", "Universal Render Pipeline/Unlit"))
            {
                name = "Soft star points"
            };
            return stars.transform;
        }

        private static void CreateDisk(Transform parent, string name, Color tint, Color inner, float opacity, float flow, Vector3 tilt, float scale = 1f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(tilt);
            go.transform.localScale = Vector3.one * scale;
            go.GetComponent<MeshFilter>().sharedMesh = DiskMesh();
            var material = new Material(LoadShader("DTET/ConceptualAccretion", "Universal Render Pipeline/Unlit")) { name = name + " material" };
            material.SetColor("_Tint", tint);
            material.SetColor("_InnerTint", inner);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_FlowSpeed", flow);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh DiskMesh()
        {
            if (diskMesh != null) return diskMesh;
            const int sectors = 192;
            const int bands = 8;
            var surfaceVertices = (sectors + 1) * (bands + 1);
            var vertices = new Vector3[surfaceVertices * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sectors * bands * 6 * 2];
            for (var surface = 0; surface < 2; surface++)
            {
                for (var band = 0; band <= bands; band++)
                {
                    var r = band / (float)bands;
                    var radius = Mathf.Lerp(0.43f, 1.14f, r);
                    // A shallow closed profile makes the illustration visible from edge-on and below.
                    var height = Mathf.Sin(r * Mathf.PI) * 0.075f * (surface == 0 ? 1f : -1f);
                    for (var sector = 0; sector <= sectors; sector++)
                    {
                        var angle = sector * Mathf.PI * 2f / sectors;
                        var index = surface * surfaceVertices + band * (sectors + 1) + sector;
                        vertices[index] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                        uv[index] = new Vector2(r, sector / (float)sectors);
                    }
                }
            }
            var cursor = 0;
            for (var surface = 0; surface < 2; surface++)
            {
                for (var band = 0; band < bands; band++)
                {
                    for (var sector = 0; sector < sectors; sector++)
                    {
                        var a = surface * surfaceVertices + band * (sectors + 1) + sector;
                        var b = a + sectors + 1;
                        triangles[cursor++] = a;
                        triangles[cursor++] = b;
                        triangles[cursor++] = a + 1;
                        triangles[cursor++] = a + 1;
                        triangles[cursor++] = b;
                        triangles[cursor++] = b + 1;
                    }
                }
            }
            diskMesh = new Mesh { name = "Shared shallow annulus: two surfaces, 192 sectors, 8 bands" };
            diskMesh.vertices = vertices;
            diskMesh.uv = uv;
            diskMesh.triangles = triangles;
            diskMesh.RecalculateNormals();
            diskMesh.RecalculateBounds();
            return diskMesh;
        }

        public static Material CreateSolidMaterial(string name, Color color)
        {
            var shader = LoadShader("DTET/SolidSurface", "Universal Render Pipeline/Unlit");
            return new Material(shader) { name = name, color = color };
        }

        private static Shader LoadShader(string resourcePath, string fallbackName)
        {
            var shader = Resources.Load<Shader>(resourcePath);
            return shader != null ? shader : Shader.Find(fallbackName);
        }

        private static float Next(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state & 0x00ffffffu) / 16777215f;
        }
    }
}
