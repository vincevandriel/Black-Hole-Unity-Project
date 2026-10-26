
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_CloserFaster : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Message Settings")]
    public string messageTitle = "Closer and Faster";
    public string messageText =
        "The objects are getting closer together.";

    [Header("Timing Settings")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_WhatIfCloser nextMessage;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private bool hasTriggered;
    private Coroutine messageCoroutine;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_CloserFaster: No UIDocument found."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel != null)
            titleLabel.text = messageTitle;

        if (bodyLabel != null)
            bodyLabel.text = messageText;

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }
    }

    // Called by Msg_SeparationShrinking after it finishes fading.
    public void ShowMessage()
    {
        if (root == null || hasTriggered)
            return;

        hasTriggered = true;
        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = duration > 0f
                ? Mathf.Clamp01(elapsed / duration)
                : 1f;

            root.style.opacity = 1f - progress;

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        messageCoroutine = null;

        // Trigger the next UI only after this message has faded.
        if (nextMessage != null)
        {
            nextMessage.ShowQuestion();
        }
    }
    }

