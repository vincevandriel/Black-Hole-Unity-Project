
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_ChirpInspiralTag : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Black Hole Objects")]
    public string objectAName =
        "Body A - illustrated black-hole marker";

    public string objectBName =
        "Body B - illustrated black-hole marker";

    [Header("Movement Detection")]
    [Tooltip("Minimum speed required to count as moving.")]
    public float movementThreshold = 0.01f;

    [Header("Tag Settings")]
    public string tagText = "Chirp Inspiral";

    [Header("Frequency Graph")]
    public Transform frequencyGraph;

    public string frequencyGraphObjectName =
        "Partner chirp card edge";

    public Vector3 graphOffset =
        new Vector3(0.8f, 0f, 0f);

    [Header("Timing Settings")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label tagLabel;
    private Coroutine tagCoroutine;

    private Transform objectA;
    private Transform objectB;

    private Vector3 previousPositionA;
    private Vector3 previousPositionB;

    private bool positionsInitialized;
    private bool hasTriggered;
    private bool isShowing;
    private bool warnedAboutGraph;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_ChirpInspiralTag: No UIDocument found."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        tagLabel = root.Q<Label>("tagLabel");

        if (tagLabel == null)
        {
            Debug.LogError(
                "Msg_ChirpInspiralTag: Could not find #tagLabel in the UXML."
            );
            enabled = false;
            return;
        }

        tagLabel.text = tagText;

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        // Find runtime objects, including the frequency graph.
        FindRuntimeObjects();
    }

    private void Update()
    {
        // Continue searching if objects spawn later.
        if (objectA == null ||
            objectB == null ||
            frequencyGraph == null)
        {
            FindRuntimeObjects();
        }

        if (hasTriggered)
            return;

        if (objectA == null || objectB == null)
            return;

        Vector3 currentPositionA = objectA.position;
        Vector3 currentPositionB = objectB.position;

        if (!positionsInitialized)
        {
            previousPositionA = currentPositionA;
            previousPositionB = currentPositionB;
            positionsInitialized = true;
            return;
        }

        float deltaTime = Mathf.Max(
            Time.deltaTime, 0.0001f
        );

        float speedA = Vector3.Distance(
            currentPositionA,
            previousPositionA
        ) / deltaTime;

        float speedB = Vector3.Distance(
            currentPositionB,
            previousPositionB
        ) / deltaTime;

        previousPositionA = currentPositionA;
        previousPositionB = currentPositionB;

        // Show the tag when either black hole starts moving.
        if (speedA > movementThreshold ||
            speedB > movementThreshold)
        {
            hasTriggered = true;
            tagCoroutine = StartCoroutine(ShowAndFade());
        }
    }

    private void FindRuntimeObjects()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (GameObject obj in allObjects)
        {
            if (objectA == null &&
                obj.name == objectAName)
            {
                objectA = obj.transform;
            }

            if (objectB == null &&
                obj.name == objectBName)
            {
                objectB = obj.transform;
            }

            if (frequencyGraph == null &&
                obj.name == frequencyGraphObjectName)
            {
                frequencyGraph = obj.transform;
                warnedAboutGraph = false;
            }
        }

        // Initialize movement tracking once both bodies are found.
        if (objectA != null &&
            objectB != null &&
            !positionsInitialized)
        {
            previousPositionA = objectA.position;
            previousPositionB = objectB.position;
            positionsInitialized = true;
        }

        if (frequencyGraph == null && !warnedAboutGraph)
        {
            Debug.LogWarning(
                "Msg_ChirpInspiralTag: Waiting for runtime frequency graph '" +
                frequencyGraphObjectName + "'."
            );

            warnedAboutGraph = true;
        }
    }

    private IEnumerator ShowAndFade()
    {
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;
        isShowing = true;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        isShowing = false;

        float duration = Mathf.Max(0f, fadeDuration);

        if (duration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                root.style.opacity = 1f - Mathf.Clamp01(
                    elapsed / duration
                );

                yield return null;
            }
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        tagCoroutine = null;
    }

    private void LateUpdate()
    {
        if (!isShowing || frequencyGraph == null)
            return;

        transform.position = frequencyGraph.position
            + frequencyGraph.TransformDirection(graphOffset);

        transform.rotation = frequencyGraph.rotation;
    }
}
