using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LDraw.Runtime
{
    public class LDrawStepHierarchyNavigator
    {
        private Dictionary<string, List<LDrawStep>> models;
        private List<(string modelName, int stepIndex, int doneSubmodel)> navigationStack = new List<(string, int, int)>();
        private Dictionary<string, ModelContainer> modelContainers = new Dictionary<string, ModelContainer>();
        private LDrawCamera ldrawCamera;
        private string mainModelName;
        private ModelContainer highlightedModel = null;
        private int highlightedStep = 0;
       
        public LDrawStepHierarchyNavigator(Dictionary<string, List<LDrawStep>> models, Camera mainCamera)
        {
            this.models = models;
            ldrawCamera = new LDrawCamera(mainCamera);
        }

        public LDrawCamera GetCamera()
        {
            return ldrawCamera;
        }

        // Call this method after setting up modelContainers with your own instantiation logic
        public void InitializeNavigation(string mainModelName)
        {
            this.mainModelName = mainModelName;
            navigationStack.Clear();
            AddNextNavigationStackStep(mainModelName, 0, 0);
            ShowHierarchicalStep();
        }

        // Method to set up modelContainers from instantiated ModelContainer objects (both editor and runtime)
        public void SetModelContainers(Dictionary<string, ModelContainer> modelContainers)
        {
            this.modelContainers = modelContainers;
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
                    && models.ContainsKey(this.mainModelName)
                    && navigationStack[0].stepIndex == models[this.mainModelName].Count - 1;
            }
        }

        private void ShowHierarchicalStep()
        {
            Vector3 defaultRotation = new Vector3(30f, 45f, 0f);

            // Hide all models
            foreach (var container in modelContainers.Values)
            {
                container.Show(false);
            }

            if (highlightedModel != null)
            {
                highlightedModel.HighlightStep(highlightedStep, false);
                highlightedModel = null;
            }

            // Show the current model
            var stackIdx = navigationStack.Count - 1;
            var (modelName, stepIdx, doneSubmodel) = navigationStack[stackIdx];
            if (!modelContainers.ContainsKey(modelName)) return;
            var modelContainer = modelContainers[modelName];
            modelContainer.Show(true);

            var modelSteps = models[modelName];
            Debug.Log($"ShowHierarchicalStep {modelName} {modelSteps.Count} {stepIdx} {modelSteps[stepIdx].rotation.HasValue}");

            // Highlight the current step
            modelContainer.HighlightStep(stepIdx, true);
            highlightedModel = modelContainer;
            highlightedStep = stepIdx;

            // Show steps up to and including stepIdx
            for (int i = 0; i <= stepIdx; i++)
            {
                modelContainer.ShowStep(i, true);
            }

            // Hide steps after stepIdx
            for (int i = stepIdx + 1; i < modelSteps.Count; i++)
            {
                modelContainer.ShowStep(i, false);
            }

            // Set camera distance for this step
            if (stepIdx < modelSteps.Count)
            {
                var step = modelSteps[stepIdx];
                var rotation = modelSteps[stepIdx].rotation;
                if (rotation == null)
                {
                    rotation = modelSteps[stepIdx].rotRef == -1 ? defaultRotation : modelSteps[modelSteps[stepIdx].rotRef].rotation;
                }
                ldrawCamera.SetCamera(step.center, step.radius, rotation);
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