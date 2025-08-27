using UnityEngine;
using System.Collections;
using System.Numerics;
using UnityEngine.UI;
using Vector2 = UnityEngine.Vector2;
using UnityEngine.SceneManagement;

public class UIManager
{
    private int screenWidth;
    private int screenHeight;
    private int baseUnit;
    private int bottomPaneHeight;
    private int padding;
    private int fixRowCount;
    private static UIManager instance;

    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UIManager();
            }

            return instance;
        }
    }

    public static IEnumerator LoadSceneDelayed(string sceneName, float delay = 0.2f)
    {
        yield return new WaitForSeconds(delay); // wait 0.2 seconds
        SceneManager.LoadScene(sceneName);
    }

    public UIManager()
    {
        this.screenWidth = Screen.width;
        this.screenHeight = Screen.height;

        baseUnit = screenHeight / 5;
        bottomPaneHeight = baseUnit * 2;
        padding = baseUnit / 10;
        fixRowCount = 3;
    }

    public int Padding
    {
        get
        {
            return padding;
        }
    }

    public int BaseUnit
    {
        get
        {
            return baseUnit;
        }
    }

    public int FixRowCount
    {
        get
        {
            return fixRowCount;
        }
    }

public static Vector2 GetScreenSize(CanvasScaler scaler)
    {
        if (scaler == null) return new Vector2(Screen.width, Screen.height);

        float width = Screen.width;
        float height = Screen.height;

        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            Vector2 reference = scaler.referenceResolution;
            float widthScale = width / reference.x;
            float heightScale = height / reference.y;
            float scaleFactor = 1f;

            switch (scaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.MatchWidthOrHeight:
                    scaleFactor = Mathf.Lerp(widthScale, heightScale, scaler.matchWidthOrHeight);
                    break;
                case CanvasScaler.ScreenMatchMode.Expand:
                    scaleFactor = Mathf.Min(widthScale, heightScale);
                    break;
                case CanvasScaler.ScreenMatchMode.Shrink:
                    scaleFactor = Mathf.Max(widthScale, heightScale);
                    break;
            }

            width = width / scaleFactor;
            height = height / scaleFactor;
        }

        return new Vector2(width, height);
    }
}
