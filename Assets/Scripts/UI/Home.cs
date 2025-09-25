using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class Home : MonoBehaviour
{
    public TMP_Text buildProgressText;
    private bool bundleLoaded = false;

    void Start()
    {
        PreloadAllBundles();
    }

    public async Task PreloadAllBundles()
    {
        // 1. Get all resource locations (all Addressable assets)
        AsyncOperationHandle<IList<IResourceLocation>> locationsHandle = Addressables.LoadResourceLocationsAsync("All");
        await locationsHandle.Task;

        if (locationsHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"Failed to load resource locations: {locationsHandle.OperationException}");
            return;
        }

        IList<IResourceLocation> allLocations = locationsHandle.Result;
        Debug.Log($"Loaded {allLocations.Count} resource locations:");

        // Log each location
        foreach (var loc in allLocations)
        {
            Debug.Log($"Address: {loc.InternalId} | Primary Key: {loc.PrimaryKey} | Resource Type: {loc.ResourceType}");
        }

        // 2. Optional: check total download size
        long totalSize = await Addressables.GetDownloadSizeAsync(allLocations).Task;
        if (totalSize > 0)
            Debug.Log($"Total download size: {totalSize / (1024f * 1024f):0.##} MB");
        else
        {
            Debug.Log("All bundles are already cached.");
            SetBundleLoaded(true);
            return;
        }

        // 3. Download all bundles (Addressables handles caching internally)
        AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(allLocations, true);
        bool completed = false;
        downloadHandle.Completed += handle =>
        {
            completed = true;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                SetBundleLoaded(true);
            }
            else
            {
                SetBundleLoaded(false);
            }
        };

        while (!downloadHandle.IsDone && !completed)
        {
            SetBundlePercent((int)(downloadHandle.PercentComplete * 100f));
            await Task.Yield(); // wait for next frame
        }
    }

    private void SetBundlePercent(int percent)
    {
        buildProgressText.text = $"Loading assets, please wait... {percent}%";
    }

    private void SetBundleLoaded(bool result)
    {
        bundleLoaded = result;

        if (bundleLoaded)
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

            if (buildProgressText.text == "")
            {
                buildProgressText.text = "Assets loaded successfully.";
            }
        }
        else
        {
            buildProgressText.text = "Failed to load assets.";
        }
    }

    // Public method to load a scene by name
    public void LoadSceneByName(string sceneName)
    {
        if (bundleLoaded)
        {
            StartCoroutine(UIManager.LoadSceneDelayed(sceneName));            
        }        
    }
}
