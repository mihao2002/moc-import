using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LDraw.Runtime;

namespace LDraw.Editor
{
    public class LDrawMesh
    {
        public Mesh mesh;
        public bool isCW;
    }

    public static class LDrawPartLoader
    {
        public static readonly Matrix4x4 swapYZ1 = new Matrix4x4(
            new Vector4(1, 0, 0, 0),   // X stays X
            new Vector4(0, 0, 1, 0),   // Y becomes Z
            new Vector4(0, 1, 0, 0),   // Z becomes Y
            new Vector4(0, 0, 0, 1)    // Homogeneous coordinate
        );

        public static readonly Matrix4x4 negateZ = new Matrix4x4(
            new Vector4(1, 0, 0, 0),   // X stays X
            new Vector4(0, 1, 0, 0),   // Y becomes Z
            new Vector4(0, 0, -1, 0),   // Z becomes Y
            new Vector4(0, 0, 0, 1)    // Homogeneous coordinate
        );    

        private static Dictionary<string, LDrawMesh> meshCache = new Dictionary<string, LDrawMesh>();
        private static HashSet<string> loadingParts = new HashSet<string>();

        public static Mesh SaveMeshAsset(Mesh mesh, string meshName)
        {
            string meshFolder = "Assets/LDrawMeshes";
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

        public static Material GetOrCreateMaterial(Color color)
        {
            string materialFolder = "Assets/Resources/LDrawMaterials";
            if (!Directory.Exists(materialFolder))
                Directory.CreateDirectory(materialFolder);

            string colorKey = $"{color.r:F3}_{color.g:F3}_{color.b:F3}";
            string matPath = Path.Combine(materialFolder, $"Mat_{colorKey}.mat");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null) return existing;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        public static GameObject SpawnPart(LDrawPart part, string partLibraryPath, string unofficialPartLibraryPath, Dictionary<string, List<LDrawStep>> models)
        {
            GameObject go = new GameObject(part.partId);

            LDrawMesh ldrawMesh = LoadMeshFromLibrary(part.partId, partLibraryPath, unofficialPartLibraryPath, models);
            if (ldrawMesh != null && ldrawMesh.mesh != null)
            {
                Mesh mesh = ldrawMesh.mesh;

                // Save mesh as asset and get asset reference
                Mesh meshAsset = SaveMeshAsset(mesh, part.partId);

                go.AddComponent<MeshFilter>().sharedMesh = meshAsset;
                go.AddComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(part.color);

                string prefabFolder = "Assets/Resources/LDrawPrefabs";
                if (!Directory.Exists(prefabFolder))
                    Directory.CreateDirectory(prefabFolder);

                string prefabPath = Path.Combine(prefabFolder, $"{part.partId}.prefab");

                // Save prefab (not connect to scene, just create asset)
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

                // Clean up the temporary object
                // GameObject.DestroyImmediate(go);
            }
            else
            {
                // Don't create prefab for placeholder cube, just create a visual cube in scene
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.AddComponent<MeshFilter>().sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = cube.GetComponent<MeshRenderer>().sharedMaterial;
                UnityEngine.Object.DestroyImmediate(cube);
                go.name = part.partId + " (missing mesh)";
            }

            go.transform.position = part.position;
            go.transform.rotation = part.rotation;

            Debug.Log(part.partId);
            Debug.Log(MatrixToString(Matrix4x4.Rotate(part.rotation)));

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

        // Overload: allow passing in-memory models for submodel mesh generation
        public static LDrawMesh LoadMeshFromLibrary(string partId, string partLibraryPath, string unofficialPartLibraryPath, Dictionary<string, List<LDrawStep>> models)
        {
            Debug.Log($"Loading mesh for {partId}, {(models != null ? string.Join(", ", models.Keys) : "null")}");
            if (meshCache.ContainsKey(partId))
                return meshCache[partId];

            if (loadingParts.Contains(partId))
            {
                Debug.LogWarning($"Circular reference detected for part: {partId}");
                return null;
            }

            // Check if this is an in-memory submodel
            if (models != null && models.ContainsKey(partId))
            {
                Debug.Log($"Loading mesh for {partId} from in-memory models");
                loadingParts.Add(partId);
                // Recursively build mesh for submodel by combining all its steps/parts
                var allMeshes = new List<Mesh>();
                foreach (var step in models[partId])
                {
                    foreach (var part in step.parts)
                    {
                        // Recursively get mesh for each part
                        LDrawMesh subMesh = LoadMeshFromLibrary(part.partId, partLibraryPath, unofficialPartLibraryPath, models);
                        if (subMesh != null && subMesh.mesh != null)
                        {
                            // Transform mesh to part's position/rotation
                            Mesh meshCopy = UnityEngine.Object.Instantiate(subMesh.mesh);
                            Vector3[] verts = meshCopy.vertices;
                            for (int i = 0; i < verts.Length; i++)
                            {
                                verts[i] = part.rotation * verts[i] + part.position;
                            }
                            meshCopy.vertices = verts;
                            meshCopy.RecalculateBounds();
                            allMeshes.Add(meshCopy);
                        }
                    }
                }
                // Combine all meshes into one
                Mesh combined = CombineMeshes(allMeshes);
                var ldrawMesh = new LDrawMesh { mesh = combined, isCW = true };
                meshCache[partId] = ldrawMesh;
                loadingParts.Remove(partId);
                return ldrawMesh;
            }

            // Fallback: load from part library as before
            LDrawMesh ldrawMeshFallback = LoadMeshFromPath(partId, partLibraryPath, unofficialPartLibraryPath, models);
            if (ldrawMeshFallback != null)
            {
                meshCache[partId] = ldrawMeshFallback;
            }
            else
            {
                Debug.LogWarning($"Failed to parse mesh for {partId} - returned null");
            }
            return ldrawMeshFallback;
        }

        // Helper to combine multiple meshes into one
        private static Mesh CombineMeshes(List<Mesh> meshes)
        {
            if (meshes.Count == 0) return null;
            if (meshes.Count == 1) return meshes[0];
            var combine = new CombineInstance[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
            {
                combine[i].mesh = meshes[i];
                combine[i].transform = Matrix4x4.identity;
            }
            Mesh combined = new Mesh();
            combined.CombineMeshes(combine, true, false);
            combined.RecalculateBounds();
            combined.RecalculateNormals();
            return combined;
        }

        private static LDrawMesh LoadMeshFromPath(string partId, string partLibraryPath, string unofficialPartLibraryPath, Dictionary<string, List<LDrawStep>> models)
        {
            string datPath = FindPartFile(partId, partLibraryPath, unofficialPartLibraryPath);
            if (datPath == null)
            {
                Debug.LogWarning($"Part file not found: {partId}");
                return null;
            }

            try
            {
                loadingParts.Add(partId);
                LDrawMesh ldrawMesh = ParseDatFileToMesh(datPath, partLibraryPath, unofficialPartLibraryPath, models);
                loadingParts.Remove(partId);

                return ldrawMesh;
            }
            catch (Exception e)
            {
                loadingParts.Remove(partId);
                Debug.LogWarning($"Failed to parse mesh for {partId}: {e.Message}");
                return null;
            }
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

        private static LDrawMesh ParseDatFileToMesh(string filePath, string partLibraryPath, string unofficialPartLibraryPath, Dictionary<string, List<LDrawStep>> models)
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
                                transform = negateZ * transform * negateZ;

                                LDrawMesh ldrawMesh = LoadMeshFromLibrary(referencedPartId, partLibraryPath, unofficialPartLibraryPath, models);
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

        public static void ClearCache()
        {
            meshCache.Clear();
            loadingParts.Clear();
        }
    }
}
