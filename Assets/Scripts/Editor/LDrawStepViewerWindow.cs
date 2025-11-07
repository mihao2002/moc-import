using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using LDraw.Runtime;
using System.Linq;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace LDraw.Editor
{
    public class LDrawStepViewerWindow : EditorWindow
    {
        private string ldrawFilePath = "C:/Users/mihao/OneDrive/Documents/test.ldr";
        private string partLibraryPath = "C:/Users/Public/Documents/LDraw";
        private string studioDataPath = "C:/Program Files/Studio 2.0/data";
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

        public static void ClearAllAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                UnityEngine.Debug.LogWarning("AddressableAssetSettings not found.");
                return;
            }

            foreach (var group in settings.groups)
            {
                // Skip built-in group like "Built In Data" if you want
                if (group.ReadOnly) continue;

                // Remove all entries
                var entries = group.entries.ToList();
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    settings.RemoveAssetEntry(entry.guid);
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("All Addressables cleared.");
        }

        public static void DeleteNonDefaultGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                UnityEngine.Debug.LogWarning("AddressableAssetSettings not found.");
                return;
            }

            // Find the default group
            var defaultGroup = settings.DefaultGroup;

            // Collect all groups that are not default and not read-only
            var groupsToRemove = settings.groups
                .Where(g => g != null && g != defaultGroup && !g.ReadOnly)
                .ToList();

            foreach (var group in groupsToRemove)
            {
                settings.RemoveGroup(group);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
            AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log($"Deleted {groupsToRemove.Count} non-default Addressable groups.");
        }

        // Delete all generated resource files and folders (prefabs, materials, red material asset, and their .meta files)
        private static void CleanUpResourceFiles()
        {
            DeleteNonDefaultGroups();
            string[] targets = {
                "Assets/Resources_moved/LDrawPrefabs",
                "Assets/Resources_moved/LDrawMeshes",
                "Assets/Resources_moved/LDrawMaterials",
                "Assets/Resources/LDrawImages",
                "Assets/Resources/LDrawStepImages",
                "Assets/Resources_moved/LDrawPrefabs.meta",
                "Assets/Resources_moved/LDrawMaterials.meta",
                "Assets/Resources_moved/LDrawMeshes.meta",
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
            studioDataPath = EditorGUILayout.TextField("Studio Data Path", studioDataPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Studio Data Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                    studioDataPath = path;
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
        }

        private IEnumerator<YieldInstruction> LoadLDrawFileCoroutine()
        {
            isLoading = true;
            isCancelled = false;

            string ldconfigPath = Path.Combine(partLibraryPath, "LDConfig.ldr");
            string studioColorPath = Path.Combine(studioDataPath, "StudioColorDefinition.txt");
            var colors = LDrawColorManager.LoadFromFile(ldconfigPath, studioColorPath);
            // Set the max mesh size is 100M
            var partLoader = new LDrawPartLoader(colors, 1024*1024*100);

            OnProgressUpdate(0f, "Clearing cache...");
            yield return null;

            partLoader.ClearCache();

            OnProgressUpdate(0f, "Parsing models...");
            yield return null;

            (List<RuntimeModelData> models, Dictionary<string, string[]> geometryModels) = partLoader.ParseModels(ldrawFilePath);
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

            // remove part models from models, so we don't need to generate steps for part models
            var nonPartModels = new List<RuntimeModelData>();
            foreach (var model in models)
            {
                if (!partLoader.isPartModel(model.modelName))
                    nonPartModels.Add(model);
            }

            models = nonPartModels;
            modelNames = new Dictionary<string, int>();
            for (var i=0; i<models.Count; i++)
            {
                var model = models[i];
                modelNames[model.modelName] = i;
            }

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

                        CreateImage(partLoader, filename, go, ldrawCamera, rt);
                        go.SetActive(false);

                        go.transform.position = part.position;
                        go.transform.rotation = part.rotation;

                        objs.Add(go);
                    }

                    foreach (var go in objs)
                        go.SetActive(true);

                    GameObject stepContainerGo = new GameObject();
                    stepContainerGo.transform.SetParent(modelContainer.ModelContainerGo.transform, worldPositionStays: false);
                    stepContainerGo.SetActive(false); // Hide by default
                    objs.ForEach(so => so.transform.SetParent(stepContainerGo.transform, false));

                    var stepContainer = new StepContainer(stepContainerGo);
                    modelContainer.AddStep(stepContainer);
                    var bounds = LDrawUtils.CalculateBounds(stepContainerGo);
                    if (!initialized)
                    {
                        modelBounds = bounds;
                        initialized = true;
                    }
                    else
                    {
                        modelBounds.Encapsulate(bounds);
                    }
                    
                    // step.center = modelBounds.center;
                    // step.radius = modelBounds.extents.magnitude;
                    step.center = bounds.center;
                    step.radius = bounds.extents.magnitude;
                    step.modelBounds = modelBounds;

                    if (isCancelled) yield break;
                }

                modelData.container = modelContainer;
                loadedSteppedModelCount++;                
            }         

            var flatSteps = new List<FlatStep>();
            var partDescriptions = partLoader.GetPartDescriptions();

            string studioPartPath = Path.Combine(studioDataPath, "StudioPartDefinition2.txt");
            var blPartMap = LoadLDrawToBLPartMap(studioPartPath);
            foreach (var kvp in partDescriptions)
            {
                var desc = partDescriptions[kvp.Key];
                if (desc.id == null)
                {
                    if (blPartMap.ContainsKey(kvp.Key))
                    {
                        desc.id = blPartMap[kvp.Key];
                    }
                    else
                    {
                        Debug.LogError($"No bricklink part id found for {kvp.Key}");
                    }
                }
            }

            var partCounts = new Dictionary<LDrawPartCore, LDrawPartCount>();
            GenerateFlatSteps(flatSteps, models, 0, modelNames, partCounts, partDescriptions);

            partLoader.SaveModelsToJsonAsset(models, flatSteps, partLoader.GetUsedColors(), partDescriptions, partCounts.Values.ToList());

            OnProgressUpdate(0f, "Creating model step previews...");
            yield return null;
            
            var previewCount = flatSteps.Count;
            var previewNavigator = new LDrawFlatStepNavigator(models, ldrawCamera, flatSteps, true);
            for (var i=0; i<previewCount; i++)
            {
                OnProgressUpdate((i+1.0f)/previewCount, $"Creating preview for step {i+1}...");
                yield return null;
                previewNavigator.HideCurrentModel();
                previewNavigator.GotoStep(i, false);
                CreateStepImage(partLoader, i, ldrawCamera, rt);
            }

            navigator = new LDrawFlatStepNavigator(models, new LDrawCamera(mainCamera, false), flatSteps);
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

            return new LDrawCamera(cam, false);
        }
        
        private static Dictionary<string, string> LoadLDrawToBLPartMap(string filePath)
        {
            var map = new Dictionary<string, string>();

            var lines = File.ReadAllLines(filePath);

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Split by tab
                var columns = line.Split('\t');

                if (columns.Length < 5) continue; // ensure enough columns

                string blPartId = columns[2];
                string ldrawPartId = columns[4];

                if (string.IsNullOrEmpty(blPartId) || string.IsNullOrEmpty(ldrawPartId))
                    continue; // skip if either is empty

                // Only add if not already present
                if (!map.ContainsKey(ldrawPartId))
                {
                    map[ldrawPartId] = blPartId;
                }
            }

            return map;
        }

        public static void CreateImage(LDrawPartLoader partLoader, string filename, GameObject go, LDrawCamera camera, RenderTexture rt)
        {
            string imageFolder = "Assets/Resources/LDrawImages";
            // string colorKey = $"{color.r:F3}_{color.g:F3}_{color.b:F3}";
            // string matName = $"Mat_{colorKey}";
            // var filename = partId.Replace('\\', '_');
            string fullFileName = $"{filename}.png";
            string imagePath = Path.Combine(imageFolder, fullFileName);

            if (File.Exists(imagePath))
            {
                return;
            }

            if (!Directory.Exists(imageFolder))
                Directory.CreateDirectory(imageFolder);

            var image = GenerateImageFromMeshPrefabTransparent(go, camera, rt);
            partLoader.SaveTextureAsPNG(image, imagePath, fullFileName, "LDrawImages");
        }

        public static void CreateStepImage(LDrawPartLoader partLoader, int step, LDrawCamera camera, RenderTexture rt)
        {
            string imageFolder = "Assets/Resources/LDrawStepImages";
            string fullFileName = $"{step}.png";
            string imagePath = Path.Combine(imageFolder, fullFileName);

            if (!Directory.Exists(imageFolder))
                Directory.CreateDirectory(imageFolder);

            var image = RenderCamera(camera, rt);
            partLoader.SaveTextureAsPNG(image, imagePath, fullFileName, "LDrawStepImages");         
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

        private void GenerateFlatSteps(List<FlatStep> flatSteps, List<RuntimeModelData> models, int modelIndex,
            Dictionary<string, int> modelNames, Dictionary<LDrawPartCore, LDrawPartCount> partCounts,
            Dictionary<string, LDrawPartDesc> partDescpritions)
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
                        GenerateFlatSteps(flatSteps, models, modelNames[part.partId], modelNames, partCounts, partDescpritions);
                    }
                    else
                    {
                        var partId = partDescpritions.ContainsKey(part.partId) && partDescpritions[part.partId].id != null
                            ? partDescpritions[part.partId].id
                            : part.partId;
                        var key = new LDrawPartCore { partId = partId, color = part.color };
                        if (partCounts.ContainsKey(key))
                        {
                            partCounts[key].count++;
                        }
                        else
                        {
                            partCounts[key] = new LDrawPartCount
                                {
                                    part = new LDrawPartCore { partId = part.partId, color = part.color },
                                    count = 1
                                };
                        }
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

