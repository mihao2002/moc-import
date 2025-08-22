using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class PartGridItem : MonoBehaviour, IPointerClickHandler
{
    public Image iconImage;
    public TMP_Text labelText;
    public GameObject border;
    public Action onClick;

    public object context;
    
    public GameObject go;

    public string partId;
    public string description;
    public string colorName;

    // Call this method when setting up the item
    public void SetContent(Sprite icon, string label, Action onClick = null, object context = null)
    {
        iconImage.sprite = icon;
        labelText.text = label;
        this.onClick = onClick;
        this.context = context;
    }

    public object Context
    {
        get
        {
            return context;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Select();
        onClick?.Invoke();
    }

    public void Select()
    {
        border.SetActive(true);
    }

    public void Deselect()
    {
        border.SetActive(false);
    }
}
