
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_AskGuide : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("AI Chatbot")]
    public Transform aiChatbot;
    public Vector3 chatbotOffset = new Vector3(0.8f, 0f, 0f);

    [Header("Timing")]
    public float visibleDuration = 10f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_MoveCloser moveCloserMessage;

    private VisualElement root;
    private Coroutine messageCoroutine;
    private bool isShowing;
    private bool hasPlayed;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_AskGuide requires a UIDocument on the same GameObject."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement.Q<VisualElement>("askGuide");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #askGuide in Msg_AskGuide.uxml."
            );
            enabled = false;
            return;
        }

        // Wait until the viewpoint sequence finishes.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (aiChatbot == null)
        {
            Debug.LogWarning(
                "Assign the AI Chatbot Transform in the Inspector."
            );
        }
    }

    private void LateUpdate()
    {
        if (!isShowing || aiChatbot == null)
            return;

        // Position the UI beside the AI chatbot.
        transform.position =
            aiChatbot.position +
            aiChatbot.TransformDirection(chatbotOffset);

        // Match the chatbot's rotation.
        transform.rotation = aiChatbot.rotation;
    }

    // Called when all required viewpoints are completed.
    public void ShowAskGuide()
    {
        if (root == null || hasPlayed)
            return;

        hasPlayed = true;
        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;
        isShowing = true;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            root.style.opacity = 1f - progress;

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        if (moveCloserMessage != null)
        {
            moveCloserMessage.ShowMoveCloser();
        }

        isShowing = false;
        messageCoroutine = null;
    }
}

