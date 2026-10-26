
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_SymmetryTask : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Timing")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_WhichMergesFirst whichMergesFirstMessage;
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
                "Msg_SymmetryTask requires a UIDocument on the same GameObject."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("symmetryTask");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #sdymmetryTask in Msg_SymmetryTask.uxml. " +
                "Check the spelling of the UXML name."
            );
            enabled = false;
            return;
        }

        // Hide the task until Stage 6 calls ShowSymmetryTask().
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
    }

    public void ShowSymmetryTask()
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

            if (whichMergesFirstMessage != null)
            whichMergesFirstMessage.ShowQuestion();
    }
}

