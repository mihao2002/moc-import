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
        // private int currentStep = 0;
        private int currentModel = -1;
        private int highlightedStep = -1;
        private int shownModel = -1;
        private bool canNavigate = true;
        private bool preview;
       
        public LDrawFlatStepNavigator(
            List<RuntimeModelData> models,
            LDrawCamera mainCamera,
            List<FlatStep> flatSteps,
            bool preview = false)
        {
            this.models = models;
            this.flatSteps = flatSteps;
            ldrawCamera = mainCamera;
            this.preview = preview;
            // ShowFlatStep();
        }

        public bool CanNavigate
        {
            get
            {
                return canNavigate;
            }
        }

        public int TotalStep
        {
            get
            {
                return flatSteps.Count;
            }
        }

        public void HighlightCurrent(int step)
        {
            if (preview) return;

            if (highlightedStep >= 0)
            {
                var flatStep = flatSteps[highlightedStep];
                var container = models[flatStep.model].container;
                container.HighlightStep(flatStep.modelStepIdx, false);
                highlightedStep = -1;
            }

            var currentFlatStep = flatSteps[step];
            var currentContainer = models[currentFlatStep.model].container;
            currentContainer.HighlightStep(currentFlatStep.modelStepIdx, true);
            highlightedStep = step;        
        }

        public void GotoStep(int step, bool animate = false)
        {
            if (step >= 0 && step < flatSteps.Count)
            {
                // currentStep = step;
                ShowFlatStep(step, animate);
            }
        }

        public List<LDrawPart> GetStepParts(int step)
        {
            var flatStep = flatSteps[step];
            var model = models[flatStep.model];
            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;
            return modelSteps[stepIdx].parts;
        }

        public GameObject GetPartFromStep(int step, int index)
        {
            var flatStep = flatSteps[step];
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

        public void HideShownModel()
        {
            if (shownModel >= 0)
            {
                models[shownModel].container.Show(false);
                shownModel = -1;
            }             
        }

        public void HideIfModelChange(int step)
        {
            var flatStep = flatSteps[step];
            if (currentModel != flatStep.model)
            {
                HideShownModel();               
            }
        } 

        private void ShowFlatStep(int step1, bool animateStep = true)
        {
            // Hide all models
            HideShownModel();

            var flatStep = flatSteps[step1];
            var model = models[flatStep.model];

            var modelContainer = model.container;

            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;

            // Highlight the current step
            HighlightCurrent(step1);

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

            var lastStepOfModel = stepIdx == modelSteps.Count-1;

            modelContainer.Show(true);
            shownModel = flatStep.model;

            // Set camera distance for this step
            var step = modelSteps[stepIdx];
            var rotation = modelSteps[stepIdx].rotation;
            if (rotation == null)
            {
                rotation = modelSteps[stepIdx].rotRef == -1 ? LDrawCamera.DefaultRotation : modelSteps[modelSteps[stepIdx].rotRef].rotation;
            }

            var center = step.center;
            var radius = step.radius;
            if (preview)
            {
                center = step.modelBounds.center;
                radius = step.modelBounds.extents.magnitude;
            }
            
            canNavigate = false;
            // Debug.LogError($"ShowFlatStep show:{shownModel} current:{currentModel}");
            ldrawCamera.SetCamera(center, radius, rotation, animateStep, 
                () =>
                {
                    modelContainer.ShowStep(stepIdx, true);
                    canNavigate = true;
                }, shownModel, lastStepOfModel);

            currentModel = shownModel; 
        }
   }
}