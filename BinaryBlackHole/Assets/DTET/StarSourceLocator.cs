using UnityEngine;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // HUD brackets identify the source direction, not its displaced images.
    public sealed class StarSourceLocator : Graphic
    {
        private Vector2 centre;
        private float radius;
        private Text caption;

        public void Initialize(Text label)
        {
            caption = label; raycastTarget = false; color = new Color(1, .79f, .4f, .9f);
        }

        public void Project(Camera camera, Vector3 source, float sourceRadius, bool visible, bool bent)
        {
            Vector3 point = camera.WorldToViewportPoint(source);
            bool onScreen = visible && point.z > 0 && point.x > .04f && point.x < .96f && point.y > .12f && point.y < .88f;
            enabled = onScreen; caption.enabled = onScreen;
            if (!onScreen) return;
            Rect area = rectTransform.rect;
            Vector2 next = new Vector2(area.xMin + point.x * area.width, area.yMin + point.y * area.height);
            float nextRadius = Mathf.Clamp(.5f * area.height * sourceRadius / (point.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f)) + 9, 14, 90);
            if (next != centre || nextRadius != radius) { centre = next; radius = nextRadius; SetVerticesDirty(); }
            caption.text = bent ? "UNBENT SOURCE DIRECTION\nBright displaced images belong to this star" : "SUN-LIKE STAR\n1 solar radius / assumed source";
            float captionX = point.x * area.width + (bent ? -radius : radius + 12);
            float captionY = (1 - point.y) * area.height + (bent ? radius + 18 : -22);
            ReducedExhibitUi.Place(caption.gameObject, Mathf.Clamp(captionX, 8, area.width - 355),
                Mathf.Clamp(captionY, 100, area.height - 150), 345, 52);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector2 corner = centre + new Vector2(x * radius, y * radius);
                    Line(helper, corner, corner - new Vector2(x * 12, 0));
                    Line(helper, corner, corner - new Vector2(0, y * 12));
                }
        }

        private void Line(VertexHelper helper, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * .8f;
            int start = helper.currentVertCount;
            helper.AddVert(a - n, color, Vector2.zero); helper.AddVert(a + n, color, Vector2.zero);
            helper.AddVert(b + n, color, Vector2.zero); helper.AddVert(b - n, color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
