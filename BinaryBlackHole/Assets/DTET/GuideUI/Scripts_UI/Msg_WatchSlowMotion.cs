
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_WatchSlowMotion : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;

    [Header("Question Timing")]
    public float visibleDuration = 20f;
    public float fadeDuration = 1f;

    private VisualElement questionRoot;
    private Coroutine questionCoroutine;
    private bool hasShown;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_WatchSlowMotion: No UIDocument found."
            );
            enabled = false;
            return;
        }

        questionRoot = uiDocument.rootVisualElement
            .Q<VisualElement>("watchSlowMotion");

        if (questionRoot == null)
        {
            Debug.LogError(
                "Msg_WatchSlowMotion: Could not find #watchSlowMotion in the UXML."
            );
            enabled = false;
            return;
        }

        questionRoot.style.display = DisplayStyle.None;
        questionRoot.style.opacity = 0f;
    }

    public void ShowQuestion()
    {
        if (questionRoot == null || hasShown)
            return;

        hasShown = true;

        if (questionCoroutine != null)
            StopCoroutine(questionCoroutine);

        questionCoroutine = StartCoroutine(ShowAndFadeQuestion());
    }

    private IEnumerator ShowAndFadeQuestion()
    {
        questionRoot.style.display = DisplayStyle.Flex;
        questionRoot.style.opacity = 1f;

        float elapsed = 0f;

        while (elapsed < visibleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = fadeDuration > 0f
                ? Mathf.Clamp01(elapsed / fadeDuration)
                : 1f;

            questionRoot.style.opacity = 1f - progress;

            yield return null;
        }

        questionRoot.style.opacity = 0f;
        questionRoot.style.display = DisplayStyle.None;

        questionCoroutine = null;
    }
}

