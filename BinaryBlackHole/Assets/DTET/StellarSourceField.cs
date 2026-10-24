using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    // Fixed source objects with a shared mesh and approximate thin-lens images.
    [DefaultExecutionOrder(100)]
    public sealed class StellarSourceField : MonoBehaviour
    {
        public const int SourceCount = 1400;
        private readonly List<StellarSource> sources = new List<StellarSource>(SourceCount);
        private Vector3[] vertices;
        private Color[] colors;
        private Mesh mesh;
        private Material material;
        private Camera observer;
        private Transform[] bodies;
        private readonly PointLens[] lenses = new PointLens[2];
        private readonly ThinLensImage[] images = new ThinLensImage[ThinLensModel.MaximumImages];
        private readonly LineRenderer[] paths = new LineRenderer[5];
        private Vector3 eye, normal, planeRight, planeUp;
        private float lensDistance;
        private int activeLensCount, visibleImages;
        private bool imageStateValid;
        private Vector3 lastEye;
        private Matrix4x4 lastFieldMatrix;
        private readonly Vector3[] lastBodyPositions = new Vector3[3];
        private readonly bool[] lastBodyActive = new bool[3];
        private bool lastLensing, lastPaths;
        private const int RingSegments = 64;
        private const float DisplayGravitationalRadius = 0.055f;
        public bool LensingEnabled { get; private set; } = true;
        public bool ShowRayPaths { get; private set; }
        public bool ApproximationAvailable { get; private set; } = true;
        private bool savedLensing, savedPaths;
        public int DemoImageCount { get; private set; }
        public int VisibleImageCount => visibleImages;
        public double LastUpdateMilliseconds { get; private set; }
        public int ImageUpdateCount { get; private set; }
        public Vector3 DemoImagePosition { get; private set; }
        public StellarSource DemoSource => sources.Count > 0 ? sources[0] : null;
        public IReadOnlyList<StellarSource> Sources => sources;
        public Mesh DisplayMesh => mesh;
        public string OpticalStatus => !ApproximationAvailable ? "SXS COORDINATE VIEW / no metric or lensing model"
            : !LensingEnabled ? "BENDING OFF / direct source images"
            : activeLensCount == 0 ? "THIN LENS / outside supported viewing distance"
            : "APPROXIMATE THIN-LENS BENDING / no redshift";

        public void BindLenses(Transform first, Transform second, Transform remnant)
        {
            bodies = new[] { first, second, remnant };
            imageStateValid = false;
        }
        public void ToggleLensing() => SetLensing(!LensingEnabled);
        public void ToggleRayPaths() => SetRayPaths(!ShowRayPaths);
        public void SetLensing(bool enabled) => LensingEnabled = ApproximationAvailable && enabled;
        public void SetRayPaths(bool enabled) => ShowRayPaths = ApproximationAvailable && enabled;
        public void SetApproximationAvailable(bool available)
        {
            if (ApproximationAvailable == available) return;
            if (!available) { savedLensing = LensingEnabled; savedPaths = ShowRayPaths; }
            ApproximationAvailable = available;
            LensingEnabled = available && savedLensing;
            ShowRayPaths = available && savedPaths;
            imageStateValid = false;
        }

        public void Initialize(Camera camera)
        {
            observer = camera;
            uint state = 0xD7E7A203u;
            var quads = SourceCount * ThinLensModel.MaximumImages + RingSegments;
            vertices = new Vector3[quads * 4];
            var uv = new Vector2[vertices.Length];
            colors = new Color[vertices.Length];
            var triangles = new int[quads * 6];
            for (var i = 0; i < SourceCount; i++)
            {
                var y = Next(ref state) * 2f - 1f;
                var angle = Next(ref state) * Mathf.PI * 2f;
                var horizontal = Mathf.Sqrt(Mathf.Max(0, 1f - y * y));
                var direction = new Vector3(Mathf.Cos(angle) * horizontal, y, Mathf.Sin(angle) * horizontal);
                var size = Mathf.Lerp(0.035f, 0.20f, Mathf.Pow(Next(ref state), 3f));
                var temperature = Next(ref state);
                var tint = temperature < 0.16f ? new Color(1f, 0.76f, 0.57f, 0.72f)
                    : temperature < 0.55f ? new Color(0.65f, 0.82f, 1f, 0.65f) : new Color(0.94f, 0.97f, 1f, 0.9f);
                var distance = Mathf.Lerp(85f, 125f, Next(ref state));
                var sourceObject = new GameObject($"Star {i:0000} / synthetic source");
                sourceObject.transform.SetParent(transform, false);
                sourceObject.transform.localPosition = direction * distance;
                var source = sourceObject.AddComponent<StellarSource>();
                source.Initialize(i, size * distance / 90f, tint);
                if (i == 0)
                {
                    sourceObject.name = "Star 0000 / fixed alignment demonstration source";
                    sourceObject.transform.localPosition = new Vector3(0, 0, 105);
                    source.Initialize(0, 0.50f, new Color(1f, 0.94f, 0.72f, 1f));
                }
                sources.Add(source);
            }
            for (var i = 0; i < quads; i++)
            {
                var v = i * 4;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                var t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            mesh = new Mesh { name = "Batched images of fixed stellar source objects" };
            mesh.MarkDynamic();
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            material = new Material(Resources.Load<Shader>("DTET/Starfield")) { name = "Unshifted synthetic star display" };
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            UpdateImages();
        }

        private void BuildProjection()
        {
            eye = observer.transform.position;
            var centre = new Vector3(-3.25f, 0, 0);
            normal = (centre - eye).normalized;
            lensDistance = Vector3.Distance(centre, eye);
            planeRight = Vector3.Cross(Mathf.Abs(normal.y) > 0.95f ? Vector3.right : Vector3.up, normal).normalized;
            planeUp = Vector3.Cross(normal, planeRight).normalized;
            activeLensCount = 0;
            if (bodies == null || lensDistance < 2f) return;
            foreach (var body in bodies)
            {
                if (body == null || !body.gameObject.activeInHierarchy) continue;
                var offset = body.position - eye;
                var depth = Vector3.Dot(offset, normal);
                if (depth < 1.5f || Vector3.Distance(body.position, eye) < 1.5f) continue;
                if (activeLensCount == 2) break;
                // The two moving markers are projected onto a common instantaneous lens plane.
                lenses[activeLensCount++] = new PointLens(Vector3.Dot(offset, planeRight) / depth,
                    Vector3.Dot(offset, planeUp) / depth, 1);
            }
        }

        private void LateUpdate() => UpdateImages();

        // Refresh fixed emitters when their transforms or activation state change.
        public void InvalidateImages() => imageStateValid = false;

        private bool NeedsImageUpdate()
        {
            if (!imageStateValid || observer.transform.position != lastEye
                || transform.localToWorldMatrix != lastFieldMatrix
                || LensingEnabled != lastLensing || ShowRayPaths != lastPaths) return true;
            if (bodies != null)
                for (var i = 0; i < bodies.Length; i++)
                    if (bodies[i] != null && (bodies[i].position != lastBodyPositions[i]
                        || bodies[i].gameObject.activeInHierarchy != lastBodyActive[i])) return true;
            foreach (var source in sources)
                if (source.transform.hasChanged || source.isActiveAndEnabled != source.CachedActive) return true;
            return false;
        }

        private void RememberImageState()
        {
            lastEye = observer.transform.position;
            lastFieldMatrix = transform.localToWorldMatrix;
            lastLensing = LensingEnabled; lastPaths = ShowRayPaths;
            if (bodies != null)
                for (var i = 0; i < bodies.Length; i++)
                    if (bodies[i] != null)
                    {
                        lastBodyPositions[i] = bodies[i].position;
                        lastBodyActive[i] = bodies[i].gameObject.activeInHierarchy;
                    }
            foreach (var source in sources)
            {
                source.transform.hasChanged = false;
                source.CachedActive = source.isActiveAndEnabled;
            }
            imageStateValid = true;
        }

        private void UpdateImages()
        {
            if (observer == null || mesh == null) return;
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!NeedsImageUpdate())
            {
                LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                return;
            }
            BuildProjection();
            visibleImages = DemoImageCount = 0;
            for (var i = 0; i < paths.Length; i++) if (paths[i] != null) paths[i].enabled = false;
            for (var i = SourceCount * ThinLensModel.MaximumImages * 4; i < vertices.Length; i++)
            { vertices[i] = Vector3.zero; colors[i] = Color.clear; }
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var p = source.transform.position;
                var direction = (p - observer.transform.position).normalized;
                var axis = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
                var right = Vector3.Cross(direction, axis).normalized * source.DisplayRadius;
                var up = Vector3.Cross(direction, right.normalized) * source.DisplayRadius;
                var firstQuad = i * ThinLensModel.MaximumImages;
                for (var image = 0; image < ThinLensModel.MaximumImages; image++)
                    WriteQuad(firstQuad + image, p, Vector3.zero, Vector3.zero, Color.clear);
                var offset = p - eye;
                var depth = Vector3.Dot(offset, normal);
                var sx = Vector3.Dot(offset, planeRight) / Mathf.Max(0.001f, depth);
                var sy = Vector3.Dot(offset, planeUp) / Mathf.Max(0.001f, depth);
                // Thin-lens/paraxial model is deliberately limited to a forward cone and background sources.
                if (!source.isActiveAndEnabled) continue;
                if (!LensingEnabled || activeLensCount == 0 || depth <= lensDistance || sx * sx + sy * sy > 1.5f)
                { WriteQuad(firstQuad, p, right, up, source.RestTint); visibleImages++; continue; }
                var strength = ThinLensModel.EinsteinAngleSquared(DisplayGravitationalRadius, lensDistance, depth);
                for (var lens = 0; lens < activeLensCount; lens++)
                    lenses[lens] = new PointLens(lenses[lens].X, lenses[lens].Y, strength);
                var count = ThinLensModel.FindImages(sx, sy, lenses, activeLensCount, images);
                if (count == 0 && activeLensCount == 1 && i == 0
                    && Mathf.Abs(sx - (float)lenses[0].X) + Mathf.Abs(sy - (float)lenses[0].Y) < 0.00001f)
                {
                    DrawAlignedRing(source, depth, strength);
                    continue;
                }
                // Numerical failure is a direct preview, never a claimed capture/shadow.
                if (count == 0) { WriteQuad(firstQuad, p, right, up, source.RestTint); visibleImages++; continue; }
                for (var image = 0; image < count; image++)
                {
                    var solution = images[image];
                    var centre = eye + normal * depth + planeRight * (float)(solution.X * depth) + planeUp * (float)(solution.Y * depth);
                    var determinant = solution.Determinant;
                    var a = right; var b = up;
                    if (System.Math.Abs(determinant) > 1e-10)
                    {
                        a = (planeRight * (float)(solution.Jyy / determinant) - planeUp * (float)(solution.Jxy / determinant)) * source.DisplayRadius;
                        b = (planeUp * (float)(solution.Jxx / determinant) - planeRight * (float)(solution.Jxy / determinant)) * source.DisplayRadius;
                        // Finite display clamp avoids singular point-source caustics, not a physical flux prediction.
                        a = Vector3.ClampMagnitude(a, source.DisplayRadius * 18);
                        b = Vector3.ClampMagnitude(b, source.DisplayRadius * 18);
                    }
                    WriteQuad(firstQuad + image, centre, a, b, source.RestTint);
                    visibleImages++;
                    if (i == 0)
                    {
                        DemoImageCount = count;
                        if (image == 0) DemoImagePosition = centre;
                        DrawPath(image, solution.X, solution.Y, p);
                    }
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.RecalculateBounds();
            RememberImageState();
            ImageUpdateCount++;
            LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        private void WriteQuad(int quad, Vector3 centre, Vector3 right, Vector3 up, Color tint)
        {
            var v = quad * 4;
            vertices[v] = transform.InverseTransformPoint(centre - right - up);
            vertices[v + 1] = transform.InverseTransformPoint(centre + right - up);
            vertices[v + 2] = transform.InverseTransformPoint(centre + right + up);
            vertices[v + 3] = transform.InverseTransformPoint(centre - right + up);
            for (var j = 0; j < 4; j++) colors[v + j] = tint; // no spectral/colour shift
        }

        private void DrawAlignedRing(StellarSource source, float depth, double strength)
        {
            var radius = Mathf.Sqrt((float)strength) * depth;
            var centre = eye + normal * depth + planeRight * (float)(lenses[0].X * depth) + planeUp * (float)(lenses[0].Y * depth);
            for (var segment = 0; segment < RingSegments; segment++)
            {
                var angle = segment * Mathf.PI * 2 / RingSegments;
                var radial = planeRight * Mathf.Cos(angle) + planeUp * Mathf.Sin(angle);
                var tangent = planeUp * Mathf.Cos(angle) - planeRight * Mathf.Sin(angle);
                WriteQuad(SourceCount * ThinLensModel.MaximumImages + segment, centre + radial * radius,
                    tangent * radius * 0.06f, radial * source.DisplayRadius * 0.5f, source.RestTint);
            }
            DemoImageCount = RingSegments;
            visibleImages += RingSegments;
            DemoImagePosition = centre + planeRight * radius;
            for (var image = 0; image < paths.Length; image++)
            {
                var angle = image * Mathf.PI * 2 / paths.Length;
                DrawPath(image, lenses[0].X + Mathf.Sqrt((float)strength) * Mathf.Cos(angle),
                    lenses[0].Y + Mathf.Sqrt((float)strength) * Mathf.Sin(angle), source.transform.position);
            }
        }

        private void DrawPath(int image, double x, double y, Vector3 source)
        {
            if (!ShowRayPaths) return;
            if (paths[image] == null)
            {
                paths[image] = VisualAssetFactory.CreateGlowLine(transform, "Thin-lens ray guide " + image + " / not a GR geodesic",
                    image % 2 == 0 ? new Color(0.5f, 0.9f, 1f, 0.6f) : new Color(1f, 0.85f, 0.4f, 0.6f), 0.008f, 3, false);
                paths[image].useWorldSpace = true;
            }
            paths[image].enabled = true;
            paths[image].SetPosition(0, eye);
            paths[image].SetPosition(1, eye + normal * lensDistance + planeRight * (float)(x * lensDistance) + planeUp * (float)(y * lensDistance));
            paths[image].SetPosition(2, source);
        }

        private static float Next(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state & 0x00ffffffu) / 16777215f;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            foreach (var path in paths) if (path != null) Destroy(path.sharedMaterial);
        }
    }
}
