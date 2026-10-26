using UnityEngine;

namespace DTET.Guide
{
    /// <summary>
    /// Keeps one UI element where it belongs: fixed in the scene, floating in front of the user's head,
    /// or following an object. Turns it (yaw only) toward the user so the text stays upright and readable.
    /// </summary>
    public sealed class GuidePlacement : MonoBehaviour
    {
        public GuideAnchorMode Mode = GuideAnchorMode.World;
        [Tooltip("World: scene position. HeadLocked: x = right, y = up, z = distance. FollowTarget: offset from the target.")]
        public Vector3 Offset;
        public Transform Target;
        public bool FaceUser = true;
        [Tooltip("How quickly a head-locked element catches up. Lower = lazier and calmer in VR.")]
        public float Smoothing = 5f;

        private Canvas canvas;
        private Transform head;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        /// <summary>Jump straight to the target position (used when something is shown, so it never swoops in).</summary>
        public void Snap()
        {
            Apply(true);
        }

        private void LateUpdate()
        {
            if (canvas != null && !canvas.enabled)
            {
                return;
            }

            Apply(false);
        }

        private void Apply(bool instant)
        {
            Transform eye = Head();

            switch (Mode)
            {
                case GuideAnchorMode.World:
                    transform.position = Offset;
                    break;

                case GuideAnchorMode.FollowTarget:
                    if (Target != null)
                    {
                        transform.position = Target.position + Offset;
                    }

                    break;

                case GuideAnchorMode.HeadLocked:
                    if (eye != null)
                    {
                        Vector3 forward = eye.forward;
                        forward.y = 0f;
                        if (forward.sqrMagnitude < 0.0001f)
                        {
                            forward = Vector3.forward;
                        }

                        forward = forward.normalized;
                        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
                        Vector3 wanted = eye.position + forward * Offset.z + right * Offset.x + Vector3.up * Offset.y;
                        float blend = instant ? 1f : 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
                        transform.position = Vector3.Lerp(transform.position, wanted, blend);
                    }

                    break;
            }

            if (FaceUser && eye != null)
            {
                Vector3 away = transform.position - eye.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(away, Vector3.up);
                }
            }
        }

        private Transform Head()
        {
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }

            return head;
        }
    }
}
