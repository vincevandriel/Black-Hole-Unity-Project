
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_OnlyStartDiffers : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Objects To Monitor")]
    public Transform objectA;
    public Transform objectB;

    [Header("Runtime Object Names")]
    public string objectAName =
        "Body A - illustrated black-hole marker";

    public string objectBName =
        "Body B - illustrated black-hole marker";

    [Header("Movement Detection")]
    public float movementThreshold = 0.01f;

    [Header("Timing")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Coroutine messageCoroutine;

    private Vector3 previousPositionA;
    private Vector3 previousPositionB;

    private bool hasTriggered;
    private bool positionsInitialized;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_OnlyStartDiffers requires a UIDocument."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("onlyStartDiffers");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #onlyStartDiffers in Msg_OnlyStartDiffers.uxml."
            );
            enabled = false;
            return;
        }

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        // Try finding the runtime objects.
        FindRuntimeObjects();

        if (objectA != null && objectB != null)
        {
            SaveStartingPositions();
        }
        else
        {
            Debug.Log(
                "Msg_OnlyStartDiffers: Waiting for Body A and Body B to spawn."
            );
        }
    }

    private void Update()
    {
        if (hasTriggered)
            return;

        // Retry until both runtime objects exist.
        if (objectA == null || objectB == null)
        {
            FindRuntimeObjects();

            if (objectA != null && objectB != null)
            {
                // Start monitoring from their current positions.
                SaveStartingPositions();

                Debug.Log(
                    "Msg_OnlyStartDiffers: Found both bodies. Monitoring started."
                );
            }

            return;
        }

        // Initialize safely if the references become available later.
        if (!positionsInitialized)
        {
            SaveStartingPositions();
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        float speedA = Vector3.Distance(
            objectA.position,
            previousPositionA
        ) / deltaTime;

        float speedB = Vector3.Distance(
            objectB.position,
            previousPositionB
        ) / deltaTime;

        previousPositionA = objectA.position;
        previousPositionB = objectB.position;

        if (speedA >= movementThreshold ||
            speedB >= movementThreshold)
        {
            hasTriggered = true;
            ShowMessage();
        }
    }

    private void FindRuntimeObjects()
    {
        if (objectA == null)
            objectA = FindTransformByName(objectAName);

        if (objectB == null)
            objectB = FindTransformByName(objectBName);
    }

    private Transform FindTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        GameObject[] objects = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (GameObject obj in objects)
        {
            if (obj.name == targetName)
                return obj.transform;
        }

        return null;
    }

    private void SaveStartingPositions()
    {
        if (objectA == null || objectB == null)
            return;

        previousPositionA = objectA.position;
        previousPositionB = objectB.position;

        positionsInitialized = true;
    }

    public void ShowMessage()
    {
        if (root == null)
            return;

        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        messageCoroutine = StartCoroutine(ShowAndFade());
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

