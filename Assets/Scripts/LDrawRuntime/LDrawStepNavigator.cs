using System.Collections.Generic;
using UnityEngine;

namespace LDraw.Runtime
{
    public class LDrawStepNavigator : MonoBehaviour
    {
        public Transform parentContainer; // Where to spawn parts in the scene

        private List<LDrawStep> steps;
        private int currentStepIndex = -1;
        private List<GameObject> spawnedParts = new List<GameObject>();
        private string stepDataJson; // This should contain serialized step data (LDrawSteps)

        void Start()
        {
            // Auto-load from Resources if not assigned
            if (stepDataJson == null)
            {
                var jsonAsset = Resources.Load<TextAsset>("LDrawStepData");
                stepDataJson = jsonAsset.text;
            }
                

            LoadStepsFromJson();
            ShowStep(0); // Start from first step
        }

        public void ShowNextStep()
        {
            Debug.Log("ShowNextStep called");
            if (currentStepIndex < steps.Count - 1)
            {
                ShowStep(currentStepIndex + 1);
            }
        }

        public void ShowPreviousStep()
        {
            Debug.Log("ShowPreviousStep called");
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

                    Debug.Log($"Spawning {part.partId} at {part.position}");

                    GameObject go = Instantiate(prefab, parentContainer);
                    go.transform.localPosition = part.position;
                    go.transform.localRotation = part.rotation;

                    Debug.DrawRay(go.transform.position, Vector3.up * 0.1f, Color.red, 5f);

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.material.color = part.color;

                    Debug.Log($"Instantiated {part.partId}, renderer: {renderer}, color: {part.color}");

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

            steps = JsonUtility.FromJson<LDrawStepListWrapper>(stepDataJson).steps;
        }
    }
}