using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace HoloLensAR.Spatial
{
    /// <summary>
    /// Controls debug rendering of ARMeshManager-created mesh instances.
    /// Visibility does not start or stop scanning.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomMeshDisplayController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("AR Foundation manager whose generated MeshRenderers will be controlled.")]
        private ARMeshManager meshManager;

        [SerializeField]
        private bool visible = true;

        private bool subscribed;

        public bool IsVisible { get { return visible; } }

        private void Awake()
        {
            ResolveManager();
        }

        private void OnEnable()
        {
            ResolveManager();
            Subscribe();
            ApplyVisibility();
        }

        private void OnDisable()
        {
            Unsubscribe();
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
            ApplyVisibility();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            ApplyVisibility();
        }

        public bool ToggleVisibility()
        {
            SetVisible(!visible);
            return visible;
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

        private void OnMeshesChanged(ARMeshesChangedEventArgs args)
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (meshManager == null || !Application.isPlaying)
            {
                return;
            }

            for (int i = 0; i < meshManager.meshes.Count; i++)
            {
                MeshFilter filter = meshManager.meshes[i];
                if (filter == null)
                {
                    continue;
                }

                MeshRenderer meshRenderer = filter.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = visible;
                }
            }
        }
    }
}
