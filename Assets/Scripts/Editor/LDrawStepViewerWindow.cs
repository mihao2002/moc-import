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

        private List<List<GameObject>> stepObjects = new List<List<GameObject>>(); // Add this field

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

            LDrawParser.SaveStepsToJsonAsset(steps);

            currentStep = 0;

            // Clear previous objects
            ClearParts();
            stepObjects.Clear();

            // Preload meshes and create prefabs for all steps
            for (int i = 0; i < steps.Count; i++)
            {
                var objs = new List<GameObject>();
                foreach (var part in steps[i].parts)
                {
                    GameObject go = LDrawPartLoader.SpawnPart(part, partLibraryPath, unofficialPartLibraryPath);
                    var meshRenderer = go.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        meshRenderer.sharedMaterial.color = part.color;
                    }
                    go.SetActive(false); // Hide initially
                    objs.Add(go);
                }
                stepObjects.Add(objs);
            }

            ShowStep(currentStep);
        }

        void ShowStep(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= steps.Count)
                return;

            // Hide all objects
            foreach (var objs in stepObjects)
            {
                foreach (var go in objs)
                {
                    if (go != null)
                        go.SetActive(false);
                }
            }

            currentStep = stepIndex;

            // Show all objects up to current step (cumulative build)
            for (int i = 0; i <= currentStep; i++)
            {
                foreach (var go in stepObjects[i])
                {
                    if (go != null)
                        go.SetActive(true);
                }
            }

            SceneView.RepaintAll();
        }

        void ClearParts()
        {
            foreach (var objs in stepObjects)
            {
                foreach (var go in objs)
                {
                    if (go != null)
                        UnityEngine.Object.DestroyImmediate(go);
                }
            }
            stepObjects.Clear();
        }

        void OnDisable()
        {
            ClearParts();
        }
    }
}
