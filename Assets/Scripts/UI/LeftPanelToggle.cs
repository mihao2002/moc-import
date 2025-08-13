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
    public Camera cam;
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

    private Color selectedColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private float panelWidth = 300f;  // Width when panel is collapsed
    private bool isExpanded = false;
    public float animationDuration = 0.2f;
    private float columnWidth;
    private float columnSpacing;
    private int padding;
    private int fixRowCount;
    private Color buttonColor;

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

        // columnWidth = grid.cellSize.x;
        // columnSpacing = grid.spacing.x;
        // padding = grid.padding.left + grid.padding.right;
        fixRowCount = grid.constraintCount;

        // buttonColor = button.colors.normalColor;
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

        // if (!isExpanded)
        // {
        //     TogglePanel();
        // }
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
            GameObject clone = Instantiate(items[this.selectedItem].go);

            int previewLayer = LayerMask.NameToLayer("Preview");
            clone.layer = previewLayer;
            clone.SetActive(true);

            // Optional: reset local transforms
            clone.transform.position = Vector3.zero;
            clone.transform.rotation = Quaternion.identity;
            
            PreviewItem(items[this.selectedItem].partId, items[this.selectedItem].description, items[this.selectedItem].colorName, clone);

        }        
    }

    public void Expand(int selectedItem = 0, bool shrinkable = true)
    {
        if (isExpanded)
        {
            SetSelectedItem(selectedItem);
        }
        else
        {
            TogglePanel(true, selectedItem, shrinkable);
        }
        
    }

    public void SetItemCount(int itemCount)
    {
        var columnCount = Mathf.CeilToInt(itemCount / (float)fixRowCount);

        panelWidth = columnCount == 0 ? 0 : columnWidth * columnCount + columnSpacing * (columnCount - 1) + padding * 2;
        float viewportX = panelWidth / Screen.width;
        float viewportWidth = 1.0f - viewportX;
        cam.rect = new Rect(viewportX, 0, viewportWidth, 1);

        partDetail.GetComponent<RectTransform>().offsetMin = new Vector2(panelWidth + columnWidth, 0);
    }

    public void ShrinkPanel()
    {
        Shrink(null);
    }

    public void Shrink(Action action)
    {
        TogglePanel(false, -1, false, action);
    }

    // Call this method from the Button onClick event
    public void TogglePanel(bool expand, int selectedItem, bool shinkable, Action action = null)
    {
        // isExpanded = !isExpanded;

        if (!expand)
        {
            DestoryPreviewPart();
        }

        float targetWidth = expand ? Screen.width : panelWidth;

        // Animate width change (optional)
        StartCoroutine(AnimateWidth(sidePanel, targetWidth, expand, selectedItem, shinkable, action));
        isExpanded = expand;

        // Rotate the arrow 180 degrees around Z to flip it
        // arrowImage.localEulerAngles = isExpanded ? new Vector3(0, 0, 90) : new Vector3(0, 0, -90);
        // SetButtonColor(isExpanded ? selectedColor : buttonColor);
    }

    public void AddItem(Sprite icon, string label, string partId, string description, string colorName, GameObject go)
    {
        // Create new item under the parent
        GameObject obj = Instantiate(gridItemPrefab, gridParent);

        // Get the UI script from the prefab
        PartGridItem itemUI = obj.GetComponent<PartGridItem>();
        int index = items.Count;
        items.Add(itemUI);

        // var go = navigator.GetPartFromCurrentStep(index);
        itemUI.SetContent(icon, label, go, ()=>
            {
                Expand(index, true);
            }, partId, description, colorName);
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

    private void HandleInput()
    {
        if (isExpanded)
        {
            inputHandler.HandleInput();
        }        
    }

    public void ClearGrid()
    {
        items.Clear();
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }
    }

    public void Update()
    {
        HandleInput();
    }
}
