using System;
using UnityEngine;

namespace DTET.VisualPrototype
{
    [DisallowMultipleComponent]
    public sealed class ObservatorySceneObjects : MonoBehaviour
    {
        [SerializeField] private GameObject worldRoot;
        [SerializeField] private Camera inspectionCamera;
        [SerializeField] private SimulationBodyBinding primaryBody, secondaryBody, remnant;
        [SerializeField] private Transform environmentRoot, gasRoot, mergerRoot, waveRoot, opticsRoot;
        [SerializeField] private RectTransform tutorRoot, wristRoot;

        public GameObject WorldRoot => worldRoot;
        public Camera InspectionCamera => inspectionCamera;
        public SimulationBodyBinding PrimaryBody => primaryBody;
        public SimulationBodyBinding SecondaryBody => secondaryBody;
        public SimulationBodyBinding Remnant => remnant;
        public Transform EnvironmentRoot => environmentRoot;
        public Transform GasRoot => gasRoot;
        public Transform MergerRoot => mergerRoot;
        public Transform WaveRoot => waveRoot;
        public Transform OpticsRoot => opticsRoot;
        public RectTransform TutorRoot => tutorRoot;
        public RectTransform WristRoot => wristRoot;

        public void ValidateBindings()
        {
            if (worldRoot == null || inspectionCamera == null || primaryBody == null || secondaryBody == null || remnant == null ||
                environmentRoot == null || gasRoot == null || mergerRoot == null || waveRoot == null || opticsRoot == null || tutorRoot == null || wristRoot == null)
                throw new InvalidOperationException("The editable observatory has an unassigned scene reference.");
            if (primaryBody == secondaryBody || primaryBody == remnant || secondaryBody == remnant)
                throw new InvalidOperationException("Body A, Body B and Remnant must be different saved objects.");
            foreach (var body in new[] { primaryBody, secondaryBody, remnant })
            {
                if (!body.transform.IsChildOf(worldRoot.transform))
                    throw new InvalidOperationException(body.name + " must belong to the saved observatory World root.");
                body.ValidateBinding();
            }
            if (tutorRoot.GetComponent<GuidedObservationController>() == null || wristRoot.GetComponent<InteractiveWristDisplay>() == null)
                throw new InvalidOperationException("The saved tutor and wrist roots require their controller components.");
        }

        public void AttachInterface(Transform presentation)
        {
            foreach (var root in new[] { tutorRoot, wristRoot })
            {
                root.SetParent(presentation, false);
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                root.offsetMin = root.offsetMax = Vector2.zero;
                root.gameObject.SetActive(true);
            }
        }

        public void ParkInterface()
        {
            foreach (var root in new[] { tutorRoot, wristRoot })
                if (root != null) { root.SetParent(transform, false); root.gameObject.SetActive(false); }
        }
    }
}
