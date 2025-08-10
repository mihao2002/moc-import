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
        private string unofficialPartLibraryPath = "C:/Users/Public/Documents/LDraw/Unofficial;C:/Models/Unofficial";
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
                "Assets/Resources/LDrawImages",
                "Assets/Resources/LDrawStepImages",
                "Assets/Resources/LDrawPrefabs.meta",
                "Assets/Resources/LDrawMaterials.meta",
                "Assets/Resources/LDrawImages.meta",
                "Assets/Resources/LDrawStepImages.meta",
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

            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (mainCamera == null)
                {
                    EditorUtility.DisplayDialog("Error", "No camera found in the scene! Please add a Camera and assign it as Main Camera.", "OK");
                }
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
            var colors = LDrawColorManager.LoadFromFile(ldconfigPath);
            var partLoader = new LDrawPartLoader(colors);

            OnProgressUpdate(0f, "Clearing cache...");
            yield return null;

            partLoader.ClearCache();

            OnProgressUpdate(0f, "Parsing models...");
            yield return null;

            (List<RuntimeModelData> models, Dictionary<string, string[]> geometryModels) = LDrawParser.ParseModels(ldrawFilePath);
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
                        if (!modelNames.ContainsKey(part.partId) && !geometryModels.ContainsKey(part.partId))
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
            var unofficeialPaths = unofficialPartLibraryPath.Split(';');

            foreach (string partId in parts)
            {
                OnProgressUpdate(loadedPartCount/partCount, $"Loading part {partId}...");
                yield return null;

                partLoader.LoadPartFromLibrary(partId, partLibraryPath, unofficeialPaths);
                loadedPartCount++;

                if (isCancelled) yield break;                
            }

            OnProgressUpdate(0f, "Loading geometry parts...");
            yield return null;

            // then load geometry models as part
            var geometryPartCount = geometryModels.Count;
            var loadedGeometryPartCount = 0f;
            foreach (var kvp in geometryModels)
            {
                var partId = kvp.Key;
                OnProgressUpdate(loadedGeometryPartCount/geometryPartCount, $"Loading geometry part {partId}...");
                yield return null;

                partLoader.LoadPartFromLibrary(partId, partLibraryPath, unofficeialPaths, kvp.Value);
                loadedGeometryPartCount++;

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

                partLoader.LoadSubmodelFromLibrary(modelId, models[modelNames[modelId]].steps);
                loadedModelCount++;

                if (isCancelled) yield break;
            }


            var resolution = 512;
            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;

            var camGO = new GameObject("PreviewCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.aspect = 1;
            var ldrawCamera = CreatePreviewCamera(cam, rt);

            // construct model steps
            OnProgressUpdate(0f, "Loading model steps...");
            yield return null;
            
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
                        GameObject go = partLoader.GetGameObject(part.partId, part.color);
                        go.SetActive(true);

                        var filename = part.partId.Replace('\\', '_');
                        if (!modelNames.ContainsKey(part.partId))
                        {
                            var color = colors[part.color].color;
                            string colorKey = $"{color.r:F3}_{color.g:F3}_{color.b:F3}";
                            string matName = $"Mat_{colorKey}";
                            filename = $"{matName}_{filename}";
                        }
                        // string imagePath = Path.Combine(imageFolder, $"{matName}_{filename}.png");

                        CreateImage(filename, go, ldrawCamera, rt);
                        go.SetActive(false);

                        go.transform.position = part.position;
                        go.transform.rotation = part.rotation;

                        objs.Add(go);
                    }

                    foreach (var go in objs)
                        go.SetActive(true);

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

            var flatSteps = new List<FlatStep>();
            GenerateFlatSteps(flatSteps, models, 0, modelNames);
            LDrawParser.SaveModelsToJsonAsset(models, flatSteps, partLoader.GetUsedColors(), partLoader.GetPartDescriptions());

            OnProgressUpdate(0f, "Creating model step previews...");
            yield return null;
            
            var previewCount = flatSteps.Count;
            var previewNavigator = new LDrawFlatStepNavigator(models, ldrawCamera, flatSteps, false);
            for (var i=0; i<previewCount; i++)
            {
                OnProgressUpdate((i+1.0f)/previewCount, $"Creating preview for step {i+1}...");
                yield return null;
                CreateStepImage(i, ldrawCamera, rt);
                previewNavigator.ShowNextStep(false);
            }

            navigator = new LDrawFlatStepNavigator(models, new LDrawCamera(mainCamera), flatSteps);
            OnProgressUpdate(1f, "Done");
            yield return null;    

            GameObject.DestroyImmediate(camGO);
            GameObject.DestroyImmediate(rt);            

            isLoading = false;
        }

        private static LDrawCamera CreatePreviewCamera(Camera cam, RenderTexture rt)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0); // Transparent
            cam.orthographic = false;
            cam.targetTexture = rt;
            cam.useOcclusionCulling = false;
            cam.aspect = 1f;

            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;

            return new LDrawCamera(cam);
        }

        public static void CreateImage(string filename, GameObject go, LDrawCamera camera, RenderTexture rt)
        {
            string imageFolder = "Assets/Resources/LDrawImages";
            // string colorKey = $"{color.r:F3}_{color.g:F3}_{color.b:F3}";
            // string matName = $"Mat_{colorKey}";
            // var filename = partId.Replace('\\', '_');
            string imagePath = Path.Combine(imageFolder, $"{filename}.png");

            if (File.Exists(imagePath))
            {
                return;
            }

            if (!Directory.Exists(imageFolder))
                Directory.CreateDirectory(imageFolder);

            var image = GenerateImageFromMeshPrefabTransparent(go, camera, rt);
            SaveTextureAsPNG(image, imagePath);         
        }

        public static void CreateStepImage(int step, LDrawCamera camera, RenderTexture rt)
        {
            string imageFolder = "Assets/Resources/LDrawStepImages";
            string imagePath = Path.Combine(imageFolder, $"{step}.png");

            if (!Directory.Exists(imageFolder))
                Directory.CreateDirectory(imageFolder);

            var image = RenderCamera(camera, rt);
            SaveTextureAsPNG(image, imagePath);         
        }

        public static void SaveTextureAsPNG(Texture2D texture, string fullPath)
        {
            byte[] bytes = texture.EncodeToPNG();

            // Write the file
            File.WriteAllBytes(fullPath, bytes);

            // Tell Unity to import it
            AssetDatabase.ImportAsset(fullPath);
            
            // Set import settings to Sprite
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(fullPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();

            Debug.Log("Saved and imported: " + fullPath);
        }

        private static Texture2D RenderCamera(LDrawCamera camera, RenderTexture rt, int resolution = 512)
        {
            camera.Render();

            var oldRT = RenderTexture.active;
            RenderTexture.active = rt;

            // Read and save texture
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();

            RenderTexture.active = oldRT;
            return tex;
        }

        public static Texture2D GenerateImageFromMeshPrefabTransparent(GameObject go, LDrawCamera camera, RenderTexture rt)
        {
            Bounds bounds = go.GetComponent<Renderer>().bounds;
            float radius = bounds.extents.magnitude;
            var rotation = LDrawCamera.DefaultRotation;

            camera.SetCamera(bounds.center, radius, rotation);
            return RenderCamera(camera, rt);
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

