using UnityEngine;

namespace DTET.VisualPrototype
{
    // Procedural exhibit art, independent of physical optics and emission.
    public static class VisualAssetFactory
    {
        private static Mesh diskMesh;

        public static GameObject CreateBody(string name, Color accent, bool remnant = false)
        {
            var root = new GameObject(name);
            var inner = remnant ? new Color(1f, 0.78f, 0.43f) : new Color(1f, 0.62f, 0.31f);

            CreateDisk(root.transform, "Layered accretion illustration / inner", inner, accent, 0.42f, 0.42f, new Vector3(-24, -16, 11));
            CreateDisk(root.transform, "Layered accretion illustration / outer haze", accent, inner, 0.10f, -0.16f, new Vector3(-22, -13, 11), 1.18f);

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Central silhouette / not an event-horizon render";
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.78f;
            core.GetComponent<Renderer>().sharedMaterial = CreateSolidMaterial("Dark core", new Color(0.006f, 0.009f, 0.019f));
            Object.Destroy(core.GetComponent<Collider>());

            // An artistic gas boundary, not a simulated photon ring or relativistic shadow.
            var innerRim = CreateGlowLine(root.transform, "Illustrative gas inner rim / not a photon ring",
                new Color(inner.r, inner.g, inner.b, 0.48f), 0.026f, 128, true);
            for (var i = 0; i < innerRim.positionCount; i++)
            {
                var t = i * Mathf.PI * 2f / innerRim.positionCount;
                innerRim.SetPosition(i, new Vector3(Mathf.Cos(t) * 0.45f, 0.045f, Mathf.Sin(t) * 0.45f));
            }
            innerRim.transform.localRotation = Quaternion.Euler(-24, -16, 11);

            var outerGuide = CreateGlowLine(root.transform, "Soft outer rim / visual guide",
                new Color(accent.r, accent.g, accent.b, 0.12f), 0.018f, 128, true);
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
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = new Material(LoadShader("DTET/GlowLine", "Universal Render Pipeline/Unlit")) { name = name + " material" };
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        public static Transform CreateStarfield(Camera camera)
        {
            var stars = new GameObject("Fixed stellar sources / approximate thin-lens display");
            stars.transform.position = new Vector3(-3.25f, 0, 0);
            stars.AddComponent<StellarSourceField>().Initialize(camera);
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
            const int bands = 16;
            var surfaceVertices = (sectors + 1) * (bands + 1);
            var vertices = new Vector3[surfaceVertices * 2];
            var normals = new Vector3[vertices.Length];
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
                    var profileSlope = 0.075f * Mathf.PI * Mathf.Cos(r * Mathf.PI) / (1.14f - 0.43f);
                    for (var sector = 0; sector <= sectors; sector++)
                    {
                        var angle = sector * Mathf.PI * 2f / sectors;
                        var index = surface * surfaceVertices + band * (sectors + 1) + sector;
                        vertices[index] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                        // Explicit periodic normals avoid a welded-UV seam; lower faces point outward.
                        normals[index] = new Vector3(-profileSlope * Mathf.Cos(angle), surface == 0 ? 1f : -1f,
                            -profileSlope * Mathf.Sin(angle)).normalized;
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
                        triangles[cursor++] = surface == 0 ? a + 1 : b;
                        triangles[cursor++] = surface == 0 ? b : a + 1;
                        triangles[cursor++] = a + 1;
                        triangles[cursor++] = surface == 0 ? b + 1 : b;
                        triangles[cursor++] = surface == 0 ? b : b + 1;
                    }
                }
            }
            diskMesh = new Mesh { name = "Shared shallow annulus: two surfaces, 192 sectors, 16 bands" };
            diskMesh.vertices = vertices;
            diskMesh.normals = normals;
            diskMesh.uv = uv;
            diskMesh.triangles = triangles;
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

    }
}
