using System.Collections.Generic;
using UnityEngine;

public class CreatureFleshBuilder : MonoBehaviour
{
    public Material fleshMaterial;
    
    public void BuildFlesh(AnimalDNA dna, Transform rootBoneTransform)
    {
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in rootBoneTransform)
        {
            if (child.name.EndsWith("_Flesh") || child.name == "BodyFlesh")
            {
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (var obj in toDestroy)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
        
        uint creatureId = (uint)Random.Range(1, int.MaxValue);
        
        List<Transform> spineChain = GetSpineChain(rootBoneTransform);
        BuildChainMesh("BodyFlesh", spineChain, dna, true, creatureId, rootBoneTransform);
        
        List<Transform> limbRoots = FindLimbRoots(rootBoneTransform);
        foreach (var limbRoot in limbRoots)
        {
            List<Transform> limbChain = GetLimbChain(limbRoot);
            BuildChainMesh(limbRoot.name + "_Flesh", limbChain, dna, false, creatureId, rootBoneTransform);
        }
        
        LineRenderer[] lrs = rootBoneTransform.GetComponentsInChildren<LineRenderer>();
        foreach (var lr in lrs) lr.enabled = false;
        TextMesh[] tms = rootBoneTransform.GetComponentsInChildren<TextMesh>();
        foreach (var tm in tms) tm.gameObject.SetActive(false);
    }
    
    private List<Transform> GetSpineChain(Transform rootBone)
    {
        List<Transform> chain = new List<Transform>();
        Transform curr = rootBone;
        while (curr != null)
        {
            chain.Add(curr);
            Transform next = null;
            foreach (Transform child in curr)
            {
                if (child.name.StartsWith("Spine_") || child.name.StartsWith("Tail_") || child.name.EndsWith("_Tip"))
                {
                    next = child;
                    break;
                }
            }
            curr = next;
        }
        return chain;
    }
    
    private List<Transform> FindLimbRoots(Transform rootBone)
    {
        List<Transform> limbRoots = new List<Transform>();
        void Traverse(Transform t)
        {
            foreach (Transform child in t)
            {
                if (child.name.Contains("_J0"))
                {
                    limbRoots.Add(child);
                }
                Traverse(child);
            }
        }
        Traverse(rootBone);
        return limbRoots;
    }
    
    private List<Transform> GetLimbChain(Transform limbRoot)
    {
        List<Transform> chain = new List<Transform>();
        Transform curr = limbRoot;
        while (curr != null)
        {
            chain.Add(curr);
            Transform next = null;
            foreach (Transform child in curr)
            {
                if (child.name.Contains("_Tip") || (child.name.Contains("_J") && !child.name.Contains("_J0")))
                {
                    next = child;
                    break;
                }
            }
            curr = next;
        }
        return chain;
    }
    
    private float GetRadius(int index, int total, bool isBody, Transform t, AnimalDNA dna)
    {
        float t_norm = total > 1 ? (float)index / (total - 1) : 0f;
        float baseRadius = 0.3f;
        
        if (isBody)
        {
            if (t.name.Contains("Tail_"))
            {
                float distFromEnd = total - 1 - index;
                float taper = Mathf.Clamp01(distFromEnd / 4f); 
                baseRadius = Mathf.Lerp(0.05f, 0.4f, taper);
            }
            else
            {
                float spineProfile = Mathf.Sin(t_norm * Mathf.PI);
                baseRadius = 0.4f + 0.05f * spineProfile;
            }
        }
        else
        {
            BoneTag tag = t.GetComponent<BoneTag>();
            LimbType type = tag != null ? tag.bone.Type : LimbType.Leg;
            
            if (type == LimbType.Leg) baseRadius = 0.25f;
            else if (type == LimbType.Manipulator) baseRadius = 0.15f;
            else if (type == LimbType.Horn) baseRadius = 0.12f;
            else if (type == LimbType.Tentacle) baseRadius = 0.2f;
            else if (type == LimbType.Head)
            {
                float profile = Mathf.Sin(t_norm * Mathf.PI);
                return 0.25f + 0.2f * profile; 
            }
            else baseRadius = 0.2f;
            
            baseRadius *= Mathf.Lerp(1.0f, 0.2f, t_norm);
        }
        
        return baseRadius;
    }
    
    private void BuildChainMesh(string name, List<Transform> chain, AnimalDNA dna, bool isBody, uint creatureId, Transform creatureRoot)
    {
        if (chain.Count < 2) return;
        GameObject skinObj = new GameObject(name);
        skinObj.transform.SetParent(creatureRoot);
        skinObj.transform.localPosition = Vector3.zero;
        skinObj.transform.localRotation = Quaternion.identity;
        skinObj.layer = LayerMask.NameToLayer("ID");
        
        SkinnedMeshRenderer smr = skinObj.AddComponent<SkinnedMeshRenderer>();
        if (fleshMaterial != null) smr.material = fleshMaterial;
        else smr.material = new Material(Shader.Find("Standard"));
        
        IDMapObject idObj = skinObj.AddComponent<IDMapObject>();
        idObj.groupId = creatureId;
        
        Mesh mesh = new Mesh();
        mesh.name = name + "_Mesh";
        int radialSegments = 6;
        int numRings = chain.Count;
        
        List<Vector3> vertices = new List<Vector3>();
        List<BoneWeight> weights = new List<BoneWeight>();
        List<int> triangles = new List<int>();
        List<Matrix4x4> bindPoses = new List<Matrix4x4>();
        
        for (int i = 0; i < chain.Count; i++)
        {
            bindPoses.Add(chain[i].worldToLocalMatrix * skinObj.transform.localToWorldMatrix);
        }
        
        int firstCenter = vertices.Count;
        vertices.Add(skinObj.transform.InverseTransformPoint(chain[0].position));
        weights.Add(new BoneWeight() { boneIndex0 = 0, weight0 = 1f });
        
        for (int j = 0; j < radialSegments; j++)
        {
            int next_j = (j + 1) % radialSegments;
            triangles.Add(firstCenter);
            triangles.Add(firstCenter + 1 + next_j);
            triangles.Add(firstCenter + 1 + j);
        }
        
        int vertexOffset = vertices.Count;

        // Since bone twisting in IK was solved, directly use the bone's own axes 
        // to securely build untwisted geometry completely matching the bind poses!
        for (int i = 0; i < numRings; i++)
        {
            Transform t = chain[i];
            float radius = GetRadius(i, numRings, isBody, t, dna);
            for (int j = 0; j < radialSegments; j++)
            {
                float angle = ((float)j / radialSegments) * Mathf.PI * 2f;
                Vector3 localDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                
                // Construct directly from the un-rolled coordinate axes!
                Vector3 worldPos = t.position + t.right * localDir.x * radius + t.up * localDir.y * radius;
                
                vertices.Add(skinObj.transform.InverseTransformPoint(worldPos));
                weights.Add(new BoneWeight() { boneIndex0 = i, weight0 = 1f });
            }
        }
        
        for (int i = 0; i < numRings - 1; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int next_j = (j + 1) % radialSegments;
                int currentRing = vertexOffset + i * radialSegments;
                int nextRing = vertexOffset + (i + 1) * radialSegments;
                int a = currentRing + j;
                int b = currentRing + next_j;
                int c = nextRing + j;
                int d = nextRing + next_j;
                
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
        }
        
        int lastCenter = vertices.Count;
        vertices.Add(skinObj.transform.InverseTransformPoint(chain[numRings - 1].position));
        weights.Add(new BoneWeight() { boneIndex0 = numRings - 1, weight0 = 1f });
        
        int lastRingOffset = vertexOffset + (numRings - 1) * radialSegments;
        for (int j = 0; j < radialSegments; j++)
        {
            int next_j = (j + 1) % radialSegments;
            triangles.Add(lastCenter);
            triangles.Add(lastRingOffset + j);
            triangles.Add(lastRingOffset + next_j);
        }
        
        mesh.SetVertices(vertices);
        mesh.boneWeights = weights.ToArray();
        mesh.SetTriangles(triangles, 0);
        mesh.bindposes = bindPoses.ToArray();
        
        Color idColor = new Color(
            ((creatureId) & 0xFF) / 255f,
            ((creatureId >> 8) & 0xFF) / 255f,
            ((creatureId >> 16) & 0xFF) / 255f,
            ((creatureId >> 24) & 0xFF) / 255f
        );
        Color[] colors = new Color[vertices.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = idColor;
        mesh.colors = colors;
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        smr.bones = chain.ToArray();
        smr.sharedMesh = mesh;
        smr.rootBone = chain[0];
    }
}
