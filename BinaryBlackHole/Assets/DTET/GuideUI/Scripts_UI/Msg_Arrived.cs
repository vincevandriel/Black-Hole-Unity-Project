
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_Arrived : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Arrival Area")]
    public Transform targetArea;
    public Camera vrCamera;
    public float arrivalDistance = 2f;

    [Header("Next Message")]
    public Msg_LearningGoals nextMessage;

    [Header("Message Timing")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Coroutine messageCoroutine;
    private bool hasArrived;

    void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError("Msg_Arrived requires a UIDocument.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("arrived");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #arrived in Msg_Arrived.uxml."
            );
            enabled = false;
            return;
        }

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (targetArea == null || vrCamera == null)
        {
            Debug.LogError(
                "Assign Target Area and VR Camera in the Inspector."
            );
            enabled = false;
        }
    }

    void Update()
    {
        if (hasArrived || targetArea == null || vrCamera == null)
            return;

        float distance = Vector3.Distance(
            vrCamera.transform.position,
            targetArea.position
        );

        if (distance <= arrivalDistance)
        {
            hasArrived = true;
            ShowArrivalMessage();
        }
    }

    public void ShowArrivalMessage()
    {
        if (root == null)
            return;

        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        yield return new WaitForSecondsRealtime(visibleDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            root.style.opacity = 1f -
                Mathf.Clamp01(elapsed / fadeDuration);

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        messageCoroutine = null;

        if (nextMessage != null)
        {
            nextMessage.ShowLearningGoals();
        }
    }
}
