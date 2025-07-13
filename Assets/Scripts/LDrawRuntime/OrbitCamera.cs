using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float xSpeed = 120f;
    public float ySpeed = 120f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    private float x = 0.0f;
    private float y = 0.0f;

    private Vector2 mouseDelta;
    private bool isDragging;

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
        if (Mouse.current.leftButton.isPressed)
        {
            isDragging = true;
            mouseDelta = Mouse.current.delta.ReadValue();
        }
        else
        {
            isDragging = false;
        }
    }

    void LateUpdate()
    {
        if (isDragging)
        {
            x += mouseDelta.x * xSpeed * Time.deltaTime;
            y -= mouseDelta.y * ySpeed * Time.deltaTime;
            y = Mathf.Clamp(y, yMinLimit, yMaxLimit);
        }

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }
}
