
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_WhichMergesFirst : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Timing")]
    public float visibleDuration = 20f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_FinalCongratulations finalCongratulationsMessage;

    private VisualElement root;
    private Coroutine messageCoroutine;
    private bool hasPlayed;

    void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_WhichMergesFirst requires a UIDocument on the same GameObject."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("whichMergesFirst");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #whichMergesFirst in the UXML. " +
                "Check the root element name."
            );
            enabled = false;
            return;
        }

        // Keep the question hidden until Stage 6 triggers it.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
    }

    public void ShowQuestion()
    {
        if (root == null || hasPlayed)
            return;

        hasPlayed = true;

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        yield return new WaitForSeconds(visibleDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            root.style.opacity = 1f -
                Mathf.Clamp01(elapsed / fadeDuration);

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        messageCoroutine = null;

        if (finalCongratulationsMessage != null)
            finalCongratulationsMessage.ShowFinalCongratulations();
    }
}
