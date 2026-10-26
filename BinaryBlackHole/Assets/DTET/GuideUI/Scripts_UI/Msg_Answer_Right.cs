
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_Answer_Right : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Timing")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Coroutine messageCoroutine;

    void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_Answer_Right requires a UIDocument on the same GameObject."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("answerRight");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #answerRight in Msg_AnswerRight.uxml."
            );
            enabled = false;
            return;
        }

        // Keep the message hidden until the AI confirms a correct answer.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
    }

    public void ShowCorrectAnswerMessage()
    {
        if (root == null)
            return;

        // Restart the display timer if another correct answer arrives.
        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

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
    }
}

