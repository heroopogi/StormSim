using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>A local-space survey camera. Has no dependency on Cesium.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("StormSim/Camera Controller")]
public sealed class StormSimCameraController : MonoBehaviour
{
    [Header("Flight: WASD / arrows, Q down, E up")]
    [Min(.1f)] public float moveSpeed = 25f;
    [Min(1f)] public float boostMultiplier = 4f;
    [Range(.05f, 1f)] public float slowMultiplier = .2f;
    [Min(.01f)] public float movementSmoothTime = .12f;

    [Header("Right-drag: look | Middle-drag: pan | Alt + left-drag: orbit")]
    [Range(.01f, 1f)] public float lookSensitivity = .15f;
    [Min(.01f)] public float rotationSharpness = 18f;
    [Range(10f, 89f)] public float pitchLimit = 85f;
    [Min(.001f)] public float panSensitivity = .002f;
    [Min(1f)] public float fallbackOrbitDistance = 100f;
    public LayerMask focusLayers = ~0;

    [Header("Wheel: zoom | Shift: fast | Ctrl: slow | Home: reset | Esc: release")]
    [Min(.1f)] public float zoomDistance = 12f;
    [Min(.01f)] public float zoomSmoothTime = .1f;

    [Header("Optional study-area limits (relative to the camera's parent)")]
    public bool limitToBounds;
    public Bounds movementBounds = new Bounds(new Vector3(0, 500, 500), new Vector3(5000, 2000, 5000));

    Camera _camera;
    Vector3 _velocity, _velocityDamping;
    Vector3 _startPosition;
    Quaternion _startRotation;
    float _yaw, _pitch, _pendingZoom, _zoomDamping;
    Vector3 _orbitPivot;
    float _orbitDistance;
    bool _hasStartView, _hasFocus = true, _ownsCursor;
    CursorLockMode _previousLock;
    bool _previousCursorVisible;
    enum DragMode { None, Look, Pan, Orbit }
    DragMode _drag;

