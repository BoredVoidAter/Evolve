using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class IDMapObject : MonoBehaviour
{
    private static readonly int EncodedIDProp = Shader.PropertyToID("_EncodedSubjectID");
    private static MaterialPropertyBlock _mpb;

    [Tooltip("If 0, the script assigns a random ID. If non-zero, uses this ID (e.g., for grouping limbs).")]
    public uint groupId = 0;

    void Start()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        
        Renderer rend = GetComponent<Renderer>();
        
        // If no group ID was assigned by a parent, generate a random one
        if (groupId == 0)
        {
            // Quick random color ID (we just need it to be unique to neighbor objects)
            groupId = (uint)Random.Range(1, int.MaxValue);
        }

        // Convert the uint to a Color so the shader can read it easily
        Color32 idColor = new Color32(
            (byte)((groupId) & 0xFF),
            (byte)((groupId >> 8) & 0xFF),
            (byte)((groupId >> 16) & 0xFF),
            (byte)((groupId >> 24) & 0xFF)
        );

        // Apply to the renderer without instantiating a new material
        rend.GetPropertyBlock(_mpb);
        _mpb.SetColor(EncodedIDProp, idColor);
        rend.SetPropertyBlock(_mpb);
    }
}
