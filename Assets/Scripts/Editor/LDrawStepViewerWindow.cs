using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;
using System.Linq;

namespace LDraw.Editor
{
    public class LDrawStepViewerWindow : EditorWindow
    {
        private string ldrawFilePath = "C:/Users/mihao/OneDrive/Documents/test.ldr";
        private string partLibraryPath = "C:/Users/Public/Documents/LDraw";
        private string unofficialPartLibraryPath = "C:/Users/Public/Documents/LDraw/Unofficial";
        private LDrawStepHierarchyNavigator navigator;
        public Camera mainCamera; // Assign in inspector or via UI

        [MenuItem("Tools/LDraw Step Viewer")]
        public static void ShowWindow()
        {
            CleanUpResourceFiles();
            GetWindow<LDrawStepViewerWindow>("LDraw Step Viewer");
        }

        // Delete all generated resource files and folders (prefabs, materials, red material asset, and their .meta files)
        private static void CleanUpResourceFiles()
        {
            string[] targets = {
                "Assets/Resources/LDrawPrefabs",
                "Assets/Resources/LDrawMeshes",
                "Assets/Resources/LDrawMaterials",
                "Assets/Resources/LDrawPrefabs.meta",
                "Assets/Resources/LDrawMaterials.meta",
            };

            foreach (var target in targets)
            {
                if (System.IO.Directory.Exists(target))
                {
                    // Delete all files in the directory (except .meta)
                    var files = System.IO.Directory.GetFiles(target);
                    foreach (var file in files)
                    {
                        if (!file.EndsWith(".meta"))
                            AssetDatabase.DeleteAsset(file.Replace("\\", "/"));
                    }
                    // Delete the directory itself (and its .meta)
                    AssetDatabase.DeleteAsset(target);
                }
                else if (System.IO.File.Exists(target))
                {
                    AssetDatabase.DeleteAsset(target);
                }
            }
            AssetDatabase.Refresh();
        }

        void OnGUI()
        {
            GUILayout.Label("LDraw Step Viewer", EditorStyles.boldLabel);

            mainCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (mainCamera == null)
            {
                EditorUtility.DisplayDialog("Error", "No camera found in the scene! Please add a Camera and assign it as Main Camera.", "OK");
            }

            EditorGUILayout.BeginHorizontal();
            ldrawFilePath = EditorGUILayout.TextField("LDraw File", ldrawFilePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select LDraw File", "", "ldr,mpd,dat");
                if (!string.IsNullOrEmpty(path))
                    ldrawFilePath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            partLibraryPath = EditorGUILayout.TextField("Part Library Path", partLibraryPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select LDraw Part Library Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                    partLibraryPath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            unofficialPartLibraryPath = EditorGUILayout.TextField("Unofficial Part Library Path", unofficialPartLibraryPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select LDraw Unofficial Part Library Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                    unofficialPartLibraryPath = path;
            }
            EditorGUILayout.EndHorizontal();        

            if (GUILayout.Button("Load LDraw File"))
            {
                LoadLDrawFile();
            }

            if (navigator != null)
            {
                var (currentModel, currentStep) = navigator.GetCurrentStep();
                if (currentModel != null && currentStep >= 0)
                {
                    var modelContainers = navigator.GetType().GetField("modelContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(navigator) as Dictionary<string, ModelContainer>;
                    int totalSteps = 0;
                    if (modelContainers != null && modelContainers.ContainsKey(currentModel))
                        totalSteps = modelContainers[currentModel].GetStepCount();
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Model: {currentModel} | Step: {currentStep + 1} / {totalSteps}");
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginDisabledGroup(navigator.IsAtStart);
                    if (GUILayout.Button("Previous Step"))
                    {
                        navigator.ShowPreviousStep();
                        SceneView.RepaintAll();
                    }
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUI.BeginDisabledGroup(navigator.IsAtEnd);
                    if (GUILayout.Button("Next Step"))
                    {
                        navigator.ShowNextStep();
                        SceneView.RepaintAll();
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        void LoadLDrawFile()
        {
            if (!File.Exists(ldrawFilePath))
            {
                EditorUtility.DisplayDialog("Error", "LDraw file not found!", "OK");
                return;
            }

            string ldconfigPath = Path.Combine(partLibraryPath, "LDConfig.ldr");
            LDrawColorManager.LoadFromFile(ldconfigPath);

            LDrawPartLoader.ClearCache();

            var models = LDrawParser.ParseModels(ldrawFilePath);
         
            // Create navigator with the models
            navigator = new LDrawStepHierarchyNavigator(models, mainCamera);
            
            // Editor-specific: instantiate parts using LDrawPartLoader
            var modelContainers = new Dictionary<string, ModelContainer>();
            foreach (var kvp in models)
            {
                var modelContainer = new ModelContainer(kvp.Key);
                Bounds modelBounds = new Bounds(Vector3.zero, Vector3.zero);
                for (int stepIdx = 0; stepIdx < kvp.Value.Count; stepIdx++)
                {
                    var step = kvp.Value[stepIdx];
                    var objs = new List<GameObject>();
                    foreach (var part in step.parts)
                    {
                        GameObject go = LDrawPartLoader.SpawnPart(part, partLibraryPath, unofficialPartLibraryPath, models);
                        objs.Add(go);
                    }
                    modelContainer.AddStep(objs);

                    // // Always assume original rotation is identity
                    // if (step.rotation.HasValue)
                    //     modelContainer.Rotate(step.rotation.Value.x, step.rotation.Value.y, step.rotation.Value.z);

                    // Calculate and store camera distance for this step
                    var stepGO = modelContainer.GetStepContainer(stepIdx);
                    if (stepGO != null)
                    {
                        var bounds = LDrawUtils.CalculateBounds(stepGO);
                        modelBounds.Encapsulate(bounds);
                        // float distance = LDrawUtils.ComputeCameraDistance(bounds);
                        // step.cameraDistance = distance;
                        step.center = modelBounds.center;
                        step.radius = modelBounds.extents.magnitude;
                    }
                }
                // Restore to identity rotation after all steps
                // modelContainer.Rotate(0, 0, 0);
                modelContainers[kvp.Key] = modelContainer;
            }

            LDrawParser.SaveModelsToJsonAsset(models);

            // Set the modelContainers in the navigator
            navigator.SetModelContainers(modelContainers);
            // Initialize navigation
            navigator.InitializeNavigation();
        }
    }
}

