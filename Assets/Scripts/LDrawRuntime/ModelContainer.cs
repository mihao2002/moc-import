using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LDraw.Runtime
{
    public class StepContainer
    {
        // private const int MaxLoadedStep = 2;
        // private static List<StepContainer> s_list = new List<StepContainer>();
        // private static int s_loadedCount = 0;

        private LDrawStep step;
        public Transform parentContainer;
        private HashSet<string> modelNames;
        private Dictionary<int, LDrawColor> colors;
        private Material mainMaterial;
        private GameObject modelContainer;

        private GameObject stepContainerGo;
        private string stepTag;
        private bool[] visible;
        private bool show;
        private bool highlighted;
        private bool unloadable;


        public StepContainer(GameObject stepContainerGo)
        {
            this.stepContainerGo = stepContainerGo;
            stepTag = "";
            unloadable = false;
        }

        public GameObject ClonePart(int index)
        {
            EnsureLoaded();
            GameObject childGo = stepContainerGo.transform.GetChild(index)?.gameObject;
            if (childGo == null)
            {
                Debug.LogError($"Failed to get object {index}");
                return null;
            }

            var clone = Object.Instantiate(childGo);
            clone.SetActive(false);

            // UnloadStepsIfNeeded();

            return clone;
        }

        public StepContainer(LDrawStep step, Transform parentContainer, HashSet<string> modelNames,
            Dictionary<int, LDrawColor> colors, Material mainMaterial, GameObject modelContainer, string stepTag)
        {
            this.step = step;
            this.parentContainer = parentContainer;
            this.modelNames = modelNames;
            this.colors = colors;
            this.mainMaterial = mainMaterial;
            this.modelContainer = modelContainer;
            stepContainerGo = null;
            this.stepTag = stepTag;
            unloadable = true;
            visible = Enumerable.Repeat(true, step.parts.Count).ToArray();
            show = false;
            highlighted = false;
            this.stepContainerGo = null;
            // s_list.Insert(0, this);
        }

        public void ShowStepParts(bool show, int start, int end)
        {
            EnsureLoaded();
            int childCount = stepContainerGo.transform.childCount;
            end = end >= 0 ? end : childCount - 1;
            for (int i = start; i <= end; i++)
            {
                var child = stepContainerGo.transform.GetChild(i);
                if (unloadable) visible[i] = show;
                child.gameObject.SetActive(show);
            }
            // UnloadStepsIfNeeded();
        }

        public void Show(bool show)
        {
            this.show = show;
            if (show)
            {
                EnsureLoaded();
            }

            stepContainerGo?.SetActive(show);
            // UnloadStepsIfNeeded();
        }

        private void EnsureLoaded()
        {
            if (stepContainerGo == null)
            {
                Load();
            }

            // var idx = s_list.IndexOf(this);
            // if (idx >= 0)
            // {
            //     s_list.RemoveAt(idx);
            //     s_list.Insert(0, this);
            // }               
        }

        // private static void UnloadStepsIfNeeded()
        // {
        //     if (s_loadedCount <= MaxLoadedStep) return;
        //     for (var i = s_list.Count - 1; i >= 0; i--)
        //     {
        //         var stepContainer = s_list[i];
        //         stepContainer.Unload();
        //     }
        // }

        public void Unload()
        {
            if (!unloadable || stepContainerGo == null) return;
            Object.Destroy(stepContainerGo);
            stepContainerGo = null;
            // s_loadedCount--;
        }

        private void Load()
        {
            var objs = new List<GameObject>();
            for (var i = 0; i < step.parts.Count; i++)
            {
                var part = step.parts[i];
                var fileName = part.partId.Replace('\\', '_');
                GameObject prefab = LDrawUtlity.LoadPrefab(fileName);
                if (prefab == null)
                {
                    Debug.LogWarning($"Missing prefab for part: {part.partId}");
                    continue;
                }
                GameObject go = Object.Instantiate(prefab, parentContainer);
                go.transform.localPosition = part.position;
                go.transform.localRotation = part.rotation;
                go.SetActive(visible[i]);
                if (!modelNames.Contains(part.partId))
                {
                    // Regular part: ensure it has a renderer, assign material asset if found
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer == null)
                        renderer = go.AddComponent<MeshRenderer>();
                    var color = colors[part.color].color;
                    var mat = LDrawUtlity.LoadMaterial(color);

                    Material[] sharedMats = renderer.sharedMaterials;
                    for (var j = 0; j < sharedMats.Length; j++)
                    {
                        if (sharedMats[j].color == mainMaterial.color)
                        {
                            sharedMats[j] = mat;
                            renderer.sharedMaterials = sharedMats;
                            break;
                        }
                    }
                }
                objs.Add(go);
                // s_loadedCount++;
            }

            // modelContainer.AddStep(objs);

            stepContainerGo = new GameObject($"{stepTag}");
            stepContainerGo.transform.SetParent(modelContainer.transform, worldPositionStays: false);
            stepContainerGo.SetActive(show); // Hide by default
            objs.ForEach(so => so.transform.SetParent(stepContainerGo.transform, false));

            Show(show);
            HighlightStep(highlighted);
        }

        public void HighlightStep(bool highlight)
        {
            this.highlighted = highlight;
            if (highlight)
            {
                EnsureLoaded();
            }

            if (stepContainerGo != null)
            {
                int layer = LayerMask.NameToLayer(highlight ? Consts.HighlightLayerName : Consts.NormalLayerName);
                SetLayerRecursively(stepContainerGo, layer);                
            }

            // UnloadStepsIfNeeded();
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    public class ModelContainer
    {
        private const string ModelNamePrefix = "model_";
        

        private List<StepContainer> stepContainers = new List<StepContainer>();
        private GameObject modelContainer;

        public GameObject ModelContainerGo
        {
            get
            {
                return modelContainer;
            }
        }

        public ModelContainer(string modelName)
        {
            modelContainer = new GameObject($"{ModelNamePrefix}{modelName}");
            modelContainer.SetActive(false); // Hide by default
        }

        public void ShowStepParts(int step, bool show, int start, int end)
        {
            var stepContainer = stepContainers[step];
            stepContainer.ShowStepParts(show, start, end);
        }

        public void AddStep(StepContainer stepContainer)
        {
            stepContainers.Add(stepContainer);
        }

        public void Show(bool show)
        {
            modelContainer.SetActive(show);
            if (!show)
            {
                stepContainers.ForEach(c => c.Unload());
            }
        }

        public void HighlightStep(int step, bool highlight)
        {
            stepContainers[step].HighlightStep(highlight);
        }

        public void ShowStep(int step, bool show)
        {
            stepContainers[step].Show(show);
        }

        public StepContainer GetStepContainer(int step)
        {
            return stepContainers[step];
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
} 