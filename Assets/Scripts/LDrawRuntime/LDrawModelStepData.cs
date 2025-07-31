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
        
        // Rotation support for submodels
        public Vector3? rotation; // null = no rotation, Vector3.zero = ROTSTEP END, other values = rotation angles
        public float radius; // always set by editor
        public Vector3 center; // the center of the game object
    }

    [Serializable]
    public class LDrawPart
    {
        public string partId;
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
    }

    [System.Serializable]
    public class LDrawModelStepData
    {
        public List<ModelStepPair> models;
        public Dictionary<string, List<LDrawStep>> ToDictionary()
        {
            var dict = new Dictionary<string, List<LDrawStep>>();
            if (models != null)
            {
                foreach (var pair in models)
                {
                    dict[pair.modelName] = pair.steps;
                }
            }
            return dict;
        }
    }
    [System.Serializable]
    public class ModelStepPair
    {
        public string modelName;
        public List<LDrawStep> steps;
    }
}