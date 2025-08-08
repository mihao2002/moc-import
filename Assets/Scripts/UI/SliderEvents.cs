using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class SliderEvents : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public Action onBeginDrag;
    public Action onEndDrag;

    public void OnBeginDrag(PointerEventData eventData)
    {
        onBeginDrag?.Invoke();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        onEndDrag?.Invoke();
    }
}
