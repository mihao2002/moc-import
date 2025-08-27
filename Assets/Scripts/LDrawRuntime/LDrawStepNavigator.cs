using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using UnityEngine.SceneManagement;

namespace LDraw.Runtime
{
    public class LDrawStepNavigator : MonoBehaviour
    {
        public Transform parentContainer; // Where to spawn parts in the scene
        public TMP_Text stepNumberText;
        public Camera mainCamera; // Assign in inspector

        public LeftPanelToggle leftPaneToggle;
        public BottomPanelToggle bottomPaneToggle;
        private LDrawCamera cam;

        // private bool suppressSliderCallback = false;
        private Dictionary<string, Sprite> partSpriteDict;
        private Sprite[] stepSprites;

        private LDrawFlatStepNavigator navigator;
        private LDrawStepManager stepManager;
        private InputHandler inputHandler;
        private Dictionary<int, LDrawColor> colors;
        private Dictionary<string, LDrawPartDesc> partDescriptions;
        private HashSet<string> modelNames;
        private Material mainMaterial;

        private bool showParts = true;
        private int partListStep = -1;
        private int currentStep = 0;

        void Start()
        {
            // Load model step data from Resources
            var jsonAsset = Resources.Load<TextAsset>("LDrawStepData");
            if (jsonAsset == null)
            {
                Debug.LogError("LDrawStepData.json not found in Resources!");
                return;
            }
            var data = JsonConvert.DeserializeObject<StepPackage>(jsonAsset.text);
            var models = data.models;
            var jsonAsset2 = Resources.Load<TextAsset>("LDrawPartDescriptionData");
            if (jsonAsset2 == null)
            {
                Debug.LogError("LDrawPartDescriptionData.json not found in Resources!");
                return;
            }
            partDescriptions = JsonConvert.DeserializeObject<Dictionary<string, LDrawPartDesc>>(jsonAsset2.text);

            var jsonAsset3 = Resources.Load<TextAsset>("LDrawPartColorData");
            if (jsonAsset3 == null)
            {
                Debug.LogError("LDrawPartColorData.json not found in Resources!");
                return;
            }
            colors = JsonConvert.DeserializeObject<Dictionary<int, LDrawColor>>(jsonAsset3.text);

            modelNames = new HashSet<string>(models.Select(m => m.modelName));
            var flatSteps = data.flatSteps;

            var color = colors[16].color;
            string colorKey = $"Mat_{color.r:F3}_{color.g:F3}_{color.b:F3}";
            mainMaterial = Resources.Load<Material>($"LDrawMaterials/{colorKey}");

            PreInstantiateAllParts(models, colors); // Runtime-specific: instantiate from prefabs

            cam = new LDrawCamera(mainCamera, true);
            navigator = new LDrawFlatStepNavigator(models, cam, flatSteps);
            stepManager = new LDrawStepManager(models, flatSteps);
            inputHandler = new InputHandler(cam);

            partSpriteDict = LoadPartSprites();
            stepSprites = LoadStepSprites();

            Load();
            showParts = stepManager.GetStepParts(currentStep).Count > 0;

            PopulateSteps();
            UpdateStepText();
            ShowCurrentStep();
        }

        private void ShowCurrentStep(bool userClick=false)
        {
            if (partListStep != currentStep)
            {
                PopulateStepParts();
            }

            if (showParts)
            {
                leftPaneToggle.SelectItem(0, false);
            }
            else
            {
                // if the current step is for a new model, then hide the current model,
                // otherwise animate the model.
                var model = stepManager.GetModel(currentStep);
                if (navigator.CurrentModel != model)
                {
                    navigator.HideCurrentModel();
                }

                leftPaneToggle.Shrink(() =>
                {
                    navigator.GotoStep(currentStep, true);
                });
            }

            bottomPaneToggle.SetSelectedItem(currentStep, !userClick);
            UpdateStepText();
        }

        private static Sprite[] LoadStepSprites()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>("LDrawStepImages");
            var result = new Sprite[sprites.Length];
            foreach (var sprite in sprites)
            {
                if (int.TryParse(sprite.name, out int idx))
                {
                    result[idx] = sprite;
                }
                else
                {
                    Debug.LogError($"Invalid step image {sprite.name}");
                }
            }

            return result;
        }

        /// <summary>
        /// Loads all sprites from Resources/LDrawImages folder into nested dictionary [subfolder][filename] = Sprite
        /// Assumes sprites are imported in Resources/LDrawImages and subfolders.
        /// </summary>
        private static Dictionary<string, Sprite> LoadPartSprites()
        {
            var result = new Dictionary<string, Sprite>();

            Sprite[] sprites = Resources.LoadAll<Sprite>("LDrawImages");
            foreach (var sprite in sprites)
            {
                result[sprite.name] = sprite;
            }

            return result;
        }

