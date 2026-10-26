
using UnityEngine;
using UnityEngine.UIElements;

public class Msg_MainAreaTag : MonoBehaviour
{
    [Header("UI Settings")]
    public UIDocument uiDocument;
    public Camera vrCamera;

    [Header("Required Area")]
    public Transform requiredArea;
    public float approachDistance = 3f;

    [Header("Tag Settings")]
    public string tagText = "Main Area";

    private VisualElement root;
    private Label tagLabel;
    private bool isVisible;

    private void Start()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null)
        {
            Debug.LogError("Msg_MainAreaTag: No UIDocument found.");
            enabled = false;
            return;
        }

        root = uiDocument.rootVisualElement;
        tagLabel = root.Q<Label>("tagLabel");

        if (tagLabel != null)
            tagLabel.text = tagText;
        else
            Debug.LogWarning(
                "Msg_MainAreaTag: Could not find a Label named 'tagLabel'."
            );

        root.style.display = DisplayStyle.None;
        isVisible = false;

        if (vrCamera == null)
            vrCamera = Camera.main;

        // Optional: place the UI in front of the VR camera.
        if (vrCamera != null)
        {
            transform.SetParent(vrCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 2f);
            transform.localRotation = Quaternion.identity;
        }

        if (requiredArea == null)
        {
            Debug.LogError(
                "Msg_MainAreaTag: Assign the Required Area in the Inspector."
            );
            enabled = false;
        }
    }

    private void Update()
    {
        if (vrCamera == null || requiredArea == null)
            return;

        float distance = Vector3.Distance(
            vrCamera.transform.position,
            requiredArea.position
        );

        bool shouldShow = distance <= approachDistance;

        if (shouldShow && !isVisible)
        {
            root.style.display = DisplayStyle.Flex;
            root.style.opacity = 1f;
            isVisible = true;
        }
        else if (!shouldShow && isVisible)
        {
            root.style.display = DisplayStyle.None;
            isVisible = false;
        }
    }
}
