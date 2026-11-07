using System;
using System.IO;
using HoloLensAR.Spatial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using MixedReality.Toolkit;

namespace HoloLensAR.Editor
{
    /// <summary>
    /// One-shot project builder for the isolated Stage B room-mesh prototype.
    /// It runs automatically only when the local request marker exists, and is also exposed as a menu item.
    /// </summary>
    [InitializeOnLoad]
    internal static class StageBSceneBuilder
    {
        private const string RequestPath = "Assets/Editor/HoloLensAR/StageBBuild.request";
        private const string ScenePath = "Assets/Scenes/MeshReadPrototype.unity";
        private const string MeshPrefabPath = "Assets/Prefabs/Spatial/RoomMeshDebug.prefab";
        private const string MaterialPath = "Assets/Materials/RoomMeshDebug.mat";
        private const string ButtonMaterialPath = "Assets/Materials/StageBButton.mat";
        private const string RigPrefabGuid = "acbf65a81ce2cf94f82a0809298acf70";
        private const string FontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const int BuilderRevision = 2;

        static StageBSceneBuilder()
        {
            EditorApplication.delayCall += RunRequestedBuild;
        }

        [MenuItem("Tools/HoloLens AR/Build Stage B Mesh Prototype")]
        private static void BuildFromMenu()
        {
            Build();
        }

        private static void RunRequestedBuild()
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            File.Delete(RequestPath);
            AssetDatabase.Refresh();

            try
            {
                Build();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                throw new InvalidOperationException(ScenePath + " already exists. Builder stopped without overwriting it.");
            }

            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Spatial");
            EnsureFolder("Assets/Materials");

            Material meshMaterial = CreateMaterial(MaterialPath, "HoloLensAR/RoomMeshDebugTransparent", new Color(0.08f, 0.78f, 1.0f, 0.22f));
            Material buttonMaterial = CreateMaterial(ButtonMaterialPath, "Standard", new Color(0.025f, 0.22f, 0.32f, 1.0f));
            MeshFilter meshPrefab = CreateRoomMeshPrefab(meshMaterial);

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene prototypeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(prototypeScene);

            try
            {
                BuildSceneContents(prototypeScene, meshPrefab, meshMaterial, buttonMaterial);
                EditorSceneManager.MarkSceneDirty(prototypeScene);

                if (!EditorSceneManager.SaveScene(prototypeScene, ScenePath))
                {
                    throw new InvalidOperationException("Unity did not save " + ScenePath + ".");
                }

                AddSceneToBuildSettings(ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Stage B v" + BuilderRevision + "] Created " + ScenePath + " and " + MeshPrefabPath + ". The original active scene was preserved.");
            }
            finally
            {
                if (prototypeScene.IsValid() && prototypeScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(prototypeScene, true);
                }

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    EditorSceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        private static void BuildSceneContents(Scene scene, MeshFilter meshPrefab, Material meshMaterial, Material buttonMaterial)
        {
            string rigPath = AssetDatabase.GUIDToAssetPath(RigPrefabGuid);
            GameObject rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
            if (rigAsset == null)
            {
                throw new InvalidOperationException("Could not load the installed MRTK XR Rig prefab. GUID path: " + rigPath);
            }

            GameObject rig = PrefabUtility.InstantiatePrefab(rigAsset, scene) as GameObject;
            if (rig == null)
            {
                throw new InvalidOperationException("Could not instantiate the MRTK XR Rig prefab.");
            }

            rig.name = "MRTK XR Rig";
            XROrigin origin = rig.GetComponentInChildren<XROrigin>(true);
            Camera xrCamera = rig.GetComponentInChildren<Camera>(true);
            if (origin == null || xrCamera == null)
            {
                throw new InvalidOperationException("The MRTK XR Rig must provide both an XROrigin and a tracked Camera.");
            }

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            lightObject.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);

            GameObject spatialRoot = new GameObject("SpatialRoot");
            spatialRoot.transform.SetParent(origin.transform, false);
            spatialRoot.transform.localPosition = Vector3.zero;
            spatialRoot.transform.localRotation = Quaternion.identity;
            spatialRoot.transform.localScale = Vector3.one;

            GameObject managerObject = new GameObject("ARMeshManager");
            managerObject.transform.SetParent(spatialRoot.transform, false);
            managerObject.transform.localScale = Vector3.one * 10.0f;

            ARMeshManager meshManager = managerObject.AddComponent<ARMeshManager>();
            meshManager.meshPrefab = meshPrefab;
            meshManager.density = 0.25f;
            meshManager.normals = false;
            meshManager.tangents = false;
            meshManager.textureCoordinates = false;
            meshManager.colors = false;
            meshManager.concurrentQueueSize = 2;
            meshManager.enabled = false;

            ARFoundationRoomMeshSource meshSource = managerObject.AddComponent<ARFoundationRoomMeshSource>();
            meshSource.Bind(meshManager);

            RoomMeshDisplayController displayController = managerObject.AddComponent<RoomMeshDisplayController>();
            displayController.Bind(meshManager);
            displayController.SetVisible(true);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                throw new InvalidOperationException("Could not load the project's LiberationSans SDF font asset: " + FontAssetPath);
            }