    void OnEnable()
    {
        _camera = GetComponent<Camera>();
        if (!_hasStartView)
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
            _hasStartView = true;
        }
        SyncRotation();
        StopMotion();
    }

    void OnDisable() { EndDrag(); StopMotion(); }
    void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        if (!hasFocus) { EndDrag(); StopMotion(); }
    }

    void Update()
    {
        Tick(Mathf.Min(Time.unscaledDeltaTime, .05f));
    }

    void Tick(float dt)
    {
        if (!_hasFocus || !_camera || !_camera.enabled) { EndDrag(); StopMotion(); return; }
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            EndDrag(); StopMotion(); return;
        }

        // The Editor/OS can release a captured cursor (Escape, focus changes).
        if (_ownsCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            EndDrag(); StopMotion(); return;
        }
        if (_drag != DragMode.None && !IsDragHeld(mouse)) EndDrag();

        bool pointerOverUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        bool pointerInView = mouse != null && _camera.pixelRect.Contains(mouse.position.ReadValue());
        if (_drag == DragMode.None && pointerInView && !pointerOverUi)
        {
            DragMode requested = DragMode.None;
            if (mouse.rightButton.wasPressedThisFrame) requested = DragMode.Look;
            else if (mouse.middleButton.wasPressedThisFrame) requested = DragMode.Pan;
            else if (keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
                && mouse.leftButton.wasPressedThisFrame) requested = DragMode.Orbit;
            if (requested != DragMode.None)
            {
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                BeginDrag(requested, mouse.position.ReadValue());
                return; // Ignore the cursor-warp delta on the capture frame.
            }
        }

        // Let selected controls keep keyboard navigation/text input until the user clicks the view.
        if (_drag == DragMode.None && (pointerOverUi || !pointerInView
            || (EventSystem.current && EventSystem.current.currentSelectedGameObject)))
        {
            StopMotion(); return;
        }
        if (keyboard != null && keyboard.homeKey.wasPressedThisFrame) { ResetView(); return; }

        float speedFactor = 1f;
        if (keyboard != null)
        {
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) speedFactor *= boostMultiplier;
            if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) speedFactor *= slowMultiplier;
        }
        Vector2 delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        if (_drag == DragMode.Look || _drag == DragMode.Orbit)
        {
            _yaw = Mathf.Repeat(_yaw + delta.x * lookSensitivity, 360f);
            _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -pitchLimit, pitchLimit);
        }
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(_pitch, _yaw, 0),
            1f - Mathf.Exp(-rotationSharpness * dt));

        float scroll = mouse != null ? ScrollSteps(mouse.scroll.ReadValue().y) : 0f;
        Vector3 position = transform.localPosition;
        if (_drag == DragMode.Orbit)
        {
            _orbitDistance = Mathf.Clamp(_orbitDistance - scroll * zoomDistance * speedFactor, 1f, _camera.farClipPlane);
            position = _orbitPivot - transform.localRotation * Vector3.forward * _orbitDistance;
            _velocity = _velocityDamping = Vector3.zero;
        }
        else
        {
            if (_drag == DragMode.Pan)
                position += transform.localRotation * new Vector3(-delta.x, -delta.y, 0) * (_orbitDistance * panSensitivity);
            Vector3 input = Vector3.zero;
            if (keyboard != null && _drag != DragMode.Pan)
            {
                input.x = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed, keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
                input.z = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed, keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
                input.y = Axis(keyboard.eKey.isPressed, keyboard.qKey.isPressed);
            }
            Vector3 direction = transform.localRotation * new Vector3(input.x, 0, input.z) + Vector3.up * input.y;
            Vector3 targetVelocity = Vector3.ClampMagnitude(direction, 1) * (moveSpeed * speedFactor);
            _velocity = Vector3.SmoothDamp(_velocity, targetVelocity, ref _velocityDamping, movementSmoothTime, Mathf.Infinity, dt);
            position += _velocity * dt;

            _pendingZoom += scroll * zoomDistance * speedFactor;
            float remaining = Mathf.SmoothDamp(_pendingZoom, 0, ref _zoomDamping, zoomSmoothTime, Mathf.Infinity, dt);
            position += transform.localRotation * Vector3.forward * (_pendingZoom - remaining);
            _pendingZoom = remaining;
        }
        Vector3 constrained = limitToBounds ? movementBounds.ClosestPoint(position) : position;
        if ((constrained - position).sqrMagnitude > .000001f)
        {
            StopMotion();
            // Rebase the pivot so an orbit at the boundary does not keep fighting the limit.
            if (_drag == DragMode.Orbit) _orbitPivot = constrained + transform.localRotation * Vector3.forward * _orbitDistance;
        }
        transform.localPosition = constrained;
    }

    static float Axis(bool positive, bool negative) { return (positive ? 1f : 0f) - (negative ? 1f : 0f); }

    static float ScrollSteps(float value)
    {
        // Input System 1.20 defaults to normalized wheel steps. Native Windows mode uses 120 units.
        if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
            && (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor))
            value /= 120f;
        return Mathf.Clamp(value, -10f, 10f);
    }

    bool IsDragHeld(Mouse mouse)
    {
        if (mouse == null) return false;
        return _drag == DragMode.Look ? mouse.rightButton.isPressed
            : _drag == DragMode.Pan ? mouse.middleButton.isPressed : mouse.leftButton.isPressed;
    }

    void BeginDrag(DragMode mode, Vector2 screenPosition)
    {
        SyncRotation();
        StopMotion();
        _drag = mode;
        if (mode == DragMode.Pan || mode == DragMode.Orbit)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            Vector3 focus;
            if (Physics.Raycast(ray, out RaycastHit hit, _camera.farClipPlane, focusLayers, QueryTriggerInteraction.Ignore)) focus = hit.point;
            else
            {
                Vector3 up = transform.parent ? transform.parent.up : Vector3.up;
                Vector3 origin = transform.parent ? transform.parent.position : Vector3.zero;
                var plane = new Plane(up, origin);
                focus = plane.Raycast(ray, out float distance) && distance <= _camera.farClipPlane
                    ? ray.GetPoint(distance) : transform.position + transform.forward * fallbackOrbitDistance;
            }
            _orbitPivot = transform.parent ? transform.parent.InverseTransformPoint(focus) : focus;
            _orbitDistance = Mathf.Max(1f, Vector3.Distance(transform.localPosition, _orbitPivot));
            if (mode == DragMode.Orbit)
            {
                // Face the picked point without moving the camera on the capture frame.
                Vector3 direction = _orbitPivot - transform.localPosition;
                transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
                SyncRotation();
            }
        }
        _previousLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _ownsCursor = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void EndDrag()
    {
        _drag = DragMode.None;
        if (!_ownsCursor) return;
        Cursor.lockState = _previousLock;
        Cursor.visible = _previousCursorVisible;
        _ownsCursor = false;
    }

    void SyncRotation()
    {
        _yaw = transform.localEulerAngles.y;
        _pitch = Mathf.Clamp(Mathf.DeltaAngle(0, transform.localEulerAngles.x), -pitchLimit, pitchLimit);
    }

    void StopMotion()
    {
        _velocity = _velocityDamping = Vector3.zero;
        _pendingZoom = _zoomDamping = 0;
    }

    [ContextMenu("Reset to Starting View")]
    public void ResetView()
    {
        if (!_hasStartView) return;
        EndDrag();
        StopMotion();
        transform.localPosition = limitToBounds ? movementBounds.ClosestPoint(_startPosition) : _startPosition;
        transform.localRotation = _startRotation;
        SyncRotation();
    }

    void OnDrawGizmosSelected()
    {
        if (!limitToBounds) return;
        Gizmos.color = new Color(.3f, .8f, 1f, .7f);
        Gizmos.matrix = transform.parent ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        Gizmos.DrawWireCube(movementBounds.center, movementBounds.size);
    }
}
