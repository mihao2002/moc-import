using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LDraw.Runtime
{
    [System.Serializable]
    public class LDrawModelStepData
    {
        public List<ModelStepPair> models;
        public Dictionary<string, List<LDrawStep>> ToDictionary()
        {
            var dict = new Dictionary<string, List<LDrawStep>>();
            if (models != null)
            {
                foreach (var pair in models)
                {
                    dict[pair.modelName] = pair.steps;
                }
            }
            return dict;
        }
    }
    [System.Serializable]
    public class ModelStepPair
    {
        public string modelName;
        public List<LDrawStep> steps;
    }

    public class LDrawStepNavigator : MonoBehaviour
    {
        public Transform parentContainer; // Where to spawn parts in the scene
        public TMP_Text navigationText; // Assign in inspector to show current model/step (TextMeshPro)

        private Dictionary<string, List<LDrawStep>> models;
        private List<(string modelName, int stepIndex, int doneSubmodel)> navigationStack = new List<(string, int, int)>();
        private Dictionary<string, List<List<GameObject>>> modelStepObjects = new Dictionary<string, List<List<GameObject>>>();

        void Start()
        {
            // Load model step data from Resources
            var jsonAsset = Resources.Load<TextAsset>("LDrawStepData");
            if (jsonAsset == null)
            {
                Debug.LogError("LDrawStepData.json not found in Resources!");
                return;
            }
            var wrapper = JsonUtility.FromJson<LDrawModelStepData>(jsonAsset.text);
            models = wrapper.ToDictionary();
            if (models == null || models.Count == 0)
            {
                Debug.LogError("No models found in LDrawStepData.json!");
                return;
            }
            // Pre-instantiate all step objects (but keep them inactive)
            modelStepObjects.Clear();
            foreach (var kvp in models)
            {
                var stepObjs = new List<List<GameObject>>();
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
                        GameObject go = Instantiate(prefab, parentContainer);
                        go.transform.localPosition = part.position;
                        go.transform.localRotation = part.rotation;
                        // Submodel or regular part?
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
                        go.SetActive(false);
                        objs.Add(go);
                    }
                    stepObjs.Add(objs);
                }
                modelStepObjects[kvp.Key] = stepObjs;
            }
            // Start navigation at main model, step 0
            navigationStack.Clear();
            AddNextNavigationStackStep("main.ldr", 0, 0);
            ShowHierarchicalStep();
        }

        public void ShowNextStep()
        {
            NextHierarchicalStep();
        }

        public void ShowPreviousStep()
        {
            PrevHierarchicalStep();
        }

        private void ShowHierarchicalStep()
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
            Debug.Log($"ShowHierarchicalStep {modelName} {stepList.Count} {stepIdx}");
            for (int i = 0; i <= stepIdx; i++)
            {
                foreach (var go in stepList[i])
                {
                    if (go != null)
                    {
                        Debug.Log($"SetActive {go.name}");
                        go.SetActive(true);
                    }                        
                }
            }
            // Update navigation text if assigned
            if (navigationText != null && models != null && models.ContainsKey(modelName))
            {
                navigationText.text = $"Model: {modelName} | Step: {stepIdx + 1} / {models[modelName].Count}";
            }
        }

        private void AddNextNavigationStackStep(string model, int step, int doneSubmodel)
        {
            var nextParts = models[model][step].parts;
            var nextModels = new List<LDrawPart>();
            foreach (var p in nextParts)
                if (models.ContainsKey(p.partId)) nextModels.Add(p);
            if (nextModels.Count > 0)
            {
                if (doneSubmodel < nextModels.Count)
                {
                    navigationStack.Add((model, step, doneSubmodel + 1));
                    AddNextNavigationStackStep(nextModels[doneSubmodel].partId, 0, 0);
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

        private void NextHierarchicalStep()
        {
            if (navigationStack.Count == 0) return;
            var (currentModel, currentStep, doneSubmodel) = navigationStack[navigationStack.Count - 1];
            var steps = models[currentModel];
            var step = steps[currentStep];
            var submodels = new List<LDrawPart>();
            foreach (var p in step.parts)
                if (models.ContainsKey(p.partId)) submodels.Add(p);
            if (doneSubmodel == submodels.Count)
            {
                navigationStack.RemoveAt(navigationStack.Count - 1);
                if (currentStep + 1 < steps.Count)
                {
                    AddNextNavigationStackStep(currentModel, currentStep + 1, 0);
                }
                else
                {
                    if (navigationStack.Count > 0)
                    {
                        var (parentModel, parentStep, parentDoneSubmodel) = navigationStack[navigationStack.Count - 1];
                        var parentStepObj = models[parentModel][parentStep];
                        var parentSubmodels = new List<LDrawPart>();
                        foreach (var p in parentStepObj.parts)
                            if (models.ContainsKey(p.partId)) parentSubmodels.Add(p);
                        if (parentDoneSubmodel < parentSubmodels.Count)
                        {
                            navigationStack.RemoveAt(navigationStack.Count - 1);
                            AddNextNavigationStackStep(parentModel, parentStep, parentDoneSubmodel);
                        }
                    }
                }
            }
            else
            {
                navigationStack.RemoveAt(navigationStack.Count - 1);
                AddNextNavigationStackStep(currentModel, currentStep, doneSubmodel + 1);
            }
            ShowHierarchicalStep();
        }

        private void AddPrevNavigationStackStep(string model, int step, int doneSubmodel, bool drillin = true)
        {
            var prevModel = models[model];
            if (step == -1) step = prevModel.Count - 1;
            var prevParts = prevModel[step].parts;
            var prevModels = new List<LDrawPart>();
            foreach (var p in prevParts)
                if (models.ContainsKey(p.partId)) prevModels.Add(p);
            if (doneSubmodel == -1) doneSubmodel = prevModels.Count;
            if (prevModels.Count == 0)
            {
                navigationStack.Add((model, step, doneSubmodel));
            }
            else
            {
                navigationStack.Add((model, step, doneSubmodel));
                if (drillin)
                {
                    AddPrevNavigationStackStep(prevModels[doneSubmodel - 1].partId, -1, -1, false);
                }
            }
        }

        private void PrevHierarchicalStep()
        {
            if (navigationStack.Count == 0) return;
            var (currentModel, currentStep, doneSubmodel) = navigationStack[navigationStack.Count - 1];
            var steps = models[currentModel];
            var step = steps[currentStep];
            var submodels = new List<LDrawPart>();
            foreach (var p in step.parts)
                if (models.ContainsKey(p.partId)) submodels.Add(p);
            navigationStack.RemoveAt(navigationStack.Count - 1);
            if (submodels.Count > 0)
            {
                if (doneSubmodel == submodels.Count)
                {
                    AddPrevNavigationStackStep(currentModel, currentStep, doneSubmodel);
                }
            }
            else
            {
                if (currentStep > 0)
                {
                    AddPrevNavigationStackStep(currentModel, currentStep - 1, -1, false);
                }
                else
                {
                    var idx = navigationStack.Count - 1;
                    while (idx >= 0)
                    {
                        var (prevModel, prevStep, prevDoneSubmodel) = navigationStack[idx];
                        if (prevDoneSubmodel > 1 || prevStep > 0)
                        {
                            while (navigationStack.Count > idx)
                            {
                                navigationStack.RemoveAt(navigationStack.Count - 1);
                            }
                            if (prevDoneSubmodel > 1)
                            {
                                AddPrevNavigationStackStep(prevModel, prevStep, prevDoneSubmodel - 1);
                            }
                            else
                            {
                                AddPrevNavigationStackStep(prevModel, prevStep - 1, -1, false);
                            }
                            break;
                        }
                        idx--;
                    }
                }
            }
            ShowHierarchicalStep();
        }
    }
}