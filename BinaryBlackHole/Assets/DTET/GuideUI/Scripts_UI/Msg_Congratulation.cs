
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_Congratulations : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Position")]
    public float distanceFromCamera = 2f;

    [Header("Display Settings")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;
    private Coroutine displayCoroutine;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_Congratulations: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        // Find the labels inside the nested GuidePopup structure.
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel == null || bodyLabel == null)
        {
            Debug.LogError(
                "Msg_Congratulations: Could not find titleLabel or " +
                "bodyLabel. Check the names in GuidePopup.uxml.");
            enabled = false;
            return;
        }

        titleLabel.text = "Well done!";
        bodyLabel.text = "";

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
    }

    private void Start()
    {
        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition =
                new Vector3(0f, 0f, distanceFromCamera);
            transform.localRotation = Quaternion.identity;
        }
    }

    // Call this when the user answers correctly.
    public void ShowCongratulations()
    {
        if (root == null)
            return;

        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);

        displayCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        // Keep the message visible for 3 seconds.
        yield return new WaitForSeconds(visibleDuration);

        // Fade out the entire UI Document.
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float alpha = fadeDuration > 0f
                ? 1f - Mathf.Clamp01(elapsed / fadeDuration)
                : 0f;

            root.style.opacity = alpha;

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        displayCoroutine = null;
    }
}
