using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using LDraw.Runtime;
using TMPro;

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

    private Color selectedColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private float panelWidth = 300f;  // Width when panel is collapsed
    private bool isExpanded = false;
    public float animationDuration = 0.2f;
    private float columnWidth;
    private float columnSpacing;
    private float padding;
    private float fixRowCount;
    private Color buttonColor;

    private GameObject previewPart = null;
    private LDrawCamera ldrawCamera;
    private InputHandler inputHandler;

    void Awake()
    {
        columnWidth = grid.cellSize.x;
        columnSpacing = grid.spacing.x;
        padding = grid.padding.left + grid.padding.right;
        fixRowCount = grid.constraintCount;
        // buttonColor = button.colors.normalColor;
        partDetail.SetActive(false);
        expander.SetActive(false);

        previewCamera.aspect = 1;
        ldrawCamera = new LDrawCamera(previewCamera);
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

        if (!isExpanded)
        {
            TogglePanel();
        }
    }

    public void SetItemCount(int itemCount)
    {
        var columnCount = Mathf.CeilToInt(itemCount / fixRowCount);

        panelWidth = columnWidth * columnCount + columnSpacing * (columnCount - 1) + padding;
        float viewportX = panelWidth / Screen.width;
        float viewportWidth = 1.0f - viewportX;
        cam.rect = new Rect(viewportX, 0, viewportWidth, 1);

        partDetail.GetComponent<RectTransform>().offsetMin = new Vector2(panelWidth, 0);

        if (isExpanded)
        {
            TogglePanel();
        }
        else
        {
            Shrink();
        }        
    }

    // void SetButtonColor(Color color)
    // {
    //     var colors = button.colors;
    //     colors.normalColor = color; 
    //     button.colors = colors;
    // }

    void Shrink()
    {
        sidePanel.sizeDelta = new Vector2(panelWidth, sidePanel.sizeDelta.y);
        // arrowImage.localEulerAngles = new Vector3(0, 0, -90);
        // SetButtonColor(buttonColor);
        partDetail.SetActive(false);
        expander.SetActive(false);
        DestoryPreviewPart();
        //sidePanel.offsetMax = new Vector2(collapsedWidth - Screen.width, sidePanel.offsetMax.y);
    }

    // Call this method from the Button onClick event
    public void TogglePanel()
    {
        isExpanded = !isExpanded;

        if (!isExpanded)
        {
            DestoryPreviewPart();
        }

        float targetWidth = isExpanded ? Screen.width : panelWidth;

        // Animate width change (optional)
        StartCoroutine(AnimateWidth(sidePanel, targetWidth));

        // Rotate the arrow 180 degrees around Z to flip it
        // arrowImage.localEulerAngles = isExpanded ? new Vector3(0, 0, 90) : new Vector3(0, 0, -90);
        // SetButtonColor(isExpanded ? selectedColor : buttonColor);
        partDetail.SetActive(isExpanded);
        expander.SetActive(isExpanded);
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

    private void HandleInput()
    {
        if (isExpanded)
        {
            inputHandler.HandleInput();
        }        
    }

    public void Update()
    {
        HandleInput();
    }
}
