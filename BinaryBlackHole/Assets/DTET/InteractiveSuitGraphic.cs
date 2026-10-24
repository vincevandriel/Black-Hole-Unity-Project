using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Illustrated desktop suit and projector, not a tracked XR hand.
    public sealed class InteractiveSuitGraphic : MaskableGraphic
    {
        private float reveal = 1;
        private bool pinned;
        public void SetReveal(float value, bool isPinned)
        {
            if (Mathf.Abs(value - reveal) < .0001f && pinned == isPinned) return;
            reveal = value; pinned = isPinned; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float rise = Mathf.Lerp(83, 0, reveal);
            var sleeve = new Color(.12f, .17f, .21f, 1);
            var lit = new Color(.29f, .36f, .41f, 1);
            var metal = new Color(.36f, .44f, .49f, 1);
            var dark = new Color(.025f, .055f, .075f, 1);
            var cyan = new Color(.29f, .89f, 1, 1);
            // Draw order: projection, suit, then instrument panels.
            if (reveal > .01f)
            {
                float left = pinned ? -500 : 12, right = pinned ? 180 : 595;
                Tri(mesh, new Vector2(348, -83 - rise), new Vector2(left, 555), new Vector2(right, 555),
                    new Color(.12f, .72f, .95f, .08f * reveal));
                Line(mesh, new Vector2(340, -82 - rise), new Vector2(left, 555), 1,
                    new Color(.28f, .85f, 1, .17f * reveal));
                Line(mesh, new Vector2(358, -82 - rise), new Vector2(right, 555), 1,
                    new Color(.28f, .85f, 1, .17f * reveal));
            }
            Poly(mesh, sleeve, rise, new Vector2(368, 89), new Vector2(610, 135), new Vector2(610, 215), new Vector2(350, 164));
            Poly(mesh, lit, rise, new Vector2(393, 95), new Vector2(610, 141), new Vector2(610, 156), new Vector2(390, 113));
            Poly(mesh, new Color(.055f, .09f, .12f), rise, new Vector2(408, 145), new Vector2(610, 197), new Vector2(610, 215), new Vector2(404, 164));
            for (int i = 0; i < 6; i++)
            {
                float x = 427 + i * 26;
                Line(mesh, new Vector2(x, -115 - i * 5 - rise), new Vector2(x - 17, -159 - i * 7 - rise), 4, dark);
            }
            // Ribbed cuff and dark screen mount.
            Poly(mesh, dark, rise, new Vector2(329, 67), new Vector2(414, 87), new Vector2(389, 172), new Vector2(306, 148));
            Poly(mesh, metal, rise, new Vector2(330, 72), new Vector2(403, 90), new Vector2(384, 160), new Vector2(310, 140));
            Poly(mesh, cyan, rise, new Vector2(329, 77), new Vector2(401, 94), new Vector2(398, 103), new Vector2(326, 86));
            Poly(mesh, new Color(.10f, .17f, .22f), rise, new Vector2(330, 91), new Vector2(392, 107), new Vector2(383, 142), new Vector2(319, 125));
            Poly(mesh, new Color(.02f, .07f, .10f), rise, new Vector2(327, 74), new Vector2(379, 86), new Vector2(367, 120), new Vector2(315, 108));
            Poly(mesh, new Color(.15f, .63f, .76f), rise, new Vector2(333, 81), new Vector2(368, 89), new Vector2(365, 98), new Vector2(329, 90));
            for (int i = 0; i < 4; i++)
                Line(mesh, new Vector2(330 + i * 10, -100 - i * 2 - rise), new Vector2(335 + i * 10, -101 - i * 2 - rise), 2, cyan);
            // Articulated glove.
            Poly(mesh, new Color(.16f, .22f, .27f), rise, new Vector2(252, 76), new Vector2(328, 87), new Vector2(309, 144), new Vector2(246, 151), new Vector2(212, 122));
            Poly(mesh, new Color(.31f, .37f, .41f), rise, new Vector2(255, 82), new Vector2(314, 92), new Vector2(305, 119), new Vector2(250, 126), new Vector2(230, 111));
            for (int i = 0; i < 4; i++)
            {
                float y = 83 + i * 15, end = 145 + i * 8;
                Poly(mesh, new Color(.20f, .27f, .32f), rise, new Vector2(249, y), new Vector2(end + 12, y - 8), new Vector2(end, y + 2), new Vector2(end + 5, y + 12), new Vector2(248, y + 13));
                Line(mesh, new Vector2(202, -y - 1 - rise), new Vector2(199, -y - 9 - rise), 3, dark);
                Line(mesh, new Vector2(228, -y - 1 - rise), new Vector2(225, -y - 11 - rise), 2, lit);
            }
            Poly(mesh, lit, rise, new Vector2(267, 128), new Vector2(242, 148), new Vector2(211, 157), new Vector2(203, 147), new Vector2(230, 125));
            // Animate the emitter on UI time.
            float brightness = Mathf.Lerp(.22f, 1, reveal);
            Poly(mesh, new Color(cyan.r, cyan.g, cyan.b, .15f * brightness), rise, new Vector2(321, 62), new Vector2(369, 72), new Vector2(365, 88), new Vector2(318, 79));
            Line(mesh, new Vector2(329, -72 - rise), new Vector2(361, -79 - rise), 3.6f, cyan * brightness);
        }
        private void Poly(VertexHelper mesh, Color color, float rise, params Vector2[] points)
        {
            int start = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++) mesh.AddVert(new Vector2(points[i].x, -points[i].y - rise), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
        }
        private static void Tri(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            int start = mesh.currentVertCount;
            Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized * thickness * .5f;
            mesh.AddVert(a - n, color, Vector2.zero); mesh.AddVert(a + n, color, Vector2.zero);
            mesh.AddVert(b + n, color, Vector2.zero); mesh.AddVert(b - n, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
