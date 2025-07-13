using System.Collections.Generic;
using UnityEngine;

namespace LDraw.Runtime
{
    public class LDrawStepNavigator : MonoBehaviour
    {
        public TextAsset stepDataJson; // This should contain serialized step data (LDrawSteps)
        public Transform parentContainer; // Where to spawn parts in the scene

        private List<LDrawStep> steps;
        private int currentStepIndex = -1;
        private List<GameObject> spawnedParts = new List<GameObject>();

        void Start()
        {
            LoadStepsFromJson();
            ShowStep(0); // Start from first step
        }

        public void ShowNextStep()
        {
            if (currentStepIndex < steps.Count - 1)
            {
                ShowStep(currentStepIndex + 1);
            }
        }

        public void ShowPreviousStep()
        {
            if (currentStepIndex > 0)
            {
                ShowStep(currentStepIndex - 1);
            }
        }

        private void ShowStep(int stepIndex)
        {
            ClearSpawnedParts();

            currentStepIndex = stepIndex;

            for (int i = 0; i <= currentStepIndex; i++)
            {
                foreach (var part in steps[i].parts)
                {
                    GameObject prefab = Resources.Load<GameObject>($"LDrawPrefabs/{part.partId}");
                    if (prefab == null)
                    {
                        Debug.LogWarning($"Missing prefab for part: {part.partId}");
                        continue;
                    }

                    GameObject go = Instantiate(prefab, parentContainer);
                    go.transform.localPosition = part.position;
                    go.transform.localRotation = part.rotation;

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.material.color = part.color;

                    spawnedParts.Add(go);
                }
            }
        }

        private void ClearSpawnedParts()
        {
            foreach (var go in spawnedParts)
            {
                if (go != null)
                    Destroy(go);
            }
            spawnedParts.Clear();
        }

        private void LoadStepsFromJson()
        {
            if (stepDataJson == null)
            {
                Debug.LogError("No step data assigned.");
                return;
            }

            steps = JsonUtility.FromJson<LDrawStepListWrapper>(stepDataJson.text).steps;
        }

        // Wrapper to deserialize list in JsonUtility
        [System.Serializable]
        private class LDrawStepListWrapper
        {
            public List<LDrawStep> steps;
        }
    }
}