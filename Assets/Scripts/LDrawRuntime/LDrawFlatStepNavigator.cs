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
        private int highlightedStep = -1;
        private int shownModel = -1;
       
        public LDrawFlatStepNavigator(
            List<RuntimeModelData> models,
            LDrawCamera mainCamera,
            List<FlatStep> flatSteps)
        {
            this.models = models;
            this.flatSteps = flatSteps;
            ldrawCamera = mainCamera;
            ShowFlatStep();
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
                ShowFlatStep();
            }
        }

        public int ShowNextStep()
        {
            if (!IsAtEnd)
            {
                currentStep++;
                ShowFlatStep();
            }

            return currentStep;
        }

        public int ShowPreviousStep()
        {
            if (!IsAtStart)
            {
                currentStep--;
                ShowFlatStep();
            }

            return currentStep;
        }

        private void ShowFlatStep()
        {
            Vector3 defaultRotation = new Vector3(30f, 45f, 0f);

            // Hide all models
            if (shownModel >= 0)
            {
                models[shownModel].container.Show(false);
            }

            var flatStep = flatSteps[currentStep];
            var model = models[flatStep.model];

            var modelContainer = model.container;

            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;

            // Highlight the current step
            HighlightCurrent();

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

            modelContainer.Show(true);
            shownModel = flatStep.model;

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
   }
}