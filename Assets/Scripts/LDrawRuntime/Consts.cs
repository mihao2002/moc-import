using UnityEngine;

namespace LDraw.Runtime
{
    public static class Consts
    {
        public static readonly Matrix4x4 NegateZ = new Matrix4x4(
            new Vector4(1, 0, 0, 0),   // X stays X
            new Vector4(0, 1, 0, 0),   // Y stays Y
            new Vector4(0, 0, -1, 0),  // Z becomes -Z
            new Vector4(0, 0, 0, 1)    // Homogeneous coordinate
        );

        public static string HighlightLayerName = "Outline";
        public static string NormalLayerName = "Default";

        public static string PreviewLayerName = "Preview";
    }
} 