using UnityEngine;
using System.Collections;

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

    public UIManager()
    {
        this.screenWidth = Screen.width;
        this.screenHeight = Screen.height;

        baseUnit = screenHeight / 6;
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
}
