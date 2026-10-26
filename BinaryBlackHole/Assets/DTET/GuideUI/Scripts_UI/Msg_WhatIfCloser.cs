
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_WhatIfCloser : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;

    [Header("Question Timing")]
    public float visibleDuration = 20f;
    public float fadeDuration = 1f;

    private VisualElement questionRoot;
    private Coroutine questionCoroutine;
    private bool hasShown;
    private bool isReady;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_WhatIfCloser: No UIDocument found on this GameObject."
            );
            enabled = false;
            return;
        }

        if (uiDocument.visualTreeAsset == null)
        {
            Debug.LogError(
                "Msg_WhatIfCloser: No Source Asset assigned to the UIDocument."
            );
            enabled = false;
            return;
        }

        Debug.Log(
            "Msg_WhatIfCloser: Assigned UXML = " +
            uiDocument.visualTreeAsset.name
        );

        // Try finding the element now.
        TryFindQuestionRoot();
    }

    private void Update()
    {
        // Retry if the element wasn't available at Start.
        if (!isReady)
            TryFindQuestionRoot();
    }

    private void TryFindQuestionRoot()
    {
        if (uiDocument == null)
            return;

        VisualElement documentRoot = uiDocument.rootVisualElement;

        if (documentRoot == null)
            return;

        questionRoot =
            documentRoot.Q<VisualElement>("whatIfCloser");

        if (questionRoot == null)
            return;

        // Initialize only once the element has been found.
        questionRoot.style.display = DisplayStyle.None;
        questionRoot.style.opacity = 0f;

        isReady = true;

        Debug.Log(
            "Msg_WhatIfCloser: Successfully found 'whatIfCloser'."
        );
    }

    public void ShowQuestion()
    {
        if (!isReady || questionRoot == null)
        {
            Debug.LogWarning(
                "Msg_WhatIfCloser: ShowQuestion called before " +
                "'whatIfCloser' was found."
            );
            return;
        }

        // Show this question only once.
        if (hasShown)
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

        yield return new WaitForSecondsRealtime(visibleDuration);

        float elapsed = 0f;

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
