
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_WalkToBlackHoles : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    private UIDocument uiDocument;
    private VisualElement root;
    private Coroutine messageCoroutine;
    private bool hasPlayed;

    private void Start()
    {
        // Get the UI Document component.
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_WalkToBlackHoles: No UIDocument found on this GameObject."
            );
            enabled = false;
            return;
        }

        // Find the VR camera if not assigned.
        if (vrCamera == null)
        {
            vrCamera = Camera.main;
        }

        if (vrCamera == null)
        {
            Debug.LogError(
                "Msg_WalkToBlackHoles: VR camera not found. " +
                "Assign your VR camera."
            );
            enabled = false;
            return;
        }

        // Attach the UI Document to the VR camera.
        transform.SetParent(vrCamera.transform, false);

        // Position the UI in front of the user's view.
        transform.localPosition =
            new Vector3(0f, 0f, distanceFromCamera);

        transform.localRotation = Quaternion.identity;

        // Get the entire UI Toolkit document.
        root = uiDocument.rootVisualElement;

        // Keep this popup hidden until the first message finishes.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
    }

    // Called by Msg_LookAround after its message fades out.
    public void ShowMessage()
    {
        if (root == null || hasPlayed)
            return;

        hasPlayed = true;

        // Show the popup at full opacity.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        // Start the visibility timer and fade-out.
        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        // Keep the message visible.
        yield return new WaitForSecondsRealtime(visibleDuration);

        // Fade out the entire UI.
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = fadeDuration > 0f
                ? Mathf.Clamp01(elapsed / fadeDuration)
                : 1f;

            float alpha = Mathf.Lerp(1f, 0f, progress);

            root.style.opacity = alpha;

            yield return null;
        }

        // Completely hide the UI.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        messageCoroutine = null;
    }
}
