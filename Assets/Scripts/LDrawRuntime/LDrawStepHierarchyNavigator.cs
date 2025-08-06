using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LDraw.Runtime
{
    public class LDrawStepHierarchyNavigator
    {
        private List<RuntimeModelData> models;
        private List<(int modelIdx, int stepIndex, int doneSubmodel)> navigationStack = new List<(int, int, int)>();
        private LDrawCamera ldrawCamera;
        private Dictionary<string, int> modelNames;
        private int highlightedModel = -1;
        private int highlightedStep = 0;
       
        public LDrawStepHierarchyNavigator(List<RuntimeModelData> models, LDrawCamera mainCamera)
        {
            this.models = models;
            modelNames = new Dictionary<string, int>();
            for (var i=0; i<models.Count; i++)
            {
                var model = models[i];
                modelNames.Add(model.modelName, i);
            }

            ldrawCamera = mainCamera;

            navigationStack.Clear();
            AddNextNavigationStackStep(0, 0, 0);
            ShowHierarchicalStep();
        }

        public LDrawCamera GetCamera()
        {
            return ldrawCamera;
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
            var (modelIdx, stepIdx, _) = navigationStack[navigationStack.Count - 1];
            return (models[modelIdx].modelName, stepIdx);
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
                    && navigationStack[0].stepIndex == models[0].steps.Count - 1;
            }
        }

        private void ShowHierarchicalStep()
        {
            Vector3 defaultRotation = new Vector3(30f, 45f, 0f);

            // Hide all models
            foreach (var model in models)
            {
                model.container.Show(false);
            }

            if (highlightedModel != -1)
            {
                var container = models[highlightedModel].container;
                container.HighlightStep(highlightedStep, false);
                highlightedModel = -1;
            }

            // Show the current model
            var stackIdx = navigationStack.Count - 1;
            var (modelIdx, stepIdx, doneSubmodel) = navigationStack[stackIdx];
            var modelContainer = models[modelIdx].container;
            modelContainer.Show(true);

            var modelSteps = models[modelIdx].steps;

            // Highlight the current step
            modelContainer.HighlightStep(stepIdx, true);
            highlightedModel = modelIdx;
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

        private void AddNextNavigationStackStep(int modelIdx, int step, int doneSubmodel)
        {
            var nextParts = models[modelIdx].steps[step].parts;
            var nextModels = new List<LDrawPart>();
            foreach (var p in nextParts)
                if (modelNames.ContainsKey(p.partId)) nextModels.Add(p);
            if (nextModels.Count > 0)
            {
                if (doneSubmodel < nextModels.Count)
                {
                    navigationStack.Add((modelIdx, step, doneSubmodel + 1));
                    AddNextNavigationStackStep(modelNames[nextModels[doneSubmodel].partId], 0, 0);
                }
                else
                {
                    navigationStack.Add((modelIdx, step, doneSubmodel));
                }
            }
            else
            {
                navigationStack.Add((modelIdx, step, 0));
            }
        }

        private void NextHierarchicalStep()
        {
            if (navigationStack.Count == 0) return;
            var (currentModelIdx, currentStep, doneSubmodel) = navigationStack[navigationStack.Count - 1];
            var steps = models[currentModelIdx].steps;
            var step = steps[currentStep];
            var submodels = new List<LDrawPart>();
            foreach (var p in step.parts)
                if (modelNames.ContainsKey(p.partId)) submodels.Add(p);
            if (doneSubmodel == submodels.Count)
            {
                navigationStack.RemoveAt(navigationStack.Count - 1);
                if (currentStep + 1 < steps.Count)
                {
                    AddNextNavigationStackStep(currentModelIdx, currentStep + 1, 0);
                }
                else
                {
                    if (navigationStack.Count > 0)
                    {
                        var (parentModelIdx, parentStep, parentDoneSubmodel) = navigationStack[navigationStack.Count - 1];
                        var parentStepObj = models[parentModelIdx].steps[parentStep];
                        var parentSubmodels = new List<LDrawPart>();
                        foreach (var p in parentStepObj.parts)
                            if (modelNames.ContainsKey(p.partId)) parentSubmodels.Add(p);
                        if (parentDoneSubmodel < parentSubmodels.Count)
                        {
                            navigationStack.RemoveAt(navigationStack.Count - 1);
                            AddNextNavigationStackStep(parentModelIdx, parentStep, parentDoneSubmodel);
                        }
                    }
                }
            }
            else
            {
                navigationStack.RemoveAt(navigationStack.Count - 1);
                AddNextNavigationStackStep(currentModelIdx, currentStep, doneSubmodel + 1);
            }
            ShowHierarchicalStep();
        }

        private void AddPrevNavigationStackStep(int modelIdx, int step, int doneSubmodel, bool drillin = true)
        {
            var prevModel = models[modelIdx].steps;
            if (step == -1) step = prevModel.Count - 1;
            var prevParts = prevModel[step].parts;
            var prevModels = new List<LDrawPart>();
            foreach (var p in prevParts)
                if (modelNames.ContainsKey(p.partId)) prevModels.Add(p);
            if (doneSubmodel == -1) doneSubmodel = prevModels.Count;
            if (prevModels.Count == 0)
            {
                navigationStack.Add((modelIdx, step, doneSubmodel));
            }
            else
            {
                navigationStack.Add((modelIdx, step, doneSubmodel));
                if (drillin)
                {
                    AddPrevNavigationStackStep(modelNames[prevModels[doneSubmodel - 1].partId], -1, -1, false);
                }
            }
        }

        private void PrevHierarchicalStep()
        {
            if (navigationStack.Count == 0) return;
            var (currentModelIdx, currentStep, doneSubmodel) = navigationStack[navigationStack.Count - 1];
            var steps = models[currentModelIdx].steps;
            var step = steps[currentStep];
            var submodels = new List<LDrawPart>();
            foreach (var p in step.parts)
                if (modelNames.ContainsKey(p.partId)) submodels.Add(p);
            navigationStack.RemoveAt(navigationStack.Count - 1);
            if (submodels.Count > 0)
            {
                if (doneSubmodel == submodels.Count)
                {
                    AddPrevNavigationStackStep(currentModelIdx, currentStep, doneSubmodel);
                }
            }
            else
            {
                if (currentStep > 0)
                {
                    AddPrevNavigationStackStep(currentModelIdx, currentStep - 1, -1, false);
                }
                else
                {
                    var idx = navigationStack.Count - 1;
                    while (idx >= 0)
                    {
                        var (prevModelIdx, prevStep, prevDoneSubmodel) = navigationStack[idx];
                        if (prevDoneSubmodel > 1 || prevStep > 0)
                        {
                            while (navigationStack.Count > idx)
                            {
                                navigationStack.RemoveAt(navigationStack.Count - 1);
                            }
                            if (prevDoneSubmodel > 1)
                            {
                                AddPrevNavigationStackStep(prevModelIdx, prevStep, prevDoneSubmodel - 1);
                            }
                            else
                            {
                                AddPrevNavigationStackStep(prevModelIdx, prevStep - 1, -1, false);
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