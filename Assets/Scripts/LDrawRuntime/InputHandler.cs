using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Newtonsoft.Json;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.IO;
using System;

namespace LDraw.Runtime
{
    public class InputHandler
    {
        private float minRadius = 0.2f;
        private float maxRadius = 30f;

        private LDrawCamera camera;
        private Vector2 lastTouchPosition;
        private Vector2 lastMousePosition;
        private bool isDragging = false;
        private bool isPinching = false;
        private float lastPinchDistance = 0f;

        public InputHandler(LDrawCamera cam)
        {
            camera = cam;
        }

        private void ApplyRotationDelta(Vector2 delta)
        {
            float rotationSpeed = 0.2f;

            var (center, radius, rotationEuler, up) = camera.GetCameraState();
            
            rotationEuler.x -= delta.y * rotationSpeed;

            var a = To360(rotationEuler.x);
            var dir = a > 90f && a < 270f;
            rotationEuler.y -= delta.x * rotationSpeed * (dir?-1:1);

            camera.SetCamera(center, radius, rotationEuler);
        }

        float To360(float angle)
        {
            angle %= 360f;           // now in (-360, 360)
            if (angle < 0) angle += 360f;  // shift negatives into [0, 360)
            return angle;
        }

        private void ApplyZoomDelta(float delta)
        {
            var (center, radius, rotationEuler, up) = camera.GetCameraState();

            radius -= delta;
            if (radius >= minRadius && radius <= maxRadius)
            {
                camera.SetCamera(center, radius, rotationEuler);
            }
        }

        private void HandleMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Rotation with left button drag
            if (mouse.leftButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMousePosition = mouse.position.ReadValue();
            }
            else if (mouse.leftButton.isPressed && isDragging)
            {
                Vector2 currentPos = mouse.position.ReadValue();
                Vector2 delta = currentPos - lastMousePosition;
                lastMousePosition = currentPos;

                ApplyRotationDelta(delta);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            // Zoom with scroll wheel
            Vector2 scroll = mouse.scroll.ReadValue();
            if (Mathf.Abs(scroll.y) > 0.01f)
            {
                ApplyZoomDelta(scroll.y); // scale scroll speed
            }
        }

        private void HandleTouchInput()
        {
            var touchscreen = Touchscreen.current;
            if (camera == null || touchscreen == null || EventSystem.current == null)
                return;

            // Only process touches that are not over UI
            var touches = touchscreen.touches
                .Where(t => t.press.isPressed && !EventSystem.current.IsPointerOverGameObject(t.touchId.ReadValue()))
                .ToArray();

            // Count active touches properly (pressed)
            var touchesArray = touchscreen.touches.ToArray();
            var activeTouches = touchesArray.Where(t => t.press.isPressed).ToList();

            if (activeTouches.Count == 1)
            {
                var touch = activeTouches[0];
                var phase = touch.phase.ReadValue();
                var pos = touch.position.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    lastTouchPosition = pos;
                    isDragging = true;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)
                {
                    Vector2 delta = pos - lastTouchPosition;
                    lastTouchPosition = pos;

                    ApplyRotationDelta(delta);
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    isDragging = false;
                }
                isPinching = false;
            }
            else if (activeTouches.Count == 2)
            {
                var touch0 = activeTouches[0];
                var touch1 = activeTouches[1];

                Vector2 pos0 = touch0.position.ReadValue();
                Vector2 pos1 = touch1.position.ReadValue();

                float currentDistance = Vector2.Distance(pos0, pos1);

                if (!isPinching)
                {
                    isPinching = true;
                    lastPinchDistance = currentDistance;
                }
                else
                {
                    float deltaDistance = currentDistance - lastPinchDistance;
                    lastPinchDistance = currentDistance;

                    ApplyZoomDelta(deltaDistance * 0.02f);
                }

                isDragging = false;
            }
            else
            {
                isDragging = false;
                isPinching = false;
            }
        }

        public void HandleInput()
        {
        #if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
        #else
            HandleTouchInput();
        #endif
        }
   }
}