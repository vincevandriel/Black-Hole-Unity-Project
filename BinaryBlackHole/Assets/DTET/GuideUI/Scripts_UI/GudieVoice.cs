
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GuideVoice : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Animation")]
    public float animationSpeed = 8f;
    public float minimumBarHeight = 6f;
    public float maximumBarHeight = 30f;

    [Header("Fade")]
    public float fadeDuration = 1f;

    private VisualElement voiceRoot;
    private VisualElement[] voiceBars;
    private float[] barOffsets;
    private Coroutine fadeCoroutine;

    private bool isSpeaking;

    void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "GuideVoice requires a UIDocument on the same GameObject."
            );
            enabled = false;
            return;
        }

        voiceRoot = uiDocument.rootVisualElement
            .Q<VisualElement>("voiceRoot");

        if (voiceRoot == null)
        {
            Debug.LogError("Could not find #voiceRoot in GuideVoice.uxml.");
            enabled = false;
            return;
        }

        voiceBars = new VisualElement[5];
        barOffsets = new float[5];

        for (int i = 0; i < voiceBars.Length; i++)
        {
            voiceBars[i] = voiceRoot.Q<VisualElement>(
                "voiceBar" + (i + 1)
            );

            if (voiceBars[i] == null)
            {
                Debug.LogError(
                    "Could not find #voiceBar" + (i + 1) +
                    " in GuideVoice.uxml."
                );
                enabled = false;
                return;
            }

            barOffsets[i] = i * 0.8f;
        }

        voiceRoot.style.display = DisplayStyle.None;
        voiceRoot.style.opacity = 0f;
    }

    void Update()
    {
        if (!isSpeaking || voiceBars == null)
            return;

        AnimateBars();
    }

    public void StartSpeaking()
    {
        if (voiceRoot == null)
            return;

        isSpeaking = true;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        voiceRoot.style.display = DisplayStyle.Flex;
        voiceRoot.style.opacity = 1f;
    }

    public void StopSpeaking()
    {
        if (voiceRoot == null || !isSpeaking)
            return;

        isSpeaking = false;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private void AnimateBars()
    {
        for (int i = 0; i < voiceBars.Length; i++)
        {
            float wave = (Mathf.Sin(
                Time.time * animationSpeed + barOffsets[i]
            ) + 1f) * 0.5f;

            float height = Mathf.Lerp(
                minimumBarHeight,
                maximumBarHeight,
                wave
            );

            voiceBars[i].style.height = height;
        }
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float startingOpacity = 1f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float alpha = 1f -
                Mathf.Clamp01(elapsed / fadeDuration);

            voiceRoot.style.opacity = alpha;

            yield return null;
        }

        voiceRoot.style.opacity = 0f;
        voiceRoot.style.display = DisplayStyle.None;
        fadeCoroutine = null;
    }
}

