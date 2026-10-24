using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    /// <summary>A finite, hypothetical background source. Its light is evaluated before foreground gas.</summary>
    public sealed class FeaturedBackgroundStar : MonoBehaviour
    {
        private static readonly Vector3 FixedDirection = new Vector3(.10f, -.547f, .831f).normalized;
        private Camera view;
        private Material material;
        private Mesh mesh;
        private GameObject surface;
        private MeshRenderer surfaceRenderer;
        private bool visible = true;
        public Vector3 SourcePositionRg { get; private set; }
        public float SourceRadiusRg { get; private set; }
        public bool Ready => material != null && surfaceRenderer != null;
        public Material StarMaterial => material;
        public bool Visibility
        {
            get => visible;
            set { visible = value; if (surfaceRenderer != null) surfaceRenderer.enabled = value; }
        }
        public string Status { get; private set; } = "Hypothetical background star has not been initialized.";

        public void Initialize(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Release(); view = camera;
            var shader = Resources.Load<Shader>("DTET/FeaturedBackgroundStar");
            if (shader == null || !shader.isSupported) { Status = "Featured stellar lens unavailable: shader support."; return; }
            material = new Material(shader) { name = "Hypothetical solar-radius source at one AU" };
            material.SetVector("_FeaturedColor", new Vector4(.86f, .94f, 1, 1.8f));
            mesh = new Mesh { name = "Analytic stellar source screen surface" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000); mesh.UploadMeshData(true);
            surface = new GameObject("Hypothetical background star / one solar radius / one AU");
            surface.transform.SetParent(transform, false); surface.layer = 0;
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = surface.AddComponent<MeshRenderer>(); surfaceRenderer.sharedMaterial = material;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off; surfaceRenderer.receiveShadows = false;
            surfaceRenderer.lightProbeUsage = LightProbeUsage.Off; surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            surfaceRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            surfaceRenderer.enabled = visible;
            Status = "Hypothetical one-solar-radius star at 1 AU; approximate thin-lens images, not an observed GW150914 source.";
        }

        public void Bind(InteractiveBinarySnapshot snapshot, Material opticsMaterial, bool enabled)
        {
            if (!Ready || view == null) return;
            StellarLensReference.SourceDimensions(snapshot.GravitationalRadiusMeters, out double distance, out double radius);
            SourcePositionRg = FixedDirection * (float)distance; SourceRadiusRg = (float)radius;
            material.SetVector("_FeaturedSource", new Vector4(SourcePositionRg.x, SourcePositionRg.y, SourcePositionRg.z, SourceRadiusRg));
            InteractiveOptics.CopyStellarUniforms(material, opticsMaterial, enabled);
            // Refresh the actual camera even when effects are off or a capture moved it this frame.
            material.SetVector("_CameraOrigin", view.transform.position); material.SetVector("_CameraRight", view.transform.right);
            material.SetVector("_CameraUp", view.transform.up); material.SetVector("_CameraForward", view.transform.forward);
            float tangent = Mathf.Tan(.5f * view.fieldOfView * Mathf.Deg2Rad);
            float aspect = view.targetTexture != null ? (float)view.targetTexture.width / view.targetTexture.height : view.aspect;
            Vector4 optics = material.GetVector("_Optics"); optics.z = tangent * aspect; optics.w = tangent;
            if (!enabled) { optics.x = 0; optics.y = 0; }
            material.SetVector("_Optics", optics);
        }

        public void Release()
        {
            if (surface != null) { surface.SetActive(false); Destroy(surface); }
            if (mesh != null) Destroy(mesh); if (material != null) Destroy(material);
            surface = null; mesh = null; material = null; surfaceRenderer = null; view = null;
        }
        private void OnDestroy() { Release(); }
    }
}
