using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.Guide
{
    /// <summary>
    /// One display-only pop-up. It cannot be clicked or pointed at (no raycaster, no raycast targets).
    /// Show / Hide / ShowFor are all it does - when to call them is decided elsewhere.
    /// </summary>
    public sealed class GuidePopup : MonoBehaviour
    {
        private Text titleText;
        private Text bodyText;
        private GuideFader fader;
        private GuidePlacement placement;
        private Coroutine hideRoutine;

        public GuideMessageSpec Spec { get; private set; }
        public GuideMessageId Id { get { return Spec.Id; } }
        public bool IsVisible { get { return fader.IsVisible; } }
        /// <summary>Change Mode / Offset / Target here to move this pop-up somewhere else.</summary>
        public GuidePlacement Placement { get { return placement; } }

        public static GuidePopup Create(GuideMessageSpec spec, Transform parent, float fadeSeconds)
        {
            Vector2 size = new Vector2(spec.Width, spec.Height);
            Canvas canvas = GuideUiStyle.WorldCanvas("Guide - " + spec.Id, parent, size + Vector2.one * 4f, spec.Scale);

            GuidePopup popup = canvas.gameObject.AddComponent<GuidePopup>();
            popup.Spec = spec;
            popup.fader = canvas.gameObject.AddComponent<GuideFader>();
            popup.fader.Init(canvas, fadeSeconds);
            popup.placement = canvas.gameObject.AddComponent<GuidePlacement>();
            popup.placement.Mode = spec.Anchor;
            popup.placement.Offset = spec.Offset;
            popup.placement.Smoothing = spec.Anchor == GuideAnchorMode.HeadLocked ? 5f : 8f;
            popup.Build(canvas.transform, size);
            popup.placement.Snap();
            return popup;
        }

        private void Build(Transform canvasTransform, Vector2 size)
        {
            GuideMessageSpec spec = Spec;
            RectTransform card = GuideUiStyle.Card(canvasTransform, spec.Id.ToString(), size, GuideUiStyle.Panel);

            if (spec.Kind == GuideKind.Tag)
            {
                bodyText = GuideUiStyle.Label(card, spec.Body, 22, GuideUiStyle.White, 10, 4, size.x - 20f, size.y - 8f,
                    TextAnchor.MiddleCenter, true);
                return;
            }

            Color kindColor = GuideUiStyle.KindColor(spec.Kind);

            if (spec.Kind == GuideKind.Success)
            {
                GuideUiStyle.Box(card, "Success tint", Vector2.zero, size,
                    new Color(GuideUiStyle.Accent.r, GuideUiStyle.Accent.g, GuideUiStyle.Accent.b, 0.16f));
            }

            GuideUiStyle.Box(card, "Accent bar", new Vector2(10f, -12f), new Vector2(6f, size.y - 24f), kindColor);

            float bodyY = 12f;
            if (!string.IsNullOrEmpty(spec.Title))
            {
                titleText = GuideUiStyle.Label(card, spec.Title, 15, kindColor, 30f, 10f, size.x - 50f, 24f);
                bodyY = 36f;
            }

            bodyText = GuideUiStyle.Label(card, spec.Body, 26, GuideUiStyle.White, 30f, bodyY, size.x - 50f,
                size.y - bodyY - 12f, TextAnchor.MiddleLeft, true);
        }

        // ---------- the whole public API ----------

        /// <summary>Fade in. If the spec has HoldSeconds (e.g. 3 s feedback messages) it hides itself afterwards.</summary>
        public void Show()
        {
            Display(Spec.HoldSeconds);
        }

        /// <summary>Fade in with different text, then behave like Show().</summary>
        public void Show(string body)
        {
            SetBody(body);
            Show();
        }

        /// <summary>Fade in and hide again after the given number of seconds.</summary>
        public void ShowFor(float seconds)
        {
            Display(seconds);
        }

        public void Hide(bool instant = false)
        {
            CancelHide();
            fader.Set(false, instant);
        }

        public void SetBody(string text)
        {
            if (bodyText != null)
            {
                bodyText.text = text;
            }
        }

        public void SetTitle(string text)
        {
            if (titleText != null)
            {
                titleText.text = text;
            }
        }

        private void Display(float holdSeconds)
        {
            CancelHide();
            if (!fader.IsVisible)
            {
                placement.Snap();
            }

            fader.Set(true);

            if (holdSeconds > 0f)
            {
                hideRoutine = StartCoroutine(HideAfter(holdSeconds));
            }
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            hideRoutine = null;
            fader.Set(false);
        }

        private void CancelHide()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
        }
    }
}
