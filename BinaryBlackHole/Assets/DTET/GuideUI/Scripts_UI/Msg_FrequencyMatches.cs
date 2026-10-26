
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_FrequencyMatches : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Message Settings")]
    public string messageTitle = "Frequency Matches";
    public string messageText =
        "The objects' frequencies are matching.";

    [Header("Timing Settings")]
    public float delayBeforeShowing = 3f;
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;
    private Coroutine messageCoroutine;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_FrequencyMatches: No UIDocument found.");
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

        // Hide until the delay has elapsed.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 1f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        // Optional: place the UI in front of the VR camera.
        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }
    }

    // Call this when Stage 3 begins.
    public void StartFrequencyMessage()
    {
        if (root == null || messageCoroutine != null)
            return;

        messageCoroutine = StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delayBeforeShowing);

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(visibleDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = fadeDuration > 0f
                ? Mathf.Clamp01(elapsed / fadeDuration)
                : 1f;

            root.style.opacity = 1f - progress;

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        messageCoroutine = null;
    }
}
