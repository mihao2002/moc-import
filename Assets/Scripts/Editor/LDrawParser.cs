using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;
using Newtonsoft.Json;

namespace LDraw.Editor
{
    public static class LDrawParser
    {
        public static string mainModelName = "main.ldr";

        // New: Parse all models and their steps, without recursive expansion
        public static List<RuntimeModelData> ParseModels(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var models = new List<RuntimeModelData>();
            var modelNames = new HashSet<string>();
            string mainModelName = null;
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
                        modelNames.Add(currentModel);
                    }
                    currentModel = line.Trim().Substring(7).Trim().ToLower();
                    modelStart = i;
                }
            }

            // Add last section
            fileSections.Add((currentModel, modelStart, lines.Length));
            modelNames.Add(currentModel);

            foreach (var (name, start, end) in fileSections)
            {
                if (mainModelName == null) mainModelName = name;
                var modelData = new RuntimeModelData{
                    modelName = name,
                    steps = ParseStepsFromLines(lines, start, end, modelNames)};
                models.Add(modelData);
            }

            return models;
        }

        // Helper: Parse a range of lines into steps
        private static List<LDrawStep> ParseStepsFromLines(string[] lines, int start, int end, HashSet<string> modelNames)
        {
            // Quaternion defaultQuaternion = (Quaternion.AngleAxis(30f, Vector3.right) * 
            //     Quaternion.AngleAxis(45f, Vector3.up) * 
            //     Quaternion.LookRotation(Vector3.back, Vector3.down));
            // Quaternion defaultQuaternion = Quaternion.Euler(30f, 45f, 0f);
            // Vector3 defaultRotation = new Vector3(30f, 45f, 0f);

            var steps = new List<LDrawStep>();
            var currentStep = new LDrawStep();
            var hasModelInStep = false;
            var currentRotationRef = -1;
            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (line.StartsWith("0 "))
                {
                    var tokens = line.Trim().Split(' ');
                    if (tokens.Length > 1)
                    {
                        var directive = tokens[1].ToUpper();
                      
                        // Handle step boundaries (create new step after processing rotation)
                        if ((directive == "STEP" || directive == "ROTSTEP") && currentStep.parts.Count > 0)
                        {
                            // Handle ROTSTEP rotation data first (for current step)
                            if (directive == "ROTSTEP" && tokens.Length >= 6)
                            {                        
                                var type = tokens[5].ToUpper();
                                if (type == "END")
                                {
                                    currentRotationRef = -1;
                                    currentStep.rotRef = currentRotationRef; // Vector3.zero; // ROTSTEP END
                                }
                                else if (type == "ABS")
                                {
                                    currentStep.rotation = new Vector3(
                                        float.Parse(tokens[2]),
                                        float.Parse(tokens[3]),
                                        float.Parse(tokens[4]));
                                    currentRotationRef = steps.Count;
                                }
                                else
                                {
                                    Debug.LogWarning($"ROTSTEP with unsupported type: {type}. Line: {line}");
                                }
                            }
                            // For normal STEP, just refer to the last rotation step
                            else
                            {
                                currentStep.rotRef = currentRotationRef;
                            }

                            steps.Add(currentStep);
                            currentStep = new LDrawStep();
                            hasModelInStep = false;
                        }
                    }
                }
                else if (line.StartsWith("1 "))
                {
                    var tokens = Regex.Split(line.Trim(), " +");
                    if (tokens.Length >= 15)
                    {
                        var part = new LDrawPart();
                        part.partId = tokens[14].ToLower();
                        hasModelInStep |= modelNames.Contains(part.partId);
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
                        Matrix4x4 RL = Consts.NegateZ * mLDraw * Consts.NegateZ;
                        part.rotation = RL.rotation;
                        int colorCode = int.Parse(tokens[1]);
                        part.color = LDrawColorManager.GetColor(colorCode);
                        currentStep.parts.Add(part);
                    }
                }
            }

            if (currentStep.parts.Count > 0)
            {
                // Always has rotation for the last step
                currentStep.rotRef = currentRotationRef;
                steps.Add(currentStep);
            }
            else
            {
                var lastStep = steps.Count - 1;
                // Ensure the last step has rotation
                if (lastStep >= 0 && steps[lastStep].rotation == null)
                {
                    steps[lastStep].rotRef = currentRotationRef;
                }
            }
                
            return steps;
        }

        public static void SaveModelsToJsonAsset(List<RuntimeModelData> models, List<FlatStep> flatSteps, string outputPath = "Assets/Resources/LDrawStepData.json")
        {
            // var list = new List<LDraw.Runtime.ModelStepPair>();
            // foreach (var kvp in models)
            // {
            //     list.Add(new LDraw.Runtime.ModelStepPair { modelName = kvp.Key, steps = kvp.Value });
            // }
            // var wrapper = new LDraw.Runtime.LDrawModelStepData { models = list };
            //string json = JsonUtility.ToJson(wrapper, true);
            var data = new CombinedData{models = models, flatSteps= flatSteps};

            var settings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            settings.Converters.Add(new Vector3Converter());
            settings.Converters.Add(new QuaternionConverter());
            settings.Converters.Add(new ColorConverter());
            settings.Converters.Add(new NullableVector3Converter());       
                     

            string json = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
            File.WriteAllText(outputPath, json);
            Debug.Log($"Saved model step data to {outputPath}");
            AssetDatabase.Refresh();
        }
    }
}
