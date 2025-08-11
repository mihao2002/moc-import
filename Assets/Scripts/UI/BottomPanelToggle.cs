using UnityEngine;
using System.Collections;
using System;

public class BottomPanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public RectTransform arrowImage;     // The arrow image inside the button
    public RectTransform previous;
    public RectTransform next;
    public RectTransform stepNumber;
    public RectTransform expander;
    public RectTransform slider;
    public GameObject stepPrefab;
    public Transform stepListParent;
    
    private float paneHeight = 400f;
    private float itemSize = 80f;

    private bool isExpanded = false;
    public float animationDuration = 0.2f;
    
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
        slider.offsetMin = new Vector2(uiManager.BaseUnit, slider.offsetMin.y);
        slider.offsetMax = new Vector2(-uiManager.BaseUnit, slider.offsetMax.y);

        Hide();
    }

    public void AddStep(Sprite sprite, int step, Action action)
    {
        // Create new item under the parent
        GameObject obj = Instantiate(stepPrefab, stepListParent);
        var rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(itemSize, itemSize);
        PartGridItem itemUI = obj.GetComponent<PartGridItem>();

        itemUI.SetContent(sprite, $"{step+1}", action);
    }

    void Hide()
    {
        sidePanel.anchoredPosition = new Vector2(sidePanel.anchoredPosition.x, -paneHeight);
      
        // Rotate the arrow 180 degrees around Z to flip it
        arrowImage.localEulerAngles = new Vector3(0, 0, 0);
        // arrowImage.anchoredPosition = new Vector2(arrowImage.anchoredPosition.x, buttonHalfHeight);
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
        // arrowImage.anchoredPosition = new Vector2(arrowImage.anchoredPosition.x, isExpanded ? -buttonHalfHeight : buttonHalfHeight);
    }
}
