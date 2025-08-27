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
        }

        public bool CanNavigate
        {
            get
            {
                return canNavigate;
            }
        }
        
        public int CurrentModel
        {
            get
            {
                return currentModel;
            }
        }

        public void GotoStep(int step, bool animate = false)
        {
            if (step >= 0 && step < flatSteps.Count)
            {
                // currentStep = step;
                ShowFlatStep(step, animate);
            }
        }

        public void HideCurrentModel()
        {
            if (currentModel >= 0)
            {
                models[currentModel].container.Show(false);
                currentModel = -1;
            }             
        }

        private void HighlightStep(int step, bool highlight)
        {
            var flatStep = flatSteps[step];
            var container = models[flatStep.model].container;
            container.HighlightStep(flatStep.modelStepIdx, highlight);
        }

        private void ShowStepModification(Dictionary<int, LDrawBuildMod> buildMods, ModelContainer modelContainer, int step)
        {
            // Show all parts in this step, this is to ensure no MOD part is hidden by a later replacement.
            if (buildMods.Count > 0)
            {
                modelContainer.ShowStepParts(step, true, 0, -1);
            }
        
            if (buildMods.ContainsKey(step))
            {
                // Hide the corresponding parts in earlier step.
                var buildMod = buildMods[step];
                modelContainer.ShowStepParts(buildMod.step, false, buildMod.start, buildMod.end);
            }
        } 
        
        private void UpdateHighlight(int step)
        {
            if (preview) return;

            if (highlightedStep >= 0)
            {
                HighlightStep(highlightedStep, false);
                highlightedStep = -1;
            }

            HighlightStep(step, true);
            highlightedStep = step;
        }

        private void ShowFlatStep(int flatStepIdx, bool animateStep = true)
        {
            // Hide all models
            // HideCurrentModel();

            // Highlight the current step
            UpdateHighlight(flatStepIdx);

            var flatStep = flatSteps[flatStepIdx];
            var model = models[flatStep.model];
            var buildMods = model.buildMods;
            var modelContainer = model.container;
            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;

            // Show steps up to stepIdx-1
            for (int i = 0; i <= stepIdx - 1; i++)
            {
                ShowStepModification(buildMods, modelContainer, i);
                modelContainer.ShowStep(i, true);
            }

            // Hide steps from stepIdx
            for (int i = stepIdx; i < modelSteps.Count; i++)
            {
                modelContainer.ShowStep(i, false);
            }

            var lastStepOfModel = stepIdx == modelSteps.Count - 1;

            modelContainer.Show(true);
            currentModel = flatStep.model;

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
            ldrawCamera.SetCamera(center, radius, rotation, animateStep,
                () =>
                {
                    ShowStepModification(buildMods, modelContainer, stepIdx);
                    modelContainer.ShowStep(stepIdx, true);
                    canNavigate = true;
                }, currentModel, lastStepOfModel);
        }
   }
}