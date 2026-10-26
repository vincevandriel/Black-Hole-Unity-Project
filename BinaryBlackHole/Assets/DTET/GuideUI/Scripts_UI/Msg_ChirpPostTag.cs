
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_ChirpPostTag : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("Objects To Monitor")]
    public GameObject objectA;
    public GameObject objectB;

    [Header("Runtime Object Search")]
    public string objectAName =
        "Body A - illustrated black-hole marker";
    public string objectBName =
        "Body B - illustrated black-hole marker";

    [Header("Timing")]
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_SystemsIntro systemsIntroMessage;

    private VisualElement root;
    private Coroutine messageCoroutine;

    private bool pointTaskHasStarted;
    private bool hasTriggered;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_ChirpPostTag requires a UIDocument."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement
            .Q<VisualElement>("chirpPostTag");

        if (root == null)
        {
            Debug.LogError(
                "Could not find #chirpPostTag in the UXML."
            );
            enabled = false;
            return;
        }

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        FindBodies();
    }

    // Called when Msg_PointTask begins showing.
    public void BeginWaitingForBodiesToDisable()
    {
        if (hasTriggered)
            return;

        FindBodies();
        pointTaskHasStarted = true;
    }

    private void Update()
    {
        if (!pointTaskHasStarted || hasTriggered)
            return;

        FindBodies();

        // Do not trigger until both objects have been found.
        if (objectA == null || objectB == null)
            return;

        if (!objectA.activeInHierarchy &&
            !objectB.activeInHierarchy)
        {
            hasTriggered = true;
            ShowPostMergerTag();
        }
    }

    private void FindBodies()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (GameObject obj in allObjects)
        {
            if (objectA == null && obj.name == objectAName)
                objectA = obj;

            if (objectB == null && obj.name == objectBName)
                objectB = obj;
        }
    }

    public void ShowPostMergerTag()
    {
        if (root == null || hasTriggered == false)
            return;

        if (messageCoroutine != null)
            return;

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

        if (systemsIntroMessage != null)
        {
            systemsIntroMessage.StartComparison();
        }       
    }
}
