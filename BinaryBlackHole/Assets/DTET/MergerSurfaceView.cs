using System;
using BlackHole.Staging.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    // An illustrative surface between retained equal-area sphere proxies, not measured horizon geometry.
    public sealed class MergerSurfaceView : MonoBehaviour
    {
        public const double LeadTg = 12;
        public const double SettleTg = 40;
        private static readonly int ActiveId = Shader.PropertyToID("_MergerActive");
        private static readonly int AId = Shader.PropertyToID("_MergerA");
        private static readonly int BId = Shader.PropertyToID("_MergerB");
        private static readonly int RId = Shader.PropertyToID("_MergerR");
        private static readonly int ShapeId = Shader.PropertyToID("_MergerShape");
        private static readonly int MinId = Shader.PropertyToID("_MergerBoundsMin");
        private static readonly int MaxId = Shader.PropertyToID("_MergerBoundsMax");
        private Shader shader;
        private Material material;
        private Mesh mesh;
        private GameObject surface;
        private MeshRenderer surfaceRenderer;
        private InteractiveBinaryScenario cachedModel;
        private long cachedGeneration = long.MinValue;
        private double cachedTime = double.NaN, lastBinaryTime;
        private Vector4 endpointA, endpointB, firstRemnant, bodyA, bodyB, remnant, shape, minimum, maximum;
        private bool active;

        public bool Ready => material != null && surfaceRenderer != null;
        public bool Visible => Ready && surfaceRenderer.enabled && surface.activeInHierarchy;
        public MeshRenderer SurfaceRenderer => surfaceRenderer;
        public float Blend => active ? shape.x : 0;

        public void Initialize()
        {
            Release();
            shader = Resources.Load<Shader>("DTET/MergerSurface");
        }

        public void Present(InteractiveBinaryScenario model, InteractiveBinarySnapshot snapshot)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (ReferenceEquals(model, cachedModel) && cachedGeneration == snapshot.Generation && cachedTime == snapshot.ElapsedSeconds)
            {
                if (surfaceRenderer != null) surfaceRenderer.enabled = active && isActiveAndEnabled;
                return;
            }
            if (!ReferenceEquals(model, cachedModel) || cachedGeneration != snapshot.Generation)
            {
                cachedModel = model; cachedGeneration = snapshot.Generation;
                // Stop just before the discrete data switch; the held endpoints are display inputs only.
                lastBinaryTime = Math.Max(0, model.MergerTimeSeconds - Math.Max(1e-10, model.GravitationalTimeSeconds * 1e-7));
                var binary = model.Evaluate(lastBinaryTime);
                var common = model.Evaluate(model.MergerTimeSeconds);
                endpointA = Sphere(binary.BodyA); endpointB = Sphere(binary.BodyB); firstRemnant = Sphere(common.Remnant);
            }
            cachedTime = snapshot.ElapsedSeconds;
            double start = model.MergerTimeSeconds - LeadTg * model.GravitationalTimeSeconds;
            double end = model.MergerTimeSeconds + SettleTg * model.GravitationalTimeSeconds;
            active = snapshot.ElapsedSeconds >= start && snapshot.ElapsedSeconds <= end;
            if (!active)
            {
                if (surfaceRenderer != null) surfaceRenderer.enabled = false;
                if (material != null) material.SetFloat(ActiveId, 0);
                return;
            }
            bodyA = snapshot.ElapsedSeconds < lastBinaryTime ? Sphere(snapshot.BodyA) : endpointA;
            bodyB = snapshot.ElapsedSeconds < lastBinaryTime ? Sphere(snapshot.BodyB) : endpointB;
            remnant = snapshot.Remnant.HasValue ? Sphere(snapshot.Remnant) : firstRemnant;
            if (!ValidSphere(bodyA) || !ValidSphere(bodyB) || !ValidSphere(remnant) || !EnsureResources())
            {
                active = false;
                if (surfaceRenderer != null) surfaceRenderer.enabled = false;
                return;
            }
            double u = Math.Max(0, Math.Min(1, (snapshot.ElapsedSeconds - start) / (end - start)));
            float blend = (float)(u * u * u * (u * (u * 6 - 15) + 10));
            float width = 2 * (bodyA.w + bodyB.w) * blend * (1 - blend);
            float epsilon = Mathf.Max(.0001f, .0005f * Mathf.Min(bodyA.w, Mathf.Min(bodyB.w, remnant.w)));
            shape = new Vector4(blend, width, epsilon, 0);
            Vector3 low = Centre(bodyA) - Vector3.one * (bodyA.w + .25f * width);
            Vector3 high = Centre(bodyA) + Vector3.one * (bodyA.w + .25f * width);
            Encapsulate(bodyB, .25f * width, ref low, ref high); Encapsulate(remnant, 0, ref low, ref high);
            low -= Vector3.one * (4 * epsilon); high += Vector3.one * (4 * epsilon);
            minimum = new Vector4(low.x, low.y, low.z, 0); maximum = new Vector4(high.x, high.y, high.z, 0);
            surface.transform.position = .5f * (low + high);
            surface.transform.rotation = Quaternion.identity;
            surface.transform.localScale = high - low;
            surfaceRenderer.enabled = isActiveAndEnabled;
            BindTo(material);
        }

        public void BindTo(Material target)
        {
            if (target == null) return;
            target.SetFloat(ActiveId, Visible ? 1 : 0);
            if (!Visible) return;
            target.SetVector(AId, bodyA); target.SetVector(BId, bodyB); target.SetVector(RId, remnant);
            target.SetVector(ShapeId, shape); target.SetVector(MinId, minimum); target.SetVector(MaxId, maximum);
        }

        private bool EnsureResources()
        {
            if (Ready) return true;
            if (shader == null || !shader.isSupported) return false;
            mesh = new Mesh { name = "Bounded merger surface ray box", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            mesh.triangles = new[] { 0,2,1,0,3,2, 4,5,6,4,6,7, 0,4,7,0,7,3, 1,2,6,1,6,5, 0,1,5,0,5,4, 3,7,6,3,6,2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one); mesh.UploadMeshData(true);
            material = new Material(shader) { name = "Opaque illustrative common-horizon transition" };
            surface = new GameObject("Illustrative smooth merger surface");
            surface.transform.SetParent(transform, false); surface.layer = 0;
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            surfaceRenderer = surface.AddComponent<MeshRenderer>(); surfaceRenderer.sharedMaterial = material;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off; surfaceRenderer.receiveShadows = false;
            surfaceRenderer.lightProbeUsage = LightProbeUsage.Off; surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            surfaceRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            surfaceRenderer.enabled = false;
            return true;
        }

        private static Vector4 Sphere(InteractiveBinaryBody? body)
        {
            if (!body.HasValue) return Vector4.zero;
            var b = body.Value;
            return new Vector4((float)b.PositionRg.X, (float)b.PositionRg.Y, (float)b.PositionRg.Z, (float)b.HorizonRadiusRg);
        }
        private static Vector3 Centre(Vector4 sphere) => new Vector3(sphere.x, sphere.y, sphere.z);
        private static bool ValidSphere(Vector4 sphere) => float.IsFinite(sphere.x) && float.IsFinite(sphere.y)
            && float.IsFinite(sphere.z) && float.IsFinite(sphere.w) && sphere.w > 0;
        private static void Encapsulate(Vector4 sphere, float padding, ref Vector3 low, ref Vector3 high)
        {
            var extent = Vector3.one * (sphere.w + padding);
            low = Vector3.Min(low, Centre(sphere) - extent); high = Vector3.Max(high, Centre(sphere) + extent);
        }
        private void OnDisable() { if (surfaceRenderer != null) surfaceRenderer.enabled = false; }
        private void OnDestroy() => Release();
        private void Release()
        {
            if (surface != null) { surface.SetActive(false); Destroy(surface); }
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            surface = null; surfaceRenderer = null; mesh = null; material = null; shader = null;
            active = false; cachedModel = null; cachedGeneration = long.MinValue; cachedTime = double.NaN;
        }
    }
}
