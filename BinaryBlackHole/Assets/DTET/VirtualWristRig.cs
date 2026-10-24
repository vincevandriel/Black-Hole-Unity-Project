using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DTET.VisualPrototype
{
    // Emulated pose for the world-space wrist UI.
    public sealed class VirtualWristRig : MonoBehaviour
    {
        private Camera head;
        private Transform pose;
        private Transform glove;
        private Transform trackedWrist;
        private Canvas menu;
        private Vector3 raisedOffset, loweredOffset;
        private Vector3 currentOffset;
        private Quaternion raisedRotation;
        private Material gloveMaterial, seamMaterial, cuffMaterial;
        public bool Raised { get; private set; }
        public Transform Wrist => pose;
        public Canvas Menu => menu;
        public bool UsingTrackedPose => trackedWrist != null;

        public void Initialize(Camera camera, Canvas wristMenu)
        {
            head = camera;
            menu = wristMenu;
            pose = new GameObject("Emulated left wrist pose").transform;
            pose.SetParent(transform, false);
            glove = new GameObject("Virtual glove and forearm / pose preview").transform;
            glove.SetParent(pose, false);
            gloveMaterial = Surface(new Color(0.22f, 0.28f, 0.36f));
            seamMaterial = Surface(new Color(0.39f, 0.47f, 0.56f));
            cuffMaterial = Surface(new Color(0.14f, 0.65f, 0.63f));
            Part("Sleeve", PrimitiveType.Capsule, new Vector3(0.16f, -0.055f, 0.05f), new Vector3(0.079f, 0.14f, 0.075f), Quaternion.Euler(0, 0, 72), gloveMaterial);
            Part("Wrist cuff", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.041f, 0.024f, 0.034f), Quaternion.Euler(0, 0, 90), cuffMaterial);
            Part("Palm", PrimitiveType.Sphere, new Vector3(-0.078f, 0.014f, 0), new Vector3(0.12f, 0.078f, 0.039f), Quaternion.Euler(0, 0, -8), gloveMaterial);
            for (var i = 0; i < 4; i++)
            {
                var y = 0.044f - i * 0.018f;
                var length = i == 1 ? 0.08f : i == 2 ? 0.075f : 0.062f;
                Part("Finger " + i, PrimitiveType.Capsule, new Vector3(-0.143f - length * 0.18f, y, 0), new Vector3(0.018f, length * 0.5f, 0.019f), Quaternion.Euler(0, 0, 92 - i * 3), gloveMaterial);
                Part("Knuckle " + i, PrimitiveType.Sphere, new Vector3(-0.125f, y, -0.011f), Vector3.one * 0.019f, Quaternion.identity, seamMaterial);
            }
            Part("Thumb", PrimitiveType.Capsule, new Vector3(-0.077f, -0.040f, -0.006f), new Vector3(0.022f, 0.040f, 0.021f), Quaternion.Euler(0, 0, 38), gloveMaterial);
            Part("Menu mount", PrimitiveType.Cube, new Vector3(0, -0.025f, -0.037f), new Vector3(0.053f, 0.038f, 0.025f), Quaternion.identity, cuffMaterial);
            menu.transform.SetParent(pose, false);
            menu.transform.localPosition = new Vector3(-0.01f, -0.053f, -0.060f);
            menu.transform.localRotation = Quaternion.identity;
            menu.transform.localScale = Vector3.one * 0.0005f;
            SetRaised(false, true);
        }

        public void SetRaised(bool value, bool immediate = false)
        {
            if (trackedWrist != null) return;
            Raised = value;
            // Hold the raised pose steady while the visitor looks around.
            raisedOffset = head.transform.TransformVector(new Vector3(0.27f, -0.05f, 0.58f));
            loweredOffset = head.transform.TransformVector(new Vector3(0.42f, -0.66f, 0.48f));
            raisedRotation = head.transform.rotation * Quaternion.Euler(-6, 10, -12);
            if (immediate)
            {
                currentOffset = Raised ? raisedOffset : loweredOffset;
                pose.position = head.transform.position + currentOffset;
                pose.rotation = raisedRotation;
            }
            menu.gameObject.SetActive(Raised);
        }

        // Accept an external pose; XR device setup is separate.
        public void BindTrackedWrist(Transform trackedPose, bool showPreviewGlove = false)
        {
            trackedWrist = trackedPose;
            glove.gameObject.SetActive(trackedPose == null || showPreviewGlove);
            Raised = trackedPose != null;
            if (trackedPose == null) SetRaised(false, true);
        }

        private void Update()
        {
            if (trackedWrist == null && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
                SetRaised(!Raised);
        }

        private void LateUpdate()
        {
            if (head == null) return;
            if (trackedWrist != null) pose.SetPositionAndRotation(trackedWrist.position, trackedWrist.rotation);
            else
            {
                var blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
                currentOffset = Vector3.Lerp(currentOffset, Raised ? raisedOffset : loweredOffset, blend);
                pose.position = head.transform.position + currentOffset;
                pose.rotation = Quaternion.Slerp(pose.rotation, raisedRotation, blend);
            }
            var toEye = (head.transform.position - menu.transform.position).normalized;
            var facingEye = Vector3.Dot(-menu.transform.forward, toEye);
            menu.gameObject.SetActive(Raised && facingEye > 0.35f);
        }

        private Material Surface(Color color)
        {
            var material = new Material(Shader.Find("DTET/WristSurface"));
            material.SetColor("_Color", color);
            return material;
        }

        private void Part(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(glove, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
        }

        private void OnDestroy()
        {
            if (gloveMaterial != null) Destroy(gloveMaterial);
            if (seamMaterial != null) Destroy(seamMaterial);
            if (cuffMaterial != null) Destroy(cuffMaterial);
        }
    }
}
