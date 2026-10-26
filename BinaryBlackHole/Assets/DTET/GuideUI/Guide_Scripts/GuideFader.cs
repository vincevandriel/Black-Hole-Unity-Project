using UnityEngine;

namespace DTET.Guide
{
    /// <summary>
    /// Fades one world-space canvas in and out. Uses unscaled time so it also works while the
    /// simulation is paused. The canvas is switched off completely when invisible.
    /// </summary>
    public sealed class GuideFader : MonoBehaviour
    {
        private Canvas canvas;
        private CanvasGroup group;
        private float target;
        private float seconds = 0.25f;

        public bool IsVisible { get { return target > 0.5f; } }
        public bool IsFading { get { return group != null && !Mathf.Approximately(group.alpha, target); } }

        public void Init(Canvas worldCanvas, float fadeSeconds)
        {
            canvas = worldCanvas;
            group = worldCanvas.GetComponent<CanvasGroup>();
            seconds = Mathf.Max(0.01f, fadeSeconds);
            group.alpha = 0f;
            canvas.enabled = false;
        }

        public void Set(bool visible, bool instant = false)
        {
            target = visible ? 1f : 0f;
            if (visible)
            {
                canvas.enabled = true;
            }

            if (instant)
            {
                group.alpha = target;
                canvas.enabled = visible;
            }
        }

        private void Update()
        {
            if (group == null || !IsFading)
            {
                return;
            }

            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / seconds);
            canvas.enabled = group.alpha > 0.001f || target > 0.5f;
        }
    }
}
