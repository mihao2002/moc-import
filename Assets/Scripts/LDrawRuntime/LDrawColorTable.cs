using UnityEngine;
using System.Collections.Generic;

namespace LDraw.Runtime
{
    public class LDrawColorTable : ScriptableObject
    {
        public List<LDrawColorEntry> colors;

        private Dictionary<int, Color> _cache;

        public Color GetColor(int code)
        {
            if (_cache == null)
            {
                _cache = new Dictionary<int, Color>();
                foreach (var entry in colors)
                {
                    _cache[entry.code] = entry.color;
                }
            }

            if (_cache.TryGetValue(code, out var color))
                return color;

            Debug.LogWarning($"Unknown LDraw color code: {code}");
            return Color.gray;
        }
    }

    [System.Serializable]
    public class LDrawColorEntry
    {
        public int code;
        public Color color;
    }
}
