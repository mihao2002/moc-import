using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float xSpeed = 120f;
    public float ySpeed = 120f;
    public float zoomSpeed = 0.5f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;
    public float minDistance = 1f;
    public float maxDistance = 20f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        if (target == null)
        {
            GameObject center = new GameObject("CameraTarget");
            center.transform.position = Vector3.zero;
            target = center.transform;
        }
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Mouse rotation
        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            x += delta.x * xSpeed * Time.deltaTime;
            y -= delta.y * ySpeed * Time.deltaTime;
        }

        // Mouse scroll zoom
        float scroll = Mouse.current.scroll.ReadValue().y;
        distance -= scroll * zoomSpeed * Time.deltaTime * 100f;
#endif

#if UNITY_IOS || UNITY_ANDROID
        var touchscreen = Touchscreen.current;
        if (touchscreen == null) return;

        int activeTouches = 0;
        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.isPressed)
                activeTouches++;
        }

        if (activeTouches == 1)
        {
            var touch = touchscreen.primaryTouch;
            if (touch.press.isPressed)
            {
                Vector2 delta = touch.delta.ReadValue();
                x += delta.x * xSpeed * Time.deltaTime * 0.02f;
                y -= delta.y * ySpeed * Time.deltaTime * 0.02f;
            }
        }
        else if (activeTouches >= 2)
        {
            var t0 = touchscreen.touches[0];
            var t1 = touchscreen.touches[1];

            if (t0.press.isPressed && t1.press.isPressed)
            {
                Vector2 t0Curr = t0.position.ReadValue();
                Vector2 t1Curr = t1.position.ReadValue();

                Vector2 t0Prev = t0Curr - t0.delta.ReadValue();
                Vector2 t1Prev = t1Curr - t1.delta.ReadValue();

                float prevDist = Vector2.Distance(t0Prev, t1Prev);
                float currDist = Vector2.Distance(t0Curr, t1Curr);
                float deltaDist = currDist - prevDist;

                distance -= deltaDist * zoomSpeed * 0.01f;
            }
        }
#endif

        y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }
}
