
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GuideTimer : MonoBehaviour
{
    [Header("UI Document")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Position")]
    public float distanceFromCamera = 2f;

    [Header("Timer Settings")]
    public float defaultDuration = 30f;
    public float fadeDuration = 1f;

    private VisualElement root;
    private Label timerLabel;
    private VisualElement timerTrack;
    private VisualElement timerFill;

    private Coroutine timerCoroutine;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (uiDocument == null)
        {
            Debug.LogError("GuideTimer: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;

        timerLabel = root.Q<Label>("timerLabel");
        timerTrack = root.Q<VisualElement>("timerTrack");
        timerFill = root.Q<VisualElement>("timerFill");

        if (timerLabel == null || timerTrack == null || timerFill == null)
        {
            Debug.LogError(
                "GuideTimer: Check the names timerLabel, timerTrack, and timerFill in your UXML.");
            enabled = false;
            return;
        }

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

    // Starts using the default duration.
    public void StartTimer()
    {
        StartTimer(defaultDuration);
    }

    // Starts or restarts the timer with a custom duration.
    public void StartTimer(float duration)
    {
        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);

        timerCoroutine = StartCoroutine(RunTimer(duration));
    }

    private IEnumerator RunTimer(float duration)
    {
        duration = Mathf.Max(0f, duration);

        root.style.display = DisplayStyle.Flex;
        root.style.opacity = 1f;

        float timeRemaining = duration;

        while (timeRemaining > 0f)
        {
            UpdateTimerDisplay(timeRemaining, duration);

            yield return null;

            timeRemaining -= Time.deltaTime;
        }

        UpdateTimerDisplay(0f, duration);

        // Fade out the entire UI.
        if (fadeDuration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;

                float alpha =
                    1f - Mathf.Clamp01(elapsed / fadeDuration);

                root.style.opacity = alpha;

                yield return null;
            }
        }

        root.style.opacity = 0f;
        root.style.display = DisplayStyle.None;

        timerCoroutine = null;
    }

    private void UpdateTimerDisplay(float timeRemaining, float duration)
    {
        int totalSeconds = Mathf.CeilToInt(timeRemaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerLabel.text = $"{minutes:00}:{seconds:00}";

        float progress = duration > 0f
            ? Mathf.Clamp01(timeRemaining / duration)
            : 0f;

        timerFill.style.width = Length.Percent(progress * 100f);
    }
}

