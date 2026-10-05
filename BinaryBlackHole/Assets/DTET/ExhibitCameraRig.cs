using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DTET.VisualPrototype
{
    /// <summary>Desktop observation rig. Commands can be reused by a future tracked XR rig.</summary>
    public sealed class ExhibitCameraRig : MonoBehaviour
    {
        public enum ObservationTarget { Binary, BodyA, BodyB }
        public enum ViewPreset { Overview, Above, Side }

        public const float MinimumDistance = 1.4f;
        public const float MaximumDistance = 36f;
        private static readonly Vector3 ExhibitCentre = new Vector3(-3.25f, 0, 0);
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        private Camera view;
        private Transform bodyA;
        private Transform bodyB;
        private Transform background;
        private Vector3 pan;
        private float yaw;
        private float elevation = 25f;
        private float distance = 13.5f;
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

        public void Initialize(Camera camera, Transform firstBody, Transform secondBody, Transform stars)
        {
            view = camera;
            bodyA = firstBody;
            bodyB = secondBody;
            background = stars;
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
            distance = 13.5f;
            yaw = preset == ViewPreset.Side ? 65f : 0f;
            elevation = preset == ViewPreset.Above ? 85f : preset == ViewPreset.Side ? 2f : 25f;
            ApplyOrbitPose();
        }

        public void Focus(ObservationTarget target)
        {
            Target = target;
            FreeFlight = false;
            pan = Vector3.zero;
            distance = target == ObservationTarget.Binary ? 13.5f : 4.2f;
            ApplyOrbitPose();
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
            }
        }

        public void Zoom(float steps)
        {
            if (!float.IsFinite(steps)) return;
            if (FreeFlight)
                MoveLocal(new Vector3(0, 0, steps * 0.8f));
            else
            {
                distance = Mathf.Clamp(distance * Mathf.Exp(-Mathf.Clamp(steps, -20f, 20f) * 0.12f), MinimumDistance, MaximumDistance);
                ApplyOrbitPose();
            }
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
            view.rect = visible ? new Rect(0, 0, 0.68f, 1) : new Rect(0, 0, 1, 1);
        }

        private void Update()
        {
            if (view == null) return;
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
            if (mouse.rightButton.wasPressedThisFrame) rotating = !overUi;
            if (mouse.middleButton.wasPressedThisFrame) panning = !overUi;
            if (!mouse.rightButton.isPressed) rotating = false;
            if (!mouse.middleButton.isPressed) panning = false;
            var delta = mouse.delta.ReadValue();
            if (rotating) Rotate(delta * 0.18f);
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
            if (background != null) background.position = transform.position;
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
            if (!focused) rotating = panning = false;
        }
    }
}
