using UnityEngine;

namespace LDraw.Editor
{
    public class LDrawUtils
    {
        // public static float ComputeCameraDistance(Bounds bounds, float multiplier = 1.2f)
        // {
        //     return bounds.extents.magnitude * multiplier;
        // }

        public static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds combinedBounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            return combinedBounds;
        }

        // public static float ComputeCameraDistance(Camera cam, Bounds bounds)
        // {
        //     Vector3 center = bounds.center;
        //     float radius = bounds.extents.magnitude;

        //     // Convert field of view to radians and calculate distance
        //     float fov = cam.fieldOfView;
        //     float aspect = cam.aspect;

        //     // Ensure the camera is facing the object along its -Z axis (forward)
        //     cam.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

        //     // This estimates the distance needed based on the field of view and bounds size
        //     float distance = radius / Mathf.Sin(Mathf.Deg2Rad * fov / 2f);

        //     // Move the camera back by `distance` along its forward vector
        //     cam.transform.position = center - cam.transform.forward * distance;

        //     // Optional: set near/far clip planes to accommodate the object's size
        //     cam.nearClipPlane = 0.01f;
        //     cam.farClipPlane = distance + radius * 2f;
        // }
    }
}
