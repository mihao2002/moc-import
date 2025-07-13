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
        public static List<LDrawStep> Parse(string filePath)
        {
            var steps = new List<LDrawStep>();
            var currentStep = new LDrawStep();
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
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

        public static void SaveStepsToJsonAsset(List<LDrawStep> steps, string outputPath = "Assets/Resources/LDrawStepData.json")
        {
            var wrapper = new LDrawStepListWrapper { steps = steps };
            string json = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(outputPath, json);
            Debug.Log($"Saved step data to {outputPath}");
            AssetDatabase.Refresh();
        }
    }
}
