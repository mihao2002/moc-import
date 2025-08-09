using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Newtonsoft.Json;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;
using System;

namespace LDraw.Runtime
{
    public class LDrawStepNavigator : MonoBehaviour
    {
        public Transform parentContainer; // Where to spawn parts in the scene
        public TMP_Text navigationText; // Assign in inspector to show current model/step (TextMeshPro)
        public TMP_Text stepNumberText;
        public Camera mainCamera; // Assign in inspector

        public GameObject gridItemPrefab;  // The prefab for each item
        public Transform gridParent;       // The container with GridLayoutGroup
        public LeftPanelToggle leftPaneToggle;
        public GameObject stepPrefab;
        public Transform stepListParent;

        private LDrawCamera cam;

        // private bool suppressSliderCallback = false;
        private Dictionary<string, Sprite> partSpriteDict;
        private Sprite[] stepSprites;

        private LDrawFlatStepNavigator navigator;
        private InputHandler inputHandler;
        private Dictionary<int, LDrawColor> colors;
        private Dictionary<string, string> partDescriptions;

        void Start()
        {
            // Load model step data from Resources
            var jsonAsset = Resources.Load<TextAsset>("LDrawStepData");
            if (jsonAsset == null)
            {
                Debug.LogError("LDrawStepData.json not found in Resources!");
                return;
            }
            var data = JsonConvert.DeserializeObject<StepPackage>(jsonAsset.text);
            var models = data.models;
            colors = data.colors;
            partDescriptions = data.partDescriptions;
            var flatSteps = data.flatSteps;

            PreInstantiateAllParts(models, colors); // Runtime-specific: instantiate from prefabs

            cam = new LDrawCamera(mainCamera);
            navigator = new LDrawFlatStepNavigator(models, cam, flatSteps);
            inputHandler = new InputHandler(cam);

            partSpriteDict = LoadAllSpritesFromResources();
            stepSprites = LoadAllStepSpritesFromResources();

            PopulateSteps();
            UpdateNavigationText();
            ShowStepParts();
        }

        public static Sprite[] LoadAllStepSpritesFromResources()
        {          
            Sprite[] sprites = Resources.LoadAll<Sprite>("LDrawStepImages");
            var result = new Sprite[sprites.Length];
            foreach (var sprite in sprites)
            {
                if (int.TryParse(sprite.name, out int idx))
                {
                    result[idx] = sprite;
                }
                else
                {
                    Debug.LogError($"Invalid step image {sprite.name}");
                }
            }

            return result;
        }

        /// <summary>
        /// Loads all sprites from Resources/LDrawImages folder into nested dictionary [subfolder][filename] = Sprite
        /// Assumes sprites are imported in Resources/LDrawImages and subfolders.
        /// </summary>
        public static Dictionary<string, Sprite> LoadAllSpritesFromResources()
        {
            var result = new Dictionary<string, Sprite>();

            Sprite[] sprites = Resources.LoadAll<Sprite>("LDrawImages");
            foreach (var sprite in sprites)
            {
                result[sprite.name] = sprite;
            }

            return result;
        }

        private void ShowStepParts()
        {
            var parts = navigator.GetCurrentParts();
            var partCounts = new Dictionary<Sprite, int>();
            var partInfo = new Dictionary<Sprite, Tuple<string, string, Color, string, int>>();
            for (var i=0; i<parts.Count; i++)
            {
                var part = parts[i];
                var color = colors[part.color];
                string spriteKey = $"Mat_{color.color.r:F3}_{color.color.g:F3}_{color.color.b:F3}_{part.partId.Replace('\\','_')}";
                if (partSpriteDict.ContainsKey(spriteKey))
                {
                    var sprite = partSpriteDict[spriteKey];
                    if (partCounts.ContainsKey(sprite))
                    {
                        partCounts[sprite]+=1;
                    }
                    else
                    {
                        partCounts[sprite]=1;
                        var description = partDescriptions.ContainsKey(part.partId) ? partDescriptions[part.partId] : null;
                        partInfo[sprite] = new Tuple<string, string, Color, string, int>(part.partId, description, color.color, color.name, i);
                    }
                }
                else
                {
                    Debug.LogError($"Can't find part image for {spriteKey}");
                }
            }

            ClearGrid();
            foreach (var kvp in partCounts)
            {
                var info = partInfo[kvp.Key];
                AddItem(kvp.Key, kvp.Value.ToString(), info.Item1, info.Item2, info.Item3, info.Item4, info.Item5);
            }

            leftPaneToggle.SetItemCount(partCounts.Count);
        }

        private bool CanNavigate
        {
            get
            {
                return navigator != null && navigator.CanNavigate;
            }            
        }

        private void HandleInput()
        {
        #if UNITY_EDITOR || UNITY_STANDALONE
            if (cam == null || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }
        #endif

            inputHandler.HandleInput();
        }

