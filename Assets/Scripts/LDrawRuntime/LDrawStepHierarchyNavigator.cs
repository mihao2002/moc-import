using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LDraw.Runtime
{
    public class LDrawStepHierarchyNavigator
    {
        private Dictionary<string, List<LDrawStep>> models;
        private List<(string modelName, int stepIndex, int doneSubmodel)> navigationStack = new List<(string, int, int)>();
        private Dictionary<string, List<List<GameObject>>> modelStepObjects = new Dictionary<string, List<List<GameObject>>>();

        public LDrawStepHierarchyNavigator(Dictionary<string, List<LDrawStep>> models)
        {
            this.models = models;
        }

        // Call this method after setting up modelStepObjects with your own instantiation logic
        public void InitializeNavigation()
        {
            navigationStack.Clear();
            AddNextNavigationStackStep("main.ldr", 0, 0);
            ShowHierarchicalStep();
        }

        // Editor-specific method to set up modelStepObjects from editor-instantiated GameObjects
        public void SetModelStepObjects(Dictionary<string, List<List<GameObject>>> modelStepObjects)
        {
            this.modelStepObjects = modelStepObjects;
        }

        public void ShowNextStep()
        {
            if (!IsAtEnd)
            {
                NextHierarchicalStep();
            }
        }

        public void ShowPreviousStep()
        {
            if (!IsAtStart)
            {
                PrevHierarchicalStep();
            }
        }

        public (string modelName, int stepIndex) GetCurrentStep()
        {
            if (navigationStack.Count == 0) return (null, -1);
            var (modelName, stepIdx, _) = navigationStack[navigationStack.Count - 1];
            return (modelName, stepIdx);
        }

        public bool IsAtStart
        {
            get
            {
                for (int i = 0; i < navigationStack.Count; i++)
                {
                    var entry = navigationStack[i];
                    if (entry.stepIndex != 0)
                        return false;
                    if (i < navigationStack.Count - 1)
                    {
                        if (entry.doneSubmodel != 1)
                            return false;
                    }
                    else
                    {
                        if (entry.doneSubmodel != 0)
                            return false;
                    }
                }
                return true;
            }
        }

        public bool IsAtEnd
        {
            get
            {
                return navigationStack.Count == 1
                    && models.ContainsKey("main.ldr")
                    && navigationStack[0].stepIndex == models["main.ldr"].Count - 1;
            }
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