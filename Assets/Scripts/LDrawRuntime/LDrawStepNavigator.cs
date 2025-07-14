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
        private Stack<List<GameObject>> hiddenMeshStack = new Stack<List<GameObject>>();
        private List<GameObject> visibleMeshes = new List<GameObject>();
        private List<List<GameObject>> stepObjects = new List<List<GameObject>>();

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
            // Hide all currently active objects
            foreach (var go in visibleMeshes)
            {
                if (go != null)
                    go.SetActive(false);
            }
            visibleMeshes.Clear();

            hiddenMeshStack.Clear();
            List<GameObject> activeObjects = new List<GameObject>();
            for (int i = 0; i <= stepIndex; i++)
            {
                var step = steps[i];
                // Ensure stepObjects is populated for this step
                while (stepObjects.Count <= i)
                {
                    var objs = new List<GameObject>();
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
                        {
                            var baseMat = Resources.Load<Material>("DefaultLDrawMaterial");
                            if (baseMat != null)
                            {
                                renderer.material = new Material(baseMat);
                                renderer.material.color = part.color;
                            }
                        }
                        objs.Add(go);
                    }
                    stepObjects.Add(objs);
                }
                foreach (var go in stepObjects[i])
                {
                    if (go != null)
                    {
                        go.SetActive(true);
                        activeObjects.Add(go);
                    }
                }
                if (hiddenMeshStack.Count > 0)
                {
                    var toRestore = hiddenMeshStack.Pop();
                    foreach (var go in toRestore)
                    {
                        if (go != null)
                        {
                            go.SetActive(true);
                            activeObjects.Add(go);
                        }
                    }
                }
            }
            visibleMeshes = activeObjects;
            currentStepIndex = stepIndex;
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