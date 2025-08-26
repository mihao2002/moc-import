using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Mathematics;

public class BottomPanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public RectTransform arrowImage;     // The arrow image inside the button
    public Slider slider;
    public GameObject stepPrefab;
    public Transform stepListParent;
    public RectTransform scrollViewer;

    public CanvasScaler canvasScaler;

    public Button expander;
    
    // private float paneHeight = 400f;
    private float itemSize = 80f;

    private bool isExpanded = false;
    public float animationDuration = 0.2f;

    private int selectedItem = -1;
    private List<PartGridItem> items = new List<PartGridItem>();

    void Awake()
    {
        var screenSize = UIManager.GetScreenSize(canvasScaler);      
        itemSize = math.min(512f, screenSize.y / 3.0f);

        // Change the height (y of sizeDelta)
        Vector2 size = scrollViewer.sizeDelta;
        size.y = itemSize;  // set desired height
        scrollViewer.sizeDelta = size;  
    }

    public void SetSelectedItem(int index, bool syncSlider)
    {
        if (this.selectedItem >= 0 && this.selectedItem < items.Count)
        {
            items[this.selectedItem].Deselect();
        }

        this.selectedItem = index;
        if (this.selectedItem >= 0 && this.selectedItem < items.Count)
        {
            items[this.selectedItem].Select();
            if (syncSlider) slider.value = (float)index/(items.Count-1);
        }        
    }
    
    public void ShowItem(int index, bool show)
    {
        items[index].gameObject.SetActive(show);
    }

    public void AddItem(Sprite sprite, string text, Action action)
    {
        // Create new item under the parent
        GameObject obj = Instantiate(stepPrefab, stepListParent);
        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(itemSize, itemSize);
        PartGridItem itemUI = obj.GetComponent<PartGridItem>();

        items.Add(itemUI);

        itemUI.SetContent(sprite, text, action);
    }

    // Call this method from the Button onClick event
    public void TogglePanel()
    {
        isExpanded = !isExpanded;

        var paneHeight = sidePanel.rect.height;
        float targetY = isExpanded ? paneHeight : 0;

        // Animate width change (optional)
        StartCoroutine(AnimateHeight(sidePanel, targetY, isExpanded));

    }

    private IEnumerator AnimateHeight(RectTransform panel, float targetY, bool isExpanded)
    {
        float elapsed = 0f;
        float startY = sidePanel.anchoredPosition.y;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float offsetY = Mathf.Lerp(startY, targetY, elapsed / animationDuration);
            panel.anchoredPosition = new Vector2(sidePanel.anchoredPosition.x, offsetY);
            yield return null;
        }

        panel.anchoredPosition = new Vector2(sidePanel.anchoredPosition.x, targetY);

        // Rotate the arrow 180 degrees around Z to flip it
        arrowImage.localEulerAngles = isExpanded ? new Vector3(0, 0, 180) : new Vector3(0, 0, 0);
        // Get the current colors
        ColorBlock cb = expander.colors;
        // Change the normal color
        cb.normalColor = isExpanded ? Color.gray3 : Color.white;
        // Assign it back
        expander.colors = cb;
    }
}
