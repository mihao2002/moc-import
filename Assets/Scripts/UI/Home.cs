using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;

public class Home : MonoBehaviour
{
    public TMP_Text buildProgressText;

    void Start()
    {
        if (PlayerPrefs.HasKey("BuildProgress"))
        {
            float progress = PlayerPrefs.GetFloat("BuildProgress");
            buildProgressText.text = $"Building progress {Math.Floor(progress * 100)}%";
        }
    }

    // Public method to load a scene by name
    public void LoadSceneByName(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
