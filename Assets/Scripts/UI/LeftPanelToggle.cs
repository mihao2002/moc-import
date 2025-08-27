using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using LDraw.Runtime;
using TMPro;
using System;
using System.Collections.Generic;

public class LeftPanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public GameObject partDetail;
    public GameObject expander;
    public Camera previewCamera;
    public TMP_Text partId;
    public TMP_Text partColor;
    public TMP_Text partDescriptions;
    public GameObject gridItemPrefab;  // The prefab for each item
    public Transform gridParent;       // The container with GridLayoutGroup

    public CanvasScaler canvasScaler;

    private bool isExpanded = false;
    public float animationDuration = 0.2f;

    // private GameObject previewPart = null;
    private LDrawCamera ldrawCamera;
    private InputHandler inputHandler;
    private int selectedItem = 0;
    private List<PartGridItem> items = new List<PartGridItem>();
    private Vector2 screenSize;

    void Awake()
    {
        partDetail.SetActive(false);
        expander.SetActive(false);

        previewCamera.aspect = 1;
        ldrawCamera = new LDrawCamera(previewCamera, false);
        inputHandler = new InputHandler(ldrawCamera);
        screenSize = UIManager.GetScreenSize(canvasScaler);
    }

    // private void DestoryPreviewPart()
    // {
    //     if (previewPart != null)
    //     {
    //         Destroy(previewPart);
    //         previewPart = null;
    //     }
    // }

    public void PreviewItem(string id, string desc, string colorName, GameObject previewPart)
    {
        // DestoryPreviewPart();

        // this.previewPart = previewPart;

        Bounds bounds = previewPart.GetComponent<Renderer>().bounds;
        float radius = bounds.extents.magnitude;
        var rotation = LDrawCamera.DefaultRotation;

        ldrawCamera.SetCamera(bounds.center, radius, rotation);
        partId.text = id;
        partColor.text = colorName;
        partDescriptions.text = desc;
    }

    private void SetSelectedItem(int index)
    {
        if (selectedItem >= 0 && selectedItem < items.Count)
        {
            items[selectedItem].Deselect();
            (items[selectedItem].Context as ItemContext).Go.SetActive(false);
        }

        selectedItem = index;
        if (selectedItem >= 0 && selectedItem < items.Count)
        {
            items[selectedItem].Select();
            var context = items[selectedItem].Context as ItemContext;
            // GameObject clone = Instantiate(context.Go);

            int previewLayer = LayerMask.NameToLayer(Consts.PreviewLayerName);
            context.Go.layer = previewLayer;
            context.Go.SetActive(true);

            // Optional: reset local transforms
            context.Go.transform.position = Vector3.zero;
            context.Go.transform.rotation = Quaternion.identity;    
            
            PreviewItem(context.PartId, context.Description, context.ColorName, context.Go);
        }        
    }

    public void SelectItem(int selectedItem = 0, bool shrinkable = true)
    {
        if (isExpanded)
        {
            SetSelectedItem(selectedItem);
        }
        else
        {
            StartCoroutine(AnimateWidth(sidePanel, screenSize.x, true, selectedItem, shrinkable, null));
            isExpanded = true;
        }
        
    }

    public void Shrink(Action action)
    {
        // DestoryPreviewPart();

        RectTransform rt = gridParent.GetComponent<RectTransform>();
        var right = rt.localPosition.x + rt.rect.width;
        StartCoroutine(AnimateWidth(sidePanel, right, false, -1, false, action));
        isExpanded = false;
    }

    public class ItemContext
    {
        public ItemContext(GameObject go, string partId, string description, string colorName)
        {
            Go = go;
            PartId = partId;
            Description = description;
            ColorName = colorName;
        }

        public GameObject Go { get; set; }
        public string PartId { get; set; }
        public string Description { get; set; }

        public string ColorName { get; set; }
    }

    public void AddItem(Sprite icon, string label, ItemContext context)
    {
        // Create new item under the parent
        GameObject gridGo = Instantiate(gridItemPrefab, gridParent);

        // Get the UI script from the prefab
        PartGridItem itemUI = gridGo.GetComponent<PartGridItem>();
        int index = items.Count;
        items.Add(itemUI);

        itemUI.SetContent(icon, label, () =>
            {
                SelectItem(index, true);
            }, context);
    }

    private IEnumerator AnimateWidth(RectTransform panel, float targetWidth, bool expand, int selectedItem, bool shinkable, Action action)
    {
        SetSelectedItem(selectedItem);
        if (!expand)
        {
            partDetail.SetActive(false);
        }

        float elapsed = 0f;
        float startWidth = sidePanel.sizeDelta.x;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float width = Mathf.Lerp(startWidth, targetWidth, elapsed / animationDuration);
            panel.sizeDelta = new Vector2(width, sidePanel.sizeDelta.y);
            yield return null;
        }

        panel.sizeDelta = new Vector2(targetWidth, sidePanel.sizeDelta.y);

        if (expand)
        {
            partDetail.SetActive(true);
        }

        expander.SetActive(expand && shinkable);
        action?.Invoke();
    }

    public void ClearGrid()
    {
        foreach (var item in items)
        {
            Destroy((item.Context as ItemContext).Go);      
        }

        items.Clear();
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }
    }

    public void ShrinkPanel()
    {
        Shrink(null);
    }

    public void Update()
    {
        if (isExpanded)
        {
            inputHandler.HandleInput();
        }
    }
}