            Canvas debugCanvas = CreateStatusCanvas(xrCamera, font);
            TMP_Text statusText = debugCanvas.GetComponentInChildren<TextMeshProUGUI>();

            StatefulInteractable scanButton;
            TMP_Text scanLabel;
            CreateMrtkButton(xrCamera.transform, "ScanToggleButton", "Start Scan", new Vector3(-0.25f, -0.31f, 1.13f), buttonMaterial, font, out scanButton, out scanLabel);

            StatefulInteractable clearButton;
            TMP_Text unusedClearLabel;
            CreateMrtkButton(xrCamera.transform, "ClearMeshesButton", "Clear Mesh", new Vector3(0.0f, -0.31f, 1.13f), buttonMaterial, font, out clearButton, out unusedClearLabel);

            StatefulInteractable displayButton;
            TMP_Text displayLabel;
            CreateMrtkButton(xrCamera.transform, "DisplayToggleButton", "Hide Mesh", new Vector3(0.25f, -0.31f, 1.13f), buttonMaterial, font, out displayButton, out displayLabel);

            GameObject diagnosticsObject = new GameObject("RoomMeshDiagnostics");
            diagnosticsObject.transform.SetParent(spatialRoot.transform, false);
            RoomMeshDiagnostics diagnostics = diagnosticsObject.AddComponent<RoomMeshDiagnostics>();
            diagnostics.Bind(meshSource, displayController, statusText, scanButton, scanLabel, clearButton, displayButton, displayLabel);

            SetLayerRecursively(debugCanvas.gameObject, 0);
            EditorUtility.SetDirty(meshManager);
            EditorUtility.SetDirty(meshSource);
            EditorUtility.SetDirty(displayController);
            EditorUtility.SetDirty(diagnostics);
            EditorUtility.SetDirty(rig);
        }

        private static Canvas CreateStatusCanvas(Camera xrCamera, TMP_FontAsset font)
        {
            GameObject canvasObject = new GameObject("DebugCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(xrCamera.transform, false);
            canvasObject.transform.localPosition = new Vector3(0.0f, 0.04f, 1.3f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.001f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = xrCamera;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(860.0f, 400.0f);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1.0f;

            GameObject panelObject = new GameObject("StatusPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = new Color(0.015f, 0.045f, 0.07f, 0.58f);

            GameObject textObject = new GameObject("MeshStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24.0f, 18.0f);
            textRect.offsetMax = new Vector2(-24.0f, -18.0f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 31.0f;
            text.color = new Color(0.86f, 0.96f, 1.0f, 1.0f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.text = "ROOM MESH PROTOTYPE\nWaiting for XR initialization...";
            return canvas;
        }

        private static void CreateMrtkButton(
            Transform parent,
            string name,
            string caption,
            Vector3 localPosition,
            Material buttonMaterial,
            TMP_FontAsset font,
            out StatefulInteractable interactable,
            out TMP_Text label)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, false);
            button.transform.localPosition = localPosition;
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = new Vector3(0.22f, 0.075f, 0.045f);
            button.GetComponent<MeshRenderer>().sharedMaterial = buttonMaterial;
            interactable = button.AddComponent<StatefulInteractable>();
            interactable.ToggleMode = StatefulInteractable.ToggleType.Button;

            GameObject labelObject = new GameObject("Label", typeof(TextMeshPro));
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0.0f, 0.0f, -0.53f);
            labelObject.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            // Counter the button's non-uniform scale so the label stays proportionate on its face.
            labelObject.transform.localScale = new Vector3(0.075f, 0.22f, 1.0f);

            TextMeshPro labelText = labelObject.GetComponent<TextMeshPro>();
            labelText.font = font;
            labelText.fontSize = 24.0f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.enableWordWrapping = false;
            labelText.text = caption;
            label = labelText;
        }

        private static MeshFilter CreateRoomMeshPrefab(Material material)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MeshPrefabPath);
            if (prefabAsset != null)
            {
                MeshFilter existingFilter = prefabAsset.GetComponent<MeshFilter>();
                if (existingFilter == null)
                {
                    throw new InvalidOperationException(MeshPrefabPath + " exists but has no MeshFilter.");
                }

                return existingFilter;
            }

            GameObject temporary = new GameObject("RoomMeshDebug", typeof(MeshFilter), typeof(MeshRenderer));
            temporary.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temporary, MeshPrefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);

            if (savedPrefab == null)
            {
                throw new InvalidOperationException("Failed to save " + MeshPrefabPath + ".");
            }

            return savedPrefab.GetComponent<MeshFilter>();
        }

        private static Material CreateMaterial(string path, string shaderName, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new InvalidOperationException("Shader not found: " + shaderName);
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < currentScenes.Length; i++)
            {
                if (currentScenes[i].path == scenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
            Array.Copy(currentScenes, updatedScenes, currentScenes.Length);
            updatedScenes[updatedScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = updatedScenes;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
