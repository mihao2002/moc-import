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

            if (models.Count > 0 && navigationStack.Count > 0)
            {
                var (currentModel, currentStep, _) = navigationStack[navigationStack.Count - 1];
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Model: {currentModel} | Step: {currentStep + 1} / {models[currentModel].Count}");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous Step"))
                {
                    PrevHierarchicalStep();
                }
                
                bool atEnd = (currentModel == LDrawParser.mainModelName && 
                    (models.ContainsKey(LDrawParser.mainModelName) && currentStep == models[LDrawParser.mainModelName].Count-1));
                EditorGUI.BeginDisabledGroup(atEnd);
                if (GUILayout.Button("Next Step"))
                {
                    NextHierarchicalStep();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }
        }

        // Hierarchical navigation stack: each entry is (modelName, stepIndex, doneSubmodel)
        private List<(string modelName, int stepIndex, int doneSubmodel)> navigationStack = new List<(string, int, int)>();
        private Dictionary<string, List<LDrawStep>> models = new Dictionary<string, List<LDrawStep>>();

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

            models = LDrawParser.ParseModels(ldrawFilePath);
            // Save step data to JSON for LDrawStepNavigator
            // LDrawParser.SaveStepsToJsonAsset(flatSteps.ConvertAll(fs => fs.step), "Assets/Resources/LDrawStepData.json");
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

            // Start navigation at main model, step 0
            navigationStack.Clear();
            AddNavigationStackStep(LDrawParser.mainModelName, 0, 0);

            ShowHierarchicalStep();
        }

        void ShowHierarchicalStep()
        {
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

            // Only show the last submodel
            var stackIdx = navigationStack.Count - 1;

            var (modelName, stepIdx, doneSubmodel) = navigationStack[stackIdx];
            var stepList = modelStepObjects[modelName];
            Debug.Log($"ShowHierarchicalStep - model:{modelName} step:{stepIdx}");
            for (int i = 0; i <= stepIdx; i++)
            {
                foreach (var go in stepList[i])
                {
                    if (go != null)
                        go.SetActive(true);
                }
            }

            SceneView.RepaintAll();
        }

        void AddNavigationStackStep(string model, int step, int doneSubmodel)
        {
            var nextParts = models[model][step].parts;
            var nextModels = nextParts.Where(p => models.ContainsKey(p.partId)).ToList();
            if (nextModels.Count > 0)
            {
                if (doneSubmodel < nextModels.Count)
                {
                    navigationStack.Add((model, step, doneSubmodel+1));
                    AddNavigationStackStep(nextModels[doneSubmodel].partId, 0, 0);
                }
                else
                {
                    navigationStack.Add((model, step, doneSubmodel));
                }
            }
            else
            {
                navigationStack.Add((model, step, 0));
            }
        }

        void NextHierarchicalStep()
        {
            if (navigationStack.Count == 0) return;
            var (currentModel, currentStep, doneSubmodel) = navigationStack[navigationStack.Count - 1];
            var steps = models[currentModel];
            var step = steps[currentStep];
            var submodels = step.parts.Where(p => models.ContainsKey(p.partId)).ToList();
            if (doneSubmodel == submodels.Count)
            {
                navigationStack.RemoveAt(navigationStack.Count - 1);
                if (currentStep + 1 < steps.Count)
                {
                    AddNavigationStackStep(currentModel, currentStep+1, 0);
                }
                else
                {
                    if (navigationStack.Count > 0)
                    {
                        var (parentModel, parentStep, parentDoneSubmodel) = navigationStack[navigationStack.Count - 1];
                        var parentStepObj = models[parentModel][parentStep];
                        var parentSubmodels = parentStepObj.parts.Where(p => models.ContainsKey(p.partId)).ToList();
                        if (parentDoneSubmodel < parentSubmodels.Count)
                        {
                            navigationStack.RemoveAt(navigationStack.Count - 1);
                            AddNavigationStackStep(parentModel, parentStep, parentDoneSubmodel);
                        }
                    }
                }
            }
            else
            {
                navigationStack.RemoveAt(navigationStack.Count - 1);
                AddNavigationStackStep(currentModel, currentStep, doneSubmodel+1);
            }

            ShowHierarchicalStep();
        }

        void PrevHierarchicalStep()
        {
            if (navigationStack.Count == 0) return;
            var (currentModel, currentStep, doneSubmodel) = navigationStack[navigationStack.Count - 1];
            if (doneSubmodel > 0)
            {
                // Pop out of submodel, decrement doneSubmodel in parent
                navigationStack.RemoveAt(navigationStack.Count - 1);
                var (parentModel, parentStep, parentDoneSubmodel) = navigationStack[navigationStack.Count - 1];
                navigationStack[navigationStack.Count - 1] = (parentModel, parentStep, parentDoneSubmodel - 1);
            }
            else if (currentStep > 0)
            {
                navigationStack[navigationStack.Count - 1] = (currentModel, currentStep - 1, 0);
            }
            else if (navigationStack.Count > 1)
            {
                // At start of submodel, pop back to parent
                navigationStack.RemoveAt(navigationStack.Count - 1);
            }
            ShowHierarchicalStep();
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

