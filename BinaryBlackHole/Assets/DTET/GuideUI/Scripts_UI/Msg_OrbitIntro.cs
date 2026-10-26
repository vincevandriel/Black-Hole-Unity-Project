
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_OrbitIntro : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Message Settings")]
    public string messageTitle = "Orbit Introduction";
    public string messageText = "Welcome to the next stage.";

    [Header("Timing Settings")]
    public float visibleDuration = 4f;
    public float fadeDuration = 1f;

    [Header("Next Stage")]
    public Msg_TryAnotherViewpoint nextViewpointMessage;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;
    private bool hasPlayed;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_OrbitIntro: No UIDocument found.");
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

        // Hide until Msg_WalkAround finishes.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError("Msg_OrbitIntro: VR camera not found.");
            enabled = false;
            return;
        }

        // Position the UI in front of the VR camera.
        transform.SetParent(vrCamera.transform, false);
        transform.localPosition = new Vector3(0f, 0f, 2f);
        transform.localRotation = Quaternion.identity;
    }

    public void ShowOrbitIntro()
    {
        if (root == null || hasPlayed)
            return;

        hasPlayed = true;
        StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        // Show the message.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        // Fade out.
        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            root.style.opacity = 1f - progress;

            yield return null;
        }

        // Hide permanently.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        if (nextViewpointMessage != null)
        {
            nextViewpointMessage.StartViewpointSequence();
        }
    }
}
