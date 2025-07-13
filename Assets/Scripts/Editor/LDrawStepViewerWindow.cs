using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;

namespace LDraw.Editor
{
    public class LDrawStepViewerWindow : EditorWindow
    {
        private string ldrawFilePath = "C:/Users/mihao/OneDrive/Documents/test.ldr";
        private string partLibraryPath = "C:/Users/Public/Documents/LDraw";
        private string unofficialPartLibraryPath = "C:/Users/Public/Documents/LDraw/Unofficial";
        private List<LDrawStep> steps = new List<LDrawStep>();
        private int currentStep = 0;
        private List<GameObject> spawnedParts = new List<GameObject>();

        [MenuItem("Tools/LDraw Step Viewer")]
        public static void ShowWindow()
        {
            GetWindow<LDrawStepViewerWindow>("LDraw Step Viewer");
        }

        void OnGUI()
        {
            GUILayout.Label("LDraw Step Viewer", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            ldrawFilePath = EditorGUILayout.TextField("LDraw File", ldrawFilePath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select LDraw File", "", "ldr,mpd,dat");
                if (!string.IsNullOrEmpty(path))
                    ldrawFilePath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            partLibraryPath = EditorGUILayout.TextField("Part Library Path", partLibraryPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select LDraw Part Library Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                    partLibraryPath = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            unofficialPartLibraryPath = EditorGUILayout.TextField("Unofficial Part Library Path", unofficialPartLibraryPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select LDraw Unofficial Part Library Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                    unofficialPartLibraryPath = path;
            }
            EditorGUILayout.EndHorizontal();        

            if (GUILayout.Button("Load LDraw File"))
            {
                LoadLDrawFile();
            }

            if (steps.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Step {currentStep + 1} / {steps.Count}");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Previous Step"))
                {
                    ShowStep(currentStep - 1);
                }
                if (GUILayout.Button("Next Step"))
                {
                    ShowStep(currentStep + 1);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void LoadLDrawFile()
        {
            if (!File.Exists(ldrawFilePath))
            {
                EditorUtility.DisplayDialog("Error", "LDraw file not found!", "OK");
                return;
            }

            string ldconfigPath = Path.Combine(partLibraryPath, "LDConfig.ldr");
            LDrawColorManager.LoadFromFile(ldconfigPath);

            LDrawPartLoader.ClearCache();

            steps = LDrawParser.Parse(ldrawFilePath);
            // LDrawPart p = new LDrawPart{
            //     partId = "confric.dat",
            //     position = Vector3.zero,
            //     rotation = Quaternion.identity};
            // steps = new List<LDrawStep>();
            // List<LDrawPart> parts = new List<LDrawPart>();
            // LDrawStep step = new LDrawStep();
            // step.parts.Add(p);
            // steps.Add(step);

            LDrawParser.SaveStepsToJsonAsset(steps);

            currentStep = 0;
            ShowStep(currentStep);
        }

        void ShowStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
                return;

            currentStep = stepIndex;
            ClearParts();

            for (int i = 0; i <= currentStep; i++)
            {
                foreach (var part in steps[i].parts)
                {
                    GameObject go = LDrawPartLoader.SpawnPart(part, partLibraryPath, unofficialPartLibraryPath);
                    // Set color dynamically here for editor visualization
                    var meshRenderer = go.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.sharedMaterial.color = part.color;
                    }
                    spawnedParts.Add(go);
                }
            }
            SceneView.RepaintAll();
        }

        void ClearParts()
        {
            foreach (var go in spawnedParts)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            spawnedParts.Clear();
        }

        void OnDisable()
        {
            ClearParts();
        }
    }
}
