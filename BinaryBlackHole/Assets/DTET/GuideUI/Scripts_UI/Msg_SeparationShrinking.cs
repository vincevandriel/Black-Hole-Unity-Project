
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_SeparationShrinking : MonoBehaviour
{
    [Header("Objects to Monitor")]
    public Transform objectA;
    public Transform objectB;

    [Header("Runtime Object Search")]
    public string objectAName =
        "Body A - illustrated black-hole marker";
    public string objectBName =
        "Body B - illustrated black-hole marker";


    [Header("Next Message")]
    public Msg_CloserFaster closerFasterMessage;

    [Header("UI Document")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Position")]
    public float distanceFromCamera = 2f;

    [Header("Detection Settings")]
    [Tooltip("Minimum decrease in distance per second to count as approaching.")]
    public float approachSpeedThreshold = 0.001f;

    [Tooltip("How long the objects must approach before triggering.")]
    public float approachConfirmDuration = 0.2f;

    [Header("Message")]
    public string title = "Separation Shrinking";

    [TextArea(2, 5)]
    public string message =
        "The two objects are moving closer together.";

    [Header("Timing")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private float previousDistance;
    private float approachTimer;

    private bool hasPlayed;
    private bool distanceInitialized;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_SeparationShrinking: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel == null || bodyLabel == null)
        {
            Debug.LogError(
                "Msg_SeparationShrinking: Check that your UXML " +
                "labels are named titleLabel and bodyLabel.");
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

        FindObjectsToMonitor();
    }

    private void Update()
    {
        if (hasPlayed)
            return;

        // Keep searching until runtime objects exist.
        if (objectA == null || objectB == null)
        {
            FindObjectsToMonitor();
            return;
        }

        float currentDistance = Vector3.Distance(
            objectA.position, objectB.position);

        if (!distanceInitialized)
        {
            previousDistance = currentDistance;
            distanceInitialized = true;
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        float approachSpeed =
            (previousDistance - currentDistance) / deltaTime;

        previousDistance = currentDistance;

        if (approachSpeed > approachSpeedThreshold)
        {
            approachTimer += Time.deltaTime;

            if (approachTimer >= approachConfirmDuration)
            {
                // Set this immediately so the message only plays once.
                hasPlayed = true;
                StartCoroutine(ShowAndFade());
            }
        }
        else
        {
            approachTimer = 0f;
        }
    }

    private void FindObjectsToMonitor()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (objectA == null && obj.name == objectAName)
                objectA = obj.transform;
            else if (objectB == null && obj.name == objectBName)
                objectB = obj.transform;
        }

        if (objectA != null && objectB != null)
        {
            previousDistance = Vector3.Distance(
                objectA.position, objectB.position);

            distanceInitialized = true;
        }
    }

 
private IEnumerator ShowAndFade()
    {
        // Show the Separation Shrinking message.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        // Keep it visible.
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration));

        // Fade it out.
        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            root.style.opacity =
                1f - Mathf.Clamp01(elapsed / duration);

            yield return null;
        }

        // Completely hide the first UI.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        // Now show the next UI.
        if (closerFasterMessage != null)
        {
            closerFasterMessage.ShowMessage();
        }
        else
        {
            Debug.LogWarning(
                "Msg_SeparationShrinking: Msg_WatchSlowMotion " +
                "has not been assigned in the Inspector.");
        }
    }
}