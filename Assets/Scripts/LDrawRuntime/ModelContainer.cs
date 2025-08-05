using System.Collections.Generic;
using UnityEngine;

namespace LDraw.Runtime
{
    public class ModelContainer
    {
        private string modelName;
        private List<GameObject> stepContainers = new List<GameObject>();
        private GameObject modelContainer;

        public ModelContainer(string modelName)
        {
            this.modelName = modelName;
            modelContainer = new GameObject($"ModelContainer_{modelName}");
            modelContainer.SetActive(false); // Hide by default
        }

        public GameObject AddStep(List<GameObject> stepObjects)
        {
            GameObject stepContainer = new GameObject($"Step_{stepContainers.Count}");
            stepContainer.transform.SetParent(modelContainer.transform, worldPositionStays: false);
            stepContainer.SetActive(false); // Hide by default

            foreach (var go in stepObjects)
            {
                if (go != null)
                    go.transform.SetParent(stepContainer.transform, false);
            }

            stepContainers.Add(stepContainer);
            return stepContainer;
        }

        public void Show(bool show)
        {
            modelContainer.SetActive(show);
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public void HighlightStep(int stepIndex, bool highlight)
        {
            if (stepIndex >= 0 && stepIndex < stepContainers.Count)
            {
                int layer = LayerMask.NameToLayer(highlight ? "Outline" : "Default");
                SetLayerRecursively(stepContainers[stepIndex], layer);
            }
        }

        public void ShowStep(int stepIndex, bool show)
        {
            if (stepIndex >= 0 && stepIndex < stepContainers.Count)
                stepContainers[stepIndex].SetActive(show);
        }

        public void Rotate(float x, float y, float z)
        {
            // Debug.Log($"Rotate {x} {y} {z}");
            // Quaternion rotation = Quaternion.Euler(-x, -y, z);
            // modelContainer.transform.localRotation = rotation;

            float zx = x;            // keep x
            float zy = y;            // keep y
            float zz = -z;           // negate z rotation!

            Quaternion qx = Quaternion.AngleAxis(zx, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(zy, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(zz, Vector3.forward);

            // Match LDCad's X → Y → Z order
            Quaternion rotation = qz * qy * qx;
            modelContainer.transform.localRotation = rotation;
        }

        public int GetStepCount()
        {
            return stepContainers.Count;
        }

        public GameObject GetStepContainer(int stepIndex)
        {
            if (stepIndex >= 0 && stepIndex < stepContainers.Count)
                return stepContainers[stepIndex];
            return null;
        }
    }
} 