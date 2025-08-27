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
            buildProgressText.text += $"{Math.Floor(progress * 100)}% parts are collected. ";
        }

        if (PlayerPrefs.HasKey("BuildProgress"))
        {
            float progress = PlayerPrefs.GetFloat("BuildProgress");
            buildProgressText.text += $"You have built {Math.Floor(progress * 100)}%.";
        }
    }

    // Public method to load a scene by name
    public void LoadSceneByName(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false; // wait until we're ready

        while (!operation.isDone)
        {
            // operation.progress goes from 0 → 0.9
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            // once loading reaches 90% we can allow activation
            if (operation.progress >= 0.9f)
            {
                // progressText.text = "Press any key to continue";
                // progressBar.value = 1f;

                // if (Input.anyKeyDown) // or a button press
                // {
                operation.allowSceneActivation = true;
                // }
            }

            yield return null;
        }
    }
}
