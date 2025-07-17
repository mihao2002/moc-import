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

        public void AddStep(List<GameObject> stepObjects)
        {
            GameObject stepContainer = new GameObject($"Step_{stepContainers.Count}");
            stepContainer.transform.SetParent(modelContainer.transform);
            stepContainer.SetActive(false); // Hide by default

            foreach (var go in stepObjects)
            {
                if (go != null)
                    go.transform.SetParent(stepContainer.transform, false);
            }

            stepContainers.Add(stepContainer);
        }

        public void Show(bool show)
        {
            modelContainer.SetActive(show);
        }

        public void ShowStep(int stepIndex, bool show)
        {
            if (stepIndex >= 0 && stepIndex < stepContainers.Count)
                stepContainers[stepIndex].SetActive(show);
        }

        public void Rotate(float x, float y, float z)
        {
            Matrix4x4 rotation = Matrix4x4.Rotate(Quaternion.Euler(x, y, z));
            Matrix4x4 lhcs = Consts.NegateZ * rotation * Consts.NegateZ;
            modelContainer.transform.localRotation = lhcs.rotation;
        }

        public int GetStepCount()
        {
            return stepContainers.Count;
        }
    }
} 