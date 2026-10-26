
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_TurnToOrbit : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("Required Direction")]
    public Transform requiredDirection;
    public float allowedAngle = 20f;

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float fadeDuration = 0.5f;

    private UIDocument uiDocument;
    private VisualElement root;

    private bool isVisible;
    private bool stoppedPermanently;
    private float currentAlpha;
    private Coroutine fadeCoroutine;

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_TurnToOrbit: No UIDocument found.");
            enabled = false;
            return;
        }

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError("Msg_TurnToOrbit: VR camera not found.");
            enabled = false;
            return;
        }

        if (requiredDirection == null)
        {
            Debug.LogError(
                "Msg_TurnToOrbit: Assign the required viewing direction."
            );
            enabled = false;
            return;
        }

        // Attach this UI to the VR camera.
        transform.SetParent(vrCamera.transform, false);
        transform.localPosition =
            new Vector3(0f, 0f, distanceFromCamera);
        transform.localRotation = Quaternion.identity;

        root = uiDocument.rootVisualElement;

        // Start hidden.
        currentAlpha = 0f;
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        if (root == null || stoppedPermanently)
            return;

        float angle = Vector3.Angle(
            vrCamera.transform.forward,
            requiredDirection.forward
        );

        // Show the reminder when the user looks away.
        bool shouldShow = angle > allowedAngle;

        if (shouldShow != isVisible)
        {
            isVisible = shouldShow;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeUI(isVisible));
        }
    }

    // Called when Learning Goals begins.
    public void StopPermanently()
    {
        if (stoppedPermanently)
            return;

        stoppedPermanently = true;
        isVisible = false;

        if (root == null)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (currentAlpha > 0f)
        {
            fadeCoroutine = StartCoroutine(FadeUI(false));
        }
        else
        {
            currentAlpha = 0f;
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.None;
        }
    }

    private IEnumerator FadeUI(bool show)
    {
        if (show)
            root.style.display = DisplayStyle.Flex;

        float startAlpha = currentAlpha;
        float endAlpha = show ? 1f : 0f;
        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        if (duration == 0f)
        {
            currentAlpha = endAlpha;
            root.style.opacity = endAlpha;

            if (!show)
                root.style.display = DisplayStyle.None;

            fadeCoroutine = null;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);

            currentAlpha = Mathf.Lerp(
                startAlpha,
                endAlpha,
                progress
            );

            root.style.opacity = currentAlpha;

            yield return null;
        }

        currentAlpha = endAlpha;
        root.style.opacity = endAlpha;

        if (!show)
            root.style.display = DisplayStyle.None;

        fadeCoroutine = null;
    }
}
