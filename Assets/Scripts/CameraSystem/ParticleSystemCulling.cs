using UnityEngine;

[RequireComponent(typeof(ParticleSystemRenderer))]
public class ParticleSystemCulling : MonoBehaviour
{
    public float hideAtZoom = 4.5f; // Match this to your Shader's Fade End Zoom
    private ParticleSystemRenderer _renderer;
    private Camera _cam;

    void Start()
    {
        _renderer = GetComponent<ParticleSystemRenderer>();
        _cam = Camera.main;
    }

    void Update()
    {
        if (_cam == null) return;
        
        // Turn off the renderer if we are zoomed out past the threshold
        bool shouldBeVisible = _cam.orthographicSize < hideAtZoom;
        
        if (_renderer.enabled != shouldBeVisible)
        {
            _renderer.enabled = shouldBeVisible;
        }
    }
}
