
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_WalkAround : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Next Message")]
    public Msg_OrbitIntro orbitIntroMessage;

    [Header("Message Timing")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private bool hasStarted;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_WalkAround requires a UIDocument.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("walkAround");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #walkAround in Msg_WalkAround.uxml."
            );
            enabled = false;
            return;
        }

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;
    }

    public void ShowWalkAroundMessage()
    {
        if (root == null || hasStarted)
            return;

        hasStarted = true;
        StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        // Show Walk Around.
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

        // Hide completely.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        // Start Orbit Introduction after Walk Around disappears.
        if (orbitIntroMessage != null)
        {
            orbitIntroMessage.ShowOrbitIntro();
        }
    }
}


