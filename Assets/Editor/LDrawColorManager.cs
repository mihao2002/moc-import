using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public static class LDrawColorManager
{
    private static Dictionary<int, Color> _colorTable = new Dictionary<int, Color>();

    public static bool IsLoaded => _colorTable.Count > 0;

    public static void LoadFromFile(string ldconfigPath)
    {
        if (IsLoaded) return;

        Debug.Log($"Loading colors...");

        _colorTable.Clear();

        if (!File.Exists(ldconfigPath))
        {
            Debug.LogError($"LDConfig.ldr file not found at: {ldconfigPath}");
            return;
        }

        string[] lines = File.ReadAllLines(ldconfigPath);

        foreach (string line in lines)
        {
            if (!line.StartsWith("0 !COLOUR", StringComparison.OrdinalIgnoreCase))
                continue;

            // Match: 0 !COLOUR Yellow CODE 14 VALUE #F2CD37 EDGE #333333
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
