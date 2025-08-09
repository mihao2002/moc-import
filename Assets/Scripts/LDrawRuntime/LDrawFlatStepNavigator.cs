using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LDraw.Runtime
{
    public class LDrawFlatStepNavigator
    {
        private List<RuntimeModelData> models;
        private List<FlatStep> flatSteps;
        private LDrawCamera ldrawCamera;
        private int currentStep = 0;
        private int currentModel = -1;
        private int highlightedStep = -1;
        private int shownModel = -1;
        private bool canNavigate = true;
        private bool highlight;
       
        public LDrawFlatStepNavigator(
            List<RuntimeModelData> models,
            LDrawCamera mainCamera,
            List<FlatStep> flatSteps,
            bool highlight = true)
        {
            this.models = models;
            this.flatSteps = flatSteps;
            ldrawCamera = mainCamera;
            this.highlight = highlight;
            ShowFlatStep();
        }

        public bool CanNavigate
        {
            get
            {
                return canNavigate;
            }
        }

        public int CurrentStep
        {
            get
            {
                return currentStep;
            }
        }

        public bool IsAtStart
        {
            get
            {
                return currentStep == 0;
            }
        }

        public bool IsAtEnd
        {
            get
            {
                return currentStep == flatSteps.Count - 1;
            }
        }

        public (string modelName, int stepIndex, int stepCount) GetCurrentStep()
        {
            var flatStep = flatSteps[currentStep];
            var model = models[flatStep.model];

            return (model.modelName, flatStep.modelStepIdx, model.steps.Count);
        }

        public void HighlightCurrent()
        {
            if (!highlight) return;

            if (highlightedStep >= 0)
            {
                var flatStep = flatSteps[highlightedStep];
                var container = models[flatStep.model].container;
                container.HighlightStep(flatStep.modelStepIdx, false);
                highlightedStep = -1;
            }

            var currentFlatStep = flatSteps[currentStep];
            var currentContainer = models[currentFlatStep.model].container;
            currentContainer.HighlightStep(currentFlatStep.modelStepIdx, true);
            highlightedStep = currentStep;        
        }

        public void GotoStep(int step)
        {
            if (step >= 0 && step < flatSteps.Count)
            {
                currentStep = step;
                ShowFlatStep(false);
            }
        }

        public void ShowNextStep(bool animate = true)
        {
            if (!IsAtEnd)
            {
                currentStep++;
                ShowFlatStep(animate);
            }
        }

        public void ShowPreviousStep()
        {
            if (!IsAtStart)
            {
                currentStep--;
                ShowFlatStep();
            }
        }

        public List<LDrawPart> GetCurrentParts()
        {
            var flatStep = flatSteps[currentStep];
            var model = models[flatStep.model];
            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;
            return modelSteps[stepIdx].parts;
        }

        public GameObject GetPartFromCurrentStep(int index)
        {
            var flatStep = flatSteps[currentStep];
            var model = models[flatStep.model];
            var modelContainer = model.container;
            var stepContainer = modelContainer.GetStepContainer(flatStep.modelStepIdx);
            GameObject childGo = stepContainer.transform.GetChild(index)?.gameObject;
            if (childGo == null)
            {
                Debug.LogError($"Failed to get object {index}");
                return null;
            }

            return childGo;
        }

        private void ShowFlatStep(bool animateStep = true)
        {
            // Hide all models
            if (shownModel >= 0)
            {
                models[shownModel].container.Show(false);
                shownModel = -1;
            }

            var flatStep = flatSteps[currentStep];
            var model = models[flatStep.model];

            var modelContainer = model.container;

            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;

            // Highlight the current step
            HighlightCurrent();

            // Show steps up to stepIdx-1
            for (int i = 0; i <= stepIdx-1; i++)
            {
                modelContainer.ShowStep(i, true);
            }

            // Hide steps after stepIdx
            for (int i = stepIdx + 1; i < modelSteps.Count; i++)
            {
                modelContainer.ShowStep(i, false);
            }

            modelContainer.Show(true);
            shownModel = flatStep.model;

            // Set camera distance for this step
            if (stepIdx < modelSteps.Count)
            {
                var step = modelSteps[stepIdx];
                var rotation = modelSteps[stepIdx].rotation;
                if (rotation == null)
                {
                    rotation = modelSteps[stepIdx].rotRef == -1 ? LDrawCamera.DefaultRotation : modelSteps[modelSteps[stepIdx].rotRef].rotation;
                }
                
                canNavigate = false;
                ldrawCamera.SetCamera(step.center, step.radius, rotation, 
                    animateStep && (currentModel != -1 && currentModel == shownModel), 
                    () =>
                    {
                        modelContainer.ShowStep(stepIdx, true);
                        canNavigate = true;
                    });
                currentModel = shownModel;
            }
        }
   }
}