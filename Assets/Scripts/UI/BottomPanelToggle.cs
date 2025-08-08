using UnityEngine;
using System.Collections;

public class BottomPanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public RectTransform arrowImage;     // The arrow image inside the button
    public float buttonHalfHeight = 40f;

    private float paneHeight = 300f;  // Width when panel is collapsed
    private bool isExpanded = false;
    public float animationDuration = 0.2f;
    

    void Start()
    {
        paneHeight = Screen.height / 3.0f;
        sidePanel.sizeDelta = new Vector2(sidePanel.sizeDelta.x, paneHeight);

        Hide();
    }

    void Hide()
    {
        sidePanel.anchoredPosition = new Vector2(sidePanel.anchoredPosition.x, -paneHeight);
      
        // Rotate the arrow 180 degrees around Z to flip it
        arrowImage.localEulerAngles = new Vector3(0, 0, 90);
        arrowImage.anchoredPosition = new Vector2(arrowImage.anchoredPosition.x, buttonHalfHeight);
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
        arrowImage.localEulerAngles = isExpanded ? new Vector3(0, 0, -90) : new Vector3(0, 0, 90);
        arrowImage.anchoredPosition = new Vector2(arrowImage.anchoredPosition.x, isExpanded ? -buttonHalfHeight : buttonHalfHeight);
    }
}
