using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

namespace LDraw.Runtime
{
    public class LDrawCamera
    {
        public static Vector3 DefaultRotation = new Vector3(30f, 45f, 0f);

        public readonly Camera cam;
        private readonly CameraAnimator animator;

        private static readonly Quaternion IsometricRotation = Quaternion.AngleAxis(30f, Vector3.right) * 
            Quaternion.AngleAxis(45f, Vector3.up) * 
            Quaternion.LookRotation(Vector3.back, Vector3.down);

        private static readonly Quaternion headOnRotation = Quaternion.LookRotation(Vector3.back, Vector3.down); // flipped
        private Vector3 cameraCenter;
        private float cameraRadius;
        private Vector3 currentRotationEuler;
        private bool animateEnabled;
        private int currentTag;
        private float nearClip;
        private Vector3 up;

        private Dictionary<int, (Vector3 /*center*/, Vector3 /*position*/, Vector3 /*up*/)> tagStates;
        
        // private static readonly Vector3 LookTarget = Vector3.zero;

        public LDrawCamera(Camera camera, bool animate)
        {
            cam = camera;
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.transform.rotation = headOnRotation; // IsometricRotation;
            cameraCenter = Vector3.zero;
            nearClip = cam.nearClipPlane;
            currentRotationEuler = Vector3.zero;
            this.animateEnabled = animate;
            currentTag = -1;
            up = cam.transform.up;

            tagStates = new Dictionary<int, (Vector3, Vector3, Vector3)>();

            RemoveAllLightsUnderCamera();
            CreateDirectionalLight("LDCad_Light1", new Vector3(30, 30, 0));
            CreateDirectionalLight("LDCad_Light2", new Vector3(-30, -30, 0));

            animator = CameraAnimator.AttachTo(camera);
        }

        public void Render()
        {
            cam.Render();
        }

        public (Vector3 center, float radius, Vector3 rotationEuler, Vector3 up) GetCameraState()
        {
            return (cameraCenter, cameraRadius, currentRotationEuler, up);
        }

