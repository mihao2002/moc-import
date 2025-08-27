using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using UnityEngine.UI;
using System.Linq;

public class Gallery : MonoBehaviour
{
    public RawImage rawImage;
    
    private Texture2D[] images;
    private int currentImage;

    void Start()
    {
        images = Resources.LoadAll<Texture2D>("GalleryPhotos");
        currentImage = 0;
        LoadImage();
    }

    // Public method to load a scene by name
    public void Back()
    {
            StartCoroutine(UIManager.LoadSceneDelayed("Home"));
    }

    public void Previous()
    {
        if (currentImage > 0)
        {
            currentImage--;
            LoadImage();
        }
    }

    public void Next()
    {
        if (currentImage < images.Length - 1)
        {
            currentImage++;
            LoadImage();
        }
    }

    private void LoadImage()
    {
        rawImage.texture = images[currentImage];
    }
}
