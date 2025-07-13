using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using LDraw.Runtime;

namespace LDraw.Editor
{
    public static class LDrawColorManager
    {
        private static Dictionary<int, Color> _colorTable = new Dictionary<int, Color>();

        public static bool IsLoaded => _colorTable.Count > 0;

        public static void LoadFromFile(string ldconfigPath)
        {
            if (IsLoaded) return;

            Debug.Log($"Loading colors from {ldconfigPath}...");

            _colorTable.Clear();
            if (!File.Exists(ldconfigPath))
            {
                Debug.LogError($"LDConfig.ldr file not found at: {ldconfigPath}");
                return;
            }

            string[] lines = File.ReadAllLines(ldconfigPath);
            var colorEntries = new List<LDrawColorEntry>();

            foreach (string line in lines)
            {
                if (!line.StartsWith("0 !COLOUR", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var codeMatch = Regex.Match(line, @"CODE\s+(\d+)", RegexOptions.IgnoreCase);
                    var valueMatch = Regex.Match(line, @"VALUE\s+(#\w{6})", RegexOptions.IgnoreCase);

                    if (!codeMatch.Success || !valueMatch.Success)
                        continue;

                    int code = int.Parse(codeMatch.Groups[1].Value);
                    string hex = valueMatch.Groups[1].Value;

                    if (ColorUtility.TryParseHtmlString(hex, out Color color))
                    {
                        _colorTable[code] = color;
                        colorEntries.Add(new LDrawColorEntry { code = code, color = color });
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid color hex: {hex} in line: {line}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to parse line: {line}\n{ex.Message}");
                }
            }

            Debug.Log($"Loaded {_colorTable.Count} colors from LDConfig.ldr");

            // Save to ScriptableObject for runtime use
            SaveColorTableAsset(colorEntries);
        }


        private static void SaveColorTableAsset(List<LDrawColorEntry> entries)
        {
            string assetPath = "Assets/Resources/LDrawColorTable.asset";
            var asset = ScriptableObject.CreateInstance<LDrawColorTable>();
            asset.colors = entries;

            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Saved LDrawColorTable.asset with {entries.Count} colors to Resources folder.");
        }

        public static Color GetColor(int code)
        {
            if (!IsLoaded)
            {
                Debug.LogWarning("LDraw color table not loaded.");
                return Color.magenta;
            }

            if (_colorTable.TryGetValue(code, out var color))
                return color;

            Debug.LogWarning($"Unknown LDraw color code: {code}");
            return Color.gray;
        }
    }
}
