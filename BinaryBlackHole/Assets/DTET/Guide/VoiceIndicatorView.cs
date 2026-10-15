using UnityEngine;
using UnityEngine.UI;

namespace DTET.Guide
{
    /// <summary>
    /// Small animated bars shown while the AI guide speaks. Display only: call SetSpeaking(true/false).
    /// Without a real audio level the bars move with smooth noise; SetLevel(0..1) can drive them from audio later.
    /// </summary>
    public sealed class VoiceIndicatorView : MonoBehaviour
    {
        private const int BarCount = 5;

        private readonly RectTransform[] bars = new RectTransform[BarCount];
        private GuideFader fader;
        private GuidePlacement placement;
        private bool speaking;
        private float level = -1f;

        public bool IsSpeaking { get { return speaking; } }
        public GuidePlacement Placement { get { return placement; } }

        public static VoiceIndicatorView Create(Transform parent, float fadeSeconds)
        {
            Vector2 size = new Vector2(150f, 70f);
            Canvas canvas = GuideUiStyle.WorldCanvas("Guide - Voice indicator", parent, size + Vector2.one * 4f, 0.002f);

            VoiceIndicatorView view = canvas.gameObject.AddComponent<VoiceIndicatorView>();
            view.fader = canvas.gameObject.AddComponent<GuideFader>();
            view.fader.Init(canvas, fadeSeconds);
            view.placement = canvas.gameObject.AddComponent<GuidePlacement>();
            view.placement.Mode = GuideAnchorMode.HeadLocked;
            view.placement.Offset = new Vector3(0f, -0.38f, 1.6f);

            RectTransform card = GuideUiStyle.Card(canvas.transform, "Voice indicator", size, GuideUiStyle.Panel);
            float spacing = size.x / (BarCount + 1);
            for (int i = 0; i < BarCount; i++)
            {
                RectTransform bar = GuideUiStyle.Box(card, "Bar " + (i + 1), Vector2.zero, new Vector2(10f, 44f), GuideUiStyle.Accent);
                bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = new Vector2(spacing * (i + 1), 0f);
                view.bars[i] = bar;
            }

            view.placement.Snap();
            return view;
        }

        /// <summary>Show the bars while the guide talks, hide them when it stops.</summary>
        public void SetSpeaking(bool isSpeaking)
        {
            speaking = isSpeaking;
            if (isSpeaking)
            {
                placement.Snap();
            }
            else
            {
                level = -1f;
            }

            fader.Set(isSpeaking);
        }

        /// <summary>Optional: real loudness 0..1 from the audio source.</summary>
        public void SetLevel(float value)
        {
            level = Mathf.Clamp01(value);
        }

        private void Update()
        {
            if (!speaking)
            {
                return;
            }

            for (int i = 0; i < BarCount; i++)
            {
                float noise = Mathf.PerlinNoise(Time.unscaledTime * 7f, i * 3.1f);
                float value = level >= 0f ? Mathf.Clamp01(level * (0.6f + 0.8f * noise)) : noise;
                Vector3 scale = bars[i].localScale;
                scale.y = Mathf.Lerp(0.25f, 1f, value);
                bars[i].localScale = scale;
            }
        }
    }
}
