
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_ChirpNotPicture : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Message Settings")]
    public string messageTitle = "A Chirp, Not a Picture";
    public string messageText =
        "Listen to the signal and observe how it changes over time.";

    [Header("Timing Settings")]
    public float visibleDuration = 4f;
    public float fadeDuration = 1f;
    [Header("Next Message")]
    public Msg_PointTask pointTaskMessage;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private Coroutine messageCoroutine;
    private bool hasPlayed;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_ChirpNotPicture: No UIDocument found."
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

        // Hide until Stage 5 begins.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 1f;

        // Optional: position the UI in front of the VR camera.
        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }
    }

    // Call this when Stage 5 begins.
    public void ShowChirpNotPicture()
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

        if (pointTaskMessage != null)
        {
            pointTaskMessage.BeginWaitingForBodiesToStop();
        }
    }
}
