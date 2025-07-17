using UnityEngine;

[ExecuteAlways] // So it updates in edit mode too
public class CameraOrbitByAngles : MonoBehaviour
{
    [Header("Target Settings")]
    public Vector3 target = Vector3.zero;
    public Vector3 up = Vector3.down;

    [Header("Orbit Settings")]
    [Range(-180f, 180f)]
    public float azimuthDeg = 45f;     // Horizontal angle in degrees
    [Range(-90f, 90f)]
    public float elevationDeg = -35.264f; // Vertical angle in degrees
    public float distance = 5f;       // Zoom distance

    void Update()
    {
        // Convert angles to radians
        float azimuthRad = azimuthDeg * Mathf.Deg2Rad;
        float elevationRad = elevationDeg * Mathf.Deg2Rad;

        // Spherical to Cartesian conversion
        float x = distance * Mathf.Cos(elevationRad) * Mathf.Cos(azimuthRad);
        float y = distance * Mathf.Sin(elevationRad);
        float z = distance * Mathf.Cos(elevationRad) * Mathf.Sin(azimuthRad);

        Vector3 cameraPos = new Vector3(x, y, z) + target;

        transform.position = cameraPos;
        transform.LookAt(target, up);
    }
}
