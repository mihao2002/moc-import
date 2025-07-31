using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Newtonsoft.Json;

namespace LDraw.Runtime
{
    public class LDrawStepNavigator : MonoBehaviour
    {
        public Transform parentContainer; // Where to spawn parts in the scene
        public TMP_Text navigationText; // Assign in inspector to show current model/step (TextMeshPro)
        public Camera mainCamera; // Assign in inspector

        private LDrawStepHierarchyNavigator navigator;

        void Start()
        {
            // Load model step data from Resources
            var jsonAsset = Resources.Load<TextAsset>("LDrawStepData");
            if (jsonAsset == null)
            {
                Debug.LogError("LDrawStepData.json not found in Resources!");
                return;
            }
            // var wrapper = JsonUtility.FromJson<LDrawModelStepData>(jsonAsset.text);
            var wrapper = JsonConvert.DeserializeObject<LDrawModelStepData>(jsonAsset.text);

            var models = wrapper.ToDictionary();
            if (models == null || models.Count == 0)
            {
                Debug.LogError("No models found in LDrawStepData.json!");
                return;
            }
            navigator = new LDrawStepHierarchyNavigator(models, mainCamera != null ? mainCamera : Camera.main);
            navigator.InitializeNavigation(); // Initialize navigation first
            PreInstantiateAllParts(); // Runtime-specific: instantiate from prefabs
            UpdateNavigationText();
        }

        public void ShowNextStep()
        {
            navigator.ShowNextStep();
            UpdateNavigationText();
        }

        public void ShowPreviousStep()
        {
            navigator.ShowPreviousStep();
            UpdateNavigationText();
        }

        private void UpdateNavigationText()
        {
            if (navigationText != null && navigator != null)
            {
                var (modelName, stepIdx) = navigator.GetCurrentStep();
                if (modelName != null && stepIdx >= 0)
                {
                    var modelContainers = navigator.GetType().GetField("modelContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(navigator) as Dictionary<string, ModelContainer>;
                    if (modelContainers != null && modelContainers.ContainsKey(modelName))
                    {
                        var container = modelContainers[modelName];
                        navigationText.text = $"Model: {modelName} | Step: {stepIdx + 1} / {container.GetStepCount()}";
                    }
                }
            }
        }

        // Runtime-specific method to instantiate all parts from prefabs
        private void PreInstantiateAllParts()
        {
            var models = navigator.GetType().GetField("models", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(navigator) as Dictionary<string, List<LDrawStep>>;
            var modelContainers = new Dictionary<string, ModelContainer>();
            
            foreach (var kvp in models)
            {
                var modelContainer = new ModelContainer(kvp.Key);
                foreach (var step in kvp.Value)
                {
                    var objs = new List<GameObject>();
                    foreach (var part in step.parts)
                    {
                        GameObject prefab = Resources.Load<GameObject>($"LDrawPrefabs/{part.partId}");
                        if (prefab == null)
                        {
                            Debug.LogWarning($"Missing prefab for part: {part.partId}");
                            continue;
                        }
                        GameObject go = Object.Instantiate(prefab, parentContainer);
                        go.transform.localPosition = part.position;
                        go.transform.localRotation = part.rotation;
                        if (!models.ContainsKey(part.partId))
                        {
                            // Regular part: ensure it has a renderer, assign material asset if found
                            var renderer = go.GetComponent<Renderer>();
                            if (renderer == null)
                                renderer = go.AddComponent<MeshRenderer>();
                            string colorKey = $"Mat_{part.color.r:F3}_{part.color.g:F3}_{part.color.b:F3}";
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
                modelContainers[kvp.Key] = modelContainer;
            }
            // Set the modelContainers in the navigator
            navigator.SetModelContainers(modelContainers);
        }
    }
}