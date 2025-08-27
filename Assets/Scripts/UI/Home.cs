using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;

public class Home : MonoBehaviour
{
    public TMP_Text buildProgressText;

    void Start()
    {
        buildProgressText.text = "";
        if (PlayerPrefs.HasKey("CollectProgress"))
        {
            float progress = PlayerPrefs.GetFloat("CollectProgress");
            buildProgressText.text += $"Collected {Math.Floor(progress * 100)}%   ";
        }

        if (PlayerPrefs.HasKey("BuildProgress"))
        {
            float progress = PlayerPrefs.GetFloat("BuildProgress");
            buildProgressText.text += $"Built {Math.Floor(progress * 100)}%";
        }
    }

    // Public method to load a scene by name
    public void LoadSceneByName(string sceneName)
    {
        StartCoroutine(UIManager.LoadSceneDelayed(sceneName));
    }
}
