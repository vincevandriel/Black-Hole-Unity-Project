using System.Collections.Generic;
using ApproachHUD;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Presents the original procedural HUD above the astronomical camera's optical pass.
    public sealed class GuidedHudGraphic : MaskableGraphic
    {
        public HudSquareGraphic Source { get; set; }
        readonly List<Vector3> vertices = new List<Vector3>(4096);
        readonly List<Color> colours = new List<Color>(4096);
        readonly List<int> triangles = new List<int>(8192);

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (Source == null || Source.GeneratedMesh == null) return;
            var mesh = Source.GeneratedMesh;
            mesh.GetVertices(vertices); mesh.GetColors(colours); mesh.GetTriangles(triangles, 0);
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            Vector2 center = rectTransform.rect.center;
            for (int i = 0; i < vertices.Count; i++)
                helper.AddVert((Vector3)center + vertices[i] * scale, colours[i] * color, Vector2.zero);
            for (int i = 0; i < triangles.Count; i += 3)
                helper.AddTriangle(triangles[i], triangles[i + 1], triangles[i + 2]);
        }
    }
}
