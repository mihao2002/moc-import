using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;
using UnityEngine.Rendering;
using Newtonsoft.Json;

namespace LDraw.Editor
{
    public class LDrawMesh
    {
        public GameObject go;
        public bool isCW;
    }

    public class LDrawPartLoader
    {
        private Dictionary<int, LDrawColor> colors;
        private Dictionary<string, (LDrawMesh, string, string)> partCache = new Dictionary<string, (LDrawMesh, string, string)>();
        private Dictionary<string, GameObject> submodelCache = new Dictionary<string, GameObject>();
        private Dictionary<string, List<LDrawStep>> models = new Dictionary<string, List<LDrawStep>>();
        private HashSet<int> usedColors;
        private Dictionary<string, LDrawPartDesc> partDescriptions;
        private Material mainMaterial;
        private int mainColorIndex = 16;

        public string mainModelName = "main.ldr";
        // public Dictionary<string, (string, string)> partModels = new Dictionary<string, (string, string)>();


        public LDrawPartLoader(Dictionary<int, LDrawColor> colors)
        {
            this.colors = colors;
            usedColors = new HashSet<int>();
            partDescriptions = new Dictionary<string, LDrawPartDesc>();
            mainMaterial = GetOrCreateMaterial(mainColorIndex);
        }

        public Dictionary<int, LDrawColor> GetUsedColors()
        {
            var usedColorDict = new Dictionary<int, LDrawColor>();
            foreach (var kvp in colors)
            {
                if (usedColors.Contains(kvp.Key))
                {
                    usedColorDict[kvp.Key] = kvp.Value;
                }
            }

            return usedColorDict;
        }

        public bool isPartModel(string partId)
        {
            return partDescriptions.ContainsKey(partId) && partDescriptions[partId].id != null;
        }

        public Dictionary<string, LDrawPartDesc> GetPartDescriptions()
        {
            return partDescriptions;
        }

        public Mesh SaveMeshAsset(Mesh mesh, string meshName)
        {
            string meshFolder = "Assets/Resources/LDrawMeshes";
            if (!Directory.Exists(meshFolder))
            {
                Directory.CreateDirectory(meshFolder);
                AssetDatabase.Refresh();
            }

            var fileName = meshName.Replace('\\', '_');
            string meshAssetPath = Path.Combine(meshFolder, fileName + ".asset");

            // Check if mesh asset already exists to avoid overwriting
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (existingMesh != null)
            {
                return existingMesh; // reuse existing asset
            }

            // Create new mesh asset
            Mesh newMesh = UnityEngine.Object.Instantiate(mesh);
            newMesh.name = meshName;
            AssetDatabase.CreateAsset(newMesh, meshAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return newMesh;
        }

        public Material GetOrCreateMaterial(int colorIdx)
        {
            string materialFolder = "Assets/Resources/LDrawMaterials";
            if (!Directory.Exists(materialFolder))
                Directory.CreateDirectory(materialFolder);

            var color = colors[colorIdx].color;
            usedColors.Add(colorIdx);

            string colorKey = $"{color.r:F3}_{color.g:F3}_{color.b:F3}";
            string matPath = Path.Combine(materialFolder, $"Mat_{colorKey}.mat");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            // Use URP Lit shader instead of Standard
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found. Make sure URP is installed and set up.");
                shader = Shader.Find("Standard"); // fallback
            }

            var mat = new Material(shader);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public GameObject GetGameObject(string partId, int color)
        {
            GameObject go; // = new GameObject(partId);
            bool setColor = false;
            if (partCache.ContainsKey(partId))
            {
                go = GameObject.Instantiate(partCache[partId].Item1.go);
                setColor = true;
            }
            else if (submodelCache.ContainsKey(partId))
            {
                go = GameObject.Instantiate(submodelCache[partId]);
                setColor = isPartModel(partId);
            }
            else
            {
                Debug.LogError($"Can't find part {partId}.");
                return null;
            }

            if (setColor)
            {
                var material = GetOrCreateMaterial(color);

                Renderer rend = go.GetComponent<Renderer>();
                Material[] sharedMats = rend.sharedMaterials;
                for (var i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] == mainMaterial)
                    {
                        sharedMats[i] = material;
                        rend.sharedMaterials = sharedMats;
                        break;
                    }
                }
            }

            return go;
        }

