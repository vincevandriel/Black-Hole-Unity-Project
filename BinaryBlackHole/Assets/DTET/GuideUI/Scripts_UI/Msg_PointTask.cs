
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_PointTask : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Bodies To Monitor")]
    public Transform objectA;
    public Transform objectB;

    [Header("Runtime Object Search")]
    public string objectAName =
        "Body A - illustrated black-hole marker";
    public string objectBName =
        "Body B - illustrated black-hole marker";

    [Header("Stop Detection")]
    [Tooltip("Movement below this speed counts as stopped.")]
    public float stoppedSpeedThreshold = 0.01f;

    [Tooltip("How long both bodies must remain still.")]
    public float stoppedConfirmDuration = 1f;

    [Header("Timing")]
    public float visibleDuration = 10f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_ChirpPostTag chirpPostTagMessage;

    private VisualElement root;
    private Coroutine messageCoroutine;

    private Vector3 previousPositionA;
    private Vector3 previousPositionB;

    private float stoppedTimer;
    private bool readyToMonitor;
    private bool hasPlayed;
    private bool positionsInitialized;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_PointTask requires a UIDocument."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("pointTask");

        if (root == null)
        {
            Debug.LogError(
                "Msg_PointTask: Could not find #pointTask in the UXML."
            );
            enabled = false;
            return;
        }

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        FindBodies();
    }

    // Called after Msg_ChirpNotPicture finishes fading.
    public void BeginWaitingForBodiesToStop()
    {
        if (hasPlayed)
            return;

        readyToMonitor = true;
        stoppedTimer = 0f;
        positionsInitialized = false;

        FindBodies();
    }

    private void Update()
    {
        if (!readyToMonitor || hasPlayed)
            return;

        if (objectA == null || objectB == null)
        {
            FindBodies();
            return;
        }

        Vector3 currentA = objectA.position;
        Vector3 currentB = objectB.position;

        if (!positionsInitialized)
        {
            previousPositionA = currentA;
            previousPositionB = currentB;
            positionsInitialized = true;
            return;
        }

        float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        float speedA =
            Vector3.Distance(currentA, previousPositionA) / deltaTime;

        float speedB =
            Vector3.Distance(currentB, previousPositionB) / deltaTime;

        previousPositionA = currentA;
        previousPositionB = currentB;

        if (speedA <= stoppedSpeedThreshold &&
            speedB <= stoppedSpeedThreshold)
        {
            stoppedTimer += Time.unscaledDeltaTime;

            if (stoppedTimer >= stoppedConfirmDuration)
            {
                hasPlayed = true;
                readyToMonitor = false;
                messageCoroutine = StartCoroutine(ShowAndFade());
            }
        }
        else
        {
            stoppedTimer = 0f;
        }
    }

    private void FindBodies()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (objectA == null && obj.name == objectAName)
                objectA = obj.transform;

            if (objectB == null && obj.name == objectBName)
                objectB = obj.transform;
        }
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

        if (chirpPostTagMessage != null)
        {
            chirpPostTagMessage.BeginWaitingForBodiesToDisable();
        }
    }
}
