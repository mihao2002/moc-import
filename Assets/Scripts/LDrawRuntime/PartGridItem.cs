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

    // Call this method when setting up the item
    public void SetContent(Sprite icon, string label, Action onClick = null)
    {
        iconImage.sprite = icon;
        labelText.text = label;
        this.onClick = onClick;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        border.SetActive(true);
        Invoke(nameof(HideOutline), 0.2f); // Hide after delay
        onClick?.Invoke();
    }

    void HideOutline()
    {
        border.SetActive(false);
    }
}
