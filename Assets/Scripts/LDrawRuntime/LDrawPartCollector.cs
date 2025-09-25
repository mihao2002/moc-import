using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Newtonsoft.Json;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.Xml;
using System.Xml.Linq;
using UnityEngine.AI;
using System.Runtime.InteropServices;
using Sych.ShareAssets.Runtime;
using System.Globalization;

namespace LDraw.Runtime
{
    public class LDrawPartCollector : MonoBehaviour
    {
        public Transform parentContainer; // Where to spawn parts in the scene
        public TMP_Text partCountText;
        public TMP_Text thisPartCountText;

        public TMP_Text partIdText;
        public TMP_Text partColorText;
        public TMP_Text partDescText;

        public GameObject partDetail;
        public TMP_Text noPartText;

        public Sprite intoBasketSprite;
        public Sprite outofBasketSprite;

        public Sprite findPartSprite;
        public Sprite ownPartSprite;

        public Image typeImage;
        public Image actionImage;

        public Camera previewCamera; // Assign in inspector

        // public LeftPanelToggle leftPaneToggle;
        public BottomPanelToggle bottomPaneToggle;
        // public GameObject stepPrefab;
        // public Transform stepListParent;

        public GameObject orderPane;
        public TMP_Text orderDescription;
        public TMP_InputField fileName;

        private LDrawCamera cam;

        // private bool suppressSliderCallback = false;
        private Dictionary<string, Sprite> partSpriteDict;

        private InputHandler inputHandler;
        private Dictionary<int, LDrawColor> colors;
        private Dictionary<string, LDrawPartDesc> partDescriptions;
        
        private List<LDrawPartCount> partCounts;
        private bool[] partCollectionStatus;
        private int totalCollectedCount;
        private int totalCount;
        private List<GameObject> partObjects;
        private int currentPart;
        private bool showCollected;
        private List<int> partInLoop;
        private int currentActivePart = -1;

        private Material mainMaterial;

        void Start()
        {
            // Load model step data from Resources
            var jsonAsset = Resources.Load<TextAsset>("LDrawPartCountData");
            if (jsonAsset == null)
            {
                Debug.LogError("LDrawPartCountData.json not found in Resources!");
                return;
            }
            partCounts = JsonConvert.DeserializeObject<List<LDrawPartCount>>(jsonAsset.text);
            // var models = data.models;
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

            var color = colors[16].color;
            mainMaterial = LDrawUtlity.LoadMaterial(color);
            partObjects = new List<GameObject>();
            currentPart = -1;
            showCollected = false;

            PreInstantiateAllParts(partCounts, colors); // Runtime-specific: instantiate from prefabs

            previewCamera.aspect = 1;
            cam = new LDrawCamera(previewCamera, false);
            inputHandler = new InputHandler(cam);

            partSpriteDict = LoadPartSprites();
            orderPane.SetActive(false);

            totalCount = partCounts.Select(c => c.count).Sum();
            partCollectionStatus = new bool[partCounts.Count];
            totalCollectedCount = 0;
            Load();

            partInLoop = new List<int>();
            PopulateParts();
            UpdateCountText();
            UpdatePartList();

            UpdateImages();

            SetSelectedItem(0);
        }

        private void UpdateImages()
        {
            if (showCollected)
            {
                typeImage.sprite = ownPartSprite;
                actionImage.sprite = outofBasketSprite;
            }
            else
            {
                typeImage.sprite = findPartSprite;
                actionImage.sprite = intoBasketSprite;
            }
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


        private void HandleInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // if (cam == null || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            // {
            //     Debug.LogError("IsPointerOverGameObject");
            //     return;
            // }
#endif

            inputHandler.HandleInput();
        }

        private void UpdateCountText()
        {
            partCountText.text = $"{totalCollectedCount}";
        }

        public void Update()
        {
            HandleInput();
        }

        public void ShowNextPart()
        {
            if (currentPart < partInLoop.Count - 1)
            {
                SetSelectedItem(currentPart+1);
            }
        }

        public void ShowPreviousPart()
        {
            if (currentPart > 0)
            {
                SetSelectedItem(currentPart-1);
            }
        }