        private void PopulateStepParts()
        {
            //Dictionary<LDrawPartCore, int>
            var parts = stepManager.GetStepParts(currentStep);
            var partInfo = new Dictionary<Sprite, (string, LeftPanelToggle.ItemContext)>();
            foreach (var kvp in parts)
            {
                var part = kvp.Key;
                var isModel = modelNames.Contains(part.partId);
                string spriteKey = $"{part.partId.Replace('\\', '_')}";
                string colorName = null;
                string id = null;
                if (!isModel)
                {
                    var color = colors[part.color];
                    colorName = color.name;
                    id = Path.GetFileNameWithoutExtension(part.partId);
                    spriteKey = $"Mat_{color.color.r:F3}_{color.color.g:F3}_{color.color.b:F3}_{spriteKey}";
                }

                if (partSpriteDict.ContainsKey(spriteKey))
                {
                    var idx = partInfo.Count;
                    var sprite = partSpriteDict[spriteKey];
                    string description = null;
                    if (partDescriptions.ContainsKey(part.partId))
                    {
                        var desc = partDescriptions[part.partId];
                        id = desc.id ?? id;
                        description = desc.description;
                    }

                    var go = stepManager.GetPartFromStep(currentStep, idx);
                    var context = new LeftPanelToggle.ItemContext(go, id, description, colorName);

                    partInfo[sprite] = (kvp.Value.ToString(), context);
                }
                else
                {
                    Debug.LogError($"Can't find part image for {spriteKey}");
                }
            }

            leftPaneToggle.ClearGrid();
            foreach (var kvp in partInfo)
            {
                leftPaneToggle.AddItem(kvp.Key, kvp.Value.Item1, kvp.Value.Item2);
            }

            partListStep = currentStep;
        }

        private bool CanNavigate
        {
            get
            {
                return navigator != null && navigator.CanNavigate;
            }
        }

        private void HandleInput()
        {
// #if UNITY_EDITOR || UNITY_STANDALONE
//             if (cam == null || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
//             {
//                 return;
//             }
// #endif

            inputHandler.HandleInput();
        }

        public void Update()
        {
            if (CanNavigate)
            {
                HandleInput();
            }
        }

        public void ShowNextStep()
        {
            if (CanNavigate)
            {
                if (showParts)
                {
                    showParts = false;
                }
                else
                {
                    if (currentStep < stepManager.TotalStep - 1)
                    {
                        currentStep++;
                        showParts = stepManager.GetStepParts(currentStep).Count > 0;
                        Save();
                    }
                }

                ShowCurrentStep();
            }

        }

        public void ShowPreviousStep()
        {
            if (CanNavigate)
            {
                if (!showParts && stepManager.GetStepParts(currentStep).Count > 0)
                {
                    showParts = true;
                }
                else
                {
                    if (currentStep > 0)
                    {
                        currentStep--;
                        showParts = false;
                        Save();
                    }
                }

                ShowCurrentStep();
            }
        }

        private void UpdateStepText()
        {
            stepNumberText.text = $"{currentStep + 1}";
        }

        // Runtime-specific method to instantiate all parts from prefabs
        private void PreInstantiateAllParts(List<RuntimeModelData> models, Dictionary<int, LDrawColor> colors)
        {
            foreach (var modelData in models)
            {
                var modelContainer = new ModelContainer(modelData.modelName);
                for (var i=0;i<modelData.steps.Count;i++)                
                {
                    var step = modelData.steps[i];
                    var stepContainer = new StepContainer(step, parentContainer, modelNames,
                        colors, mainMaterial, modelContainer.ModelContainerGo, $"{modelData.modelName}_{i+1}");
                    modelContainer.AddStep(stepContainer);
                }
                modelData.container = modelContainer;
            }
        }

        private void PopulateSteps()
        {
            for (var i = 0; i < stepSprites.Length; i++)
            {
                int stepIdx = i;
                bottomPaneToggle.AddItem(stepSprites[stepIdx], $"{stepIdx+1}", () =>
                {
                    currentStep = stepIdx;
                    showParts = stepManager.GetStepParts(stepIdx).Count > 0; ;
                    Save();
                    ShowCurrentStep(true);
                });
            }
        }

        public void Back()
        {
            SceneManager.LoadScene("Home");
        }

        private void Load()
        {
            currentStep = PlayerPrefs.GetInt("CurrentStep", 0);
            currentStep = Mathf.Clamp(currentStep, 0, stepManager.TotalStep - 1);
        }

        private void Save()
        {
            PlayerPrefs.SetInt("CurrentStep", currentStep);
            PlayerPrefs.SetFloat("BuildProgress", (currentStep + 1f) / stepManager.TotalStep);
            PlayerPrefs.Save(); // Force save to disk
        }
    }
}