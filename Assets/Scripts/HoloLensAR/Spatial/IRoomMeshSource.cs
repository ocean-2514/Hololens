using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoloLensAR.Spatial
{
    /// <summary>
    /// Application-facing contract for a spatial room-mesh provider.
    /// </summary>
    public interface IRoomMeshSource
    {
        bool IsAvailable { get; }
        bool IsScanning { get; }
        IEnumerable<MeshFilter> CurrentMeshes { get; }
        RoomMeshStatus LastStatus { get; }
        event Action<RoomMeshStatus> StatusChanged;

        bool TryStartScanning();
        void StopScanning();
        void ClearMeshes();
    }
}