        // Runtime-specific method to instantiate all parts from prefabs
        private void PreInstantiateAllParts(List<LDrawPartCount> partCounts, Dictionary<int, LDrawColor> colors)
        {
            foreach (var partCount in partCounts)
            {
                var part = partCount.part;
                var fileName = part.partId.Replace('\\', '_');
                GameObject prefab = LDrawUtlity.LoadPrefab(fileName);
                if (prefab == null)
                {
                    Debug.LogWarning($"Missing prefab for part: {part.partId}");
                    continue;
                }
                GameObject go = Instantiate(prefab, parentContainer);

                // Regular part: ensure it has a renderer, assign material asset if found
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null)
                    renderer = go.AddComponent<MeshRenderer>();
                var color = colors[part.color].color;
                var mat = LDrawUtlity.LoadMaterial(color);

                Material[] sharedMats = renderer.sharedMaterials;
                for (var i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i].color == mainMaterial.color)
                    {
                        sharedMats[i] = mat;
                        renderer.sharedMaterials = sharedMats;
                        break;
                    }
                }

                int previewLayer = LayerMask.NameToLayer(Consts.PreviewLayerName);
                go.layer = previewLayer;
                go.SetActive(false);

                partObjects.Add(go);
            }
        }

        private void PopulateParts()
        {
            for (var i = 0; i < partCounts.Count; i++)
            {
                var partIdx = i;
                var part = partCounts[i].part;
                string spriteKey = $"{part.partId.Replace('\\', '_')}";
                string colorName = null;
                string id = null;
                var color = colors[part.color];
                colorName = color.name;
                id = Path.GetFileNameWithoutExtension(part.partId);
                spriteKey = string.Format(CultureInfo.InvariantCulture, "Mat_{0:F3}_{1:F3}_{2:F3}_{3}",
                        color.color.r, color.color.g, color.color.b, spriteKey);

                bottomPaneToggle.AddItem(partSpriteDict[spriteKey], $"x{partCounts[i].count}", () =>
                {
                    SetSelectedItem(partInLoop.IndexOf(partIdx), true);
                });
            }
        }

        private void UpdatePartList()
        {
            partInLoop.Clear();
            for (var i = 0; i < partCollectionStatus.Length; i++)
            {
                var show = showCollected ^ !partCollectionStatus[i];
                bottomPaneToggle.ShowItem(i, show);
                if (show)
                    partInLoop.Add(i);
            }
        }

        private void SetSelectedItem(int index, bool userClick = false)
        {
            if (currentActivePart >= 0)
            {
                partObjects[currentActivePart].SetActive(false);
                currentActivePart = -1;
            }

            currentPart = index;
            if (currentPart < 0) currentPart = 0;
            if (currentPart >= partInLoop.Count) currentPart = partInLoop.Count - 1;

            if (currentPart >= 0 && currentPart < partInLoop.Count)
            {
                noPartText.gameObject.SetActive(false);
                partDetail.SetActive(true);
                thisPartCountText.gameObject.SetActive(true);

                var partIdx = partInLoop[currentPart];

                var partObj = partObjects[partIdx];
                partObj.SetActive(true);
                currentActivePart = partIdx;

                // Optional: reset local transforms
                partObj.transform.position = Vector3.zero;
                partObj.transform.rotation = Quaternion.identity;

                var part = partCounts[partIdx].part;
                var partId = partDescriptions.ContainsKey(part.partId) && partDescriptions[part.partId].id != null
                    ? partDescriptions[part.partId].id
                    : part.partId;

                PreviewItem(partId, partDescriptions[part.partId].description, colors[part.color].name, partObj, partCounts[partIdx].count);
                bottomPaneToggle.SetSelectedItem(partIdx, !userClick);
            }
            else
            {
                currentPart = -1;
                noPartText.text = showCollected ? "You haven't collected any part." : "You have collected all parts!";
                noPartText.gameObject.SetActive(true);
                partDetail.SetActive(false);
                thisPartCountText.gameObject.SetActive(false);
            }
            // }        
        }

        public void SwithMode()
        {
            showCollected = !showCollected;
            UpdatePartList();
            UpdateCountText();
            SetSelectedItem(0);
            UpdateImages();
        }

        private void PreviewItem(string id, string desc, string colorName, GameObject previewPart, int count)
        {
            Bounds bounds = previewPart.GetComponent<Renderer>().bounds;
            float radius = bounds.extents.magnitude;
            var rotation = LDrawCamera.DefaultRotation;

            cam.SetCamera(bounds.center, radius, rotation);
            partIdText.text = id.EndsWith(".dat") ? id.Substring(0, id.Length-4) : id;
            partColorText.text = colorName;
            partDescText.text = desc;
            thisPartCountText.text = $"x{count}";
        }

        public void CollectCurrent()
        {
            if (currentPart >= 0 && currentPart < partInLoop.Count)
            {
                var partIdx = partInLoop[currentPart];
                partCollectionStatus[partIdx] = !showCollected;
                partInLoop.RemoveAt(currentPart);
                totalCollectedCount += showCollected ? -partCounts[partIdx].count : partCounts[partIdx].count;
                bottomPaneToggle.ShowItem(partIdx, false);
                UpdateCountText();
                Save();

                SetSelectedItem(currentPart);
            }
        }

        public void Back()
        {
            StartCoroutine(UIManager.LoadSceneDelayed("Home"));
        }

        public void Order()
        {
            orderPane.SetActive(true);
            orderDescription.text = $"You have {totalCollectedCount} parts.\nA file having {totalCount - totalCollectedCount} missing parts will be created for bricklink.com wanted list.";
            fileName.text = "partlist.xml";            
        }

        public void CancelOrder()
        {
            orderPane.SetActive(false);            
        }

        private XDocument GetUncollectedPartXml()
        {
            var root = new XElement("INVENTORY");
            for (var i = 0; i < partCounts.Count; i++)
            {
                if (!partCollectionStatus[i])
                {
                    var part = partCounts[i].part;
                    var partId = part.partId;
                    if (partDescriptions.ContainsKey(partId) && partDescriptions[partId].id != null)
                    {
                        partId = partDescriptions[partId].id;
                    }
                    else
                    {
                        partId = partId.Split('.')[0];
                    }
                    
                    var item = new XElement("ITEM",
                        new XElement("ITEMTYPE", "P"),
                        new XElement("ITEMID", partId),
                        new XElement("COLOR", colors[part.color].blColor),
                        new XElement("MINQTY", partCounts[i].count));
                    root.Add(item);                     
                }
            }

            return new XDocument(root);
        }

