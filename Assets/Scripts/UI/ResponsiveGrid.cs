using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class ResponsiveGrid : MonoBehaviour
{
    public Camera mainCamera;
    public CanvasScaler canvasScaler;

    private GridLayoutGroup grid;
    private int fixRowCount;
    private Vector2 screenSize;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        fixRowCount = grid.constraintCount;
        screenSize = UIManager.GetScreenSize(canvasScaler);
    }

    void Update()
    {
        RectTransform rt = GetComponent<RectTransform>();
        float height = rt.rect.height;
        float cellSize = height / fixRowCount;

        grid.cellSize = new Vector2(cellSize, cellSize);

        float viewportX = rt.rect.width / screenSize.x;
        float viewportWidth = 1.0f - viewportX;
        mainCamera.rect = new Rect(viewportX, 0, viewportWidth, 1);
    }
}
