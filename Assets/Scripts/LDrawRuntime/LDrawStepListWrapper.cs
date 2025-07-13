using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace LDraw.Runtime
{
    [Serializable]
    public class LDrawStep
    {
        public List<LDrawPart> parts = new List<LDrawPart>();
    }

    [Serializable]
    public class LDrawPart
    {
        public string partId;
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
    }

    [Serializable]
    public class LDrawStepListWrapper
    {
        public List<LDrawStep> steps;
    }
}