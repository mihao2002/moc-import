using UnityEngine;

namespace LDraw.Runtime
{
    public class LDrawCamera
    {
        private readonly Camera cam;

        private static readonly Quaternion IsometricRotation = Quaternion.AngleAxis(30f, Vector3.right) * 
            Quaternion.AngleAxis(45f, Vector3.up) * 
            Quaternion.LookRotation(Vector3.back, Vector3.down);
    // Quaternion.Euler(35.264f, -45f, 0f);  // flipped
        private static readonly Quaternion headOnRotation = Quaternion.LookRotation(Vector3.back, Vector3.down); // flipped
        // private static readonly Vector3 LookTarget = Vector3.zero;

        public LDrawCamera(Camera camera)
        {
            cam = camera;
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.transform.rotation = headOnRotation; // IsometricRotation;

            RemoveAllLightsUnderCamera();
            CreateDirectionalLight("LDCad_Light1", new Vector3(30, 30, 0));
            CreateDirectionalLight("LDCad_Light2", new Vector3(-30, -30, 0));
        }

        void RemoveAllLightsUnderCamera()
        {
            foreach (Transform child in cam.transform)
            {
                Light light = child.GetComponent<Light>();
                if (light != null)
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        void CreateDirectionalLight(string name, Vector3 eulerOffset)
        {
            GameObject lightObj = new GameObject(name);
            lightObj.transform.SetParent(cam.transform);
            lightObj.transform.localPosition = Vector3.zero;
            lightObj.transform.localRotation = Quaternion.Euler(eulerOffset);

            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.None; // Optional: disable shadows for clarity
        }

        public void SetCamera(Vector3 center, float radius, Vector3? rotation)
        {
            Debug.Log($"SetCamera center:{center} radius:{radius} rotation:{rotation}");

            float verticalFOV = cam.fieldOfView * Mathf.Deg2Rad;
            float aspect = cam.aspect;
            float horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(verticalFOV / 2f) * aspect);

            // Distance required to fit the sphere inside vertical FOV
            float distanceV = radius / Mathf.Sin(verticalFOV / 2f);

            // Distance required to fit the sphere inside horizontal FOV
            float distanceH = radius / Mathf.Sin(horizontalFOV / 2f);

            // Use the larger one to ensure full fit
            float distance = Mathf.Max(distanceV, distanceH);

            if (rotation.HasValue)
            {
                // Apply rotation in the order of X, Y and Z. Can not use line below because Unity default applies
                // rotation with order of Z, X and Y, which is different from LDraw.
                // Quaternion stepRotation = Quaternion.Euler(rotation.Value.x, rotation.Value.y, -rotation.Value.z);
                Quaternion qx = Quaternion.AngleAxis(rotation.Value.x, Vector3.right);
                Quaternion qy = Quaternion.AngleAxis(rotation.Value.y, Vector3.up);
                Quaternion qz = Quaternion.AngleAxis(-rotation.Value.z, Vector3.forward);  // Negate Z if coordinate system is mirrored

                Quaternion stepRotation = qz * qy * qx; // Apply in XYZ order

                // Rotate a point from the "behind camera" position (e.g., Vector3.back)
                Vector3 orbitOffset = stepRotation * (Vector3.forward * distance);
                Vector3 up = stepRotation * Vector3.down;

                // Set camera position and orientation
                cam.transform.position = center + orbitOffset;
                cam.transform.LookAt(center, up);
            }
            else
            {
                Vector3 direction = cam.transform.forward;
                cam.transform.position = center - direction * distance; 
            }
            

            // Debug.Log($"SetCamera {distance}");
            // Debug.Log($"SetCamera modelRotated:{modelRotated} cam:{cam.name}");
            // if (modelRotated)
            // {
            //     cam.transform.rotation = headOnRotation;
            // }
            // else
            // {
            //     // Start from your custom orientation
            //     cam.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.down);

            //     // 1. Yaw left 45° — around world Y axis
            //     cam.transform.Rotate(Vector3.up, -45f, Space.Self);

            //     // 2. Pitch down 45° — around the camera's local X axis
            //     cam.transform.Rotate(Vector3.right, 30f, Space.Self);
            //     //cam.transform.rotation = modelRotated ? headOnRotation : IsometricRotation;

            // }
            
            // // Vector3 direction = cam.transform.forward;
            // // cam.transform.position = LookTarget - direction * distance; 
            // Vector3 forward = Vector3.back; // Camera looks from +Z toward origin
            // Vector3 up = Vector3.down;

            // cam.transform.rotation = Quaternion.LookRotation(forward, up);
            // // 2.  Yaw 45° to the left (world‑space Y axis)
            // cam.transform.Rotate(Vector3.up, 35.264f, Space.World);

            // // 3.  Pitch 45° downward (camera’s own right axis)
            // cam.transform.Rotate(Vector3.right, 45f, Space.Self);

            // Vector3 direction = cam.transform.forward;
            // cam.transform.position = LookTarget - direction * distance; 
        }

    }
}
