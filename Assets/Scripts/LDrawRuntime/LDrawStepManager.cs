using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LDraw.Runtime
{
    public class LDrawStepManager
    {
        private List<RuntimeModelData> models;
        private List<FlatStep> flatSteps;

        public LDrawStepManager(
            List<RuntimeModelData> models,
            List<FlatStep> flatSteps)
        {
            this.models = models;
            this.flatSteps = flatSteps;
        }

        public int TotalStep
        {
            get
            {
                return flatSteps.Count;
            }
        }

        public Dictionary<LDrawPartCore, (int, int) /*count, index*/> GetStepParts(int step)
        {
            var flatStep = flatSteps[step];
            var model = models[flatStep.model];
            var modelSteps = model.steps;
            var stepIdx = flatStep.modelStepIdx;
            var parts = modelSteps[stepIdx].parts;

            var results = new Dictionary<LDrawPartCore, int>();
            var partIndexes = new Dictionary<LDrawPartCore, int>();
            var totalCount = 0;
            foreach (var part in parts)
            {
                if (results.ContainsKey(part))
                {
                    results[part] += 1;
                }
                else
                {
                    results[part] = 1;
                    partIndexes[part] = totalCount;
                }

                totalCount++;
            }

            var buildMods = model.buildMods;
            if (buildMods.ContainsKey(flatStep.modelStepIdx))
            {
                var buildMod = buildMods[flatStep.modelStepIdx];
                var refStepParts = modelSteps[buildMod.step].parts;
                for (var i = buildMod.start; i <= buildMod.end; i++)
                {
                    var part = refStepParts[i];
                    if (results.ContainsKey(part))
                    {
                        results[part]--;
                        if (results[part] == 0)
                        {
                            results.Remove(part);
                        }
                    }
                }
            }

            var newResults = new Dictionary<LDrawPartCore, (int, int)>();
            foreach (var kvp in results)
            {
                newResults.Add(kvp.Key, (kvp.Value, partIndexes[kvp.Key]));
            }

            return newResults;
        }

        public GameObject GetPartFromStep(int step, int index)
        {
            var flatStep = flatSteps[step];
            var model = models[flatStep.model];
            var modelContainer = model.container;
            var stepContainer = modelContainer.GetStepContainer(flatStep.modelStepIdx);
            return stepContainer.ClonePart(index);
        }

        public int GetModel(int step)
        {
            var flatStep = flatSteps[step];
            return flatStep.model;
        }
    }
}