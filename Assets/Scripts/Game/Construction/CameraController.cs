using System;
using CloudGarden.Islands;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CloudGarden.Construction
{
    /// <summary>
    /// Isometric orthographic camera: touch pan (finger stays glued to the ground), pinch zoom,
    /// inertia, world bounds, focus on a building. Mouse input is supported for desktop testing.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        public const float Pitch = 40f;
        public const float Yaw = 45f;
        public const float MinZoom = 4.5f;
        public const float MaxZoom = 17f;
        const float Distance = 90f;

        public Camera Cam;
        public WorldView World;

        /// <summary>Returns true when a drag starting at this screen position must not move the camera.</summary>
        public Func<Vector2, bool> DragCapture;
        public Action<Vector2> CapturedDrag;
        public Action<Vector2> CapturedDragEnd;
        public Action<Vector2> Tap;

        Vector3 target = Vector3.zero;
        float zoom = 9f;
        float targetZoom = 9f;

        enum Mode { None, Pan, Pinch, Captured }
        Mode mode = Mode.None;
        Vector2 downPos;
        float downTime;
        bool moved;
        bool tapCandidate;
        Vector3 anchor;
        Vector3 velocity;
        float lastPinchDist;

        bool focusing;
        Vector3 focusTarget;
        Vector3 focusVel;

        public Vector3 Target { get { return target; } }
        public float Zoom { get { return zoom; } }

        public void Init(Camera cam, WorldView world)
        {
            Cam = cam;
            World = world;
            Cam.orthographic = true;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 400f;
            Cam.orthographicSize = zoom;
            ApplyTransform();
        }

        public void FocusOn(Vector3 worldPoint, float newZoom)
        {
            focusTarget = worldPoint;
            focusing = true;
            if (newZoom > 0f) targetZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom);
        }

        public void SnapTo(Vector3 worldPoint, float newZoom)
        {
            target = worldPoint;
            if (newZoom > 0f) { zoom = targetZoom = Mathf.Clamp(newZoom, MinZoom, MaxZoom); }
            focusing = false;
            ClampTarget();
            ApplyTransform();
        }

        // ------------------------------------------------------------------ helpers

        void ApplyTransform()
        {
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Cam.transform.rotation = rot;
            Cam.transform.position = target - (rot * Vector3.forward) * Distance;
            Cam.orthographicSize = zoom;
        }

        bool GroundAt(Vector2 screen, out Vector3 point)
        {
            return World.GroundPoint(Cam.ScreenPointToRay(screen), out point);
        }

        void ClampTarget()
        {
            if (World == null) return;
            Bounds b = World.FocusBounds();
            target.x = Mathf.Clamp(target.x, b.min.x, b.max.x);
            target.z = Mathf.Clamp(target.z, b.min.z, b.max.z);
            target.y = 0f;
        }

        static bool OverUI(int fingerId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        float TapRadiusPixels { get { return Mathf.Max(12f, Screen.dpi * 0.07f); } }

        // ------------------------------------------------------------------ update

        void Update()
        {
            if (Cam == null || World == null) return;

            if (Input.touchCount > 0) HandleTouch();
            else HandleMouse();

            if (focusing && mode == Mode.None)
            {
                target = Vector3.SmoothDamp(target, focusTarget, ref focusVel, 0.22f);
                if ((target - focusTarget).sqrMagnitude < 0.01f) focusing = false;
            }
            else if (mode == Mode.None && velocity.sqrMagnitude > 0.0004f)
            {
                target += velocity * Time.deltaTime;
                velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-6f * Time.deltaTime));
            }

            zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-12f * Time.deltaTime));
            ClampTarget();
            ApplyTransform();
            World.SetSkyForPoint(target);
        }

        void HandleTouch()
        {
            if (Input.touchCount >= 2)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                Vector2 mid = (a.position + b.position) * 0.5f;
                float dist = Vector2.Distance(a.position, b.position);
                if (mode != Mode.Pinch)
                {
                    EndCapturedIfNeeded();
                    mode = Mode.Pinch;
                    tapCandidate = false;
                    lastPinchDist = dist;
                    focusing = false;
                    Vector3 g;
                    if (GroundAt(mid, out g)) anchor = g;
                }
                else
                {
                    if (lastPinchDist > 1f && dist > 1f)
                        targetZoom = Mathf.Clamp(targetZoom * (lastPinchDist / dist), MinZoom, MaxZoom);
                    lastPinchDist = dist;
                    zoom = Mathf.Lerp(zoom, targetZoom, 0.5f);
                    ApplyTransform();
                    Vector3 cur;
                    if (GroundAt(mid, out cur)) target += anchor - cur;
                }
                return;
            }

            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                BeginPointer(t.position, t.fingerId);
            }
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                if (mode == Mode.Pinch) { mode = Mode.None; return; }
                DragPointer(t.position);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                EndPointer(t.position, t.phase == TouchPhase.Ended);
            }
        }

        void HandleMouse()
        {
            if (mode == Mode.Pinch) mode = Mode.None;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !OverUI(-1))
                targetZoom = Mathf.Clamp(targetZoom * (1f - scroll * 0.1f), MinZoom, MaxZoom);

            if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition, -1);
            else if (Input.GetMouseButton(0)) DragPointer(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition, true);
        }

        void BeginPointer(Vector2 pos, int fingerId)
        {
            if (OverUI(fingerId)) { mode = Mode.None; tapCandidate = false; return; }
            downPos = pos;
            downTime = Time.unscaledTime;
            moved = false;
            tapCandidate = true;
            velocity = Vector3.zero;
            focusing = false;

            if (DragCapture != null && DragCapture(pos))
            {
                mode = Mode.Captured;
                return;
            }
            mode = Mode.Pan;
            Vector3 g;
            if (GroundAt(pos, out g)) anchor = g;
        }

        void DragPointer(Vector2 pos)
        {
            if (mode == Mode.None) return;
            if (!moved && (pos - downPos).magnitude > TapRadiusPixels)
            {
                moved = true;
                tapCandidate = false;
            }

            if (mode == Mode.Captured)
            {
                if (moved && CapturedDrag != null) CapturedDrag(pos);
                return;
            }
            if (mode == Mode.Pan && moved)
            {
                Vector3 cur;
                if (GroundAt(pos, out cur))
                {
                    Vector3 delta = anchor - cur;
                    target += delta;
                    if (Time.deltaTime > 0.0001f) velocity = Vector3.Lerp(velocity, delta / Time.deltaTime, 0.5f);
                }
            }
        }

        void EndPointer(Vector2 pos, bool released)
        {
            Mode was = mode;
            mode = Mode.None;
            if (was == Mode.None) return;

            if (was == Mode.Captured)
            {
                if (moved && CapturedDragEnd != null) CapturedDragEnd(pos);
                else if (released && Tap != null) Tap(pos);
                return;
            }
            if (tapCandidate && released && Time.unscaledTime - downTime < 0.45f)
            {
                velocity = Vector3.zero;
                if (Tap != null) Tap(pos);
            }
            else if (velocity.magnitude > 40f)
            {
                velocity = velocity.normalized * 40f;
            }
        }

        void EndCapturedIfNeeded()
        {
            if (mode == Mode.Captured && CapturedDragEnd != null) CapturedDragEnd(downPos);
        }
    }
}
