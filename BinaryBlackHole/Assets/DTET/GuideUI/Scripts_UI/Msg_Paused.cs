using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_Paused : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;
    public float distanceFromCamera = 2f;

    [Header("Black Hole Objects")]
    public string bodyAName =
        "Body A - illustrated black-hole marker";
    public string bodyBName =
        "Body B - illustrated black-hole marker";

    [Header("Movement Detection")]
    public float movementThreshold = 0.01f;

    [Tooltip("How long both objects must remain still before showing the message.")]
    public float stoppedDuration = 0.5f;

    [Header("Message")]
    public string title = "System Paused";

    [TextArea(2, 5)]
    public string message =
        "The system is paused. Resume when you are ready.";

    [Header("Fade Settings")]
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label titleLabel;
    private Label bodyLabel;

    private Transform bodyA;
    private Transform bodyB;

    private Vector3 previousPositionA;
    private Vector3 previousPositionB;

    private float stoppedTimer;
    private bool isPaused;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError("Msg_Paused: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        titleLabel = root.Q<Label>("titleLabel");
        bodyLabel = root.Q<Label>("bodyLabel");

        if (titleLabel == null || bodyLabel == null)
        {
            Debug.LogError(
                "Msg_Paused: Could not find titleLabel or bodyLabel. " +
                "Check your UXML element names."
            );
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
    }

    private void Update()
    {
        // Find runtime-created objects when they become available.
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

        // If either object moves, the system is considered active.
        if (movingA || movingB)
        {
            stoppedTimer = 0f;

            if (isPaused)
                ResumeSystem();

            return;
        }

        // Both objects are still.
        stoppedTimer += Time.deltaTime;

        if (!isPaused && stoppedTimer >= stoppedDuration)
            PauseSystem();
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
            }
            else if (bodyB == null && obj.name == bodyBName)
            {
                bodyB = obj.transform;
                previousPositionB = bodyB.position;
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

    private void PauseSystem()
    {
        isPaused = true;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;
    }

    private void ResumeSystem()
    {
        isPaused = false;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float startOpacity = root.resolvedStyle.opacity;
        float elapsed = 0f;
        float duration = Mathf.Max(0f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            root.style.opacity =
                Mathf.Lerp(startOpacity, 0f, progress);

            yield return null;
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;
        fadeCoroutine = null;
    }
}