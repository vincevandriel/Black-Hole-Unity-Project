
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_SystemsIntro : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Position")]
    public float distanceFromCamera = 2f;

    [Header("Message")]
    public string title = "Comparing Two Systems";
    [TextArea(3, 6)]
    public string message =
        "Observe the two systems and compare how they behave.";

    [Header("Fade Settings")]
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_SymmetryTask symmetryTaskMessage;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;
    private Coroutine fadeCoroutine;
    private bool comparisonActive;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_SystemsIntro: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel == null || bodyLabel == null)
        {
            Debug.LogError(
                "Msg_SystemsIntro: Could not find titleLabel or bodyLabel.");
            enabled = false;
            return;
        }

        titleLabel.text = title;
        bodyLabel.text = message;

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

    // Call when the comparison between the two systems begins.
    public void StartComparison()
    {
        if (root == null)
            return;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        comparisonActive = true;

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;
    }

    // Call when the comparison is finished.
    public void EndComparison()
    {
        if (!comparisonActive || root == null)
            return;

        comparisonActive = false;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
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

        fadeCoroutine = null;
        if (symmetryTaskMessage != null)
        {
            symmetryTaskMessage.ShowSymmetryTask();
        }
    }
}
