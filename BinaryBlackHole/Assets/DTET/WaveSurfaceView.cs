using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    // An exaggerated strain diagram; no metric geometry or independent wave clock.
    public sealed class WaveSurfaceView : MonoBehaviour
    {
        private Material material;
        private Mesh mesh;
        private GameObject surface;
        private MeshRenderer surfaceRenderer;
        private bool requestedVisible, hasHistory;

        public bool Ready => material != null && surfaceRenderer != null;
        public int GridResolution { get; private set; }
        public int DisplayGridSegments { get; private set; }
        public int VertexCount { get; private set; }
        public int IndexCount { get; private set; }
        public long EstimatedMeshBytes => 12L * VertexCount + 2L * IndexCount;
        public float DisplayRadiusRg { get; private set; }
        public float MaximumHeightRg { get; private set; }
        public bool Visible
        {
            get => surfaceRenderer != null && surfaceRenderer.enabled;
            set { requestedVisible = value; UpdateVisibility(); }
        }

        public void Initialize(ExhibitQualitySettings quality)
        {
            if (quality == null) throw new ArgumentNullException(nameof(quality));
            Release();
            var shader = Resources.Load<Shader>("DTET/WaveSurface");
            if (shader == null || !shader.isSupported) return;
            GridResolution = quality.EffectiveProfile == ExhibitQualityProfile.High ? 192
                : quality.EffectiveProfile == ExhibitQualityProfile.Balanced ? 160 : 96;
            DisplayGridSegments = quality.EffectiveProfile == ExhibitQualityProfile.High ? 48
                : quality.EffectiveProfile == ExhibitQualityProfile.Balanced ? 40 : 32;
            int segments = GridResolution - 1;
            VertexCount = GridResolution * GridResolution;
            IndexCount = 6 * segments * segments;
            var positions = new Vector3[VertexCount];
            var indices = new ushort[IndexCount];
            for (int z = 0; z < GridResolution; z++)
                for (int x = 0; x < GridResolution; x++)
                {
                    int vertex = z * GridResolution + x;
                    positions[vertex] = new Vector3(2f * x / segments - 1, 0, 2f * z / segments - 1);
                    if (x == segments || z == segments) continue;
                    int triangle = 6 * (z * segments + x);
                    indices[triangle] = (ushort)vertex; indices[triangle + 1] = (ushort)(vertex + GridResolution);
                    indices[triangle + 2] = (ushort)(vertex + 1); indices[triangle + 3] = (ushort)(vertex + 1);
                    indices[triangle + 4] = (ushort)(vertex + GridResolution); indices[triangle + 5] = (ushort)(vertex + GridResolution + 1);
                }
            mesh = new Mesh { name = "Immutable illustrative strain grid", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.subMeshCount = 1;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false, 0);
            // Vertex displacement remains within these local bounds.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2, 2, 2));
            mesh.UploadMeshData(true);
            material = new Material(shader) { name = "Illustrative retarded strain / cyan positive, amber negative" };
            material.SetFloat("_GridSegments", DisplayGridSegments);
            surface = new GameObject("Illustrative gravitational-wave surface / exaggerated strain");
            surface.transform.SetParent(transform, false); surface.layer = 0;
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = surface.AddComponent<MeshRenderer>(); surfaceRenderer.sharedMaterial = material;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off; surfaceRenderer.receiveShadows = false;
            surfaceRenderer.lightProbeUsage = LightProbeUsage.Off; surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            surfaceRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            surfaceRenderer.enabled = false;
        }

        // Call after Optics.Prepare; the texture owns the event epoch, retardation and coverage.
        public void Bind(Material opticsMaterial, bool visible)
        {
            requestedVisible = visible; hasHistory = false;
            if (!Ready) return;
            var history = opticsMaterial == null ? null : opticsMaterial.GetTexture("_WaveHistory");
            Vector4 parameters = opticsMaterial == null ? Vector4.zero : opticsMaterial.GetVector("_WaveParameters");
            if (history != null && float.IsFinite(parameters.x) && parameters.x > 0
                && float.IsFinite(parameters.y) && parameters.y > 0
                && float.IsFinite(parameters.w) && parameters.w > 0)
            {
                float radius = parameters.x / parameters.w;
                if (float.IsFinite(radius) && radius > 0)
                {
                    DisplayRadiusRg = radius;
                    MaximumHeightRg = Mathf.Min(18, .12f * radius);
                    surface.transform.localScale = new Vector3(radius, MaximumHeightRg, radius);
                    material.SetTexture("_WaveHistory", history);
                    material.SetVector("_WaveParameters", parameters);
                    hasHistory = true;
                }
            }
            if (!hasHistory)
            {
                DisplayRadiusRg = 0; MaximumHeightRg = 0;
                material.SetTexture("_WaveHistory", null);
                material.SetVector("_WaveParameters", Vector4.zero);
            }
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (surfaceRenderer != null) surfaceRenderer.enabled = requestedVisible && hasHistory;
        }

        public void Release()
        {
            if (surface != null) { surface.SetActive(false); Destroy(surface); }
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            surface = null; surfaceRenderer = null; mesh = null; material = null;
            requestedVisible = false; hasHistory = false;
            GridResolution = 0; DisplayGridSegments = 0; VertexCount = 0; IndexCount = 0;
            DisplayRadiusRg = 0; MaximumHeightRg = 0;
        }

        private void OnDisable() { if (surfaceRenderer != null) surfaceRenderer.enabled = false; }
        private void OnDestroy() { Release(); }
    }
}
