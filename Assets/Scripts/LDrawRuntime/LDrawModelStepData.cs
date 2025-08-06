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
        public int rotRef; // index of another step whose rotation will be applied in this step, -1 means the default rotation
        public float radius; // always set by editor
        public Vector3 center; // the center of the game object
    }

    [Serializable]
    public class FlatStep
    {
        public int model; // index of another step whose rotation will be applied in this step, -1 means the default rotation
        public int modelStepIdx; // always set by editor
    }

    [Serializable]
    public class RuntimeModelData
    {
        public string modelName;
        public List<LDrawStep> steps;
        public ModelContainer container;
    }   

    [Serializable]
    public class CombinedData
    {
        public List<RuntimeModelData> models;
        public List<FlatStep> flatSteps;
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