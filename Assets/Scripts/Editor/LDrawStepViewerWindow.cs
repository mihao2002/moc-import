using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;

namespace LDraw.Editor
{
    public class LDrawStepViewerWindow : EditorWindow
    {
        private string ldrawFilePath = "C:/Users/mihao/OneDrive/Documents/test.ldr";
        private string partLibraryPath = "C:/Users/Public/Documents/LDraw";
        private string unofficialPartLibraryPath = "C:/Users/Public/Documents/LDraw/Unofficial";
        private List<LDrawStep> steps = new List<LDrawStep>();
        private List<GameObject> spawnedParts = new List<GameObject>();
        private Dictionary<string, List<List<GameObject>>> modelStepObjects = new Dictionary<string, List<List<GameObject>>>();

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

            if (flatSteps.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Step {flatStepIndex + 1} / {flatSteps.Count} | Model: {currentContextModel}");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous Step"))
                {
                    if (flatStepIndex > 0)
                    {
                        flatStepIndex--;
                        ShowFlatStep();
                    }
                }
                if (GUILayout.Button("Next Step"))
                {
                    if (flatStepIndex < flatSteps.Count - 1)
                    {
                        flatStepIndex++;
                        ShowFlatStep();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private List<LDrawParser.FlatStep> flatSteps = new List<LDrawParser.FlatStep>();
        private int flatStepIndex = 0;
        private string currentContextModel = LDrawParser.mainModelName;

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
            flatSteps = LDrawParser.FlattenSteps(models);
            modelStepObjects.Clear();
            foreach (var kvp in models)
            {
                var stepObjs = new List<List<GameObject>>();
                foreach (var step in kvp.Value)
                {
                    var objs = new List<GameObject>();
                    foreach (var part in step.parts)
                    {
                        GameObject go = LDrawPartLoader.SpawnPart(part, partLibraryPath, unofficialPartLibraryPath, models);
                        go.SetActive(false);
                        objs.Add(go);
                    }
                    stepObjs.Add(objs);
                }
                modelStepObjects[kvp.Key] = stepObjs;
            }

            flatStepIndex = 0;
            currentContextModel = flatSteps.Count > 0 ? flatSteps[0].modelName : LDrawParser.mainModelName;
            ShowFlatStep();
        }

        void ShowFlatStep()
        {
            if (flatStepIndex < 0 || flatStepIndex >= flatSteps.Count)
                return;
            var flatStep = flatSteps[flatStepIndex];
            currentContextModel = flatStep.modelName;
            // Hide all objects in all models
            foreach (var stepObjs in modelStepObjects.Values)
            {
                foreach (var objs in stepObjs)
                {
                    foreach (var go in objs)
                    {
                        if (go != null)
                            go.SetActive(false);
                    }
                }
            }
            // Show all objects up to and including the current step in the current context model
            var stepList = modelStepObjects[currentContextModel];
            for (int i = 0; i <= flatStep.stepIndexInModel && i < stepList.Count; i++)
            {
                foreach (var go in stepList[i])
                {
                    if (go != null)
                        go.SetActive(true);
                }
            }
            SceneView.RepaintAll();
        }

        void ClearParts()
        {
            foreach (var stepObjs in modelStepObjects.Values)
            {
                foreach (var objs in stepObjs)
                {
                    foreach (var go in objs)
                    {
                        if (go != null)
                            UnityEngine.Object.DestroyImmediate(go);
                    }
                }
            }
            modelStepObjects.Clear();
        }

        void OnDisable()
        {
            ClearParts();
        }
    }
}

