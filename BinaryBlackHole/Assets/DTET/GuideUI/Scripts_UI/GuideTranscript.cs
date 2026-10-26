
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GuideTranscript : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Position")]
    public float distanceFromCamera = 2f;

    [Header("Display Settings")]
    public float visibleDuration = 15f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label line1;
    private Label line2;
    private Label line3;

    private Coroutine displayCoroutine;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError("GuideTranscript: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        line1 = root.Q<Label>("line1");
        line2 = root.Q<Label>("line2");
        line3 = root.Q<Label>("line3");

        if (line1 == null || line2 == null || line3 == null)
        {
            Debug.LogError(
                "GuideTranscript: Could not find line1, line2, or line3. " +
                "Check the element names in your UXML.");

            enabled = false;
            return;
        }

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

    // Call when the user sends a message and the AI responds.
    public void ShowConversation(string userInput, string aiResponse)
    {
        if (root == null)
            return;

        line1.text = "You: " + userInput;
        line2.text = "AI: " + aiResponse;
        line3.text = "";

        RestartDisplayTimer();
    }

    // Optional: display a conversation across all three lines.
    public void ShowThreeLines(string firstLine, string secondLine,
        string thirdLine)
    {
        if (root == null)
            return;

        line1.text = firstLine;
        line2.text = secondLine;
        line3.text = thirdLine;

        RestartDisplayTimer();
    }

    private void RestartDisplayTimer()
    {
        if (displayCoroutine != null)
            StopCoroutine(displayCoroutine);

        displayCoroutine = StartCoroutine(DisplayAndFade());
    }

    private IEnumerator DisplayAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSeconds(visibleDuration);

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
