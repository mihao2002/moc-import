using UnityEngine;
using UnityEngine.UI;

public class ScrollSync : MonoBehaviour
{
    public ScrollRect scrollRect;
    public Slider slider;
    private bool isDraggingSlider = false;

    void Start()
    {
        // Listen to slider drag begin and end
        var sliderEvents = slider.GetComponent<SliderEvents>();
        if (sliderEvents == null)
        {
            sliderEvents = slider.gameObject.AddComponent<SliderEvents>();
        }
        sliderEvents.onBeginDrag += () => isDraggingSlider = true;
        sliderEvents.onEndDrag += () => isDraggingSlider = false;

        // Connect slider to scroll view
        slider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    void Update()
    {
        // Only update slider if it's NOT being dragged
        if (!isDraggingSlider)
        {
            float pos = scrollRect.horizontalNormalizedPosition;
            slider.SetValueWithoutNotify(pos);
        }
    }

    void OnSliderValueChanged(float value)
    {
        if (isDraggingSlider)
        {
                scrollRect.horizontalNormalizedPosition = value;
        }
    }
}
