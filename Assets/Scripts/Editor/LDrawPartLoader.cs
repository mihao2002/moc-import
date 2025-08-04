using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;
using UnityEngine.Rendering;

namespace LDraw.Editor
{
    public class LDrawMesh
    {
        public Mesh mesh;
        public bool isCW;
    }

    public static class LDrawPartLoader
    {
        private static Dictionary<string, LDrawMesh> partCache = new Dictionary<string, LDrawMesh>();
        private static Dictionary<string, GameObject> submodelCache = new Dictionary<string, GameObject>();
        private static Dictionary<string, List<LDrawStep>> models = new Dictionary<string, List<LDrawStep>>();     
        
        // Progress callback
        public static System.Action<float, string> OnProgressUpdate;

        public static Mesh SaveMeshAsset(Mesh mesh, string meshName)
        {
            string meshFolder = "Assets/Resources/LDrawMeshes";
            if (!Directory.Exists(meshFolder))
            {
                Directory.CreateDirectory(meshFolder);
                AssetDatabase.Refresh();
            }

            string meshAssetPath = Path.Combine(meshFolder, meshName + ".asset");

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

            Debug.Log($"Saved mesh asset at {meshAssetPath}");
            return newMesh;
        }

        // public static Material GetOrCreateMaterial(Color color)
        // {
        //     string materialFolder = "Assets/Resources/LDrawMaterials";
        //     if (!Directory.Exists(materialFolder))
        //         Directory.CreateDirectory(materialFolder);

        //     string colorKey = $"{color.r:F3}_{color.g:F3}_{color.b:F3}";
        //     string matPath = Path.Combine(materialFolder, $"Mat_{colorKey}.mat");
        //     var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        //     if (existing != null) return existing;