        // New: Parse all models and their steps, without recursive expansion
        public (List<RuntimeModelData>, Dictionary<string, string[]>) ParseModels(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var models = new List<RuntimeModelData>();
            var geometryModels = new Dictionary<string, string[]>();
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
                (List<LDrawStep> steps, Dictionary<int, LDrawBuildMod> buildMods, string alias, string description) = ParseStepsFromLines(lines, start, end, modelNames);
                if (alias != null)
                {
                    partDescriptions[name] = new LDrawPartDesc { id = alias, description = description };
                }

                if (steps.Count > 0)
                {
                    var modelData = new RuntimeModelData
                    {
                        modelName = name,
                        steps = steps,
                        buildMods = buildMods
                    };
                    models.Add(modelData);
                }
                else
                {
                    var modelLines = new List<string>();
                    for (var i = start; i < end; i++)
                    {
                        modelLines.Add(lines[i]);
                    }
                    geometryModels[name] = modelLines.ToArray();
                }
            }

            return (models, geometryModels);
        }

        // Helper: Parse a range of lines into steps
        private (List<LDrawStep>, Dictionary<int, LDrawBuildMod>, string, string) ParseStepsFromLines(string[] lines, int start, int end, HashSet<string> modelNames)
        {
            var steps = new List<LDrawStep>();
            var currentStep = new LDrawStep();
            var hasModelInStep = false;
            var currentRotationRef = -1;
            string modName = null;
            int modStart = 0;
            string alias = null;
            string description = null;

            Dictionary<string, LDrawBuildMod> modInfo = new Dictionary<string, LDrawBuildMod>();
            Dictionary<int, LDrawBuildMod> buildMods = new Dictionary<int, LDrawBuildMod>();

            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (line.StartsWith("3 ") || line.StartsWith("4 "))
                {
                    // This is a geometry part
                    return (steps, buildMods, alias, description);
                }
            }

            for (int i = start; i < end; i++)
            {
                var line = lines[i];
                if (line.StartsWith("0 "))
                {
                    if (line.StartsWith("0 COMMENT"))
                    {
                        var comment = line.Substring(10);
                        var parts = comment.Split(':');
                        if (parts.Length == 2)
                        {
                            alias = parts[0];
                            description = parts[1];
                        }
                    }

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
                        else if (directive == "!LPUB" && tokens.Length > 3 && tokens[2].ToUpper() == "BUILD_MOD")
                        {
                            var type = tokens[3].ToUpper();
                            if (type == "BEGIN")
                            {
                                if (tokens.Length > 4)
                                {
                                    modName = tokens[4];
                                    modStart = currentStep.parts.Count;
                                }
                            }
                            else if (type == "END_MOD")
                            {
                                if (modName != null)
                                {
                                    var mod = new LDrawBuildMod { step = steps.Count, start = modStart, end = currentStep.parts.Count - 1 };
                                    modInfo[modName] = mod;
                                    modName = null;
                                }
                            }
                            else if (type == "REMOVE")
                            {
                                if (tokens.Length > 4)
                                {
                                    var name = tokens[4];
                                    if (modInfo.ContainsKey(name))
                                    {
                                        buildMods[steps.Count] = modInfo[name];
                                    }
                                }
                            }
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
                        // int colorCode = int.Parse(tokens[1]);
                        // part.color = LDrawColorManager.GetColor(colorCode);
                        part.color = int.Parse(tokens[1]);
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

            return (steps, buildMods, alias, description);
        }

        public void SaveModelsToJsonAsset(List<RuntimeModelData> models, List<FlatStep> flatSteps,
            Dictionary<int, LDrawColor> colors,
            Dictionary<string, LDrawPartDesc> partDescriptions,
            List<LDrawPartCount> partCounts)
        {
            var data = new StepPackage { colors = colors, models = models, flatSteps = flatSteps };

            var settings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            settings.Converters.Add(new Vector3Converter());
            settings.Converters.Add(new QuaternionConverter());
            settings.Converters.Add(new ColorConverter());
            settings.Converters.Add(new NullableVector3Converter());


            string json = JsonConvert.SerializeObject(data, Formatting.Indented, settings);
            var stepDataPath = "Assets/Resources/LDrawStepData.json";
            File.WriteAllText(stepDataPath, json);
            Debug.Log($"Saved model step data to {stepDataPath}");
            var partDataCountPath = "Assets/Resources/LDrawPartCountData.json";
            string json2 = JsonConvert.SerializeObject(partCounts, Formatting.Indented, settings);
            File.WriteAllText(partDataCountPath, json2);
            Debug.Log($"Saved model part data to {partDataCountPath}");
            var partDescriptionPath = "Assets/Resources/LDrawPartDescriptionData.json";
            string json3 = JsonConvert.SerializeObject(partDescriptions, Formatting.Indented, settings);
            File.WriteAllText(partDescriptionPath, json3);
            Debug.Log($"Saved model part data to {partDescriptionPath}");
            AssetDatabase.Refresh();
        }

        private string FindPartFile(string partId, string partLibraryPath, string[] unofficialPartLibraryPaths)
        {
            string path = FindPartFileInPath(partId, partLibraryPath);
            int i = 0;
            while (path == null && i < unofficialPartLibraryPaths.Length)
            {
                path = FindPartFileInPath(partId, unofficialPartLibraryPaths[i]);
                i++;
            }

            return path;
        }

        private string FindPartFileInPath(string partId, string partLibraryPath)
        {
            // LDraw folder structure:
            // - parts/ (main parts folder)
            // - p/ (subparts folder, at same level as parts/)
            // - parts/s/ (studs folder inside parts/)

            // First check in the main parts folder
            string partsPath = Path.Combine(partLibraryPath, "parts", partId);
            if (File.Exists(partsPath))
            {
                return partsPath;
            }

            // Check in the p/ folder (subparts, at same level as parts/)
            string pPath = Path.Combine(partLibraryPath, "p", partId);
            if (File.Exists(pPath))
            {
                return pPath;
            }

            // Check in parts/s/ folder (studs inside parts/)
            string sPath = Path.Combine(partLibraryPath, "parts", "s", partId);
            if (File.Exists(sPath))
            {
                return sPath;
            }

            // Check other common subfolders inside parts/
            string[] subfolders = { "48", "8", "studs" };
            foreach (string subfolder in subfolders)
            {
                string fullPath = Path.Combine(partLibraryPath, "parts", subfolder, partId);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private Mesh CombineMeshesPreserveSubmeshes(List<CombineInstance> combineInstances)
        {
            List<Vector3> allVertices = new List<Vector3>();
            List<Vector3> allNormals = new List<Vector3>();
            List<Vector2> allUVs = new List<Vector2>();
            List<int> allTriangles = new List<int>();

            int vertexOffset = 0;

            foreach (var ci in combineInstances)
            {
                Mesh mesh = ci.mesh;
                Matrix4x4 transform = ci.transform;

                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var uvs = mesh.uv;

                // Transform and append vertices and normals
                for (int i = 0; i < vertices.Length; i++)
                {
                    allVertices.Add(transform.MultiplyPoint3x4(vertices[i]));
                    if (i < normals.Length)
                        allNormals.Add(transform.MultiplyVector(normals[i]));
                }

                // Append UVs (if available)
                if (uvs != null && uvs.Length == vertices.Length)
                {
                    allUVs.AddRange(uvs);
                }

                // Collect triangles from all submeshes and offset indices
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] tris = mesh.GetTriangles(sub);
                    for (int i = 0; i < tris.Length; i++)
                    {
                        allTriangles.Add(tris[i] + vertexOffset);
                    }
                }

                vertexOffset += vertices.Length;
            }

            // Build the combined mesh
            Mesh combined = new Mesh();
            combined.name = "CombinedMesh";

            // Set index format to UInt32 to support more than 65,535 vertices
            combined.indexFormat = IndexFormat.UInt32;

            combined.SetVertices(allVertices);

            if (allNormals.Count == allVertices.Count)
                combined.SetNormals(allNormals);

            if (allUVs.Count == allVertices.Count)
                combined.SetUVs(0, allUVs);

            combined.subMeshCount = 1;
            combined.SetTriangles(allTriangles, 0);

            combined.RecalculateBounds();
            return combined;
        }

        private (LDrawMesh, string, string) ParsePartFileLines(string partId, string[] lines, string partLibraryPath, string[] unofficialPartLibraryPaths)
        {
            var colorInstances = new Dictionary<Material, (List<Vector3>, List<int>)>();
            var hasContent = false;

            bool invertNext = false;
            bool isCW = false;

            string alias = null;
            string description = null;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (description == null && trimmed.StartsWith("0 "))
                {
                    description = trimmed.Substring(2);
                }

                if (trimmed.StartsWith("0 COMMENT"))
                {
                    var comment = trimmed.Substring(10);
                    var parts = comment.Split(':');
                    if (parts.Length == 2)
                    {
                        alias = parts[0];
                        description = parts[1];
                    }
                }

                if (trimmed.StartsWith("0 BFC CERTIFY CW", StringComparison.OrdinalIgnoreCase))
                {
                    isCW = true;
                    continue;
                }
                if (trimmed.StartsWith("0 BFC CERTIFY CCW", StringComparison.OrdinalIgnoreCase))
                {
                    isCW = false;
                    continue;
                }
                if (trimmed.StartsWith("0 BFC INVERTNEXT", StringComparison.OrdinalIgnoreCase))
                {
                    invertNext = true;
                    continue;
                }

                string[] tokens = trimmed.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string type = tokens[0];

                try
                {
                    switch (type)
                    {
                        case "0":
                            break;

                        case "1":
                        case "3":
                        case "4":
                            var color = int.Parse(tokens[1]);
                            var mat = GetOrCreateMaterial(color);

                            List<Vector3> allVertices = null;
                            List<int> allTriangles = null;

                            if (colorInstances.ContainsKey(mat))
                            {
                                (allVertices, allTriangles) = colorInstances[mat];
                            }
                            else
                            {
                                allVertices = new List<Vector3>();
                                allTriangles = new List<int>();
                                colorInstances[mat] = (allVertices, allTriangles);
                            }

                            if (type == "1")
                            {
                                if (tokens.Length >= 15)
                                {
                                    string referencedPartId = tokens[14].ToLower();

                                    float tx = float.Parse(tokens[2]) * 0.01f;
                                    float ty = float.Parse(tokens[3]) * 0.01f;
                                    float tz = float.Parse(tokens[4]) * 0.01f;

                                    Matrix4x4 transform = new Matrix4x4();
                                    transform.SetColumn(0, new Vector4(float.Parse(tokens[5]), float.Parse(tokens[8]), float.Parse(tokens[11]), 0));
                                    transform.SetColumn(1, new Vector4(float.Parse(tokens[6]), float.Parse(tokens[9]), float.Parse(tokens[12]), 0));
                                    transform.SetColumn(2, new Vector4(float.Parse(tokens[7]), float.Parse(tokens[10]), float.Parse(tokens[13]), 0));
                                    transform.SetColumn(3, new Vector4(tx, ty, tz, 1));

                                    bool isMirrored = MatrixIsMirrored(transform);
                                    transform = Consts.NegateZ * transform * Consts.NegateZ;

                                    (LDrawMesh ldrawMesh, _, _) = LoadPartMesh(referencedPartId, partLibraryPath, unofficialPartLibraryPaths, null);
                                    if (ldrawMesh != null)
                                    {
                                        hasContent = true;

                                        //bool invertFace = invertNext ^ (isCW != ldrawMesh.isCW);
                                        //bool mirrorXform = MatrixIsMirrored(transform);

                                        // Apply coordinate system swap
                                        // Check for mirroring (OPTIONAL)
                                        //bool isMirrored = MatrixIsMirrored(transform);

                                        // Final winding decision
                                        bool invertFace = invertNext ^ isMirrored;

                                        var gameObject = ldrawMesh.go;
                                        var subRenderer = gameObject.GetComponent<MeshRenderer>();
                                        var meshFilter = gameObject.GetComponent<MeshFilter>();
                                        var meshCopy = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);

                                        for (int subIdx = 0; subIdx < meshCopy.subMeshCount; subIdx++)
                                        {
                                            Material material = subRenderer.sharedMaterials[subIdx];
                                            if (material == mainMaterial)
                                            {
                                                material = mat;
                                            }

                                            List<Vector3> vertices = null;
                                            List<int> triangles = null;

                                            if (colorInstances.ContainsKey(material))
                                            {
                                                (vertices, triangles) = colorInstances[material];
                                            }
                                            else
                                            {
                                                vertices = new List<Vector3>();
                                                triangles = new List<int>();
                                                colorInstances[material] = (vertices, triangles);
                                            }

                                            Mesh submesh = ExtractSubMesh(meshCopy, subIdx);

                                            int vertexOffset = vertices.Count;
                                            foreach (var v in submesh.vertices)
                                            {
                                                Vector3 v2 = transform.MultiplyPoint3x4(v);
                                                vertices.Add(v2);
                                            }

                                            //Apply correct winding if mirrored
                                            if (invertFace)
                                            {
                                                for (int i = 0; i < submesh.triangles.Length; i += 3)
                                                {
                                                    triangles.Add(vertexOffset + submesh.triangles[i]);
                                                    triangles.Add(vertexOffset + submesh.triangles[i + 2]);
                                                    triangles.Add(vertexOffset + submesh.triangles[i + 1]);
                                                    //Debug.Log($"Triangle : {allVertices[vertexOffset + referencedMesh.triangles[i]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 2]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 1]]} (invertFace={invertFace})");
                                                }
                                            }
                                            else
                                            {
                                                for (int i = 0; i < submesh.triangles.Length; i += 3)
                                                {
                                                    triangles.Add(vertexOffset + submesh.triangles[i]);
                                                    triangles.Add(vertexOffset + submesh.triangles[i + 1]);
                                                    triangles.Add(vertexOffset + submesh.triangles[i + 2]);
                                                    //Debug.Log($"Triangle : {allVertices[vertexOffset + referencedMesh.triangles[i]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 1]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 2]]} (invertFace={invertFace})");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"Missing subpart: {referencedPartId}");
                                    }
                                }
                                invertNext = false;
                                break;
                            }
                            else if (type == "3")
                            {
                                if (tokens.Length >= 11)
                                {
                                    hasContent = true;
                                    Vector3 v1 = new Vector3(float.Parse(tokens[2]), float.Parse(tokens[3]), float.Parse(tokens[4])) * 0.01f;
                                    Vector3 v2 = new Vector3(float.Parse(tokens[5]), float.Parse(tokens[6]), float.Parse(tokens[7])) * 0.01f;
                                    Vector3 v3 = new Vector3(float.Parse(tokens[8]), float.Parse(tokens[9]), float.Parse(tokens[10])) * 0.01f;

                                    int baseIdx = allVertices.Count;
                                    allVertices.Add(NegateZ(v1));
                                    allVertices.Add(NegateZ(v2));
                                    allVertices.Add(NegateZ(v3));

                                    bool invertFace = invertNext ^ !isCW;
                                    if (invertFace)
                                    {
                                        allTriangles.Add(baseIdx);
                                        allTriangles.Add(baseIdx + 2);
                                        allTriangles.Add(baseIdx + 1);
                                    }
                                    else
                                    {
                                        allTriangles.Add(baseIdx);
                                        allTriangles.Add(baseIdx + 1);
                                        allTriangles.Add(baseIdx + 2);
                                    }
                                }
                                invertNext = false;
                            }
                            else
                            {
                                if (tokens.Length >= 14)
                                {
                                    hasContent = true;
                                    Vector3 v1 = new Vector3(float.Parse(tokens[2]), float.Parse(tokens[3]), float.Parse(tokens[4])) * 0.01f;
                                    Vector3 v2 = new Vector3(float.Parse(tokens[5]), float.Parse(tokens[6]), float.Parse(tokens[7])) * 0.01f;
                                    Vector3 v3 = new Vector3(float.Parse(tokens[8]), float.Parse(tokens[9]), float.Parse(tokens[10])) * 0.01f;
                                    Vector3 v4 = new Vector3(float.Parse(tokens[11]), float.Parse(tokens[12]), float.Parse(tokens[13])) * 0.01f;

                                    int baseIdx = allVertices.Count;
                                    allVertices.Add(NegateZ(v1));
                                    allVertices.Add(NegateZ(v2));
                                    allVertices.Add(NegateZ(v3));
                                    allVertices.Add(NegateZ(v4));

                                    bool invertFace = invertNext ^ !isCW;
                                    if (invertFace)
                                    {
                                        allTriangles.Add(baseIdx);
                                        allTriangles.Add(baseIdx + 2);
                                        allTriangles.Add(baseIdx + 1);
                                        allTriangles.Add(baseIdx);
                                        allTriangles.Add(baseIdx + 3);
                                        allTriangles.Add(baseIdx + 2);
                                    }
                                    else
                                    {
                                        allTriangles.Add(baseIdx);
                                        allTriangles.Add(baseIdx + 1);
                                        allTriangles.Add(baseIdx + 2);
                                        allTriangles.Add(baseIdx);
                                        allTriangles.Add(baseIdx + 2);
                                        allTriangles.Add(baseIdx + 3);
                                    }
                                }
                                invertNext = false;
                                break;
                            }
                            break;

                        case "2":
                        case "5":
                            invertNext = false;
                            break;

                        default:
                            invertNext = false;
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error parsing line: {line} → {e.Message}");
                    invertNext = false;
                }
            }

            if (!hasContent)
            {
                return (null, null, null);
            }

            var go = CreateMultiMeshObject(partId, colorInstances);
            go.SetActive(false);
            return (new LDrawMesh { go = go, isCW = isCW }, alias, description);
        }

        private GameObject CreateMultiMeshObject(string partId, Dictionary<Material, (List<Vector3>, List<int>)> coloredContent)
        {
            // Combine meshes by color/material group into submeshes
            var subMeshList = new List<CombineInstance>();
            var materialList = new List<Material>();

            foreach (var kvp in coloredContent)
            {
                var material = kvp.Key;

                var mesh = new Mesh();

                // Assign vertices and triangles to mesh
                mesh.vertices = kvp.Value.Item1.ToArray();
                mesh.triangles = kvp.Value.Item2.ToArray();

                // Optional: recalculate normals and bounds
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                subMeshList.Add(new CombineInstance
                {
                    mesh = mesh,
                    transform = Matrix4x4.identity
                });

                materialList.Add(material);
            }

            Mesh finalMesh = new Mesh();

            // Set index format to UInt32 to support more than 65,535 vertices
            finalMesh.indexFormat = IndexFormat.UInt32;
            finalMesh.CombineMeshes(subMeshList.ToArray(), false, false); // keep submeshes separate

            if (finalMesh.subMeshCount != materialList.Count)
            {
                Debug.LogError($"Mismatch in submesh count: {finalMesh.subMeshCount} vs materials: {materialList.Count} for for submodel {partId}.");
                return null;
            }

            // Save prefab with all submeshes and materials
            GameObject go = new GameObject(partId);

            // Mesh meshAsset = SaveMeshAsset(finalMesh, partId);
            go.AddComponent<MeshFilter>().sharedMesh = finalMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materialList.ToArray();

            return go;
        }

        private (LDrawMesh, string, string) LoadPartMesh(string partId, string partLibraryPath, string[] unofficialPartLibraryPaths, string[] lines)
        {
            if (partCache.ContainsKey(partId))
            {
                return partCache[partId];
            }

            if (lines == null)
            {
                string datPath = FindPartFile(partId, partLibraryPath, unofficialPartLibraryPaths);
                if (datPath == null)
                {
                    Debug.LogError($"Part file not found: {partId}");
                    return (null, null, null);
                }

                lines = File.ReadAllLines(datPath);
            }

            try
            {
                (LDrawMesh ldrawMesh, string alias, string description) = ParsePartFileLines(partId, lines, partLibraryPath, unofficialPartLibraryPaths);

                if (ldrawMesh == null)
                {
                    Debug.LogWarning($"No geometry parsed from: {partId}");
                    return (null, null, null);
                }

                var mat = ldrawMesh.go.GetComponent<Renderer>().sharedMaterials[0];

                partCache[partId] = (ldrawMesh, alias, description);

                return (ldrawMesh, alias, description);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse mesh for {partId}: {e.Message}");
                return (null, null, null);
            }
        }

        public LDrawMesh LoadPartFromLibrary(string partId, string partLibraryPath, string[] unofficialPartLibraryPaths, string[] lines = null)
        {
            (LDrawMesh ldrawMesh, string alias, string description) = LoadPartMesh(partId, partLibraryPath, unofficialPartLibraryPaths, lines);
            if (description != null)
            {
                partDescriptions[partId] = new LDrawPartDesc { id = alias, description = description };
            }
            if (ldrawMesh != null)
            {
                var go = ldrawMesh.go;
                go.SetActive(true);
                MeshFilter mf = go.GetComponent<MeshFilter>();
                Mesh meshAsset = SaveMeshAsset(mf.sharedMesh, partId);
                mf.sharedMesh = meshAsset;

                string prefabFolder = "Assets/Resources/LDrawPrefabs";
                if (!Directory.Exists(prefabFolder))
                    Directory.CreateDirectory(prefabFolder);

                var fileName = partId.Replace('\\', '_');
                string prefabPath = Path.Combine(prefabFolder, $"{fileName}.prefab");
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

                go.SetActive(false);
            }
            else
            {
                Debug.LogError($"Failed to parse mesh for {partId} - returned null");
            }
            return ldrawMesh;
        }

        private Mesh ExtractSubMesh(Mesh sourceMesh, int subMeshIndex)
        {
            int[] triangles = sourceMesh.GetTriangles(subMeshIndex);
            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;
            Vector2[] sourceUVs = sourceMesh.uv;

            // Find all used vertex indices
            HashSet<int> usedIndices = new HashSet<int>(triangles);

            // Map old vertex index → new vertex index
            Dictionary<int, int> indexMap = new Dictionary<int, int>();
            List<Vector3> newVertices = new List<Vector3>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();

            foreach (int i in usedIndices)
            {
                int newIndex = newVertices.Count;
                indexMap[i] = newIndex;
                newVertices.Add(sourceVertices[i]);
                if (sourceNormals.Length > i) newNormals.Add(sourceNormals[i]);
                if (sourceUVs.Length > i) newUVs.Add(sourceUVs[i]);
            }

            // Remap triangle indices
            int[] newTriangles = new int[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                newTriangles[i] = indexMap[triangles[i]];
            }

            // Create new mesh
            Mesh subMesh = new Mesh();
            subMesh.indexFormat = IndexFormat.UInt32;
            subMesh.vertices = newVertices.ToArray();
            if (newNormals.Count == newVertices.Count) subMesh.normals = newNormals.ToArray();
            if (newUVs.Count == newVertices.Count) subMesh.uv = newUVs.ToArray();
            subMesh.triangles = newTriangles;
            subMesh.RecalculateBounds();
            //subMesh.RecalculateNormals(); // Optional: recalculate if you don't trust imported normals

            return subMesh;
        }

        // Overload: allow passing in-memory models for submodel mesh generation, and indicate if this is a top-level part
        public GameObject LoadSubmodelFromLibrary(string partId, List<LDrawStep> steps)
        {
            if (submodelCache.ContainsKey(partId))
            {
                return submodelCache[partId];
            }

            // Group meshes by color
            var colorToInstances = new Dictionary<Material, List<CombineInstance>>(); // Changed to object to handle both Color and Material

            foreach (var step in steps)
            {
                foreach (var part in step.parts)
                {
                    bool setColor = false;
                    Mesh meshCopy = null;
                    GameObject gameObject = null;
                    // decide if this is a part of submodel
                    if (partCache.ContainsKey(part.partId))
                    {
                        setColor = true;

                        gameObject = GameObject.Instantiate(partCache[part.partId].Item1.go);
                        var meshFilter = gameObject.GetComponent<MeshFilter>();
                        if (meshFilter == null)
                        {
                            Debug.LogError($"Can't find mesh filter for {part.partId} for submodel {partId}.");
                            return null;
                        }

                        meshCopy = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
                    }
                    else if (submodelCache.ContainsKey(part.partId))
                    {
                        setColor = isPartModel(part.partId);
                        gameObject = submodelCache[part.partId];
                        var meshFilter = gameObject.GetComponent<MeshFilter>();
                        if (meshFilter == null)
                        {
                            Debug.LogError($"Can't find mesh filter for {part.partId} for submodel {partId}.");
                            return null;
                        }

                        meshCopy = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
                    }
                    else
                    {
                        Debug.LogError($"Can't find {part.partId} for submodel {partId}.");
                        return null;
                    }

                    // Transform mesh to part's position/rotation
                    Vector3[] verts = meshCopy.vertices;
                    Vector3[] norms = meshCopy.normals;

                    for (int i = 0; i < verts.Length; i++)
                    {
                        verts[i] = part.rotation * verts[i] + part.position;  // Transform vertex position
                        norms[i] = part.rotation * norms[i];                   // Rotate normal direction only
                    }

                    meshCopy.vertices = verts;
                    meshCopy.normals = norms;
                    meshCopy.RecalculateBounds();

                    if (setColor)
                    {
                        // Assign the main color
                        var ci = new CombineInstance
                        {
                            mesh = meshCopy,
                            transform = Matrix4x4.identity
                        };

                        var material = GetOrCreateMaterial(part.color);

                        var renderer = gameObject.GetComponent<Renderer>();
                        // Get a copy of shared materials array
                        Material[] mats = renderer.sharedMaterials;

                        bool replaced = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == mainMaterial)
                            {
                                mats[i] = material;
                                replaced = true;
                            }
                        }

                        // If replaced at least one, assign the modified array back
                        if (replaced)
                        {
                            renderer.sharedMaterials = mats;
                        }
                    }

                    var subRenderer = gameObject.GetComponent<MeshRenderer>();

                    for (int subIdx = 0; subIdx < meshCopy.subMeshCount; subIdx++)
                    {
                        Mesh submesh = ExtractSubMesh(meshCopy, subIdx);
                        var ci = new CombineInstance
                        {
                            mesh = submesh,
                            transform = Matrix4x4.identity
                        };

                        Material mat = subRenderer.sharedMaterials[subIdx];
                        if (!colorToInstances.ContainsKey(mat))
                            colorToInstances[mat] = new List<CombineInstance>();
                        colorToInstances[mat].Add(ci);
                    }
                }
            }

            // Combine meshes by color/material group into submeshes
            var subMeshList = new List<CombineInstance>();
            var materialList = new List<Material>();

            foreach (var kvp in colorToInstances)
            {
                var material = kvp.Key;
                // Can use groupMesh.CombineMeshes, because it only take the first mesh for some reason
                // var groupMesh = CombineMeshesPreserveSubmeshes(kvp.Value);
                Mesh groupMesh = new Mesh();
                groupMesh.indexFormat = IndexFormat.UInt32;
                groupMesh.CombineMeshes(kvp.Value.ToArray(), true, false); // keep submeshes separate

                subMeshList.Add(new CombineInstance
                {
                    mesh = groupMesh,
                    transform = Matrix4x4.identity
                });

                materialList.Add(material);
            }

            Mesh finalMesh = new Mesh();

            // Set index format to UInt32 to support more than 65,535 vertices
            finalMesh.indexFormat = IndexFormat.UInt32;
            finalMesh.CombineMeshes(subMeshList.ToArray(), false, false); // keep submeshes separate

            if (finalMesh.subMeshCount != materialList.Count)
            {
                Debug.LogError($"Mismatch in submesh count: {finalMesh.subMeshCount} vs materials: {materialList.Count} for for submodel {partId}.");
                return null;
            }

            // Save prefab with all submeshes and materials
            GameObject go = new GameObject(partId);

            Mesh meshAsset = SaveMeshAsset(finalMesh, partId);
            go.AddComponent<MeshFilter>().sharedMesh = meshAsset;
            go.AddComponent<MeshRenderer>().sharedMaterials = materialList.ToArray();

            string prefabFolder = "Assets/Resources/LDrawPrefabs";
            if (!Directory.Exists(prefabFolder))
                Directory.CreateDirectory(prefabFolder);

            var fileName = partId.Replace('\\', '_');
            string prefabPath = Path.Combine(prefabFolder, $"{fileName}.prefab");
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

            go.SetActive(false);
            submodelCache[partId] = go;

            return go;
        }

        private static bool MatrixIsMirrored(Matrix4x4 m)
        {
            Vector3 x = m.GetColumn(0);
            Vector3 y = m.GetColumn(1);
            Vector3 z = m.GetColumn(2);
            return Vector3.Dot(Vector3.Cross(x, y), z) < 0;
        }

        public static string MatrixToString(Matrix4x4 m)
        {
            return $"{m.m00:F4}\t{m.m01:F4}\t{m.m02:F4}\t{m.m03:F4}\n" +
                $"{m.m10:F4}\t{m.m11:F4}\t{m.m12:F4}\t{m.m13:F4}\n" +
                $"{m.m20:F4}\t{m.m21:F4}\t{m.m22:F4}\t{m.m23:F4}\n" +
                $"{m.m30:F4}\t{m.m31:F4}\t{m.m32:F4}\t{m.m33:F4}";
        }

        private static Vector3 NegateZ(Vector3 v)
        {
            return new Vector3(v.x, v.y, -v.z);
        }

        public void ClearCache()
        {
            partCache.Clear();
            foreach (var kvp in submodelCache)
            {
                UnityEngine.Object.DestroyImmediate(kvp.Value);
            }
            submodelCache.Clear();
        }
    }
}
