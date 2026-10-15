using UnityEngine;
using UnityEngine.UI;

namespace DTET.Guide
{
    /// <summary>
    /// A small name tag for one object (a black hole, the orbit, a data card, the chirp graph...).
    /// Display only: Show() / Hide(). It follows its target and turns toward the user.
    /// </summary>
    public sealed class GuideElementLabel : MonoBehaviour
    {
        private Text text;
        private GuideFader fader;
        private GuidePlacement placement;

        public GuidePlacement Placement { get { return placement; } }
        public bool IsVisible { get { return fader.IsVisible; } }

        /// <summary>target may be null: then 'offset' is used as a fixed world position.</summary>
        public static GuideElementLabel Create(Transform parent, string name, Transform target, Vector3 offset, float fadeSeconds)
        {
            Vector2 size = new Vector2(240f, 64f);
            Canvas canvas = GuideUiStyle.WorldCanvas("Guide - Label " + name, parent, size + Vector2.one * 4f, 0.003f);

            GuideElementLabel label = canvas.gameObject.AddComponent<GuideElementLabel>();
            label.fader = canvas.gameObject.AddComponent<GuideFader>();
            label.fader.Init(canvas, fadeSeconds);
            label.placement = canvas.gameObject.AddComponent<GuidePlacement>();
            label.placement.Mode = target != null ? GuideAnchorMode.FollowTarget : GuideAnchorMode.World;
            label.placement.Target = target;
            label.placement.Offset = offset;

            RectTransform card = GuideUiStyle.Card(canvas.transform, name, size, GuideUiStyle.Panel);
            label.text = GuideUiStyle.Label(card, name, 24, GuideUiStyle.White, 10f, 4f, size.x - 20f, size.y - 8f,
                TextAnchor.MiddleCenter, true);
            label.placement.Snap();
            return label;
        }

        public void SetText(string value)
        {
            text.text = value;
        }

        public void Show()
        {
            placement.Snap();
            fader.Set(true);
        }

        public void Hide()
        {
            fader.Set(false);
        }
    }
}
