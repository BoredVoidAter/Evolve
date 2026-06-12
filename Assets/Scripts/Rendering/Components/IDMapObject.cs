using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class IDMapObject : MonoBehaviour
{
    private static readonly int EncodedIDProp = Shader.PropertyToID("_EncodedSubjectID");
    private MaterialPropertyBlock _mpb;

    [Tooltip("If 0, the script assigns a random ID. If non-zero, uses this ID.")]
    public uint groupId = 0;

    void Start()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Renderer rend = GetComponent<Renderer>();
        
        if (groupId == 0)
        {
            groupId = (uint)Random.Range(1, int.MaxValue);
        }
        
        // Use 0-1 Float colors so the shader reads it correctly
        Color idColor = new Color(
            ((groupId) & 0xFF) / 255f,
            ((groupId >> 8) & 0xFF) / 255f,
            ((groupId >> 16) & 0xFF) / 255f,
            ((groupId >> 24) & 0xFF) / 255f
        );

        rend.GetPropertyBlock(_mpb);
        _mpb.SetColor(EncodedIDProp, idColor);
        rend.SetPropertyBlock(_mpb);
    }
}
