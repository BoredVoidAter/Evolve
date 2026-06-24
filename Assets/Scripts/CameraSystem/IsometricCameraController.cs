using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class IsometricCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float zoomSpeed = 2f;
    public float minZoom = 5f;
    public float maxZoom = 40f;
    public float heightOffset = 150f;
    private Camera _cam;
    private ModernPixelPerfectCamera _pixelCam;
    private Vector3 _targetPos;
    private Vector2 _lastMousePos;
    private Plane _groundPlane;

    void Start()
    {
        _cam = GetComponent<Camera>();
        _pixelCam = GetComponent<ModernPixelPerfectCamera>();
        _cam.orthographic = true;
        _cam.nearClipPlane = -100f;
        _cam.farClipPlane = 1000f;
        transform.rotation = Quaternion.Euler(30f, 45f, 0f);
        _groundPlane = new Plane(Vector3.up, Vector3.zero);
        Vector3 backward = -transform.forward;
        if (backward.y > 0)
        {
            _targetPos = backward * (heightOffset / backward.y);
        }
        else
        {
            _targetPos = new Vector3(0, heightOffset, 0);
        }
        UpdateCameraPosition();
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // Check for Ctrl + Scroll to toggle underwater view
        if (Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                HexGridVisualizer visualizer = FindObjectOfType<HexGridVisualizer>();
                if (visualizer != null)
                {
                    // Scroll down = Go Underwater | Scroll up = Surface
                    if (scroll < 0 && !visualizer.isUnderwater)
                        visualizer.SetUnderwaterMode(true);
                    else if (scroll > 0 && visualizer.isUnderwater)
                        visualizer.SetUnderwaterMode(false);
                }
            }
        }
        else
        {
            HandleZoom(); // Standard zoom if Ctrl isn't held
        }

        HandlePan();
        Shader.SetGlobalFloat("_GlobalZoom", _cam.orthographicSize);
    }

    private void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float scrollDir = Mathf.Sign(scroll);
            _cam.orthographicSize -= scrollDir * zoomSpeed;
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
        }
    }

    private void HandlePan()
    {
        bool isClickDown = Mouse.current.leftButton.wasPressedThisFrame ||
                           Mouse.current.rightButton.wasPressedThisFrame ||
                           Mouse.current.middleButton.wasPressedThisFrame;
        bool isHolding = Mouse.current.leftButton.isPressed ||
                         Mouse.current.rightButton.isPressed ||
                         Mouse.current.middleButton.isPressed;
        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        if (isClickDown)
        {
            _lastMousePos = currentMousePos;
        }
        if (isHolding)
        {
            Vector2 currentViewportPos = new Vector2(currentMousePos.x / Screen.width, currentMousePos.y / Screen.height);
            Vector2 lastViewportPos = new Vector2(_lastMousePos.x / Screen.width, _lastMousePos.y / Screen.height);
            Ray currentRay = _cam.ViewportPointToRay(currentViewportPos);
            Ray lastRay = _cam.ViewportPointToRay(lastViewportPos);
            if (_groundPlane.Raycast(currentRay, out float currentDist) && _groundPlane.Raycast(lastRay, out float lastDist))
            {
                Vector3 currentPoint = currentRay.GetPoint(currentDist);
                Vector3 lastPoint = lastRay.GetPoint(lastDist);
                Vector3 difference = lastPoint - currentPoint;
                _targetPos += difference;
                _targetPos.y = heightOffset;
                UpdateCameraPosition();
            }
            _lastMousePos = currentMousePos;
        }
    }

    private void UpdateCameraPosition()
    {
        if (_pixelCam != null)
            _pixelCam.SetTargetPosition(_targetPos);
        else
            transform.position = _targetPos;
    }
}
