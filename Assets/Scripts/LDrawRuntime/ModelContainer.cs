using System.Collections.Generic;
using UnityEngine;

namespace LDraw.Runtime
{
    public class ModelContainer
    {
        private const string ModelNamePrefix = "model_";
        private const string StepNamePrefix = "step_";

        private List<GameObject> stepContainers = new List<GameObject>();
        private GameObject modelContainer;

        public ModelContainer(string modelName)
        {
            modelContainer = new GameObject($"{ModelNamePrefix}{modelName}");
            modelContainer.SetActive(false); // Hide by default
        }

        public void ShowStepParts(int step, bool show, int start, int end)
        {
            var stepContainer = stepContainers[step];
            int childCount = stepContainer.transform.childCount;
            end = end >= 0 ? end : childCount - 1;
            for (int i = start; i <= end; i++)
            {
                var child = stepContainer.transform.GetChild(i);
                child.gameObject.SetActive(show);
            }
        }

        public GameObject AddStep(List<GameObject> stepObjects)
        {
            GameObject stepContainer = new GameObject($"{StepNamePrefix}{stepContainers.Count}");
            stepContainer.transform.SetParent(modelContainer.transform, worldPositionStays: false);
            stepContainer.SetActive(false); // Hide by default
            stepObjects.ForEach(so => so.transform.SetParent(stepContainer.transform, false));
            stepContainers.Add(stepContainer);
            return stepContainer;
        }

        public void Show(bool show)
        {
            modelContainer.SetActive(show);
        }

        public void HighlightStep(int step, bool highlight)
        {
            int layer = LayerMask.NameToLayer(highlight ? Consts.HighlightLayerName : Consts.NormalLayerName);
            SetLayerRecursively(stepContainers[step], layer);
        }

        public void ShowStep(int step, bool show)
        {
            stepContainers[step].SetActive(show);
        }

        public GameObject GetStepContainer(int step)
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