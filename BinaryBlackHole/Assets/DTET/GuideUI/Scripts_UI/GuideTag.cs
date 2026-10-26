
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GuideTag : MonoBehaviour
{
    [Header("UI")]
    public UIDocument uiDocument;

    [Header("VR Pointing")]
    public Transform controller;
    public string controllerObjectName =
        "Virtual glove and forearm / pose preview";
    public float maxDistance = 10f;
    public LayerMask targetLayers = ~0;

    [Header("Tag Position")]
    public Vector3 tagOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Fade")]
    public float fadeDuration = 0.5f;

    private VisualElement tagRoot;
    private Label tagLabel;
    private Coroutine fadeCoroutine;
    private GameObject currentTarget;
    private bool isVisible;
    private bool warnedControllerMissing;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("GuideTag requires a UIDocument.");
            enabled = false;
            return;
        }

        tagRoot = uiDocument.rootVisualElement
            .Q<VisualElement>("tagRoot");

        tagLabel = uiDocument.rootVisualElement
            .Q<Label>("tagLabel");

        if (tagRoot == null || tagLabel == null)
        {
            Debug.LogError(
                "GuideTag could not find #tagRoot or #tagLabel. " +
                "Check the UXML element names."
            );
            enabled = false;
            return;
        }

        tagRoot.style.display = DisplayStyle.None;
        tagRoot.style.opacity = 0f;

        // Find the runtime controller if one wasn't assigned.
        FindController();
    }

    private void Update()
    {
        // Runtime VR objects may appear after Start().
        if (controller == null)
            FindController();

        if (controller == null)
        {
            if (isVisible)
            {
                currentTarget = null;
                HideTag();
            }

            return;
        }

        Ray ray = new Ray(
            controller.position,
            controller.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            maxDistance,
            targetLayers,
            QueryTriggerInteraction.Ignore))
        {
            GameObject target = hit.collider.gameObject;

            if (currentTarget != target)
            {
                currentTarget = target;
                ShowTag(target.name);
            }

            transform.position = hit.point + tagOffset;

            Vector3 direction =
                transform.position - controller.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    direction,
                    Vector3.up
                );
            }
        }
        else
        {
            currentTarget = null;

            if (isVisible && fadeCoroutine == null)
                HideTag();
        }
    }

    private void FindController()
    {
        // Search all loaded objects, including inactive runtime objects.
        GameObject[] objects = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (GameObject obj in objects)
        {
            if (obj.name == controllerObjectName)
            {
                controller = obj.transform;
                warnedControllerMissing = false;

                Debug.Log(
                    "GuideTag found controller candidate: " + obj.name
                );
                return;
            }
        }

        if (!warnedControllerMissing)
        {
            Debug.LogWarning(
                "GuideTag is waiting for runtime object '" +
                controllerObjectName + "'."
            );
            warnedControllerMissing = true;
        }
    }

    private void ShowTag(string objectName)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        tagLabel.text = objectName;
        tagRoot.style.display = DisplayStyle.Flex;
        tagRoot.style.opacity = 1f;
        isVisible = true;
    }

    private void HideTag()
    {
        if (tagRoot == null || !isVisible)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (fadeDuration <= 0f)
        {
            tagRoot.style.opacity = 0f;
            tagRoot.style.display = DisplayStyle.None;
            isVisible = false;
            fadeCoroutine = null;
            return;
        }

        fadeCoroutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / fadeDuration
            );

            tagRoot.style.opacity = 1f - progress;
            yield return null;
        }

        tagRoot.style.opacity = 0f;
        tagRoot.style.display = DisplayStyle.None;

        isVisible = false;
        fadeCoroutine = null;
    }
}
