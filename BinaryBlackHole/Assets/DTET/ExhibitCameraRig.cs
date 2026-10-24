using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DTET.VisualPrototype
{
    // Emulated visitor pose for the world-space exhibit.
    public sealed class ExhibitCameraRig : MonoBehaviour
    {
        public enum ObservationTarget { Binary, BodyA, BodyB }
        public enum ViewPreset { Overview, Above, Side }

        public const float MinimumDistance = 1.4f;
        public const float MaximumDistance = 36f;
        public const float DefaultDistance = 9f;
        public static readonly Vector3 LearningBoardPosition = new Vector3(3.4f, 2f, 3f);
        public event System.Action PoseRelocated;
        public bool EmulatedInputEnabled { get; set; } = true;
        public bool PersistInputSettings { get; set; } = true;
        private const string LookPreference = "DTET.Observer.LookSensitivity.v1";
        private const string ScrollPreference = "DTET.Observer.ScrollSensitivity.v1";
        private readonly ObserverInputSettings inputSettings = new ObserverInputSettings();
        public float LookSensitivity => inputSettings.Look;
        public float ScrollSensitivity => inputSettings.Scroll;
        private static readonly Vector3 ExhibitCentre = new Vector3(-3.25f, 0, 0);
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        private Camera view;
        private Transform bodyA;
        private Transform bodyB;
        private Vector3 pan;
        private float yaw;
        private float elevation = 25f;
        private float distance = DefaultDistance;
        private float flyYaw;
        private float flyPitch;
        private bool rotating;
        private bool panning;
        private PointerEventData pointerData;

        public bool FreeFlight { get; private set; }
        public ObservationTarget Target { get; private set; }
        public float Distance => distance;
        public Vector3 FocusPoint => TrackedPoint() + pan;
        public string Status => FreeFlight
            ? "FREE FLIGHT  |  F returns to the binary"
            : "ORBIT  /  " + (Target == ObservationTarget.Binary ? "BINARY" : Target == ObservationTarget.BodyA ? "BODY A" : "BODY B") + "  |  wheel or +/- moves closer / farther";

        public void Initialize(Camera camera, Transform firstBody, Transform secondBody)
        {
            view = camera;
            bodyA = firstBody;
            bodyB = secondBody;
            inputSettings.SetLook(PlayerPrefs.GetFloat(LookPreference, ObserverInputSettings.DefaultLook));
            inputSettings.SetScroll(PlayerPrefs.GetFloat(ScrollPreference, ObserverInputSettings.DefaultScroll));
            view.orthographic = false;
            view.fieldOfView = 50f;
            view.nearClipPlane = 0.05f;
            view.farClipPlane = 200f;
            SetPreset(ViewPreset.Overview);
        }

        private Vector3 TrackedPoint()
        {
            var selected = Target == ObservationTarget.BodyA ? bodyA : Target == ObservationTarget.BodyB ? bodyB : null;
            return selected != null && selected.gameObject.activeInHierarchy ? selected.position : ExhibitCentre;
        }

        public void SetPreset(ViewPreset preset)
        {
            FreeFlight = false;
            Target = ObservationTarget.Binary;
            pan = Vector3.zero;
            distance = DefaultDistance;
            yaw = preset == ViewPreset.Side ? 65f : 0f;
            elevation = preset == ViewPreset.Above ? 85f : preset == ViewPreset.Side ? 2f : 12f;
            ApplyOrbitPose();
            ToggleFlight();
            PoseRelocated?.Invoke();
        }

        public void FocusInformation() => Relocate(new Vector3(1.3f, 1.5f, -2.8f), new Vector3(1.3f, 1.5f, 0));
        public void FocusClockComparison() => Relocate(new Vector3(4f, 2.1f, -3f), new Vector3(4f, 2.1f, 0));
        public void AlignForLensing(Transform lens, Transform source)
        {
            if (lens == null || source == null) return;
            var direction = (source.position - lens.position).normalized;
            // A slight off-axis view avoids the artistic disk covering all aligned point images.
            Relocate(lens.position - direction * 8f + Vector3.up * 0.35f, lens.position);
        }
        public void FocusLearning() => Relocate(LearningBoardPosition + new Vector3(0, 0, -4.6f), LearningBoardPosition);

        private void Relocate(Vector3 position, Vector3 lookAt)
        {
            FreeFlight = true;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            flyYaw = transform.eulerAngles.y;
            flyPitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            rotating = panning = false;
            PoseRelocated?.Invoke();
        }

        public void Focus(ObservationTarget target)
        {
            Target = target;
            FreeFlight = false;
            pan = Vector3.zero;
            distance = target == ObservationTarget.Binary ? DefaultDistance : 4.2f;
            ApplyOrbitPose();
            PoseRelocated?.Invoke();
        }

        public void ToggleFlight()
        {
            FreeFlight = !FreeFlight;
            rotating = panning = false;
            if (FreeFlight)
            {
                flyYaw = transform.eulerAngles.y;
                flyPitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            }
            else
            {
                var offset = transform.position - TrackedPoint();
                distance = Mathf.Clamp(offset.magnitude, MinimumDistance, MaximumDistance);
                if (offset.sqrMagnitude > 0.001f)
                {
                    elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -85f, 85f);
                    yaw = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
                }
                pan = Vector3.zero;
                ApplyOrbitPose();
                PoseRelocated?.Invoke();
            }
        }

        public void Zoom(float steps)
        {
            if (!float.IsFinite(steps)) return;
            if (FreeFlight)
                MoveLocal(new Vector3(0, 0, Mathf.Clamp(steps, -20f, 20f) * 0.8f * ScrollSensitivity));
            else
            {
                distance = Mathf.Clamp(distance * Mathf.Exp(-Mathf.Clamp(steps, -20f, 20f) * 0.12f * ScrollSensitivity), MinimumDistance, MaximumDistance);
                ApplyOrbitPose();
            }
        }

        public void Look(Vector2 pixelDelta)
        {
            if (!float.IsFinite(pixelDelta.x) || !float.IsFinite(pixelDelta.y)) return;
            Rotate(pixelDelta * (0.18f * LookSensitivity));
        }

        public void SetLookSensitivity(float value)
        {
            if (inputSettings.SetLook(value) && PersistInputSettings)
                PlayerPrefs.SetFloat(LookPreference, LookSensitivity);
        }

        public void SetScrollSensitivity(float value)
        {
            if (inputSettings.SetScroll(value) && PersistInputSettings)
                PlayerPrefs.SetFloat(ScrollPreference, ScrollSensitivity);
        }

        public void ResetInputSensitivities()
        {
            SetLookSensitivity(ObserverInputSettings.DefaultLook);
            SetScrollSensitivity(ObserverInputSettings.DefaultScroll);
        }

        public void SaveInputSettings()
        {
            if (PersistInputSettings) PlayerPrefs.Save();
        }

        public void Rotate(Vector2 delta)
        {
            if (FreeFlight)
            {
                flyYaw += delta.x;
                flyPitch = Mathf.Clamp(flyPitch - delta.y, -85f, 85f);
                transform.rotation = Quaternion.Euler(flyPitch, flyYaw, 0);
            }
            else
            {
                yaw += delta.x;
                elevation = Mathf.Clamp(elevation - delta.y, -85f, 85f);
                ApplyOrbitPose();
            }
        }

        public void MoveLocal(Vector3 movement)
        {
            var displacement = transform.right * movement.x + Vector3.up * movement.y + transform.forward * movement.z;
            if (FreeFlight)
            {
                transform.position = ExhibitCentre + Vector3.ClampMagnitude(transform.position + displacement - ExhibitCentre, 60f);
            }
            else
            {
                pan = Vector3.ClampMagnitude(pan + displacement, 12f);
                ApplyOrbitPose();
            }
        }

        public void SetLearningViewport(bool visible)
        {
            view.rect = new Rect(0, 0, 1, 1);
        }

        private void Update()
        {
            if (view == null || !EmulatedInputEnabled) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.fKey.wasPressedThisFrame || keyboard.homeKey.wasPressedThisFrame) SetPreset(ViewPreset.Overview);
                if (keyboard.cKey.wasPressedThisFrame) ToggleFlight();
                if (keyboard.escapeKey.wasPressedThisFrame) rotating = panning = false;
                var direction = new Vector3(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                var speed = (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? 8f : 3f;
                MoveLocal(Vector3.ClampMagnitude(direction, 1f) * speed * Time.unscaledDeltaTime);
            }
            if (mouse == null) return;
            var overUi = IsOverUi(mouse.position.ReadValue());
            if (mouse.rightButton.wasPressedThisFrame) rotating = FreeFlight || !overUi;
            if (mouse.middleButton.wasPressedThisFrame) panning = !overUi;
            if (!mouse.rightButton.isPressed) rotating = false;
            if (!mouse.middleButton.isPressed) panning = false;
            var delta = mouse.delta.ReadValue();
            if (rotating) Look(delta);
            if (panning)
            {
                var scale = (FreeFlight ? 8f : distance) * 0.0015f;
                var displacement = -(transform.right * delta.x + transform.up * delta.y) * scale;
                if (FreeFlight) transform.position = ExhibitCentre + Vector3.ClampMagnitude(transform.position + displacement - ExhibitCentre, 60f);
                else pan = Vector3.ClampMagnitude(pan + displacement, 12f);
            }
            if (!overUi) Zoom(mouse.scroll.ReadValue().y / 120f);
        }

        private bool IsOverUi(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            pointerHits.Clear();
            if (pointerData == null) pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = position;
            EventSystem.current.RaycastAll(pointerData, pointerHits);
            return pointerHits.Count > 0;
        }

        private void LateUpdate()
        {
            if (view == null) return;
            if (!FreeFlight) ApplyOrbitPose();
        }

        private void ApplyOrbitPose()
        {
            var radians = elevation * Mathf.Deg2Rad;
            var azimuth = yaw * Mathf.Deg2Rad;
            var offset = new Vector3(Mathf.Sin(azimuth) * Mathf.Cos(radians), Mathf.Sin(radians), -Mathf.Cos(azimuth) * Mathf.Cos(radians)) * distance;
            transform.position = FocusPoint + offset;
            transform.rotation = Quaternion.LookRotation(FocusPoint - transform.position, Vector3.up);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                rotating = panning = false;
                SaveInputSettings();
            }
        }

        private void OnApplicationQuit() => SaveInputSettings();
    }
}
