using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    // Fixed distant stars and unresolved galactic light.
    public sealed class ReducedExhibitSky : MonoBehaviour
    {
        private Camera view;
        private Material material;
        private Mesh mesh;
        private GameObject batch;
        private Material backdropMaterial;
        private Mesh backdropMesh;
        private GameObject backdrop;
        public int StarCount { get; private set; }
        public ulong CatalogFingerprint { get; private set; }
        // Uploaded mesh payload only; excludes engine overhead.
        public long EstimatedMeshBytes => 156L * StarCount;
        public bool Ready => material != null && batch != null;
        public bool BackgroundReady => backdropMaterial != null && backdrop != null;
        public long EstimatedBackdropMeshBytes => backdropMesh == null ? 0 : 60;
        public bool BackdropVisible { get => backdrop != null && backdrop.activeSelf; set { if (backdrop != null) backdrop.SetActive(value); } }
        public bool Visible
        {
            get => batch != null && batch.activeSelf;
            set { if (batch != null) batch.SetActive(value); BackdropVisible = value; }
        }
        public string Status { get; private set; } = "Sky has not been initialized.";

        public void Initialize(Camera camera, int starCount)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (starCount < 1 || starCount > SyntheticSkyCatalog.MaximumStars)
                throw new ArgumentOutOfRangeException(nameof(starCount));
            ReleaseResources();
            view = camera;
            var shader = Resources.Load<Shader>("DTET/ExhibitSky");
            if (shader == null || !shader.isSupported)
            {
                Status = "Synthetic star shader unavailable; solid background diagnostic.";
                return;
            }
            SyntheticSkyStar[] stars = SyntheticSkyCatalog.Create(starCount);
            CatalogFingerprint = SyntheticSkyCatalog.Fingerprint(stars);
            var positions = new Vector3[starCount * 4];
            var corners = new Vector2[starCount * 4];
            var colors = new Color[starCount * 4];
            var indices = new int[starCount * 6];
            for (int i = 0; i < stars.Length; i++)
            {
                SyntheticSkyStar star = stars[i];
                var direction = new Vector3((float)star.X, (float)star.Y, (float)star.Z);
                var color = new Color((float)star.Red, (float)star.Green, (float)star.Blue, (float)star.RelativeFlux);
                int v = i * 4, t = i * 6;
                for (int j = 0; j < 4; j++) { positions[v + j] = direction; colors[v + j] = color; }
                corners[v] = new Vector2(-1, -1); corners[v + 1] = new Vector2(-1, 1);
                corners[v + 2] = new Vector2(1, 1); corners[v + 3] = new Vector2(1, -1);
                indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
                indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "Synthetic sky immutable directions", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.uv = corners; mesh.colors = colors; mesh.triangles = indices;
            // Actual projection uses directions, not these vertex positions or object translation.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
            mesh.UploadMeshData(true);
            material = new Material(shader) { name = "Synthetic sky normalized pixel PSF" };
            material.SetFloat("_Exposure", (float)SyntheticSkyCatalog.DefaultExposure);
            batch = new GameObject("Synthetic distant sources / fixed celestial directions");
            batch.transform.SetParent(transform, false); batch.layer = 0;
            batch.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = batch.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            StarCount = starCount;
            Status = $"Synthetic sky: {starCount} fixed directions; dim cutoff m={stars[starCount - 1].ApparentMagnitude:F2}.";
            InitializeBackdrop();
            UpdateRenderSize();
        }

        private void InitializeBackdrop()
        {
            var shader = Resources.Load<Shader>("DTET/StellarBackdrop");
            if (shader == null || !shader.isSupported) return;
            backdropMaterial = new Material(shader) { name = "Fixed synthetic stellar background" };
            backdropMesh = new Mesh { name = "Stellar background clip quad", indexFormat = IndexFormat.UInt16 };
            backdropMesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            backdropMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            backdropMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
            backdropMesh.UploadMeshData(true);
            backdrop = new GameObject("Fixed distant stellar light / synthetic galactic band");
            backdrop.transform.SetParent(transform, false); backdrop.layer = 0;
            backdrop.AddComponent<MeshFilter>().sharedMesh = backdropMesh;
            var renderer = backdrop.AddComponent<MeshRenderer>(); renderer.sharedMaterial = backdropMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void LateUpdate() { UpdateRenderSize(); }

        // Deflect star centres before foreground gas is drawn.
        public void SetLensing(InteractiveBinarySnapshot snapshot, float gain)
        {
            if (material == null) return;
            if (!float.IsFinite(gain)) throw new ArgumentOutOfRangeException(nameof(gain));
            material.SetFloat("_SkyLensGain", Mathf.Clamp(gain, 0, 2));
            material.SetVector("_SkyLensA", LensBody(snapshot.BodyA));
            material.SetVector("_SkyLensB", LensBody(snapshot.BodyB));
            material.SetVector("_SkyLensR", LensBody(snapshot.Remnant));
            material.SetVector("_SkyLensRadii", new Vector4((float)(snapshot.BodyA?.HorizonRadiusRg ?? 0),
                (float)(snapshot.BodyB?.HorizonRadiusRg ?? 0), (float)(snapshot.Remnant?.HorizonRadiusRg ?? 0), 0));
            UpdateRenderSize();
        }

        public void SetStellarWaves(Material opticsMaterial, bool enabled)
        {
            InteractiveOptics.CopyStellarUniforms(material, opticsMaterial, enabled);
            InteractiveOptics.CopyStellarUniforms(backdropMaterial, opticsMaterial, enabled);
            BindBackdropCamera();
        }

        private void BindBackdropCamera()
        {
            if (view == null || backdropMaterial == null) return;
            var pose = view.transform;
            backdropMaterial.SetVector("_CameraOrigin", pose.position);
            backdropMaterial.SetVector("_CameraRight", pose.right);
            backdropMaterial.SetVector("_CameraUp", pose.up);
            backdropMaterial.SetVector("_CameraForward", pose.forward);
            int width = view.targetTexture != null ? view.targetTexture.width : Math.Max(2, view.pixelWidth);
            int height = view.targetTexture != null ? view.targetTexture.height : Math.Max(2, view.pixelHeight);
            float tangent = Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
            Vector4 optics = backdropMaterial.GetVector("_Optics");
            optics.z = tangent * width / height; optics.w = tangent;
            backdropMaterial.SetVector("_Optics", optics);
        }

        private static Vector4 LensBody(InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return Vector4.zero;
            var b = body.Value;
            return new Vector4((float)b.PositionRg.X, (float)b.PositionRg.Y, (float)b.PositionRg.Z, (float)b.MassFraction);
        }

        private void UpdateRenderSize()
        {
            if (view == null || material == null) return;
            int width = view.targetTexture != null ? view.targetTexture.width : Math.Max(2, view.pixelWidth);
            int height = view.targetTexture != null ? view.targetTexture.height : Math.Max(2, view.pixelHeight);
            material.SetVector("_RenderSize", new Vector4(width, height, 1f / width, 1f / height));
            BindBackdropCamera();
        }

        private void OnDestroy() { ReleaseResources(); }

        private void ReleaseResources()
        {
            if (batch != null) { batch.SetActive(false); Destroy(batch); }
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (backdrop != null) { backdrop.SetActive(false); Destroy(backdrop); }
            if (backdropMesh != null) Destroy(backdropMesh);
            if (backdropMaterial != null) Destroy(backdropMaterial);
            backdrop = null; backdropMesh = null; backdropMaterial = null;
            batch = null; mesh = null; material = null; view = null;
            StarCount = 0; CatalogFingerprint = 0;
        }
    }
}
