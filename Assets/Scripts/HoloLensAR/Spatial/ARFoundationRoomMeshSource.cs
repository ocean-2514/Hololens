using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloLensAR.Spatial
{
    /// <summary>
    /// Adapts AR Foundation's ARMeshManager to the application's IRoomMeshSource contract.
    /// ARMeshManager must be below an XROrigin in the hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ARFoundationRoomMeshSource : MonoBehaviour, IRoomMeshSource
    {
        private static readonly MeshFilter[] EmptyMeshes = new MeshFilter[0];

        [SerializeField]
        [Tooltip("AR Foundation manager that owns the generated room meshes.")]
        private ARMeshManager meshManager;

        private string lastError = string.Empty;
        private bool subscribed;

        public bool IsAvailable
        {
            get { return meshManager != null && meshManager.subsystem != null; }
        }

        public bool IsScanning
        {
            get
            {
                return meshManager != null && meshManager.enabled &&
                       meshManager.subsystem != null && meshManager.subsystem.running;
            }
        }

        public IEnumerable<MeshFilter> CurrentMeshes
        {
            get
            {
                if (meshManager == null || (!Application.isPlaying && meshManager.subsystem == null))
                {
                    return EmptyMeshes;
                }

                return meshManager.meshes;
            }
        }

        public RoomMeshStatus LastStatus { get; private set; }

        public event Action<RoomMeshStatus> StatusChanged;

        private void Awake()
        {
            ResolveManager();
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
            PublishStatus(0, 0, 0);
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (meshManager != null && meshManager.enabled)
            {
                meshManager.enabled = false;
            }

            PublishStatus(0, 0, 0);
        }

        /// <summary>
        /// Starts the provider only after the scene has an active XR mesh subsystem.
        /// </summary>
        public bool TryStartScanning()
        {
            ResolveManager();

            if (!isActiveAndEnabled)
            {
                return Fail("ARFoundationRoomMeshSource is disabled.");
            }

            if (meshManager == null)
            {
                return Fail("ARMeshManager is not assigned.");
            }

            if (meshManager.meshPrefab == null)
            {
                return Fail("ARMeshManager.meshPrefab is not assigned.");
            }

            XROrigin origin = meshManager.GetComponentInParent<XROrigin>();
            if (origin == null)
            {
                return Fail("ARMeshManager must be a child of an XROrigin.");
            }

            lastError = string.Empty;
            if (!meshManager.enabled)
            {
                meshManager.enabled = true;
            }

            if (!meshManager.enabled || meshManager.subsystem == null)
            {
                return Fail("No active XRMeshSubsystem is available. Check the device XR loader and SpatialPerception capability.");
            }

            if (!meshManager.subsystem.running)
            {
                return Fail("XRMeshSubsystem did not start. Check the device OpenXR mesh support.");
            }

            PublishStatus(0, 0, 0);
            return true;
        }

        /// <summary>
        /// Disabling ARMeshManager stops its XRMeshSubsystem polling while retaining this adapter.
        /// </summary>
        public void StopScanning()
        {
            if (meshManager != null && meshManager.enabled)
            {
                meshManager.enabled = false;
            }

            PublishStatus(0, 0, 0);
        }

        /// <summary>
        /// Destroys generated mesh objects. If scanning stays enabled, the provider may discover them again.
        /// </summary>
        public void ClearMeshes()
        {
            if (meshManager == null)
            {
                Fail("ARMeshManager is not assigned.");
                return;
            }

            int removedCount = 0;
            if (Application.isPlaying)
            {
                removedCount = meshManager.meshes != null ? meshManager.meshes.Count : 0;
                meshManager.DestroyAllMeshes();
            }

            PublishStatus(0, 0, removedCount);
        }

        public void Bind(ARMeshManager manager)
        {
            if (subscribed)
            {
                Unsubscribe();
            }

            meshManager = manager;
            ResolveManager();
            Subscribe();
        }

        private void ResolveManager()
        {
            if (meshManager == null)
            {
                meshManager = GetComponent<ARMeshManager>();
            }
        }

        private void Subscribe()
        {
            if (meshManager == null || subscribed)
            {
                return;
            }

            meshManager.meshesChanged += OnMeshesChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (meshManager != null && subscribed)
            {
                meshManager.meshesChanged -= OnMeshesChanged;
            }

            subscribed = false;
        }

        private bool Fail(string message)
        {
            lastError = message;
            PublishStatus(0, 0, 0);
            Debug.LogWarning("[Room Mesh] " + message, this);
            return false;
        }

        private void OnMeshesChanged(ARMeshesChangedEventArgs args)
        {
            int added = args.added != null ? args.added.Count : 0;
            int updated = args.updated != null ? args.updated.Count : 0;
            int removed = args.removed != null ? args.removed.Count : 0;
            PublishStatus(added, updated, removed);
        }

        private void PublishStatus(int added, int updated, int removed)
        {
            int meshCount = 0;
            long vertexCount = 0;
            long triangleCount = 0;

            if (meshManager != null && (Application.isPlaying || meshManager.subsystem != null))
            {
                IList<MeshFilter> meshes = meshManager.meshes;
                if (meshes != null)
                {
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        MeshFilter filter = meshes[i];
                        if (filter == null || filter.sharedMesh == null)
                        {
                            continue;
                        }

                        Mesh mesh = filter.sharedMesh;
                        meshCount++;
                        vertexCount += mesh.vertexCount;

                        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                        {
                            triangleCount += mesh.GetIndexCount(subMesh) / 3;
                        }
                    }
                }
            }

            LastStatus = new RoomMeshStatus(
                IsAvailable,
                IsScanning,
                added,
                updated,
                removed,
                meshCount,
                vertexCount,
                triangleCount,
                Time.realtimeSinceStartup,
                lastError);

            Action<RoomMeshStatus> handler = StatusChanged;
            if (handler != null)
            {
                handler(LastStatus);
            }
        }
    }
}
