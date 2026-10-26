using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_LearningBoardTag : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Target Location")]
    public Transform targetLocation;
    public float triggerDistance = 2f;

    [Header("Tag Settings")]
    public string tagText = "Learning Board";

    [Header("Timing Settings")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label tagLabel;

    private bool hasTriggered;
    private Coroutine messageCoroutine;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_LearningBoardTag: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        tagLabel = root.Q<Label>("tagLabel");

        if (tagLabel != null)
            tagLabel.text = tagText;
        else
            Debug.LogWarning(
                "Msg_LearningBoardTag: Could not find Label 'tagLabel'."
            );

        root.style.display = DisplayStyle.None;
        root.style.opacity = 1f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        // Optional: place the tag in front of the VR camera.
        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }

        if (targetLocation == null)
        {
            Debug.LogError(
                "Msg_LearningBoardTag: Assign Target Location in the Inspector."
            );
            enabled = false;
        }
    }

    private void Update()
    {
        if (hasTriggered || vrCamera == null || targetLocation == null)
            return;

        float distance = Vector3.Distance(
            vrCamera.transform.position,
            targetLocation.position
        );

        if (distance <= triggerDistance)
        {
            hasTriggered = true;
            messageCoroutine = StartCoroutine(ShowAndFade());
        }
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(visibleDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = fadeDuration > 0f
                ? Mathf.Clamp01(elapsed / fadeDuration)
                : 1f;

            root.style.opacity = 1f - progress;

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        messageCoroutine = null;
    }
}