#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern void _ShowShareSheet(string filePath);
#endif

        public void SaveOrder()
        {
            string folder = Application.persistentDataPath;

            var baseName = fileName.text;
            var extension = ".xml";
            if (baseName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(0, baseName.Length - 4);
            }

            string path = Path.Combine(folder, baseName + extension);
            int counter = 2;

            // Find available filename
            while (File.Exists(path))
            {
                string newName = $"{baseName} ({counter++})";
                path = Path.Combine(folder, newName + extension);
            }

            // Write content to file
            var doc = GetUncollectedPartXml();
            //File.WriteAllText(path, xml);
            doc.Save(path);

            Debug.Log("Saved at: " + path);
            orderPane.SetActive(false);

#if UNITY_IOS && !UNITY_EDITOR
            _ShowShareSheet(path);
#elif UNITY_ANDROID && !UNITY_EDITOR
            var items = new List<string>{path};
            Share.Items(items, success => {});
#endif
        }

        private void Load()
        {
            if (PlayerPrefs.HasKey("CollectionStatus"))
            {
                //00100101
                var statusString = PlayerPrefs.GetString("CollectionStatus");
                var statuses = statusString.ToCharArray();
                var collectedCount = 0;
                if (partCollectionStatus.Length == statuses.Length)
                {
                    for (var i = 0; i < statuses.Length; i++)
                    {
                        partCollectionStatus[i] = statuses[i] == '1';
                        if (partCollectionStatus[i])
                        {
                            collectedCount += partCounts[i].count;
                        }

                    }

                    totalCollectedCount = collectedCount;
                }
            }
        }

        private void Save()
        {
            var statuses = partCollectionStatus.Select(s => s ? '1' : '0').ToArray();
            PlayerPrefs.SetString("CollectionStatus", new string(statuses));
            PlayerPrefs.SetFloat("CollectProgress", ((float)totalCollectedCount)/totalCount);            
        }
    }
}