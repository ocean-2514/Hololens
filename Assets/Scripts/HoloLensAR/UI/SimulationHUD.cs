using System;
using System.Text;
using HoloLensAR.Interaction;
using HoloLensAR.Models;
using HoloLensAR.Spatial;
using TMPro;
using UnityEngine;

namespace HoloLensAR.UI
{
    /// <summary>
    /// Compact training-mode HUD for room mesh, model manipulation, and input status.
    /// It displays command enums and results only; it never stores recognized speech text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationHUD : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private string modeLabel = "TRAINING SIMULATION";
        [SerializeField] private string modelLabel = "Squirrel";
        [SerializeField, Min(0.1f)] private float refreshIntervalSeconds = 0.25f;

        [Header("Services")]
        [SerializeField] private MonoBehaviour meshSourceBehaviour;
        [SerializeField] private RoomMeshDisplayController meshDisplayController;
        [SerializeField] private TrainingModelDefinition modelDefinition;
        [SerializeField] private ModelManipulationController modelController;
        [SerializeField] private SimulationCommandRouter commandRouter;
        [SerializeField] private MrtkKeywordCommandSource keywordCommandSource;

        private IRoomMeshSource meshSource;
        private RoomMeshStatus lastMeshStatus;
        private string lastInput = "None";
        private string lastResult = "No command received.";
        private float nextRefreshTime;

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RefreshOutput();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            refreshIntervalSeconds = Mathf.Max(0.1f, refreshIntervalSeconds);
            nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
            RefreshOutput();
        }

        private void ResolveReferences()
        {
            meshSource = meshSourceBehaviour as IRoomMeshSource;
            if (meshSource != null)
            {
                lastMeshStatus = meshSource.LastStatus;
            }
        }

        private void Subscribe()
        {
            if (meshSource != null)
            {
                meshSource.StatusChanged -= OnMeshStatusChanged;
                meshSource.StatusChanged += OnMeshStatusChanged;
            }

            if (modelController != null)
            {
                modelController.StatusChanged -= OnModelStatusChanged;
                modelController.StatusChanged += OnModelStatusChanged;
            }

            if (commandRouter != null)
            {
                commandRouter.CommandProcessed -= OnCommandProcessed;
                commandRouter.CommandProcessed += OnCommandProcessed;
            }

            if (keywordCommandSource != null)
            {
                keywordCommandSource.AvailabilityChanged -= OnVoiceAvailabilityChanged;
                keywordCommandSource.AvailabilityChanged += OnVoiceAvailabilityChanged;
            }
        }

        private void Unsubscribe()
        {
            if (meshSource != null)
            {
                meshSource.StatusChanged -= OnMeshStatusChanged;
            }

            if (modelController != null)
            {
                modelController.StatusChanged -= OnModelStatusChanged;
            }

            if (commandRouter != null)
            {
                commandRouter.CommandProcessed -= OnCommandProcessed;
            }

            if (keywordCommandSource != null)
            {
                keywordCommandSource.AvailabilityChanged -= OnVoiceAvailabilityChanged;
            }
        }

        private void OnMeshStatusChanged(RoomMeshStatus status)
        {
            lastMeshStatus = status;
            RefreshOutput();
        }

        private void OnModelStatusChanged(string message)
        {
            lastResult = Limit(message, 74);
            RefreshOutput();
        }

        private void OnCommandProcessed(
            SimulationCommand command,
            CommandSource source,
            bool succeeded,
            string message)
        {
            string inputSource = source == CommandSource.Voice ? "VOICE" : "BUTTON";
            lastInput = inputSource + " / " + FormatCommand(command);
            lastResult = (succeeded ? "OK: " : "Blocked: ") + Limit(message, 64);
            RefreshOutput();
        }

        private void OnVoiceAvailabilityChanged(bool available, string message)
        {
            RefreshOutput();
        }

        private void RefreshOutput()
        {
            if (outputText == null)
            {
                return;
            }

            StringBuilder text = new StringBuilder(512);
            text.AppendLine(string.IsNullOrWhiteSpace(modeLabel) ? "TRAINING SIMULATION" : modeLabel.Trim());
            text.Append("Model: ").Append(GetModelLabel());
            if (modelDefinition != null && !string.IsNullOrWhiteSpace(modelDefinition.AssetVersion))
            {
                text.Append("  |  v").Append(modelDefinition.AssetVersion);
            }
            text.AppendLine();

            text.Append("Scale: ")
                .Append(modelController != null ? modelController.CurrentScaleMultiplier.ToString("F2") : "1.00")
                .Append("x  |  Hand: ")
                .AppendLine(modelController != null && modelController.IsHandManipulating ? "Manipulating" : "Ready");

            if (meshSource == null)
            {
                text.AppendLine("Room mesh: Source not assigned");
                text.AppendLine("Geometry: waiting for mesh source");
                text.AppendLine("Mesh changes: n/a");
            }
            else
            {
                RoomMeshStatus status = lastMeshStatus ?? meshSource.LastStatus;
                string scanState = status == null
                    ? "Initializing"
                    : status.IsScanning ? "Scanning" : "Stopped";
                text.Append("Room mesh: ").Append(scanState)
                    .Append("  |  Render: ")
                    .AppendLine(meshDisplayController != null && meshDisplayController.IsVisible ? "Visible" : "Hidden");

                if (status == null)
                {
                    text.AppendLine("Geometry: waiting for XR initialization");
                    text.AppendLine("Mesh changes: n/a");
                }
                else
                {
                    text.Append("Geometry: ").Append(status.CurrentMeshCount)
                        .Append(" meshes  |  ").Append(status.VertexCount)
                        .Append(" vertices  |  ").Append(status.TriangleCount).AppendLine(" triangles");

                    float age = Mathf.Max(0f, Time.realtimeSinceStartup - status.LastUpdateRealtime);
                    text.Append("Mesh changes: +").Append(status.AddedCount)
                        .Append("  ~").Append(status.UpdatedCount)
                        .Append("  -").Append(status.RemovedCount)
                        .Append("  |  update ").Append(age.ToString("F1")).AppendLine("s ago");

                    if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
                    {
                        text.Append("Mesh status: ").AppendLine(Limit(status.ErrorMessage, 74));
                    }
                }
            }

            if (keywordCommandSource == null)
            {
                text.AppendLine("Voice: adapter not assigned");
            }
            else if (keywordCommandSource.IsAvailable)
            {
                text.Append("Voice: Ready (")
                    .Append(keywordCommandSource.RegisteredKeywordCount)
                    .AppendLine(" commands)");
            }
            else
            {
                string voiceStatus = string.IsNullOrWhiteSpace(keywordCommandSource.StatusMessage)
                    ? "waiting for speech subsystem"
                    : Limit(keywordCommandSource.StatusMessage, 72);
                text.Append("Voice: ").AppendLine(voiceStatus);
            }

            text.Append("Last input: ").AppendLine(lastInput);
            text.Append("Result: ").Append(lastResult);

            string nextText = text.ToString();
            if (outputText.text != nextText)
            {
                outputText.text = nextText;
            }
        }

private string GetModelLabel()
        {
            if (!string.IsNullOrWhiteSpace(modelLabel))
            {
                return modelLabel.Trim();
            }

            if (modelDefinition != null && modelDefinition.ModelPrefab != null)
            {
                return modelDefinition.ModelPrefab.name;
            }

            return "Training model";
        }

        private static string FormatCommand(SimulationCommand command)
        {
            switch (command)
            {
                case SimulationCommand.ZoomIn: return "Zoom In";
                case SimulationCommand.ZoomOut: return "Zoom Out";
                case SimulationCommand.RotateLeft: return "Rotate Left";
                case SimulationCommand.RotateRight: return "Rotate Right";
                case SimulationCommand.Reset: return "Reset";
                case SimulationCommand.Undo: return "Undo";
                case SimulationCommand.Confirm: return "Confirm";
                case SimulationCommand.Cancel: return "Cancel";
                default: return command.ToString();
            }
        }

        private static string Limit(string value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "No details";
            }

            string normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength - 3) + "...";
        }
    }
}