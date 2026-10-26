
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_OutOfRange : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Message Settings")]
    public string messageTitle = "Value Out of Range";
    public string messageText =
        "Please enter a value within the allowed range.";

    [Header("Fade Settings")]
    public float fadeDuration = 1f;

    private Label titleLabel;
    private Label bodyLabel;
    private VisualElement root;

    private Coroutine fadeCoroutine;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_OutOfRange: No UIDocument found."
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

        // Hide the warning when the scene starts.
        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        // Position the UI in front of the VR camera.
        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition =
                new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning(
                "Msg_OutOfRange: No VR camera found. " +
                "The UI will not be attached to the camera."
            );
        }
    }

    /// <summary>
    /// Returns false if the value or range is invalid,
    /// or if the value is outside the allowed range.
    /// Returns true if the value is valid.
    /// The minimum and maximum values are inclusive.
    /// </summary>
    public bool ValidateValue(
        float value,
        float minValue,
        float maxValue)
    {
        // Check that the range is valid and finite.
        if (float.IsNaN(minValue) ||
            float.IsInfinity(minValue) ||
            float.IsNaN(maxValue) ||
            float.IsInfinity(maxValue) ||
            minValue > maxValue)
        {
            Debug.LogError(
                "Msg_OutOfRange: The minimum and maximum " +
                "values must be finite, and minimum must " +
                "not exceed maximum."
            );

            ShowMessage(
                "The allowed parameter range is invalid."
            );

            return false;
        }

        // Reject NaN and infinity.
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            ShowMessage(
                "Please enter a valid finite number."
            );

            return false;
        }

        // Check whether the value is outside the range.
        if (value < minValue || value > maxValue)
        {
            ShowMessage(
                $"Please enter a value between " +
                $"{minValue} and {maxValue}."
            );

            return false;
        }

        // The value is valid, so hide the warning.
        HideMessage();

        return true;
    }

    public void ShowMessage(string message)
    {
        if (root == null)
            return;

        // Cancel any fade already in progress.
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (titleLabel != null)
            titleLabel.text = messageTitle;

        if (bodyLabel != null)
            bodyLabel.text = message;

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;
    }

    public void HideMessage()
    {
        if (root == null ||
            root.style.display == DisplayStyle.None)
            return;

        // Avoid running multiple fade coroutines.
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // Hide immediately if fading is disabled.
        if (fadeDuration <= 0f)
        {
            root.style.opacity = 0f;
            root.style.display = DisplayStyle.None;
            return;
        }

        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float startingOpacity = 1f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / fadeDuration
            );

            root.style.opacity = Mathf.Lerp(
                startingOpacity,
                0f,
                progress
            );

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        fadeCoroutine = null;
    }
}