        //     var mat = new Material(Shader.Find("Standard"));
        //     mat.color = color;
        //     AssetDatabase.CreateAsset(mat, matPath);
        //     AssetDatabase.SaveAssets();
        //     return mat;
        // }
        public static Material GetOrCreateMaterial(Color color)
        {
            string materialFolder = "Assets/Resources/LDrawMaterials";
            if (!Directory.Exists(materialFolder))
                Directory.CreateDirectory(materialFolder);

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

        public static GameObject GetGameObject(string partId, Color color)
        {
            GameObject go = new GameObject(partId);
            if (partCache.ContainsKey(partId))
            {
                // Add MeshFilter and assign the mesh
                MeshFilter meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.mesh = partCache[partId].mesh;

                // Add MeshRenderer and assign material
                MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = GetOrCreateMaterial(color);
            }
            else if (submodelCache.ContainsKey(partId))
            {
                go = GameObject.Instantiate(submodelCache[partId]);
                go.SetActive(true);
            }
            else
            {
                Debug.LogError($"Can't find part {partId}.");
            }

            return go;
        }

        private static string FindPartFile(string partId, string partLibraryPath, string unofficialPartLibraryPath)
        {
            string path = FindPartFileInPath(partId, partLibraryPath);
            if (path == null)
            {
                path = FindPartFileInPath(partId, unofficialPartLibraryPath);
            }

            return path;
        }

        private static string FindPartFileInPath(string partId, string partLibraryPath)
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

        private static Mesh CombineMeshesPreserveSubmeshes(List<CombineInstance> combineInstances)
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

        private static LDrawMesh ParsePartFile(string filePath, string partLibraryPath, string unofficialPartLibraryPath)
        {
            var lines = File.ReadAllLines(filePath);
            var allVertices = new List<Vector3>();
            var allTriangles = new List<int>();

            bool invertNext = false;
            bool isCW = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

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

                                LDrawMesh ldrawMesh = LoadPartMesh(referencedPartId, partLibraryPath, unofficialPartLibraryPath);
                                if (ldrawMesh != null)
                                {
                                    //bool invertFace = invertNext ^ (isCW != ldrawMesh.isCW);
                                    //bool mirrorXform = MatrixIsMirrored(transform);

                                    // Apply coordinate system swap
                                    // Check for mirroring (OPTIONAL)
                                    //bool isMirrored = MatrixIsMirrored(transform);

                                    // Final winding decision
                                    bool invertFace = invertNext ^ isMirrored;

                                    Debug.Log($"{filePath} -> {referencedPartId}-{invertFace}");
                                    // bool invertFace = invertNext ^ mirrorXform ^ !isCW;
                                    Mesh referencedMesh = ldrawMesh.mesh;

                                    int vertexOffset = allVertices.Count;
                                    //Debug.Log(MatrixToString(transform));
                                    foreach (var v in referencedMesh.vertices)
                                    {
                                        Vector3 v2 = transform.MultiplyPoint3x4(v);
                                        //Vector3 worldPos = NegateZ(v2);
                                        allVertices.Add(v2);
                                    }

                                    //Apply correct winding if mirrored
                                    if (invertFace)
                                    {
                                        for (int i = 0; i < referencedMesh.triangles.Length; i += 3)
                                        {
                                            allTriangles.Add(vertexOffset + referencedMesh.triangles[i]);
                                            allTriangles.Add(vertexOffset + referencedMesh.triangles[i + 2]);
                                            allTriangles.Add(vertexOffset + referencedMesh.triangles[i + 1]);
                                            //Debug.Log($"Triangle : {allVertices[vertexOffset + referencedMesh.triangles[i]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 2]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 1]]} (invertFace={invertFace})");
                                        }
                                    }
                                    else
                                    {
                                        for (int i = 0; i < referencedMesh.triangles.Length; i += 3)
                                        {
                                            allTriangles.Add(vertexOffset + referencedMesh.triangles[i]);
                                            allTriangles.Add(vertexOffset + referencedMesh.triangles[i + 1]);
                                            allTriangles.Add(vertexOffset + referencedMesh.triangles[i + 2]);
                                            //Debug.Log($"Triangle : {allVertices[vertexOffset + referencedMesh.triangles[i]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 1]]} -> {allVertices[vertexOffset + referencedMesh.triangles[i + 2]]} (invertFace={invertFace})");
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

                        case "3":
                            if (tokens.Length >= 11)
                            {
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
                            break;

                        case "4":
                            if (tokens.Length >= 14)
                            {
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

            if (allVertices.Count == 0 || allTriangles.Count == 0)
            {
                Debug.LogWarning($"No geometry parsed from: {filePath}");
                return null;
            }

            Mesh mesh = new Mesh();
            mesh.vertices = allVertices.ToArray();
            mesh.triangles = allTriangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return new LDrawMesh{mesh=mesh, isCW=isCW};
        }

        private static LDrawMesh LoadPartMesh(string partId, string partLibraryPath, string unofficialPartLibraryPath)
        {
            if (partCache.ContainsKey(partId))
            {
                return partCache[partId];
            }

            string datPath = FindPartFile(partId, partLibraryPath, unofficialPartLibraryPath);
            if (datPath == null)
            {
                Debug.LogError($"Part file not found: {partId}");
                return null;
            }

            try
            {
                LDrawMesh ldrawMesh = ParsePartFile(datPath, partLibraryPath, unofficialPartLibraryPath);
                partCache[partId] = ldrawMesh;

                return ldrawMesh;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse mesh for {partId}: {e.Message}");
                return null;
            }
        }

        public static LDrawMesh LoadPartFromLibrary(string partId, string partLibraryPath, string unofficialPartLibraryPath)
        {
            LDrawMesh ldrawMesh = LoadPartMesh(partId, partLibraryPath, unofficialPartLibraryPath);
            if (ldrawMesh != null)
            {
                GameObject go = new GameObject(partId);
                Mesh meshAsset = SaveMeshAsset(ldrawMesh.mesh, partId);
                go.AddComponent<MeshFilter>().sharedMesh = meshAsset;
                // !!! Why do I have to assign material here???
                // var meshRenderer = go.GetComponent<MeshRenderer>();
                // if (meshRenderer == null)
                //     meshRenderer = go.AddComponent<MeshRenderer>();
                // meshRenderer.sharedMaterial = GetOrCreateMaterial(Color.white);
                // Do NOT add MeshRenderer or assign material here for regular parts
                string prefabFolder = "Assets/Resources/LDrawPrefabs";
                if (!Directory.Exists(prefabFolder))
                    Directory.CreateDirectory(prefabFolder);
                string prefabPath = Path.Combine(prefabFolder, $"{partId}.prefab");
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Debug.Log($"Saved Prefab {partId}");
                UnityEngine.Object.DestroyImmediate(go);
            }
            else
            {
                Debug.LogError($"Failed to parse mesh for {partId} - returned null");
            }
            return ldrawMesh;
        }

        private static Mesh ExtractSubMesh(Mesh sourceMesh, int subMeshIndex)
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
        public static GameObject LoadSubmodelFromLibrary(string partId, List<LDrawStep> steps)
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
                    bool isPart = false;
                    Mesh meshCopy = null;
                    // decide if this is a part of submodel
                    if (partCache.ContainsKey(part.partId))
                    {
                        isPart = true;
                        LDrawMesh subMesh = partCache[part.partId];
                        meshCopy = UnityEngine.Object.Instantiate(subMesh.mesh);
                    }
                    else if (submodelCache.ContainsKey(part.partId))
                    {
                        var gameObject = submodelCache[part.partId];
                        var meshFilter = gameObject.GetComponent<MeshFilter>();
                        if (meshFilter == null)
                        {
                            Debug.LogError($"Can't find mesh filter for {part.partId} for submodel {partId}.");
                            return null;
                        }

                        //meshCopy = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
                        meshCopy = meshFilter.sharedMesh;
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

                    if (isPart)
                    {
                        var ci = new CombineInstance
                        {
                            mesh = meshCopy,
                            transform = Matrix4x4.identity
                        };

                        var material = GetOrCreateMaterial(part.color);

                        if (!colorToInstances.ContainsKey(material))
                            colorToInstances[material] = new List<CombineInstance>();
                        colorToInstances[material].Add(ci);
                    }
                    else
                    {
                        var gameObject = submodelCache[part.partId];
                        var subRenderer = gameObject.GetComponent<MeshRenderer>();

                        for (int subIdx = 0; subIdx < meshCopy.subMeshCount; subIdx++)
                        {
                            // Mesh submesh = new Mesh();
                            // submesh.vertices = meshCopy.vertices;
                            // submesh.normals = meshCopy.normals;
                            // submesh.triangles = meshCopy.GetTriangles(subIdx);
                            // submesh.RecalculateBounds();
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
            }

            // Combine meshes by color/material group into submeshes
            var subMeshList = new List<CombineInstance>();
            var materialList = new List<Material>();

            Debug.Log($"Creating prefab for {partId}");
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
            finalMesh.CombineMeshes(subMeshList.ToArray(), false, true); // keep submeshes separate

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

            string prefabPath = Path.Combine(prefabFolder, $"{partId}.prefab");
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

        public static void ClearCache()
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
