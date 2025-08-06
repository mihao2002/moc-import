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
        private LDrawFlatStepNavigator navigator;
        public Camera mainCamera; // Assign in inspector or via UI        
        
        // Progress tracking
        private bool isLoading = false;
        private float progressValue = 0f;
        private string progressMessage = "";
        private bool isCancelled = false;

        [MenuItem("Tools/LDraw Step Viewer")]
        public static void ShowWindow()
        {
            GetWindow<LDrawStepViewerWindow>("LDraw Step Viewer");
        }

        private void OnProgressUpdate(float progress, string message)
        {
            progressValue = progress;
            progressMessage = message;
            Repaint(); // Let UI refresh
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
                // LoadLDrawFile();
                StartLoadingRoutine();
            }
            
            // Show progress bar when loading
            if (isLoading && !isCancelled)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Loading Progress", EditorStyles.boldLabel);
                
                // Progress bar
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Progress:", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{progressValue:P1}", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                
                // Custom progress bar
                Rect progressRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(progressRect, progressValue, progressMessage);
                
                // Cancel button
                if (GUILayout.Button("Cancel Loading"))
                {
                    isCancelled = true;
                }
                
                EditorGUILayout.Space();
            }

            if (navigator != null)
            {
                var (currentModel, currentStep, stepCount) = navigator.GetCurrentStep();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Model: {currentModel} | Step: {currentStep + 1} / {stepCount}");
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

        private IEnumerator<YieldInstruction> LoadLDrawFileCoroutine()
        {
            isLoading = true;
            isCancelled = false;

            string ldconfigPath = Path.Combine(partLibraryPath, "LDConfig.ldr");
            LDrawColorManager.LoadFromFile(ldconfigPath);

            OnProgressUpdate(0f, "Clearing cache...");
            yield return null;

            LDrawPartLoader.ClearCache();

            OnProgressUpdate(0f, "Parsing models...");
            yield return null;

            var models = LDrawParser.ParseModels(ldrawFilePath);
            if (models.Count == 0)
            {
                Debug.LogError($"No model at all.");
                yield break;
            }
            var mainModelName = models[0].modelName;
            var allModelNameSet = new HashSet<string>();
            foreach (var modelData in models)
            {
                allModelNameSet.Add(modelData.modelName);
            }

            // Build the model dependency
            var modelDependency = new Dictionary<string, HashSet<string>>();
            var allDependants = new HashSet<string>();
            foreach (var modelData in models)
            {
                var dependencies = new HashSet<string>();
                for (int stepIdx = 0; stepIdx < modelData.steps.Count; stepIdx++)
                {
                    var step = modelData.steps[stepIdx];
                    foreach (var part in step.parts)
                    {
                        if (allModelNameSet.Contains(part.partId))
                        {
                            dependencies.Add(part.partId);
                            allDependants.Add(part.partId);
                        }
                    }
                }

                modelDependency[modelData.modelName] = dependencies;
            }

            // Get all models being used
            var usedModelNames = new List<string>{mainModelName};
            var index = 0;
            while (index < usedModelNames.Count)
            {
                usedModelNames.AddRange(modelDependency[usedModelNames[index]]);
                index++;
            }

            var usedModelSet = new HashSet<string>(usedModelNames);

            // Remove all unused models
            var usedModels = new List<RuntimeModelData>();
            foreach (var modelData in models)
            {
                if (usedModelSet.Contains(modelData.modelName))
                {
                    usedModels.Add(modelData);
                }
                else
                {
                    modelDependency.Remove(modelData.modelName);
                }
            }

            models = usedModels;
            var modelNames = new Dictionary<string, int>();
            for (var i=0; i<models.Count; i++)
            {
                var model = models[i];
                modelNames[model.modelName] = i;
            }

            //var modelContainers = new Dictionary<string, ModelContainer>();

            // get model dependency and all parts
            var parts = new HashSet<string>();
            foreach (var modelData in models)
            {
                for (int stepIdx = 0; stepIdx < modelData.steps.Count; stepIdx++)
                {
                    var step = modelData.steps[stepIdx];
                    foreach (var part in step.parts)
                    {
                        if (!modelNames.ContainsKey(part.partId))
                        {
                            parts.Add(part.partId);
                        }
                    }
                }
            }            

            // sort all models so that a model always locates behind any of its dependency.
            var sortedModels = new List<string>();
            while (modelDependency.Count > 1)
            {
                string key = null;
                foreach (var kvp in modelDependency)
                {
                    if (kvp.Value.Count == 0)
                    {
                        key = kvp.Key;
                        break;
                    }
                }

                if (key == null)
                {
                    Debug.LogError($"Circular dependency in models.");
                    yield break;
                }

                sortedModels.Add(key);
                modelDependency.Remove(key);
                foreach (var kvp in modelDependency)
                {
                    kvp.Value.Remove(key);
                }
            }

            OnProgressUpdate(0f, "Loading parts...");
            yield return null;

            // first load all parts
            var partCount = parts.Count;
            var loadedPartCount = 0f;

            foreach (string partId in parts)
            {
                OnProgressUpdate(loadedPartCount/partCount, $"Loading part {partId}...");
                yield return null;

                LDrawPartLoader.LoadPartFromLibrary(partId, partLibraryPath, unofficialPartLibraryPath);
                loadedPartCount++;

                if (isCancelled) yield break;                
            }

            OnProgressUpdate(0f, "Loading models...");
            yield return null;

            // then load all submodels
            var modelCount = sortedModels.Count;
            var loadedModelCount = 0f;
            foreach (var modelId in sortedModels)
            {
                OnProgressUpdate(loadedModelCount/modelCount, $"Loading model {modelId}...");
                yield return null;

                LDrawPartLoader.LoadSubmodelFromLibrary(modelId, models[modelNames[modelId]].steps);
                loadedModelCount++;

                if (isCancelled) yield break;
            }

            OnProgressUpdate(0f, "Loading model steps...");
            yield return null;

            // construct model steps
            var steppedModelCount = models.Count;
            var loadedSteppedModelCount = 0f;
            foreach (var modelData in models)
            {
                var steps = modelData.steps;
                var modelContainer = new ModelContainer(modelData.modelName);
                Bounds modelBounds = new Bounds();
                bool initialized = false;

                for (int stepIdx = 0; stepIdx < steps.Count; stepIdx++)
                {
                    OnProgressUpdate(loadedSteppedModelCount/steppedModelCount, $"Loading model {modelData.modelName} step {stepIdx+1}...");
                    yield return null;

                    var step = steps[stepIdx];
                    var objs = new List<GameObject>();

                    foreach (var part in step.parts)
                    {
                        GameObject go = LDrawPartLoader.GetGameObject(part.partId, part.color);
                        go.transform.position = part.position;
                        go.transform.rotation = part.rotation;

                        objs.Add(go);
                    }

                    var stepGO = modelContainer.AddStep(objs);
                    var bounds = LDrawUtils.CalculateBounds(stepGO);
                    if (!initialized)
                    {
                        modelBounds = bounds;
                        initialized = true;
                    }
                    else
                    {
                        modelBounds.Encapsulate(bounds);
                    }
                    
                    step.center = modelBounds.center;
                    step.radius = modelBounds.extents.magnitude;

                    if (isCancelled) yield break;
                }

                modelData.container = modelContainer;
                loadedSteppedModelCount++;                
            }

            OnProgressUpdate(1f, "Done");
            yield return null;    

            var flatSteps = new List<FlatStep>();
            GenerateFlatSteps(flatSteps, models, 0, modelNames);
            LDrawParser.SaveModelsToJsonAsset(models, flatSteps);


            navigator = new LDrawFlatStepNavigator(models, new LDrawCamera(mainCamera), flatSteps);

            LDrawPartLoader.OnProgressUpdate = null;
            isLoading = false;
        }

        private void GenerateFlatSteps(List<FlatStep> flatSteps, List<RuntimeModelData> models, int modelIndex, Dictionary<string, int> modelNames)
        {
            var model = models[modelIndex];
            var steps = model.steps;
            for (var i=0; i < steps.Count; i++)
            {
                var step = steps[i];
                foreach (var part in step.parts)
                {
                    if (modelNames.ContainsKey(part.partId))
                    {
                        GenerateFlatSteps(flatSteps, models, modelNames[part.partId], modelNames);
                    }
                }

                var flatStep = new FlatStep{model=modelIndex, modelStepIdx=i};
                flatSteps.Add(flatStep);
            }
        }

        private IEnumerator<YieldInstruction> loadingRoutine;

        private void StartLoadingRoutine()
        {
            CleanUpResourceFiles();
            loadingRoutine = LoadLDrawFileCoroutine();
            EditorApplication.update += RunLoadingRoutine;
        }

        private void RunLoadingRoutine()
        {
            if (loadingRoutine == null || !loadingRoutine.MoveNext())
            {
                EditorApplication.update -= RunLoadingRoutine;
                loadingRoutine = null;
            }
        }
    }
}

