using System.Globalization;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LDraw.Runtime
{
    public static class LDrawUtlity
    {
        public static Material LoadMaterial(Color color)
        {
            string colorKey = string.Format(CultureInfo.InvariantCulture, "Mat_{0:F3}_{1:F3}_{2:F3}",
                color.r, color.g, color.b);
            string address = $"LDrawMaterials/{colorKey}";
            var handle = Addressables.LoadAssetAsync<Material>(address);
            Material mat = handle.WaitForCompletion();
            // By default, the shader link is broken from remote asset.
            // Relink the shader to local shader here.
            mat.shader = Shader.Find(mat.shader.name);
            return mat;
        }

        public static GameObject LoadPrefab(string fileName)
        {
            string address = $"LDrawPrefabs/{fileName}";
            var handle = Addressables.LoadAssetAsync<GameObject>(address);

            // Block until complete
            handle.WaitForCompletion();

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return handle.Result;
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to load prefab at address: {address}");
                return null;
            }
        }
    }
} 