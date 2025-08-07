using UnityEngine;
using System.Collections;

public class PanelToggle : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform sidePanel;      // The panel to expand/shrink
    public RectTransform arrowImage;     // The arrow image inside the button
    public Camera camera;

    private float collapsedWidth = 300f;  // Width when panel is collapsed
    private bool isExpanded = false;
    public float animationDuration = 0.2f;

    void Start()
    {
        collapsedWidth = Screen.width / 4.0f;
        float viewportX = collapsedWidth / Screen.width;
        float viewportWidth = 1.0f - viewportX;

        Shink();
        camera.rect = new Rect(viewportX, 0, viewportWidth, 1);
    }

    void Shink()
    {
        sidePanel.offsetMax = new Vector2(collapsedWidth - Screen.width, sidePanel.offsetMax.y);
    }

    // Call this method from the Button onClick event
    public void TogglePanel()
    {
        isExpanded = !isExpanded;

        float targetWidth = isExpanded ? 0f : collapsedWidth - Screen.width;

        // Animate width change (optional)
        StartCoroutine(AnimateWidth(sidePanel, targetWidth));

        // Rotate the arrow 180 degrees around Z to flip it
        arrowImage.localEulerAngles = isExpanded ? new Vector3(0, 0, 180) : new Vector3(0, 0, 0);
    }

    private IEnumerator AnimateWidth(RectTransform panel, float targetOffsetX)
    {
        float elapsed = 0f;
        float startOffsetX = sidePanel.offsetMax.x;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float offsetX = Mathf.Lerp(startOffsetX, targetOffsetX, elapsed / animationDuration);
            panel.offsetMax = new Vector2(offsetX, sidePanel.offsetMax.y);
            yield return null;
        }

        panel.offsetMax = new Vector2(targetOffsetX, panel.offsetMax.y);
    }
}
