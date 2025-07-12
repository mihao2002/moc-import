using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class LDrawStepViewerWindow : EditorWindow
{
    private string ldrawFilePath = "C:/Users/mihao/OneDrive/Documents/test.ldr";
    private string partLibraryPath = "C:/Users/Public/Documents/LDraw";
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
                GameObject go = LDrawPartLoader.SpawnPart(part, partLibraryPath);
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

public class LDrawStep
{
    public List<LDrawPart> parts = new List<LDrawPart>();
}

public class LDrawPart
{
    public string partId;
    public Vector3 position;
    public Quaternion rotation;
    public Color color;
}

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
                    float x = float.Parse(tokens[2]);
                    float y = float.Parse(tokens[3]);
                    float z = float.Parse(tokens[4]);
                    part.position = new Vector3(x, y, z) * 0.01f;
                    Matrix4x4 m = new Matrix4x4();
                    m.SetColumn(0, new Vector4(float.Parse(tokens[5]), float.Parse(tokens[8]), float.Parse(tokens[11]), 0));
                    m.SetColumn(1, new Vector4(float.Parse(tokens[6]), float.Parse(tokens[9]), float.Parse(tokens[12]), 0));
                    m.SetColumn(2, new Vector4(float.Parse(tokens[7]), float.Parse(tokens[10]), float.Parse(tokens[13]), 0));
                    m.SetColumn(3, new Vector4(0, 0, 0, 1));
                    part.rotation = m.rotation;
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
}
