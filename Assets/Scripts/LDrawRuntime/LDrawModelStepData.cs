using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace LDraw.Runtime
{
    [Serializable]
    public class LDrawColor
    {
        public Color color;

        public int blColor;
        public string name;
    }

    [Serializable]
    public class LDrawBuildMod
    {
        public int step;
        public int start;
        public int end;
    }

    [Serializable]
    public class LDrawPartCount
    {
        public LDrawPartCore part;
        public int count;
    }

    [Serializable]
    public class LDrawPartCore
    {
        public string partId;
        public int color;

        public override bool Equals(object obj)
        {
            return obj is LDrawPartCore other &&
                    partId == other.partId &&
                    color == other.color;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(partId, color);
        }
    }

    [Serializable]
    public class LDrawPart: LDrawPartCore
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class LDrawStep
    {
        public List<LDrawPart> parts = new List<LDrawPart>();
        
        // Rotation support for submodels
        public Vector3? rotation; // null = no rotation, Vector3.zero = ROTSTEP END, other values = rotation angles
        public int rotRef; // index of another step whose rotation will be applied in this step, -1 means the default rotation
        public float radius; // always set by editor
        public Vector3 center; // the center of the game object

        [JsonIgnore]
        public Bounds modelBounds;
    }

    [Serializable]
    public class RuntimeModelData
    {
        public string modelName;
        public List<LDrawStep> steps;

        [System.NonSerialized]
        public ModelContainer container;
        public Dictionary<int /*removestep*/, LDrawBuildMod> buildMods;
    }   

    [Serializable]
    public class FlatStep
    {
        public int model; // index of another step whose rotation will be applied in this step, -1 means the default rotation
        public int modelStepIdx; // always set by editor
    }

    [Serializable]
    public class LDrawPartDesc
    {
        public string id;
        public string description;
    }

    [Serializable]
    public class StepPackage
    {
        public List<RuntimeModelData> models;
        public List<FlatStep> flatSteps;
    }  
}