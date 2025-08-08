using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartGridItem : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text labelText;

    // Call this method when setting up the item
    public void SetContent(Sprite icon, string label)
    {
        iconImage.sprite = icon;
        labelText.text = label;
    }
}
