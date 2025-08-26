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
        // private static Dictionary<int, LDrawColor> _colorTable = new Dictionary<int, LDrawColor>();

        public static Dictionary<int, int> LoadLDrawToBLColorMap(string filePath)
        {
            var map = new Dictionary<int, int>();

            var lines = File.ReadAllLines(filePath);

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Split by tab
                var columns = line.Split('\t');

                if (columns.Length < 3) continue; // ensure enough columns

                string blCodeStr = columns[1];
                string ldrawCodeStr = columns[2];

                if (string.IsNullOrEmpty(blCodeStr)) continue; // skip if BL Color Code is empty

                if (int.TryParse(ldrawCodeStr, out int ldrawCode) &&
                    int.TryParse(blCodeStr, out int blCode))
                {
                    // Only add if not already present
                    if (!map.ContainsKey(ldrawCode))
                    {
                        map[ldrawCode] = blCode;
                    }
                }
            }

            return map;
        }


        public static Dictionary<int, LDrawColor> LoadFromFile(string ldconfigPath, string studioColorPath)
        {
            // if (IsLoaded) return;
            Dictionary<int, LDrawColor> _colorTable = new Dictionary<int, LDrawColor>();

            // _colorTable.Clear();
            if (!File.Exists(ldconfigPath) || !File.Exists(studioColorPath))
            {
                Debug.LogError($"LDConfig.ldr file or Studio color file not found at: {ldconfigPath}");
                return null;
            }

            var colorMap = LoadLDrawToBLColorMap(studioColorPath);

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
                    var nameMatch = Regex.Match(line, @"!COLOUR\s+(\S+)", RegexOptions.IgnoreCase);

                    if (!codeMatch.Success || !valueMatch.Success || !nameMatch.Success)
                        continue;

                    int code = int.Parse(codeMatch.Groups[1].Value);
                    string hex = valueMatch.Groups[1].Value;
                    string name = nameMatch.Groups[1].Value;

                    if (ColorUtility.TryParseHtmlString(hex, out Color color))
                    {
                        if (colorMap.ContainsKey(code))
                        {
                            _colorTable[code] = new LDrawColor { name = name, blColor = colorMap[code], color = color };
                            colorEntries.Add(new LDrawColorEntry { code = code, color = color });
                        }
                        else
                        {
                            Debug.LogError($"Color code {code} doesn't exist in bricklink color map.");
                        }
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

            return _colorTable;
        }

        // public static Color GetColor(int code)
        // {
        //     if (!IsLoaded)
        //     {
        //         Debug.LogWarning("LDraw color table not loaded.");
        //         return Color.magenta;
        //     }

        //     if (_colorTable.TryGetValue(code, out var color))
        //         return color;

        //     Debug.LogWarning($"Unknown LDraw color code: {code}");
        //     return Color.gray;
        // }
    }
}
