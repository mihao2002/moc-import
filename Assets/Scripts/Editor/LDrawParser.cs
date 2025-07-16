using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;

namespace LDraw.Editor
{
    public static class LDrawParser
    {
        public static string mainModelName = "main.ldr";
        public static List<LDrawStep> Parse(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var models = new Dictionary<string, List<LDrawStep>>();
            var modelOrder = new List<string>();
            string currentModel = mainModelName;
            int modelStart = 0;
            var fileSections = new List<(string name, int start, int end)>();

            // 1. Identify model sections (by 0 FILE ...)
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("0 FILE ", StringComparison.OrdinalIgnoreCase))
                {
                    // Save previous section
                    if (i > modelStart)
                    {
                        fileSections.Add((currentModel, modelStart, i));
                    }
                    currentModel = line.Trim().Substring(7).Trim().ToLower();
                    modelOrder.Add(currentModel);
                    modelStart = i + 1;
                }
            }
            // Add last section
            if (modelStart < lines.Length)
                fileSections.Add((currentModel, modelStart, lines.Length));

            // 2. Parse each model section into steps
            if (fileSections.Count == 0)
            {
                // No 0 FILE, treat as single model
                models[mainModelName] = ParseStepsFromLines(lines, 0, lines.Length);
                modelOrder.Add(mainModelName);
            }
            else
            {
                foreach (var (name, start, end) in fileSections)
                {
                    models[name] = ParseStepsFromLines(lines, start, end);
                }
            }

