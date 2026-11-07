using System;

namespace HoloLensAR.Spatial
{
    /// <summary>
    /// Lightweight snapshot of the room-mesh subsystem. It intentionally contains counts,
    /// not copied vertex/index buffers.
    /// </summary>
    public sealed class RoomMeshStatus
    {
        public bool IsAvailable { get; private set; }
        public bool IsScanning { get; private set; }
        public int AddedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int CurrentMeshCount { get; private set; }
        public long VertexCount { get; private set; }
        public long TriangleCount { get; private set; }
        public float LastUpdateRealtime { get; private set; }
        public string ErrorMessage { get; private set; }

        public RoomMeshStatus(
            bool isAvailable,
            bool isScanning,
            int addedCount,
            int updatedCount,
            int removedCount,
            int currentMeshCount,
            long vertexCount,
            long triangleCount,
            float lastUpdateRealtime,
            string errorMessage)
        {
            IsAvailable = isAvailable;
            IsScanning = isScanning;
            AddedCount = addedCount;
            UpdatedCount = updatedCount;
            RemovedCount = removedCount;
            CurrentMeshCount = currentMeshCount;
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            LastUpdateRealtime = lastUpdateRealtime;
            ErrorMessage = errorMessage ?? string.Empty;
        }
    }
}
