using UnityEngine;

namespace ApproachHUD
{
    [DisallowMultipleComponent, RequireComponent(typeof(HudSquareGraphic))]
    public sealed class HudSquareController : MonoBehaviour
    {
        public enum VisibilityState { Hidden, Opening, Idle, Closing }

        [Header("Visibility")]
        public bool automaticTick = true;
        public bool automaticProximity = true;
        [Tooltip("Assign the player or XR headset. Falls back to Camera.main when left empty.")]
        public Transform observer;
        [Tooltip("Optional distance anchor. Defaults to this object's position.")]
        public Transform distanceAnchor;
        [Min(0.01f)] public float showDistance = 3f;
        [Min(0.01f)] public float hideDistance = 3.5f;
        public bool faceObserver = true;
        [Tooltip("Only applies when Automatic Proximity is off.")]
        public bool showOnEnable = true;

        [Header("Timing")]
        [Min(0.01f)] public float openingDuration = 1.2f;
        [Min(0f)] public float hideDuration = 1.2f;
        [Min(0f)] public float idleReturnDuration = 0.12f;
        public bool useUnscaledTime;

        public VisibilityState State { get; private set; } = VisibilityState.Hidden;
        public float OpeningProgress { get; private set; }
        public float IdleSeconds { get; private set; }
        public float Opacity { get; private set; }
        public int OpeningCount { get; private set; }

        HudSquareGraphic graphic;
        Transform fallbackObserver;
        float idleMotion;
        bool retainedIdlePose;
        float cameraRetry;

        void OnEnable()
        {
            graphic = GetComponent<HudSquareGraphic>();
            ResetHidden();
            if (!automaticProximity && showOnEnable) Show();
        }

        void OnDisable() { ResetHidden(); }

        void ResetHidden()
        {
            State = VisibilityState.Hidden;
            OpeningProgress = IdleSeconds = Opacity = idleMotion = 0f;
            retainedIdlePose = false;
            if (graphic != null) graphic.RenderSample(0, 0, 0, 0);
        }

        Transform ResolveObserver(float dt)
        {
            if (observer != null) return observer;
            cameraRetry -= dt;
            if (fallbackObserver == null && cameraRetry <= 0f)
            {
                Camera c = Camera.main;
                if (c != null) fallbackObserver = c.transform;
                cameraRetry = 0.5f;
            }
            return fallbackObserver;
        }

        void Update() { if (automaticTick) Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime); }

        /// <summary>Advances the shared animation clock; disable automaticTick when calling manually.</summary>
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new System.ArgumentOutOfRangeException(nameof(deltaTime), "HUD time must be finite.");
            float dt = Mathf.Max(0, deltaTime);
            if (graphic == null) graphic = GetComponent<HudSquareGraphic>();
            Transform viewer = automaticProximity || faceObserver ? ResolveObserver(dt) : null;
            if (automaticProximity)
            {
                if (viewer == null) Hide();
                else
                {
                    Vector3 origin = distanceAnchor != null ? distanceAnchor.position : transform.position;
                    float d2 = (viewer.position - origin).sqrMagnitude;
                    float enter = Mathf.Max(0.01f, showDistance);
                    float exit = Mathf.Max(enter + 0.01f, hideDistance);
                    bool present = State == VisibilityState.Opening || State == VisibilityState.Idle;
                    if (!present && d2 <= enter * enter) Show();
                    else if (present && d2 >= exit * exit) Hide();
                }
            }

            if (State == VisibilityState.Opening)
            {
                float duration = Mathf.Max(0.01f, openingDuration);
                float remaining = Mathf.Max(0, (1f - OpeningProgress) * duration);
                float advance = Mathf.Min(dt, remaining);
                OpeningProgress = Mathf.Clamp01(OpeningProgress + advance / duration);
                dt -= advance;
                Opacity = 1f;
                if (advance >= remaining)
                {
                    OpeningProgress = 1f;
                    if (!retainedIdlePose || idleReturnDuration <= 0f) idleMotion = 1f;
                    else
                    {
                        remaining = Mathf.Max(0, (1f - idleMotion) * idleReturnDuration);
                        advance = Mathf.Min(dt, remaining);
                        idleMotion = Mathf.Clamp01(idleMotion + advance / idleReturnDuration);
                        dt -= advance;
                        if (advance >= remaining) idleMotion = 1f;
                    }
                    if (idleMotion >= 1f)
                    {
                        State = VisibilityState.Idle;
                        IdleSeconds += dt;
                    }
                }
            }
            else if (State == VisibilityState.Idle) IdleSeconds += dt;
            else if (State == VisibilityState.Closing)
            {
                // Return the frozen idle pose to the opening endpoint before reversing its assembly.
                if (!retainedIdlePose || idleReturnDuration <= 0f) idleMotion = 0f;
                else if (idleMotion > 0f)
                {
                    float remaining = idleMotion * idleReturnDuration;
                    float advance = Mathf.Min(dt, remaining);
                    idleMotion = Mathf.Clamp01(idleMotion - advance / idleReturnDuration);
                    dt -= advance;
                    if (advance >= remaining) idleMotion = 0f;
                }
                if (idleMotion <= 0f)
                {
                    OpeningProgress = hideDuration <= 0f ? 0f : Mathf.Max(0, OpeningProgress - dt / hideDuration);
                    if (OpeningProgress <= 0f) ResetHidden();
                }
            }
            graphic.RenderSample(OpeningProgress, IdleSeconds, Opacity, idleMotion);
        }

        void LateUpdate()
        {
            Transform viewer = observer != null ? observer : fallbackObserver;
            if (!faceObserver || viewer == null) return;
            Vector3 forward = transform.position - viewer.position;
            if (forward.sqrMagnitude < 0.000001f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        /// <summary>Continues a closing animation from its current pose without replaying the opening.</summary>
        public void Show()
        {
            if (State == VisibilityState.Opening || State == VisibilityState.Idle) return;
            if (State == VisibilityState.Hidden)
            {
                OpeningProgress = IdleSeconds = idleMotion = 0f;
                retainedIdlePose = false;
                OpeningCount++;
            }
            State = VisibilityState.Opening;
            Opacity = 1f;
        }

        public void Hide()
        {
            if (State == VisibilityState.Hidden || State == VisibilityState.Closing) return;
            if (State == VisibilityState.Idle) retainedIdlePose = true;
            State = VisibilityState.Closing;
        }

        void OnValidate()
        {
            showDistance = Mathf.Max(0.01f, showDistance);
            hideDistance = Mathf.Max(showDistance + 0.01f, hideDistance);
            openingDuration = Mathf.Max(0.01f, openingDuration);
            hideDuration = Mathf.Max(0f, hideDuration);
            idleReturnDuration = Mathf.Max(0f, idleReturnDuration);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 center = distanceAnchor != null ? distanceAnchor.position : transform.position;
            Gizmos.color = new Color(0.3f, 1f, 0.7f, 0.6f); Gizmos.DrawWireSphere(center, showDistance);
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.4f); Gizmos.DrawWireSphere(center, hideDistance);
        }
    }
}
