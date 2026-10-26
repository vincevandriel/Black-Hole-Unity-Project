using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.Guide
{
    /// <summary>
    /// "What was said" panel. Display only: AddLine(speaker, text) adds a line, Show()/Hide()/Toggle() open it.
    /// It always shows the newest lines that fit; older ones scroll out of view by themselves.
    /// </summary>
    public sealed class TranscriptView : MonoBehaviour
    {
        private const int MaxStoredLines = 60;
        private const string GuideColor = "#5EE0D4";
        private const string UserColor = "#9EB3C7";

        private readonly List<string> lines = new List<string>();
        private Text body;
        private float bodyHeight;
        private GuideFader fader;
        private GuidePlacement placement;

        public GuidePlacement Placement { get { return placement; } }
        public bool IsVisible { get { return fader.IsVisible; } }

        public static TranscriptView Create(Transform parent, float fadeSeconds)
        {
            Vector2 size = new Vector2(600f, 380f);
            Canvas canvas = GuideUiStyle.WorldCanvas("Guide - Transcript", parent, size + Vector2.one * 4f, 0.003f);

            TranscriptView view = canvas.gameObject.AddComponent<TranscriptView>();
            view.fader = canvas.gameObject.AddComponent<GuideFader>();
            view.fader.Init(canvas, fadeSeconds);
            view.placement = canvas.gameObject.AddComponent<GuidePlacement>();
            view.placement.Mode = GuideAnchorMode.HeadLocked;
            view.placement.Offset = new Vector3(0f, -0.05f, 2f);
            view.placement.Smoothing = 3f;

            RectTransform card = GuideUiStyle.Card(canvas.transform, "Transcript", size, GuideUiStyle.Panel);
            GuideUiStyle.Label(card, "TRANSCRIPT", 15, GuideUiStyle.Accent, 24f, 14f, 300f, 24f);
            GuideUiStyle.Box(card, "Divider", new Vector2(24f, -44f), new Vector2(size.x - 48f, 2f), new Color(1f, 1f, 1f, 0.12f));

            view.bodyHeight = size.y - 70f;
            view.body = GuideUiStyle.Label(card, string.Empty, 22, GuideUiStyle.White, 24f, 54f, size.x - 48f, view.bodyHeight,
                TextAnchor.UpperLeft);
            view.body.verticalOverflow = VerticalWrapMode.Overflow;
            view.Redraw();
            view.placement.Snap();
            return view;
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

        public void Toggle()
        {
            if (fader.IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>Add one line. Use any speaker name, for example "Guide" or "You".</summary>
        public void AddLine(string speaker, string text)
        {
            string color = string.Equals(speaker, "You", System.StringComparison.OrdinalIgnoreCase) ? UserColor : GuideColor;
            lines.Add("<color=" + color + "><b>" + Safe(speaker) + "</b></color>  " + Safe(text));
            if (lines.Count > MaxStoredLines)
            {
                lines.RemoveAt(0);
            }

            Redraw();
        }

        public void Clear()
        {
            lines.Clear();
            Redraw();
        }

        private void Redraw()
        {
            if (lines.Count == 0)
            {
                body.text = "<color=" + UserColor + ">Nothing said yet.</color>";
                return;
            }

            int first = 0;
            body.text = Join(first);
            while (body.preferredHeight > bodyHeight && first < lines.Count - 1)
            {
                first++;
                body.text = Join(first);
            }
        }

        private string Join(int first)
        {
            return string.Join("\n\n", lines.GetRange(first, lines.Count - first).ToArray());
        }

        // "<" in spoken text must not be read as a rich-text tag.
        private static string Safe(string value)
        {
            return (value ?? string.Empty).Replace("<", "‹").Replace(">", "›");
        }
    }
}
