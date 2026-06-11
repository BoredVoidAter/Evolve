using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ModernPixelPerfectCamera : MonoBehaviour
{
    public RenderTexture targetRenderTexture;
    
    private Vector3 _targetPosition;
    private Camera _cam;

    void Start()
    {
        _cam = GetComponent<Camera>();
        _targetPosition = transform.position;
    }

    // Call this from your movement scripts instead of moving the transform directly
    public void SetTargetPosition(Vector3 newPos)
    {
        _targetPosition = newPos;
    }

    void LateUpdate()
    {
        if (targetRenderTexture == null) return;

        // Calculate world units per pixel
        float orthoHeight = _cam.orthographicSize * 2f;
        float unitsPerPixelY = orthoHeight / targetRenderTexture.height;
        float unitsPerPixelX = unitsPerPixelY; // Assuming square pixels

        // Snap position
        float snappedX = Mathf.Round(_targetPosition.x / unitsPerPixelX) * unitsPerPixelX;
        float snappedY = Mathf.Round(_targetPosition.y / unitsPerPixelY) * unitsPerPixelY;
        
        // Retain the Z depth
        transform.position = new Vector3(snappedX, snappedY, _targetPosition.z);
    }
}
