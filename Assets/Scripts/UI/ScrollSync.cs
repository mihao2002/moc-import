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
        if (!isDraggingSlider)
        {
            float pos = scrollRect.horizontalNormalizedPosition;
            if (!Mathf.Approximately(slider.value, pos))
            {
                slider.SetValueWithoutNotify(pos);
            }
        }
    }

    void OnSliderValueChanged(float value)
    {
        scrollRect.horizontalNormalizedPosition = value;
    }
}
