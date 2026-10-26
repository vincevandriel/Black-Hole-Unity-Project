
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_TryAnotherViewpoint : MonoBehaviour
{
    [Header("VR Settings")]
    public Camera vrCamera;

    [Header("Viewpoint Settings")]
    public Transform[] viewpoints;
    public int requiredViewpoints = 4;
    public float lookDuration = 2f;
    public float allowedAngle = 15f;

    [Header("UI Settings")]
    public float distanceFromCamera = 1.5f;
    public float visibleDuration = 5f;
    public float fadeDuration = 1f;

    [Header("Next Message")]
    public Msg_AskGuide askGuideMessage;

    private UIDocument uiDocument;
    private VisualElement root;

    private int completedViewpoints = 0;
    private float lookTimer = 0f;
    private bool sequenceComplete = false;
    
    private bool canStart;

    private void Start()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_TryAnotherViewpoint: No UIDocument found."
            );
            enabled = false;
            return;
        }

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
        {
            Debug.LogError("VR camera not found.");
            enabled = false;
            return;
        }

        if (viewpoints == null || viewpoints.Length < requiredViewpoints)
        {
            Debug.LogError(
                "Assign at least as many viewpoints as requiredViewpoints."
            );
            enabled = false;
            return;
        }

        transform.SetParent(vrCamera.transform, false);
        transform.localPosition =
            new Vector3(0f, 0f, distanceFromCamera);
        transform.localRotation = Quaternion.identity;

        root = uiDocument.rootVisualElement;

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        if (!canStart)
            return;
        if (sequenceComplete || root == null)
            return;

        if (completedViewpoints >= requiredViewpoints)
        {
            sequenceComplete = true;
            StartCoroutine(ShowAndFade());



            if (askGuideMessage != null)
            {
                askGuideMessage.ShowAskGuide();
            }
            return;
        }

        Transform target = viewpoints[completedViewpoints];

        if (target == null)
            return;

        Vector3 directionToTarget =
            target.position - vrCamera.transform.position;

        float angle = Vector3.Angle(
            vrCamera.transform.forward,
            directionToTarget.normalized
        );

        if (angle <= allowedAngle)
        {
            lookTimer += Time.deltaTime;

            if (lookTimer >= lookDuration)
            {
                completedViewpoints++;
                lookTimer = 0f;

                Debug.Log(
                    "Viewpoints completed: " +
                    completedViewpoints + "/" + requiredViewpoints
                );
            }
        }
        else
        {
            lookTimer = 0f;
        }
    }
    public void StartViewpointSequence()
    {
        canStart = true;
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

            float progress = Mathf.Clamp01(elapsed / duration);
            root.style.opacity = Mathf.Lerp(1f, 0f, progress);

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
    }
}
