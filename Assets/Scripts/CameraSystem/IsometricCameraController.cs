using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class IsometricCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float zoomSpeed = 2f; // Lowered slightly since new Input System scroll values are larger
    public float minZoom = 5f;
    public float maxZoom = 40f;
    public float heightOffset = 150f; // Maintained high above the terrain to prevent clipping

    private Camera _cam;
    private ModernPixelPerfectCamera _pixelCam;
    private Vector3 _targetPos;
    private Vector2 _lastMousePos;
    private Plane _groundPlane;

    void Start()
    {
        _cam = GetComponent<Camera>();
        _pixelCam = GetComponent<ModernPixelPerfectCamera>();

        // Enforce orthographic setup to ensure consistent isometric scale
        _cam.orthographic = true;
        _cam.nearClipPlane = -100f; // Generous clip bounds for high terrain
        _cam.farClipPlane = 1000f;

        // Set textbook isometric perspective rotation
        transform.rotation = Quaternion.Euler(30f, 45f, 0f);

        // Define our panning reference plane at Y=0
        _groundPlane = new Plane(Vector3.up, Vector3.zero);

        // Pre-calculate starting position to look at the center of the world
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
        
        HandleZoom();
        HandlePan();
    }

    private void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            // Normalize scroll value (-1 or 1) because Input System scroll wheel values vary wildly (typically 120 per notch)
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
            Ray currentRay = _cam.ScreenPointToRay(currentMousePos);
            Ray lastRay = _cam.ScreenPointToRay(_lastMousePos);

            if (_groundPlane.Raycast(currentRay, out float currentDist) && _groundPlane.Raycast(lastRay, out float lastDist))
            {
                Vector3 currentPoint = currentRay.GetPoint(currentDist);
                Vector3 lastPoint = lastRay.GetPoint(lastDist);

                Vector3 difference = lastPoint - currentPoint;
                _targetPos += difference;
                
                // Lock Y position so zooming/panning doesn't plunge us into the terrain
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
