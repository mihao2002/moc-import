using UnityEngine;
using System.Collections;

public class LeftPanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public RectTransform arrowImage;     // The arrow image inside the button
    public Camera camera;

    private float panelWidth = 300f;  // Width when panel is collapsed
    private bool isExpanded = false;
    public float animationDuration = 0.2f;

    void Start()
    {
        panelWidth = Screen.width / 4.0f;

        Shrink();

        float viewportX = panelWidth / Screen.width;
        float viewportWidth = 1.0f - viewportX;
        camera.rect = new Rect(viewportX, 0, viewportWidth, 1);
    }

    void Shrink()
    {
        sidePanel.sizeDelta = new Vector2(panelWidth, sidePanel.sizeDelta.y);
        //sidePanel.offsetMax = new Vector2(collapsedWidth - Screen.width, sidePanel.offsetMax.y);
    }

    // Call this method from the Button onClick event
    public void TogglePanel()
    {
        isExpanded = !isExpanded;

        float targetWidth = isExpanded ? Screen.width : panelWidth;

        // Animate width change (optional)
        StartCoroutine(AnimateWidth(sidePanel, targetWidth));

        // Rotate the arrow 180 degrees around Z to flip it
        arrowImage.localEulerAngles = isExpanded ? new Vector3(0, 0, 180) : new Vector3(0, 0, 0);
    }

    private IEnumerator AnimateWidth(RectTransform panel, float targetWidth)
    {
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
    }
}
