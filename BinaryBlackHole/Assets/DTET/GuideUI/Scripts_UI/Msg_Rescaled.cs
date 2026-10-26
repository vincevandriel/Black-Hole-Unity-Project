
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_Rescaled : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("Objects to Monitor")]
    public Transform objectA;
    public Transform objectB;

    [Header("Runtime Object Names")]
    public string objectAName = "Body A - illustrated black-hole marker";
    public string objectBName = "Body B - illustrated black-hole marker";

    [Header("Detection Settings")]
    [Tooltip("Minimum change in distance, in world units.")]
    public float distanceChangeThreshold = 0.01f;

    [Tooltip("Minimum change in local scale to trigger the message.")]
    public float sizeChangeThreshold = 0.01f;

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float visibleDuration = 2f;
    public float fadeDuration = 1f;

    private UIDocument uiDocument;
    private VisualElement root;

    private float previousDistance;
    private Vector3 previousScaleA;
    private Vector3 previousScaleB;

    private Coroutine uiCoroutine;
    private bool initialValuesSaved;

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_Rescaled: No UIDocument found on this GameObject."
            );
            enabled = false;
            return;
        }

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError(
                "Msg_Rescaled: VR camera not found. Assign it in the Inspector."
            );
            enabled = false;
            return;
        }

        // Attach the UI to the VR camera.
        transform.SetParent(vrCamera.transform, false);
        transform.localPosition =
            new Vector3(0f, 0f, distanceFromCamera);
        transform.localRotation = Quaternion.identity;

        root = uiDocument.rootVisualElement;

        // Hide the UI until a change is detected.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        // Try finding the runtime objects.
        FindRuntimeObjects();

        if (objectA != null && objectB != null)
        {
            SaveCurrentValues();
        }
        else
        {
            Debug.Log(
                "Msg_Rescaled: Waiting for Body A and Body B to spawn."
            );
        }
    }

    private void Update()
    {
        if (root == null)
            return;

        // Runtime objects may spawn after this script starts.
        if (objectA == null || objectB == null)
        {
            FindRuntimeObjects();

            if (objectA != null && objectB != null)
            {
                // Establish a baseline when both objects are available.
                // Do not show a message just because they spawned.
                SaveCurrentValues();

                Debug.Log(
                    "Msg_Rescaled: Found Body A and Body B. Monitoring started."
                );
            }

            return;
        }

        // Safety check in case the objects are replaced at runtime.
        if (!initialValuesSaved)
        {
            SaveCurrentValues();
            return;
        }

        float currentDistance = Vector3.Distance(
            objectA.position,
            objectB.position
        );

        Vector3 currentScaleA = objectA.localScale;
        Vector3 currentScaleB = objectB.localScale;

        bool distanceChanged =
            Mathf.Abs(currentDistance - previousDistance)
            >= distanceChangeThreshold;

        bool sizeAChanged =
            ScaleChanged(currentScaleA, previousScaleA);

        bool sizeBChanged =
            ScaleChanged(currentScaleB, previousScaleB);

        if (distanceChanged || sizeAChanged || sizeBChanged)
        {
            SaveCurrentValues();

            // Restart the display timer while manipulation continues.
            if (uiCoroutine != null)
                StopCoroutine(uiCoroutine);

            uiCoroutine = StartCoroutine(ShowAndFade());
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

    private bool ScaleChanged(Vector3 current, Vector3 previous)
    {
        return Mathf.Abs(current.x - previous.x) >= sizeChangeThreshold
            || Mathf.Abs(current.y - previous.y) >= sizeChangeThreshold
            || Mathf.Abs(current.z - previous.z) >= sizeChangeThreshold;
    }

    private void SaveCurrentValues()
    {
        if (objectA == null || objectB == null)
            return;

        previousDistance = Vector3.Distance(
            objectA.position,
            objectB.position
        );

        previousScaleA = objectA.localScale;
        previousScaleB = objectB.localScale;

        initialValuesSaved = true;
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

        uiCoroutine = null;
    }
}


