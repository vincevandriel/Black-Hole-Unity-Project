
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_LearningGoals : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Message Settings")]
    public string messageTitle = "Learning Goals";
    public string messageText =
        "Explore the system, observe the objects, and understand how they interact.";

    [Header("Reminder Control")]
    public Msg_TurnToOrbit turnToOrbitMessage;

    [Header("Timing Settings")]
    public float visibleDuration = 10f;
    public float fadeDuration = 1f;
    public Msg_WalkAround walkAroundMessage;

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
            Debug.LogError("Msg_LearningGoals: No UIDocument found.");
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

        // Hide until Msg_Arrived calls ShowLearningGoals().
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError("Msg_LearningGoals: VR camera not found.");
            enabled = false;
            return;
        }

        // Position this UI in front of the VR camera.
        transform.SetParent(vrCamera.transform, false);
        transform.localPosition = new Vector3(0f, 0f, 2f);
        transform.localRotation = Quaternion.identity;
    }

    public void ShowLearningGoals()
    {
        if (root == null || hasPlayed)
            return;

        hasPlayed = true;

        // Stop the reminder permanently.
        if (turnToOrbitMessage != null)
            turnToOrbitMessage.StopPermanently();

        StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = fadeDuration > 0f
                ? Mathf.Clamp01(elapsed / fadeDuration)
                : 1f;

            root.style.opacity = Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        if (walkAroundMessage != null)
        {
            walkAroundMessage.ShowWalkAroundMessage();
        }
    }
}
