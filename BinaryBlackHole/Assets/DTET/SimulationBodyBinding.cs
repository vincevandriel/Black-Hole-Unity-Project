using System;
using BlackHole.Staging.Simulation;
using UnityEngine;

namespace DTET.VisualPrototype
{
    [DisallowMultipleComponent]
    public sealed class SimulationBodyBinding : MonoBehaviour
    {
        [SerializeField] private Renderer surface;
        [SerializeField] private SphereCollider interactionCollider;

        public Renderer Surface => surface;
        public SphereCollider InteractionCollider => interactionCollider;
        public InteractiveBinaryBody? State { get; private set; }
        public bool HasBody => State.HasValue;
        public event Action<SimulationBodyBinding> StateUpdated;

        public void Apply(InteractiveBinaryBody? state)
        {
            State = state;
            gameObject.SetActive(state.HasValue);
            if (state.HasValue)
            {
                var p = state.Value.PositionRg;
                transform.position = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
                transform.localScale = Vector3.one * (float)(2 * state.Value.HorizonRadiusRg);
            }
            StateUpdated?.Invoke(this);
        }

        public void ValidateBinding()
        {
            if (surface == null || interactionCollider == null || surface.transform != transform || interactionCollider.transform != transform)
                throw new InvalidOperationException(name + " requires its saved Renderer and SphereCollider references.");
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
                throw new InvalidOperationException(name + " is driven by the binary simulation. Use a kinematic Rigidbody for interaction.");
        }

        private void Reset()
        {
            surface = GetComponent<Renderer>();
            interactionCollider = GetComponent<SphereCollider>();
        }
    }
}
