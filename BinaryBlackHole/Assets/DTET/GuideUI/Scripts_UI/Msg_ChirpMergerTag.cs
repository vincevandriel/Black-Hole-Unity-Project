
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_ChirpMergerTag : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Tag Settings")]
    public string tagText = "Merger Almost Complete!";

    [Header("Position")]
    public float distanceFromCamera = 2f;

    [Header("Timing Settings")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;
    [Header("Next Message")]
    public Msg_ChirpNotPicture nextMessage;

    private VisualElement root;
    private Label tagLabel;
    private Coroutine messageCoroutine;
    private bool hasTriggered;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_ChirpMergerTag: No UIDocument found."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        tagLabel = root.Q<Label>("tagLabel");

        if (tagLabel == null)
        {
            Debug.LogError(
                "Msg_ChirpMergerTag: Could not find Label named 'tagLabel'."
            );
            enabled = false;
            return;
        }

        tagLabel.text = tagText;

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition =
                new Vector3(0f, 0f, distanceFromCamera);
            transform.localRotation = Quaternion.identity;
        }
    }

    public void ShowTag()
    {
        if (root == null || hasTriggered)
            return;

        hasTriggered = true;
        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            root.style.opacity =
                1f - Mathf.Clamp01(elapsed / duration);

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        messageCoroutine = null;

        if (nextMessage != null)
        {
            nextMessage.ShowChirpNotPicture();
        }
    }
}

