using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class IDMapObject : MonoBehaviour
{
    public uint groupId = 0;

    void Start()
    {
        if (groupId == 0) groupId = (uint)Random.Range(1, int.MaxValue);

        Color idColor = new Color(
            ((groupId) & 0xFF) / 255f,
            ((groupId >> 8) & 0xFF) / 255f,
            ((groupId >> 16) & 0xFF) / 255f,
            ((groupId >> 24) & 0xFF) / 255f
        );

        // Pass ID via Vertex Colors to avoid disabling the SRP Batcher
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Mesh instancedMesh = mf.mesh; // Clones the mesh so we don't overwrite the shared asset
            Color[] colors = new Color[instancedMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = idColor;
            
            instancedMesh.colors = colors;
            mf.mesh = instancedMesh;
        }
    }
}
