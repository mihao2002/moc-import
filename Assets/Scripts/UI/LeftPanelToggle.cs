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
    // public RectTransform arrowImage;     // The arrow image inside the button
    public Camera mainCamera;
    public GridLayoutGroup grid;
    public GameObject partDetail;
    public GameObject expander;
    public Camera previewCamera;
    public TMP_Text partId;
    public TMP_Text partColor;
    public TMP_Text partDescriptions;
    public RectTransform partImageContainer;
    public RectTransform partInfo;
    public GameObject gridItemPrefab;  // The prefab for each item
    public Transform gridParent;       // The container with GridLayoutGroup

    private float panelWidth = 300f;  // Width when panel is collapsed
    private bool isExpanded = false;
    public float animationDuration = 0.2f;
    private float columnWidth;
    private float columnSpacing;
    private int padding;
    private int fixRowCount;

    private GameObject previewPart = null;
    private LDrawCamera ldrawCamera;
    private InputHandler inputHandler;
    private int selectedItem = 0;
    private List<PartGridItem> items = new List<PartGridItem>();

    void Awake()
    {
        var uiManager = UIManager.Instance;

        grid.cellSize = new Vector2(uiManager.BaseUnit, uiManager.BaseUnit);
        columnWidth = uiManager.BaseUnit;
        columnSpacing = 0;
        padding = uiManager.Padding;
        grid.constraintCount = uiManager.FixRowCount;
        grid.padding = new RectOffset(padding, padding, uiManager.BaseUnit, 0);

        partImageContainer.offsetMin = new Vector2(partImageContainer.offsetMin.x, uiManager.BaseUnit);
        partInfo.offsetMax = new Vector2(partInfo.offsetMax.x, uiManager.BaseUnit);

        fixRowCount = grid.constraintCount;

        partDetail.SetActive(false);
        expander.SetActive(false);

        previewCamera.aspect = 1;
        ldrawCamera = new LDrawCamera(previewCamera, false);
        inputHandler = new InputHandler(ldrawCamera);
    }

    private void DestoryPreviewPart()
    {
        if (this.previewPart != null)
        {
            Destroy(this.previewPart);
            this.previewPart = null;
        }
    }

    public void PreviewItem(string id, string desc, string colorName, GameObject previewPart)
    {
        DestoryPreviewPart();

        this.previewPart = previewPart;

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
        if (this.selectedItem >= 0 && this.selectedItem < items.Count)
        {
            items[this.selectedItem].Deselect();
        }

        this.selectedItem = index;
        if (this.selectedItem >= 0 && this.selectedItem < items.Count)
        {
            items[this.selectedItem].Select();
            var context = items[this.selectedItem].Context as ItemContext;
            GameObject clone = Instantiate(context.Go);

            int previewLayer = LayerMask.NameToLayer(Consts.PreviewLayerName);
            clone.layer = previewLayer;
            clone.SetActive(true);

            // Optional: reset local transforms
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;    
            
            PreviewItem(context.PartId, context.Description, context.ColorName, clone);
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
            StartCoroutine(AnimateWidth(sidePanel, Screen.width, true, selectedItem, shrinkable, null));
            isExpanded = true;
        }
        
    }

    public void Shrink(Action action)
    {
        DestoryPreviewPart();
        StartCoroutine(AnimateWidth(sidePanel, panelWidth, false, -1, false, action));
        isExpanded = false;
    }

    public void SetItemCount(int itemCount)
    {
        var columnCount = Mathf.CeilToInt(itemCount / (float)fixRowCount);

        panelWidth = columnCount == 0 ? 0 : columnWidth * columnCount + columnSpacing * (columnCount - 1) + padding * 2;
        float viewportX = panelWidth / Screen.width;
        float viewportWidth = 1.0f - viewportX;
        mainCamera.rect = new Rect(viewportX, 0, viewportWidth, 1);

        partDetail.GetComponent<RectTransform>().offsetMin = new Vector2(panelWidth + columnWidth, 0);
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
