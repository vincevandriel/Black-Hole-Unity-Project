
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GuidePopUp : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float visibleDuration = 15f;
    public float fadeDuration = 1f;

    [Header("Text Settings")]
    public string titleText = "AI Guide";

    private UIDocument uiDocument;
    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private Coroutine uiCoroutine;

    private void Start()
    {
        // Find the UIDocument.
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "GuidePopUp: No UIDocument found on this GameObject."
            );
            enabled = false;
            return;
        }

        // Find the VR camera.
        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError(
                "GuidePopUp: VR camera not found. " +
                "Assign your VR camera in the Inspector."
            );
            enabled = false;
            return;
        }

        // Attach the popup to the VR camera.
        transform.SetParent(vrCamera.transform, false);
        transform.localPosition =
            new Vector3(0f, 0f, distanceFromCamera);
        transform.localRotation = Quaternion.identity;

        // Get the UI root and labels.
        root = uiDocument.rootVisualElement;
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (bodyLabel == null)
        {
            Debug.LogError(
                "GuidePopUp: Could not find #bodyLabel in the UXML."
            );
            enabled = false;
            return;
        }

        if (titleLabel != null)
            titleLabel.text = titleText;

        // Start hidden.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Call when the AI produces new transcript text.
    /// </summary>
    public void ShowTranscript(string transcript)
    {
        if (root == null || bodyLabel == null)
            return;

        // Cancel any previous timer or fade.
        if (uiCoroutine != null)
        {
            StopCoroutine(uiCoroutine);
            uiCoroutine = null;
        }

        // Update the transcript and heading.
        bodyLabel.text = transcript;

        if (titleLabel != null)
            titleLabel.text = titleText;

        // Show the popup again with a fresh timer.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        uiCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        // Keep the popup visible for the configured duration.
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        // Hide immediately if fading is disabled.
        if (fadeDuration <= 0f)
        {
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.None;
            uiCoroutine = null;
            yield break;
        }

        // Fade out smoothly.
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / fadeDuration
            );

            root.style.opacity = 1f - progress;

            yield return null;
        }

        // Hide the popup completely.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        uiCoroutine = null;
    }

    /// <summary>
    /// Hides the popup immediately.
    /// </summary>
    public void HideImmediately()
    {
        if (uiCoroutine != null)
        {
            StopCoroutine(uiCoroutine);
            uiCoroutine = null;
        }

        if (root != null)
        {
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.None;
        }
    }
}