        void RemoveAllLightsUnderCamera()
        {
            foreach (Transform child in cam.transform)
            {
                Light light = child.GetComponent<Light>();
                if (light != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(child.gameObject); // editor mode
#else
                    GameObject.Destroy(child.gameObject);
#endif
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
            light.intensity = 0.8f;
            light.shadows = LightShadows.None; // Optional: disable shadows for clarity
        }

        public void SetCamera(Vector3 center, float radius, Vector3? rotation, bool animate = false, Action onAnimationComplete = null, int tag = -1, bool cleanState = false)
        {
            cameraCenter = center;
            cameraRadius = radius;

            float verticalFOV = cam.fieldOfView * Mathf.Deg2Rad;
            float aspect = cam.aspect;
            float horizontalFOV = 2f * Mathf.Atan(Mathf.Tan(verticalFOV / 2f) * aspect);

            // Distance required to fit the sphere inside vertical FOV
            float distanceV = radius / Mathf.Tan(verticalFOV / 2f);

            // Distance required to fit the sphere inside horizontal FOV
            float distanceH = radius / Mathf.Tan(horizontalFOV / 2f);

            // Use the larger one to ensure full fit
            float distance = Mathf.Max(distanceV, distanceH);
            distance = Mathf.Max(distance + nearClip, distance * 1.2f);
            Vector3 targetPos;
            // Vector3? up = null;

            if (rotation.HasValue)
            {
                currentRotationEuler = rotation.Value;

                // Apply rotation in the order of X, Y and Z. Can not use line below because Unity default applies
                // rotation with order of Z, X and Y, which is different from LDraw.
                // Quaternion stepRotation = Quaternion.Euler(rotation.Value.x, rotation.Value.y, -rotation.Value.z);
                Quaternion qx = Quaternion.AngleAxis(rotation.Value.x, Vector3.right);
                Quaternion qy = Quaternion.AngleAxis(rotation.Value.y, Vector3.up);
                Quaternion qz = Quaternion.AngleAxis(-rotation.Value.z, Vector3.forward);  // Negate Z if coordinate system is mirrored

                Quaternion stepRotation = qz * qy * qx; // Apply in XYZ order

                // Rotate a point from the "behind camera" position (e.g., Vector3.back)
                Vector3 orbitOffset = stepRotation * (Vector3.forward * distance);
                up = stepRotation * Vector3.down;

                // Set camera position and orientation
                targetPos = center + orbitOffset;
            }
            else
            {
                Vector3 direction = cam.transform.forward;
                targetPos = center - direction * distance;
            }

            if (tag == -1)
            {
                tag = currentTag;
            }
            else
            {
                currentTag = tag;
            }

            if (cleanState) currentTag = -1;

            if (animateEnabled && animate && tagStates.ContainsKey(tag))
            {
                (Vector3 oldCenter, Vector3 oldPosition, Vector3 oldUp) = tagStates[tag];
                animator.AnimateTo(tag, tagStates, cleanState, oldCenter, cameraCenter, oldPosition, targetPos, oldUp, up, onAnimationComplete);
            }
            else
            {
                cam.transform.position = targetPos;
                if (up != null)
                {
                    cam.transform.LookAt(center, up);
                }

                if (animateEnabled && tag != -1)
                {
                    if (!cleanState)
                        tagStates[tag] = (center, targetPos, up);
                    else
                    {
                        tagStates.Remove(tag);
                    }
                        
                }
                
                onAnimationComplete?.Invoke();                
            }
        }

        /// <summary>
        /// Internal helper MonoBehaviour that animates the camera.
        /// </summary>
        internal class CameraAnimator : MonoBehaviour
        {
            private Coroutine animationCoroutine;
            private Camera cam;

            public static CameraAnimator AttachTo(Camera cam)
            {
                CameraAnimator anim = cam.GetComponent<CameraAnimator>();
                if (anim == null)
                    anim = cam.gameObject.AddComponent<CameraAnimator>();
                anim.cam = cam;
                return anim;
            }

            public void AnimateTo(int tag, Dictionary<int, (Vector3, Vector3, Vector3)> tagStates, bool cleanState,
                Vector3 startCenter, Vector3 center, Vector3 startPos, Vector3 targetPos, Vector3 startUp, Vector3 endUp, Action onComplete)
            {
                if (animationCoroutine != null)
                {
                    StopCoroutine(animationCoroutine);
                }

                float distance = Vector3.Distance(startPos, targetPos);
                float angle = Vector3.Angle(startUp, endUp);

                float duration = Mathf.Clamp((distance + angle * 0.05f) * 0.2f, 0.2f, 2.0f); // tweak as needed
                    
                animationCoroutine = StartCoroutine(Animate(tag, tagStates, cleanState, startCenter, center, startPos, targetPos, startUp, endUp, duration, onComplete));
            }

            private IEnumerator Animate(int tag, Dictionary<int, (Vector3, Vector3, Vector3)> tagStates, bool cleanState,
                Vector3 startCenter, Vector3 center, Vector3 startPos, Vector3 targetPos, Vector3 startUp, Vector3 endUp, float duration, Action onComplete)
            {
                Vector3 startDir = (startPos - startCenter).normalized;
                Vector3 endDir = (targetPos - center).normalized;

                float startDistance = Vector3.Distance(startPos, startCenter);
                float endDistance = Vector3.Distance(targetPos, center);

                float elapsed = 0f; 

                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);

                    // Interpolate direction and normalize
                    Vector3 currentDir = Vector3.Slerp(startDir, endDir, smoothT).normalized;

                    // Interpolate distance
                    float currentDistance = Mathf.Lerp(startDistance, endDistance, smoothT);

                    // Interpolate up vector
                    Vector3 currentUp = Vector3.Slerp(startUp, endUp, smoothT).normalized;

                    Vector3 currentCenter = Vector3.Lerp(startCenter, center, smoothT);

                    // Update position and look at center with interpolated up
                    cam.transform.position = currentCenter + currentDir * currentDistance;
                    cam.transform.LookAt(currentCenter, currentUp);

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Ensure exact final position and rotation
                cam.transform.position = targetPos;
                cam.transform.rotation = Quaternion.LookRotation(center - targetPos, endUp);

                if (tag != -1)
                {
                    if (!cleanState)
                        tagStates[tag] = (center, targetPos, endUp);
                    else
                    {
                        tagStates.Remove(tag);
                    }
                        
                }

                onComplete?.Invoke();
            }
        }
    }
}
