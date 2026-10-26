

using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_AnswerWrong : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private UIDocument uiDocument;
    private VisualElement root;
    private Coroutine uiCoroutine;

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_AnswerWrong: No UIDocument found."
            );
            enabled = false;
            return;
        }

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError(
                "Msg_AnswerWrong: VR camera not found. " +
                "Assign your VR camera in the Inspector."
            );
            enabled = false;
            return;
        }

        // Attach the UI to the VR camera.
        transform.SetParent(vrCamera.transform, false);
        transform.localPosition =
            new Vector3(0f, 0f, distanceFromCamera);
        transform.localRotation = Quaternion.identity;

        root = uiDocument.rootVisualElement;

        // Start hidden.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
    }

    // Call this ONLY when the AI confirms the answer is wrong.
    public void ShowWrongAnswerMessage()
    {
        if (root == null)
        {
            Debug.LogWarning(
                "Msg_AnswerWrong: UI is not ready yet."
            );
            return;
        }

        // Cancel the previous timer or fade.
        if (uiCoroutine != null)
        {
            StopCoroutine(uiCoroutine);
            uiCoroutine = null;
        }

        // Show the message and restart its timer.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        uiCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        // Keep the message visible.
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

        // Hide completely.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        uiCoroutine = null;
    }

    // Optional: hide the message immediately.
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
