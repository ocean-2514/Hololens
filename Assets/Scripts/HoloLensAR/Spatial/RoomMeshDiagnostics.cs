using System.Text;
using MixedReality.Toolkit;
using TMPro;
using UnityEngine;

namespace HoloLensAR.Spatial
{
    /// <summary>
    /// Connects the room-mesh status view and three MRTK pressable controls to the mesh services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomMeshDiagnostics : MonoBehaviour
    {
        [Header("Services")]
        [SerializeField] private MonoBehaviour meshSourceBehaviour;
        [SerializeField] private RoomMeshDisplayController displayController;

        [Header("Status text")]
        [SerializeField] private TMP_Text statusText;

        [Header("MRTK controls")]
        [SerializeField] private StatefulInteractable scanButton;
        [SerializeField] private TMP_Text scanButtonLabel;
        [SerializeField] private StatefulInteractable clearButton;
        [SerializeField] private StatefulInteractable displayButton;
        [SerializeField] private TMP_Text displayButtonLabel;

        private IRoomMeshSource meshSource;
        private RoomMeshStatus lastStatus;

        public void Bind(
            MonoBehaviour sourceBehaviour,
            RoomMeshDisplayController display,
            TMP_Text output,
            StatefulInteractable scan,
            TMP_Text scanLabel,
            StatefulInteractable clear,
            StatefulInteractable displayToggle,
            TMP_Text displayLabel)
        {
            Unsubscribe();

            meshSourceBehaviour = sourceBehaviour;
            displayController = display;
            statusText = output;
            scanButton = scan;
            scanButtonLabel = scanLabel;
            clearButton = clear;
            displayButton = displayToggle;
            displayButtonLabel = displayLabel;

            Subscribe();
            RefreshView();
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshView();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            meshSource = meshSourceBehaviour as IRoomMeshSource;
            if (meshSource != null)
            {
                meshSource.StatusChanged -= OnStatusChanged;
                meshSource.StatusChanged += OnStatusChanged;
                lastStatus = meshSource.LastStatus;
            }

            if (scanButton != null)
            {
                scanButton.OnClicked.RemoveListener(ToggleScanning);
                scanButton.OnClicked.AddListener(ToggleScanning);
            }

            if (clearButton != null)
            {
                clearButton.OnClicked.RemoveListener(ClearMeshes);
                clearButton.OnClicked.AddListener(ClearMeshes);
            }

            if (displayButton != null)
            {
                displayButton.OnClicked.RemoveListener(ToggleDisplay);
                displayButton.OnClicked.AddListener(ToggleDisplay);
            }
        }

        private void Unsubscribe()
        {
            if (meshSource != null)
            {
                meshSource.StatusChanged -= OnStatusChanged;
            }

            if (scanButton != null)
            {
                scanButton.OnClicked.RemoveListener(ToggleScanning);
            }

            if (clearButton != null)
            {
                clearButton.OnClicked.RemoveListener(ClearMeshes);
            }

            if (displayButton != null)
            {
                displayButton.OnClicked.RemoveListener(ToggleDisplay);
            }
        }

        public void ToggleScanning()
        {
            if (meshSource == null)
            {
                SetStatusMessage("Mesh source is not assigned.");
                return;
            }

            if (meshSource.IsScanning)
            {
                meshSource.StopScanning();
            }
            else if (!meshSource.TryStartScanning())
            {
                lastStatus = meshSource.LastStatus;
            }

            RefreshView();
        }

        public void ClearMeshes()
        {
            if (meshSource == null)
            {
                SetStatusMessage("Mesh source is not assigned.");
                return;
            }

            meshSource.ClearMeshes();
            lastStatus = meshSource.LastStatus;
            RefreshView();
        }

        public void ToggleDisplay()
        {
            if (displayController != null)
            {
                displayController.ToggleVisibility();
            }

            RefreshView();
        }

        private void OnStatusChanged(RoomMeshStatus status)
        {
            lastStatus = status;
            RefreshView();
        }

        private void RefreshView()
        {
            if (scanButtonLabel != null)
            {
                scanButtonLabel.text = meshSource != null && meshSource.IsScanning ? "Stop Scan" : "Start Scan";
            }

            if (displayButtonLabel != null)
            {
                displayButtonLabel.text = displayController != null && displayController.IsVisible
                    ? "Hide Mesh"
                    : "Show Mesh";
            }

            if (statusText == null)
            {
                return;
            }

            if (meshSource == null)
            {
                statusText.text = "Room Mesh\nMesh source is not assigned.";
                return;
            }

            RoomMeshStatus status = lastStatus ?? meshSource.LastStatus;
            if (status == null)
            {
                statusText.text = "Room Mesh\nWaiting for XR initialization...";
                return;
            }

            StringBuilder text = new StringBuilder(256);
            text.AppendLine("ROOM MESH PROTOTYPE");
            if (status.IsAvailable)
            {
                text.AppendLine("Subsystem: available");
            }
            else if (!status.IsScanning && string.IsNullOrEmpty(status.ErrorMessage))
            {
                text.AppendLine("Subsystem: not initialized (scan is off)");
                text.AppendLine("Press Start Scan to initialize and check device mesh support.");
            }
            else
            {
                text.AppendLine("Subsystem: not available");
            }

            text.Append("Scanning: ").AppendLine(status.IsScanning ? "ON" : "OFF");
            text.Append("Meshes: ").Append(status.CurrentMeshCount)
                .Append("   Vertices: ").AppendLine(status.VertexCount.ToString());
            text.Append("Triangles: ").AppendLine(status.TriangleCount.ToString());
            text.Append("Last change  +").Append(status.AddedCount)
                .Append("  ~").Append(status.UpdatedCount)
                .Append("  -").AppendLine(status.RemovedCount.ToString());
            text.Append("Status time: ").Append(status.LastUpdateRealtime.ToString("F1"))
                .AppendLine("s since app start");
            text.Append("Debug render: ").AppendLine(
                displayController != null && displayController.IsVisible ? "ON" : "OFF");

            if (!string.IsNullOrEmpty(status.ErrorMessage))
            {
                text.Append("Status: ").AppendLine(status.ErrorMessage);
            }

            statusText.text = text.ToString();
        }

        private void SetStatusMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = "ROOM MESH PROTOTYPE\n" + message;
            }
        }
    }
}
