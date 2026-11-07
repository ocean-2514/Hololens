using UnityEngine;

namespace HoloLensAR.Models
{
    /// <summary>
    /// Unit metadata and presentation defaults for a non-patient training model.
    /// The referenced prefab root is responsible for applying sourceUnitsToMeters
    /// to the imported visual hierarchy.
    /// </summary>
    [CreateAssetMenu(fileName = "TrainingModelDefinition", menuName = "HoloLens AR/Training Model Definition")]
    public sealed class TrainingModelDefinition : ScriptableObject
    {
        public enum SourceUnit
        {
            Meter,
            Centimeter,
            Millimeter,
            Custom
        }

        public enum OriginConvention
        {
            SourceDefined,
            BoundingBoxCenter,
            AnatomicalLandmark,
            Unknown
        }

        [Header("Asset")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private string assetVersion = "1.0.0";

        [Header("Coordinate Metadata")]
        [SerializeField] private SourceUnit sourceUnit = SourceUnit.Meter;
        [SerializeField, Min(0.000001f)] private float sourceUnitsToMeters = 1f;
        [SerializeField] private Vector3 sourceUpAxis = Vector3.up;
        [SerializeField] private Vector3 sourceForwardAxis = Vector3.forward;
        [SerializeField] private OriginConvention originConvention = OriginConvention.Unknown;
        [SerializeField, TextArea(2, 4)] private string originNotes;

        [Header("Presentation")]
        [SerializeField, Min(0.0001f)] private float defaultDisplayScale = 1f;

        [Header("Provenance")]
        [SerializeField, TextArea(2, 5)] private string sourceAndLicenseNotes;
        [SerializeField] private string contentHash;

        public GameObject ModelPrefab => modelPrefab;
        public string AssetVersion => assetVersion;
        public SourceUnit Unit => sourceUnit;
        public float SourceUnitsToMeters => sourceUnitsToMeters;
        public Vector3 SourceUpAxis => sourceUpAxis;
        public Vector3 SourceForwardAxis => sourceForwardAxis;
        public OriginConvention Origin => originConvention;
        public string OriginNotes => originNotes;
        public float DefaultDisplayScale => defaultDisplayScale;
        public string SourceAndLicenseNotes => sourceAndLicenseNotes;
        public string ContentHash => contentHash;

        private void OnValidate()
        {
            sourceUnitsToMeters = Mathf.Max(0.000001f, sourceUnitsToMeters);
            defaultDisplayScale = Mathf.Max(0.0001f, defaultDisplayScale);
        }
    }
}