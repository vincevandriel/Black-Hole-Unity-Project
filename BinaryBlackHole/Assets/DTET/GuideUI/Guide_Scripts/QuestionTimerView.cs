using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DTET.Guide
{
    /// <summary>
    /// Countdown for the guide's questions: m:ss plus a shrinking bar, turns amber for the last seconds.
    /// Begin(seconds) counts down by itself for display; SetRemaining(...) lets other code drive it instead.
    /// </summary>
    public sealed class QuestionTimerView : MonoBehaviour
    {
        private const float WarningSeconds = 5f;

        private Text label;
        private RectTransform fill;
        private Image fillImage;
        private GuideFader fader;
        private GuidePlacement placement;
        private Coroutine running;

        /// <summary>Raised when a Begin() countdown reaches zero (nothing else happens automatically).</summary>
        public event Action Expired;

        public GuidePlacement Placement { get { return placement; } }
        public bool IsVisible { get { return fader.IsVisible; } }

        public static QuestionTimerView Create(Transform parent, float fadeSeconds)
        {
            Vector2 size = new Vector2(220f, 96f);
            Canvas canvas = GuideUiStyle.WorldCanvas("Guide - Question timer", parent, size + Vector2.one * 4f, 0.002f);

            QuestionTimerView view = canvas.gameObject.AddComponent<QuestionTimerView>();
            view.fader = canvas.gameObject.AddComponent<GuideFader>();
            view.fader.Init(canvas, fadeSeconds);
            view.placement = canvas.gameObject.AddComponent<GuidePlacement>();
            view.placement.Mode = GuideAnchorMode.HeadLocked;
            view.placement.Offset = new Vector3(0f, 0.38f, 1.6f);

            RectTransform card = GuideUiStyle.Card(canvas.transform, "Question timer", size, GuideUiStyle.Panel);
            view.label = GuideUiStyle.Label(card, "0:00", 40, GuideUiStyle.White, 10f, 8f, size.x - 20f, 52f, TextAnchor.MiddleCenter);
            GuideUiStyle.Box(card, "Bar background", new Vector2(20f, -68f), new Vector2(size.x - 40f, 12f), new Color(1f, 1f, 1f, 0.12f));
            view.fill = GuideUiStyle.Box(card, "Bar fill", new Vector2(20f, -68f), new Vector2(size.x - 40f, 12f), GuideUiStyle.White);
            view.fillImage = view.fill.GetComponent<Image>();
            view.fillWidth = size.x - 40f;
            view.placement.Snap();
            return view;
        }

        private float fillWidth;

        /// <summary>Start a display countdown and show the timer.</summary>
        public void Begin(float seconds)
        {
            Stop();
            if (seconds <= 0f)
            {
                return;
            }

            placement.Snap();
            fader.Set(true);
            running = StartCoroutine(CountDown(seconds));
        }

        /// <summary>Hide the timer and stop any running countdown.</summary>
        public void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }

            fader.Set(false);
        }

        /// <summary>Drive the display from outside instead of Begin(). Shows the timer if it is hidden.</summary>
        public void SetRemaining(float remaining, float total)
        {
            if (!fader.IsVisible)
            {
                placement.Snap();
                fader.Set(true);
            }

            Render(Mathf.Max(0f, remaining), Mathf.Max(0.01f, total));
        }

        private IEnumerator CountDown(float total)
        {
            float remaining = total;
            while (remaining > 0f)
            {
                Render(remaining, total);
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            Render(0f, total);
            running = null;
            if (Expired != null)
            {
                Expired();
            }

            yield return new WaitForSecondsRealtime(0.8f);
            fader.Set(false);
        }

        private void Render(float remaining, float total)
        {
            int whole = Mathf.CeilToInt(remaining);
            Color color = remaining <= WarningSeconds ? GuideUiStyle.Warning : GuideUiStyle.White;
            label.text = string.Format("{0}:{1:00}", whole / 60, whole % 60);
            label.color = color;
            fillImage.color = color;

            Vector2 size = fill.sizeDelta;
            size.x = fillWidth * Mathf.Clamp01(remaining / total);
            fill.sizeDelta = size;
        }
    }
}
