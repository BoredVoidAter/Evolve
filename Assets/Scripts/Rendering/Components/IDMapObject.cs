using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class IDMapObject : MonoBehaviour
{
    public uint groupId = 0;

    void Start()
    {
        if (groupId == 0) groupId = (uint)Random.Range(1, int.MaxValue);
        Color32 idColor = new Color32(
            (byte)((groupId) & 0xFF),
            (byte)((groupId >> 8) & 0xFF),
            (byte)((groupId >> 16) & 0xFF),
            (byte)((groupId >> 24) & 0xFF)
        );
        
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Mesh instancedMesh = mf.mesh;
            Color32[] colors = new Color32[instancedMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = idColor;
            instancedMesh.colors32 = colors;
            mf.mesh = instancedMesh;
        }

        SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            Mesh instancedMesh = smr.sharedMesh;
            Color32[] colors = new Color32[instancedMesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = idColor;
            instancedMesh.colors32 = colors;
            smr.sharedMesh = instancedMesh;
        }
    }
}