        public void Update()
        {
            if (CanNavigate)
            {
                HandleInput();
            }            
        }

        public void ShowNextStep()
        {
            if (CanNavigate)
            {
                navigator.ShowNextStep();
                UpdateNavigationText(); 
                ShowStepParts();               
            }

        }

        public void ShowPreviousStep()
        {
            if (CanNavigate)
            {
                navigator.ShowPreviousStep();
                UpdateNavigationText();
                ShowStepParts();                  
            }
        }

        private void UpdateNavigationText()
        {
            if (navigator == null)
            {
                return;
            }

            if (navigationText != null)
            {
                var (modelName, stepIdx, stepCount) = navigator.GetCurrentStep();
                navigationText.text = $"Model: {modelName} | Step: {stepIdx + 1} / {stepCount}";
            }

            if (stepNumberText != null)
            {
                stepNumberText.text = $"{navigator.CurrentStep+1}";
            }
        }

        // Runtime-specific method to instantiate all parts from prefabs
        private void PreInstantiateAllParts(List<RuntimeModelData> models, Dictionary<int, LDrawColor> colors)
        {
            var modelNames = new Dictionary<string, int>();
            for (var i=0; i<models.Count; i++)
            {
                var model = models[i];
                modelNames.Add(model.modelName, i);
            }

            foreach (var modelData in models)
            {
                var modelContainer = new ModelContainer(modelData.modelName);
                foreach (var step in modelData.steps)
                {
                    var objs = new List<GameObject>();
                    foreach (var part in step.parts)
                    {
                        var fileName = part.partId.Replace('\\', '_');
                        GameObject prefab = Resources.Load<GameObject>($"LDrawPrefabs/{fileName}");
                        if (prefab == null)
                        {
                            Debug.LogWarning($"Missing prefab for part: {part.partId}");
                            continue;
                        }
                        GameObject go = Instantiate(prefab, parentContainer);
                        go.transform.localPosition = part.position;
                        go.transform.localRotation = part.rotation;
                        if (!modelNames.ContainsKey(part.partId))
                        {
                            // Regular part: ensure it has a renderer, assign material asset if found
                            var renderer = go.GetComponent<Renderer>();
                            if (renderer == null)
                                renderer = go.AddComponent<MeshRenderer>();
                            var color = colors[part.color].color;
                            string colorKey = $"Mat_{color.r:F3}_{color.g:F3}_{color.b:F3}";
                            var mat = Resources.Load<Material>($"LDrawMaterials/{colorKey}");
                            if (mat != null)
                            {
                                renderer.material = mat;
                            }
                            else
                            {
                                Debug.LogError($"Missing material asset for color {colorKey} on part {part.partId}. Material asset must exist. Skipping material assignment.");
                            }
                        }
                        objs.Add(go);
                    }
                    modelContainer.AddStep(objs);
                }
                modelData.container = modelContainer;
            }
        }

        private void PopulateSteps()
        {
            for (var i=0;i<stepSprites.Length;i++)
            {
                // Create new item under the parent
                GameObject obj = Instantiate(stepPrefab, stepListParent);
                PartGridItem itemUI = obj.GetComponent<PartGridItem>();

                if (itemUI != null)
                {
                    var stepIdx = i;
                    itemUI.SetContent(stepSprites[i], $"{stepIdx+1}", ()=>
                    {
                        navigator.GotoStep(stepIdx);
                        UpdateNavigationText();
                        ShowStepParts();  
                    });
                }
                else
                {
                    Debug.LogWarning("Step item prefab is missing PartGridItem script.");
                }
            }
        }

        /// <summary>
        /// Adds a new item to the grid.
        /// </summary>
        public void AddItem(Sprite icon, string label, string partId, string description, Color color, string colorName, int index)
        {
            // Create new item under the parent
            GameObject obj = Instantiate(gridItemPrefab, gridParent);

            // Get the UI script from the prefab
            PartGridItem itemUI = obj.GetComponent<PartGridItem>();

            if (itemUI != null)
            {
                var go = navigator.GetPartFromCurrentStep(index);
                itemUI.SetContent(icon, label, ()=>
                    {
                        GameObject clone = Instantiate(go);

                        int previewLayer = LayerMask.NameToLayer("Preview");
                        clone.layer = previewLayer;
                        clone.SetActive(true);

                        // Optional: reset local transforms
                        clone.transform.position = Vector3.zero;
                        clone.transform.rotation = Quaternion.identity;
                        
                        leftPaneToggle.PreviewItem(partId, description, color, colorName, clone);
                    });
            }
            else
            {
                Debug.LogWarning("Grid item prefab is missing PartGridItem script.");
            }
        }

        /// <summary>
        /// Clears all items from the grid.
        /// </summary>
        public void ClearGrid()
        {
            foreach (Transform child in gridParent)
            {
                Destroy(child.gameObject);
            }
        }
    }
}