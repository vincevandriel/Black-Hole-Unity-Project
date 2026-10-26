
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_LookAround : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;
    public Msg_WalkToBlackHoles nextMessage;

    [Header("Message Settings")]
    public string messageTitle = "Look Around";
    public string messageText =
        "Take a moment to look around and explore your surroundings.";

    [Header("Timing Settings")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private Coroutine messageCoroutine;

    private void Start()
    {
        // Get the UI Document.
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_LookAround: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        // Find the title and body labels in the UXML.
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel != null)
            titleLabel.text = messageTitle;

        if (bodyLabel != null)
            bodyLabel.text = messageText;

        // Find the VR camera if not assigned.
        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera != null)
        {
            // Attach the UI to the VR camera.
            transform.SetParent(vrCamera.transform, false);

            // Position it in front of the user's view.
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning(
                "Msg_LookAround: VR camera not found. " +
                "Assign the camera in the Inspector."
            );
        }

        // Show the first popup automatically when the program starts.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        // Automatically start the timer and fade-out.
        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        // Keep the message visible.
        yield return new WaitForSecondsRealtime(visibleDuration);

        // Fade out gradually.
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

        // Hide the message completely.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        messageCoroutine = null;
        if (nextMessage != null)
        {
            nextMessage.ShowMessage();
        }
        else
        {
            Debug.LogWarning(
                "Msg_LookAround: Next Message is not assigned."
            );

        }
    }
}