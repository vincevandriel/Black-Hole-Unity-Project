
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_PostMerger : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("Runtime Object Search")]
    public string objectAName =
        "Body A - illustrated black-hole marker";

    public string objectBName =
        "Body B - illustrated black-hole marker";

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;
    [Header("Next Message")]
    public Msg_ChirpMergerTag chirpMergerTag;

    private UIDocument uiDocument;
    private VisualElement root;

    private Transform objectA;
    private Transform objectB;

    private Coroutine uiCoroutine;
    private bool hasShown;

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_PostMerger: No UIDocument found."
            );
            enabled = false;
            return;
        }

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError(
                "Msg_PostMerger: VR camera not found. " +
                "Assign the VR camera in the Inspector."
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

        // Keep the UI hidden until the merger is detected.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        FindBlackHoles();
    }

    private void Update()
    {
        if (hasShown)
            return;

        // The black holes may be created at runtime.
        if (objectA == null || objectB == null)
        {
            FindBlackHoles();
            return;
        }

        // Trigger only after BOTH objects become inactive.
        bool bothInactive =
            !objectA.gameObject.activeInHierarchy &&
            !objectB.gameObject.activeInHierarchy;

        if (bothInactive)
        {
            hasShown = true;
            uiCoroutine = StartCoroutine(ShowAndFade());
        }
    }

    private void FindBlackHoles()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsSortMode.None
            );

        foreach (GameObject obj in allObjects)
        {
            if (objectA == null && obj.name == objectAName)
            {
                objectA = obj.transform;
            }
            else if (objectB == null && obj.name == objectBName)
            {
                objectB = obj.transform;
            }
        }
    }

    private IEnumerator ShowAndFade()
    {
        // Show the UI.
        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        // Keep it visible.
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration)
        );

        // Fade out.
        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            root.style.opacity =
                1f - Mathf.Clamp01(elapsed / duration);

            yield return null;
        }



        // Hide the UI completely.
        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        if (chirpMergerTag != null)
        {
            chirpMergerTag.ShowTag();
        }

        uiCoroutine = null;
    }
}
