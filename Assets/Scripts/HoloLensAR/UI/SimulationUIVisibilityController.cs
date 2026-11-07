using TMPro;
using UnityEngine;

namespace HoloLensAR.UI
{
    /// <summary>
    /// Keeps one small UI toggle available while hiding or showing the rest of the
    /// camera-relative training controls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationUIVisibilityController : MonoBehaviour
    {
        [SerializeField] private GameObject interfaceRoot;
        [SerializeField] private TMP_Text toggleLabel;
        [SerializeField] private string hideLabel = "Hide UI";
        [SerializeField] private string showLabel = "Show UI";

        public bool IsVisible
        {
            get { return interfaceRoot != null && interfaceRoot.activeSelf; }
        }

        private void Awake()
        {
            RefreshLabel();
        }

        public void ToggleVisibility()
        {
            if (interfaceRoot == null)
            {
                return;
            }

            SetVisible(!interfaceRoot.activeSelf);
        }

        public void SetVisible(bool visible)
        {
            if (interfaceRoot != null)
            {
                interfaceRoot.SetActive(visible);
            }

            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (toggleLabel != null)
            {
                toggleLabel.text = IsVisible ? hideLabel : showLabel;
            }
        }
    }
}