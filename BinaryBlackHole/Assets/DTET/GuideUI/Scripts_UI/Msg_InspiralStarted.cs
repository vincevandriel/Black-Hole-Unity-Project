
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_InspiralStarted : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Object Search")]
    public string bodyAName =
        "Body A - illustrated black-hole marker";
    public string bodyBName =
        "Body B - illustrated black-hole marker";

    [Header("Movement Detection")]
    public float movementThreshold = 0.01f;

    [Header("Message Settings")]
    public string messageTitle = "Inspiral Started";
    public string messageText =
        "The black holes are now moving toward each other.";

    [Header("Timing Settings")]
    public float visibleDuration = 3f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private Transform bodyA;
    private Transform bodyB;

    private Vector3 previousPositionA;
    private Vector3 previousPositionB;

    private bool wasMovingA;
    private bool wasMovingB;
    private bool isShowing;

    private Coroutine messageCoroutine;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError(
                "Msg_InspiralStarted: No UIDocument found."
            );
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel != null)
            titleLabel.text = messageTitle;

        if (bodyLabel != null)
            bodyLabel.text = messageText;

        root.style.display = DisplayStyle.None;
        root.style.opacity = 0f;

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        // Keep searching if the runtime objects have not spawned yet.
        if (bodyA == null || bodyB == null)
        {
            FindBlackHoles();
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        bool movingA = CheckMovement(
            bodyA, ref previousPositionA, deltaTime);

        bool movingB = CheckMovement(
            bodyB, ref previousPositionB, deltaTime);

        // Trigger when either black hole starts moving.
        if ((movingA && !wasMovingA) ||
            (movingB && !wasMovingB))
        {
            ShowMessage();
        }

        wasMovingA = movingA;
        wasMovingB = movingB;
    }

    private void FindBlackHoles()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (bodyA == null && obj.name == bodyAName)
            {
                bodyA = obj.transform;
                previousPositionA = bodyA.position;
                wasMovingA = false;
            }
            else if (bodyB == null && obj.name == bodyBName)
            {
                bodyB = obj.transform;
                previousPositionB = bodyB.position;
                wasMovingB = false;
            }
        }
    }

    private bool CheckMovement(
        Transform target,
        ref Vector3 previousPosition,
        float deltaTime)
    {
        if (target == null)
            return false;

        float speed =
            Vector3.Distance(target.position, previousPosition)
            / deltaTime;

        previousPosition = target.position;

        return speed > movementThreshold;
    }

    private void ShowMessage()
    {
        // Don't restart or overlap the message while it is showing.
        if (isShowing)
            return;

        messageCoroutine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        isShowing = true;

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, visibleDuration));

        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            root.style.opacity = 1f - progress;

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        isShowing = false;
        messageCoroutine = null;
    }
}
