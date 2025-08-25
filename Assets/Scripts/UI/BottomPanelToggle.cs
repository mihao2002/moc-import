using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UI;
using System.Collections.Generic;

public class BottomPanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public RectTransform arrowImage;     // The arrow image inside the button
    public RectTransform previous;
    public RectTransform next;
    public RectTransform stepNumber;
    public RectTransform expander;
    public RectTransform sliderTrans;
    public Slider slider;
    public GameObject stepPrefab;
    public Transform stepListParent;
    
    private float paneHeight = 400f;
    private float itemSize = 80f;

    private bool isExpanded = false;
    public float animationDuration = 0.2f;

    private int selectedItem = -1;
    private List<PartGridItem> items = new List<PartGridItem>();
    
    void Awake()
    {
        var uiManager = UIManager.Instance;

        paneHeight = uiManager.BaseUnit * 3;        
        itemSize = uiManager.BaseUnit*2;

        // paneHeight = Screen.height / 3.0f;
        sidePanel.sizeDelta = new Vector2(sidePanel.sizeDelta.x, paneHeight);
        previous.sizeDelta = next.sizeDelta = new Vector2(uiManager.BaseUnit, uiManager.BaseUnit);
        expander.sizeDelta = new Vector2(uiManager.BaseUnit, uiManager.BaseUnit/2);

        var padding = uiManager.Padding;
        previous.anchoredPosition = new Vector2(padding, previous.anchoredPosition.y);
        next.anchoredPosition = new Vector2(-padding, next.anchoredPosition.y);
        stepNumber.anchoredPosition = new Vector2(-padding, stepNumber.anchoredPosition.y);

        // Set left gap to 20
        sliderTrans.offsetMin = new Vector2(uiManager.BaseUnit, sliderTrans.offsetMin.y);
        sliderTrans.offsetMax = new Vector2(-uiManager.BaseUnit, sliderTrans.offsetMax.y);

        Hide();
    }

    public void SetSelectedItem(int index)
    {
        if (this.selectedItem >= 0 && this.selectedItem < items.Count)
        {
            items[this.selectedItem].Deselect();
        }

        this.selectedItem = index;
        if (this.selectedItem >= 0 && this.selectedItem < items.Count)
        {
            items[this.selectedItem].Select();
            slider.value = (float)index/(items.Count-1);
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

    void Hide()
    {
        sidePanel.anchoredPosition = new Vector2(sidePanel.anchoredPosition.x, -paneHeight);
      
        // Rotate the arrow 180 degrees around Z to flip it
        arrowImage.localEulerAngles = new Vector3(0, 0, 0);
    }

    // Call this method from the Button onClick event
    public void TogglePanel()
    {
        isExpanded = !isExpanded;

        float targetY = isExpanded ? 0f : -paneHeight;

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
    }
}
