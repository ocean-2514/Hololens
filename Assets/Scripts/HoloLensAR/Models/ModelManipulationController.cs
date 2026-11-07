using System;
using HoloLensAR.Interaction;
using MixedReality.Toolkit.SpatialManipulation;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace HoloLensAR.Models
{
    /// <summary>
    /// Applies discrete training commands to the model root and arbitrates them
    /// against active MRTK hand manipulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModelManipulationController : MonoBehaviour, ISimulationCommandTarget
    {
        [Serializable]
        private struct TransformSnapshot
        {
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;

            public TransformSnapshot(Transform target)
            {
                LocalPosition = target.localPosition;
                LocalRotation = target.localRotation;
                LocalScale = target.localScale;
            }

            public void Restore(Transform target)
            {
                target.localPosition = LocalPosition;
                target.localRotation = LocalRotation;
                target.localScale = LocalScale;
            }
        }

        [Header("References")]
        [SerializeField] private Transform manipulationRoot;
        [SerializeField] private ObjectManipulator objectManipulator;

        [Header("Training prototype controls")]
        [SerializeField, Min(1.01f)] private float zoomStepMultiplier = 1.15f;
        [SerializeField, Min(0.0001f)] private float minimumScaleMultiplier = 0.5f;
        [SerializeField, Min(0.0001f)] private float maximumScaleMultiplier = 3f;
        [SerializeField, Min(0.1f)] private float rotationStepDegrees = 15f;
        [SerializeField] private Vector3 localRotationAxis = Vector3.up;

        private TransformSnapshot initialState;
        private TransformSnapshot confirmedState;
        private TransformSnapshot undoState;
        private bool hasInitialState;
        private bool hasConfirmedState;
        private bool hasUndoState;
        private int activeSelectionCount;

        public bool IsHandManipulating { get; private set; }
        public string LastCommandMessage { get; private set; }
        public float CurrentScaleMultiplier
        {
            get
            {
                ResolveReferences();
                return hasInitialState && manipulationRoot != null ? GetCurrentScaleMultiplier() : 1f;
            }
        }
        public event Action<bool> HandManipulationChanged;
        public event Action<string> StatusChanged;

        private void Awake()
        {
            ResolveReferences();
            CacheInitialState();
            ValidateSettings();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (objectManipulator != null)
            {
                objectManipulator.selectEntered.AddListener(OnSelectEntered);
                objectManipulator.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (objectManipulator != null)
            {
                objectManipulator.selectEntered.RemoveListener(OnSelectEntered);
                objectManipulator.selectExited.RemoveListener(OnSelectExited);
            }

            activeSelectionCount = 0;
            SetHandManipulating(false);
        }

        public bool TryExecute(SimulationCommand command, CommandSource source)
        {
            ResolveReferences();

            if (manipulationRoot == null || !hasInitialState)
            {
                return Report(false, "Model root is not ready.");
            }

            if (IsHandManipulating)
            {
                return Report(false, "A hand is manipulating the model.");
            }

            switch (command)
            {
                case SimulationCommand.ZoomIn:
                    return ApplyZoom(zoomStepMultiplier);

                case SimulationCommand.ZoomOut:
                    return ApplyZoom(1f / zoomStepMultiplier);

                case SimulationCommand.RotateLeft:
                    return ApplyRotation(-rotationStepDegrees);

                case SimulationCommand.RotateRight:
                    return ApplyRotation(rotationStepDegrees);

                case SimulationCommand.Reset:
                    undoState = CaptureState();
                    hasUndoState = true;
                    initialState.Restore(manipulationRoot);
                    confirmedState = initialState;
                    hasConfirmedState = true;
                    return Report(true, "Model reset to its initial pose.");

                case SimulationCommand.Undo:
                    if (!hasUndoState)
                    {
                        return Report(false, "There is no discrete command to undo.");
                    }

                    undoState.Restore(manipulationRoot);
                    hasUndoState = false;
                    return Report(true, "Last discrete command undone.");

                case SimulationCommand.Confirm:
                    confirmedState = CaptureState();
                    hasConfirmedState = true;
                    hasUndoState = false;
                    return Report(true, "Current model pose confirmed.");

                case SimulationCommand.Cancel:
                    if (!hasConfirmedState)
                    {
                        return Report(false, "There is no confirmed pose to restore.");
                    }

                    confirmedState.Restore(manipulationRoot);
                    hasUndoState = false;
                    return Report(true, "Restored the last confirmed pose.");

                default:
                    return Report(false, "Unsupported simulation command.");
            }
        }

        private bool ApplyZoom(float factor)
        {
            float currentMultiplier = GetCurrentScaleMultiplier();
            float nextMultiplier = Mathf.Clamp(
                currentMultiplier * factor,
                minimumScaleMultiplier,
                maximumScaleMultiplier);

            if (Mathf.Approximately(currentMultiplier, nextMultiplier))
            {
                return Report(false, nextMultiplier <= minimumScaleMultiplier
                    ? "Minimum model scale reached."
                    : "Maximum model scale reached.");
            }

            undoState = CaptureState();
            hasUndoState = true;
            manipulationRoot.localScale = initialState.LocalScale * nextMultiplier;
            return Report(true, "Model scale changed to " + nextMultiplier.ToString("F2") + "x.");
        }

        private bool ApplyRotation(float degrees)
        {
            Vector3 axis = localRotationAxis.sqrMagnitude > 0.0001f
                ? localRotationAxis.normalized
                : Vector3.up;

            undoState = CaptureState();
            hasUndoState = true;
            manipulationRoot.localRotation *= Quaternion.AngleAxis(degrees, axis);
            return Report(true, "Model rotated " + degrees.ToString("F0") + " degrees.");
        }

        private float GetCurrentScaleMultiplier()
        {
            float initialMagnitude = initialState.LocalScale.magnitude;
            if (initialMagnitude <= 0.0001f)
            {
                return 1f;
            }

            return manipulationRoot.localScale.magnitude / initialMagnitude;
        }

        private TransformSnapshot CaptureState()
        {
            return new TransformSnapshot(manipulationRoot);
        }

        private void ResolveReferences()
        {
            if (manipulationRoot == null)
            {
                manipulationRoot = transform;
            }

            if (objectManipulator == null)
            {
                objectManipulator = GetComponent<ObjectManipulator>();
            }
        }

        private void CacheInitialState()
        {
            if (hasInitialState || manipulationRoot == null)
            {
                return;
            }

            initialState = CaptureState();
            confirmedState = initialState;
            hasInitialState = true;
            hasConfirmedState = true;
        }

        private void ValidateSettings()
        {
            zoomStepMultiplier = Mathf.Max(1.01f, zoomStepMultiplier);
            minimumScaleMultiplier = Mathf.Max(0.0001f, minimumScaleMultiplier);
            maximumScaleMultiplier = Mathf.Max(minimumScaleMultiplier, maximumScaleMultiplier);
            rotationStepDegrees = Mathf.Max(0.1f, rotationStepDegrees);
        }

        private void OnValidate()
        {
            ValidateSettings();
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            activeSelectionCount++;
            hasUndoState = false;
            SetHandManipulating(activeSelectionCount > 0);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            activeSelectionCount = Mathf.Max(0, activeSelectionCount - 1);
            SetHandManipulating(activeSelectionCount > 0);
        }

        private void SetHandManipulating(bool isManipulating)
        {
            if (IsHandManipulating == isManipulating)
            {
                return;
            }

            IsHandManipulating = isManipulating;
            if (HandManipulationChanged != null)
            {
                HandManipulationChanged(IsHandManipulating);
            }
        }

        private bool Report(bool succeeded, string message)
        {
            LastCommandMessage = message;
            if (StatusChanged != null)
            {
                StatusChanged(message);
            }

            return succeeded;
        }
    }
}