            // 3. Recursively expand steps for the main model
            string mainModel = modelOrder.Count > 0 ? modelOrder[0] : mainModelName;
            var expandedSteps = ExpandModelSteps(mainModel, models, new HashSet<string>());
            return expandedSteps;
        }

        // New: Parse all models and their steps, without recursive expansion
        public static Dictionary<string, List<LDrawStep>> ParseModels(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var models = new Dictionary<string, List<LDrawStep>>();
            var modelOrder = new List<string>();
            string currentModel = mainModelName;
            int modelStart = 0;
            var fileSections = new List<(string name, int start, int end)>();

            // Identify model sections (by 0 FILE ...)
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("0 FILE ", StringComparison.OrdinalIgnoreCase))
                {
                    // Save previous section
                    if (i > modelStart)
                    {
                        fileSections.Add((currentModel, modelStart, i));
                    }
                    currentModel = line.Trim().Substring(7).Trim().ToLower();
                    modelOrder.Add(currentModel);
                    modelStart = i + 1;
                }
            }
            // Add last section
            if (modelStart < lines.Length)
                fileSections.Add((currentModel, modelStart, lines.Length));

            // Parse each model section into steps
            if (fileSections.Count == 0)
            {
                // No 0 FILE, treat as single model
                models[mainModelName] = ParseStepsFromLines(lines, 0, lines.Length);
            }
            else
            {
                foreach (var (name, start, end) in fileSections)
                {
                    models[name] = ParseStepsFromLines(lines, start, end);
                }
            }
            return models;
        }

        // Helper: Parse a range of lines into steps
        private static List<LDrawStep> ParseStepsFromLines(string[] lines, int start, int end)
        {
            var steps = new List<LDrawStep>();
            var currentStep = new LDrawStep();
            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (line.Trim().ToLower() == "0 step")
                {
                    if (currentStep.parts.Count > 0)
                    {
                        steps.Add(currentStep);
                        currentStep = new LDrawStep();
                    }
                }
                else if (line.StartsWith("1 "))
                {
                    var tokens = Regex.Split(line.Trim(), " +");
                    if (tokens.Length >= 15)
                    {
                        var part = new LDrawPart();
                        part.partId = tokens[14].ToLower();
                        Vector3 posLDraw = new Vector3(
                            float.Parse(tokens[2]),
                            float.Parse(tokens[3]),
                            float.Parse(tokens[4])
                        ) * 0.01f;
                        part.position = new Vector3(posLDraw.x, posLDraw.y, -posLDraw.z);
                        Matrix4x4 mLDraw = new Matrix4x4();
                        mLDraw.SetColumn(0, new Vector4(float.Parse(tokens[5]), float.Parse(tokens[8]), float.Parse(tokens[11]), 0));
                        mLDraw.SetColumn(1, new Vector4(float.Parse(tokens[6]), float.Parse(tokens[9]), float.Parse(tokens[12]), 0));
                        mLDraw.SetColumn(2, new Vector4(float.Parse(tokens[7]), float.Parse(tokens[10]), float.Parse(tokens[13]), 0));
                        mLDraw.SetColumn(3, new Vector4(0, 0, 0, 1));
                        Matrix4x4 RL = LDrawPartLoader.negateZ * mLDraw * LDrawPartLoader.negateZ;
                        part.rotation = RL.rotation;
                        int colorCode = int.Parse(tokens[1]);
                        part.color = LDrawColorManager.GetColor(colorCode);
                        currentStep.parts.Add(part);
                    }
                }
            }
            if (currentStep.parts.Count > 0)
                steps.Add(currentStep);
            return steps;
        }

        // Recursively expand steps for a model, inlining submodel steps and adding a step for the assembled submodel
        private static List<LDrawStep> ExpandModelSteps(string modelName, Dictionary<string, List<LDrawStep>> models, HashSet<string> callStack)
        {
            if (!models.ContainsKey(modelName))
                return new List<LDrawStep>();
            var result = new List<LDrawStep>();
            var modelSteps = models[modelName];

            foreach (var step in modelSteps)
            {
                var newStep = new LDrawStep();
                foreach (var part in step.parts)
                {
                    if (models.ContainsKey(part.partId) && !callStack.Contains(part.partId))
                    {
                        // Entering submodel: expand recursively
                        callStack.Add(part.partId);
                        var subSteps = ExpandModelSteps(part.partId, models, callStack);
                        callStack.Remove(part.partId);
                        result.AddRange(subSteps);
                        // Set showAllPrevious on the step that adds the assembled submodel if needed
                        if (subSteps.Count > 0)
                        {
                            var showStep = new LDrawStep();
                            showStep.parts.Add(part); // Add the assembled submodel as a part
                            result.Add(showStep);
                        }
                        else
                        {
                            // If no context, just add the submodel as a part
                            var submodelStep = new LDrawStep();
                            submodelStep.parts.Add(part);
                            result.Add(submodelStep);
                        }
                    }
                    else
                    {
                        newStep.parts.Add(part);
                    }
                }
                if (newStep.parts.Count > 0)
                {
                    result.Add(newStep);
                }
            }
            return result;
        }

        // Flatten all steps into a single sequence, with context tracking
        public class FlatStep
        {
            public LDrawStep step;
            public string modelName;
            public int stepIndexInModel;
            public string parentModel;
            public int? parentStepIndex;
        }

        public static List<FlatStep> FlattenSteps(Dictionary<string, List<LDrawStep>> models)
        {
            var flatSteps = new List<FlatStep>();
            var visited = new HashSet<string>();
            Debug.Log($"Flattening steps for {models.Count} models, {string.Join(", ", models.Keys)}");
            FlattenModelStepsRecursive(mainModelName, models, flatSteps, null, null, visited);
            return flatSteps;
        }

        private static void FlattenModelStepsRecursive(string modelName, Dictionary<string, List<LDrawStep>> models, List<FlatStep> flatSteps, string parentModel, int? parentStepIndex, HashSet<string> visited)
        {
            if (!models.ContainsKey(modelName) || visited.Contains(modelName))
                return;
            visited.Add(modelName);
            var steps = models[modelName];
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                // First, flatten submodels referenced in this step
                foreach (var part in step.parts)
                {
                    if (models.ContainsKey(part.partId))
                    {
                        FlattenModelStepsRecursive(part.partId, models, flatSteps, modelName, i, visited);
                    }
                }
                // Then, add the parent step
                flatSteps.Add(new FlatStep
                {
                    step = step,
                    modelName = modelName,
                    stepIndexInModel = i,
                    parentModel = parentModel,
                    parentStepIndex = parentStepIndex
                });
                Debug.Log($"Added flat step {flatSteps.Count - 1} for model {modelName} step {i}");
            }
            visited.Remove(modelName);
        }

        public static void SaveModelsToJsonAsset(Dictionary<string, List<LDrawStep>> models, string outputPath = "Assets/Resources/LDrawStepData.json")
        {
            var list = new List<LDraw.Runtime.ModelStepPair>();
            foreach (var kvp in models)
            {
                list.Add(new LDraw.Runtime.ModelStepPair { modelName = kvp.Key, steps = kvp.Value });
            }
            var wrapper = new LDraw.Runtime.LDrawModelStepData { models = list };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(outputPath, json);
            Debug.Log($"Saved model step data to {outputPath}");
            AssetDatabase.Refresh();
        }
    }
}
