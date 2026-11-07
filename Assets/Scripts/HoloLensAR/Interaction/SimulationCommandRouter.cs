using System;
using HoloLensAR;
using HoloLensAR.Models;
using UnityEngine;

namespace HoloLensAR.Interaction
{
    /// <summary>
    /// Routes button or voice commands to one target and reports the result to a HUD.
    /// It does not perform input recognition or edit model transforms itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationCommandRouter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour targetBehaviour;
        [SerializeField] private MonoBehaviour keywordSourceBehaviour;
        [SerializeField] private bool commandsEnabled = true;

        private IKeywordCommandSource keywordSource;

        public string LastMessage { get; private set; }
        public bool IsKeywordSourceAvailable
        {
            get { return keywordSource != null && keywordSource.IsAvailable; }
        }
        public event Action<SimulationCommand, CommandSource, bool, string> CommandProcessed;

        private void OnEnable()
        {
            SubscribeKeywordSource();
        }

        private void OnDisable()
        {
            UnsubscribeKeywordSource();
        }

        public void Bind(MonoBehaviour target)
        {
            targetBehaviour = target;
        }

        public void BindKeywordSource(MonoBehaviour source)
        {
            UnsubscribeKeywordSource();
            keywordSourceBehaviour = source;
            SubscribeKeywordSource();
        }

        public void SetCommandsEnabled(bool enabled)
        {
            commandsEnabled = enabled;
        }

        public bool Submit(SimulationCommand command, CommandSource source)
        {
            if (!commandsEnabled)
            {
                return Publish(command, source, false, "Command routing is disabled.");
            }

            if (targetBehaviour == null)
            {
                return Publish(command, source, false, "Command target is not assigned.");
            }

            ISimulationCommandTarget target = targetBehaviour as ISimulationCommandTarget;
            if (target == null)
            {
                return Publish(command, source, false, "Assigned target does not implement ISimulationCommandTarget.");
            }

            ModelManipulationController modelController = targetBehaviour as ModelManipulationController;
            if (modelController != null && modelController.IsHandManipulating)
            {
                return Publish(command, source, false, "A hand is manipulating the model.");
            }

            bool succeeded = target.TryExecute(command, source);
            string message = succeeded
                ? (modelController != null ? modelController.LastCommandMessage : "Command completed.")
                : (modelController != null ? modelController.LastCommandMessage : "Command was rejected.");

            return Publish(command, source, succeeded, message);
        }

        public void SubmitZoomIn() { Submit(SimulationCommand.ZoomIn, CommandSource.UserInterface); }
        public void SubmitZoomOut() { Submit(SimulationCommand.ZoomOut, CommandSource.UserInterface); }
        public void SubmitRotateLeft() { Submit(SimulationCommand.RotateLeft, CommandSource.UserInterface); }
        public void SubmitRotateRight() { Submit(SimulationCommand.RotateRight, CommandSource.UserInterface); }
        public void SubmitReset() { Submit(SimulationCommand.Reset, CommandSource.UserInterface); }
        public void SubmitUndo() { Submit(SimulationCommand.Undo, CommandSource.UserInterface); }
        public void SubmitConfirm() { Submit(SimulationCommand.Confirm, CommandSource.UserInterface); }
        public void SubmitCancel() { Submit(SimulationCommand.Cancel, CommandSource.UserInterface); }

        private void SubscribeKeywordSource()
        {
            keywordSource = keywordSourceBehaviour as IKeywordCommandSource;
            if (keywordSource != null)
            {
                keywordSource.CommandRecognized -= OnKeywordRecognized;
                keywordSource.CommandRecognized += OnKeywordRecognized;
            }
        }

        private void UnsubscribeKeywordSource()
        {
            if (keywordSource != null)
            {
                keywordSource.CommandRecognized -= OnKeywordRecognized;
            }

            keywordSource = null;
        }

        private void OnKeywordRecognized(SimulationCommand command)
        {
            Submit(command, CommandSource.Voice);
        }

        private bool Publish(
            SimulationCommand command,
            CommandSource source,
            bool succeeded,
            string message)
        {
            LastMessage = message;
            if (CommandProcessed != null)
            {
                CommandProcessed(command, source, succeeded, message);
            }

            return succeeded;
        }
    }
}
