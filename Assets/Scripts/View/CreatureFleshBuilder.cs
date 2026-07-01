using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;

public class CreatureFleshBuilder : MonoBehaviour
{
    public Material fleshMaterial;

    private class RingDef
    {
        public Vector3 position;
        public Vector3 right;
        public Vector3 up;
        public float radius;
        public BoneWeight weight;
    }

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

        // Main body is now perfectly ordered from Head to Tail Tip
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
        List<Transform> spineBones = new List<Transform>();
        List<Transform> tailBones = new List<Transform>();
        
        void FindBones(Transform t)
        {
            if (t.name.StartsWith("Spine_")) spineBones.Add(t);
            else if (t.name.StartsWith("Tail_")) tailBones.Add(t); // Includes Tips
            
            foreach (Transform child in t) FindBones(child);
        }
        FindBones(rootBone);

        int ExtractNumber(string name)
        {
            var match = Regex.Match(name, @"\d+");
            return match.Success ? int.Parse(match.Value) : 0;
        }

        spineBones.Sort((a, b) => 
        {
            int numA = ExtractNumber(a.name);
            int numB = ExtractNumber(b.name);
            if (numA != numB) return numA.CompareTo(numB);
            if (a.name.Contains("_Tip")) return 1;
            if (b.name.Contains("_Tip")) return -1;
            return 0;
        });
        
        tailBones.Sort((a, b) => 
        {
            int numA = ExtractNumber(a.name);
            int numB = ExtractNumber(b.name);
            if (numA != numB) return numA.CompareTo(numB); 
            if (a.name.Contains("_Tip")) return 1;
            if (b.name.Contains("_Tip")) return -1;
            return 0;
        });

        List<Transform> chain = new List<Transform>();
        chain.AddRange(spineBones);
        chain.AddRange(tailBones);
        
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

    private float GetRadius(int index, List<Transform> chain, bool isBody, AnimalDNA dna)
    {
        Transform t = chain[index];
        float baseRadius = 0.3f;
        
        if (isBody)
        {
            if (t.name.StartsWith("Tail_"))
            {
                float distFromEnd = chain.Count - 1 - index;
                float taper = Mathf.Clamp01(distFromEnd / 4f);
                baseRadius = Mathf.Lerp(0.05f, 0.4f, taper);
            }
            else
            {
                int spineCount = 0;
                for (int i = 0; i < chain.Count; i++) 
                    if (chain[i].name.StartsWith("Spine_")) spineCount++;

                int spineIndex = 0;
                for (int i = 0; i < chain.Count; i++)
                {
                    if (chain[i] == t) break;
                    if (chain[i].name.StartsWith("Spine_")) spineIndex++;
                }

                float t_norm = spineCount > 1 ? (float)spineIndex / (spineCount - 1) : 0f;
                float spineProfile = Mathf.Sin(t_norm * Mathf.PI);
                baseRadius = 0.4f + 0.05f * spineProfile;
            }
        }
        else
        {
            float t_norm = chain.Count > 1 ? (float)index / (chain.Count - 1) : 0f;
            BoneTag tag = t.GetComponent<BoneTag>();
            LimbType type = tag != null ? tag.bone.Type : LimbType.Leg;
            
            if (type == LimbType.Leg) baseRadius = 0.25f;
            else if (type == LimbType.Manipulator) baseRadius = 0.15f;
            else if (type == LimbType.Horn) baseRadius = 0.12f;
            else if (type == LimbType.Tentacle) baseRadius = 0.2f;
            else if (type == LimbType.Head)
            {
                float skullProfile = Mathf.Sin(t_norm * Mathf.PI);
                float jawProfile = Mathf.Lerp(1.0f, 0.3f, t_norm);
                float baseR = 0.3f * jawProfile;
                return baseR + 0.2f * skullProfile;
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
        
        List<Transform> bones = new List<Transform>();
        bool isLimb = (!isBody && chain[0].parent != null);
        bool isHead = false;

        if (isLimb)
        {
            bones.Add(chain[0].parent);
            bones.AddRange(chain);
            BoneTag tag = chain[0].GetComponent<BoneTag>();
            if (tag != null && tag.bone.Type == LimbType.Head) isHead = true;
        }
        else
        {
            bones.AddRange(chain);
        }
        
        List<RingDef> rings = new List<RingDef>();
        Vector3 lastUp = Vector3.zero;

        if (isBody)
        {
            // Clean Bishop Frame: Guarantees a perfectly non-twisted tube geometry
            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 forward = Vector3.zero;
                if (i < chain.Count - 1) 
                    forward = chain[i + 1].position - chain[i].position;
                else if (i > 0) 
                    forward = chain[i].position - chain[i - 1].position;
                
                if (forward.sqrMagnitude < 0.001f) forward = chain[i].forward;
                forward = forward.normalized;

                Vector3 up;
                if (i == 0)
                {
                    up = Vector3.ProjectOnPlane(chain[i].up, forward).normalized;
                    if (up.sqrMagnitude < 0.001f) up = Vector3.up; 
                }
                else
                {
                    up = Vector3.ProjectOnPlane(lastUp, forward).normalized;
                    if (up.sqrMagnitude < 0.001f) up = lastUp; 
                }

                Vector3 right = Vector3.Cross(up, forward).normalized;
                lastUp = up;

                rings.Add(new RingDef {
                    position = chain[i].position,
                    right = right,
                    up = up,
                    radius = GetRadius(i, chain, true, dna),
                    weight = new BoneWeight { boneIndex0 = i, weight0 = 1f }
                });
            }
        }
        else
        {
            float r0 = GetRadius(0, chain, false, dna);
            Vector3 outwardDir = chain[0].position - chain[0].parent.position;
            float distToSpine = outwardDir.magnitude;
            
            if (distToSpine < 0.001f)
            {
                outwardDir = chain[0].forward;
                distToSpine = r0 * 2.5f;
            }
            
            Vector3 socketNormal = outwardDir.normalized;
            Vector3 socketUp = chain[0].parent.up;
            if (Mathf.Abs(Vector3.Dot(socketNormal, socketUp)) > 0.95f)
                socketUp = chain[0].parent.forward;
                
            Quaternion socketRot = Quaternion.LookRotation(socketNormal, socketUp);
            Vector3 socketRight = socketRot * Vector3.right;
            socketUp = socketRot * Vector3.up;
            
            float embedDepth = Mathf.Min(r0 * 2.5f, distToSpine * 0.9f);
            float socketRadius = Mathf.Min(r0 * 1.6f, r0 + distToSpine * 0.5f);

            if (isHead)
            {
                embedDepth = distToSpine * 0.5f; 
                socketRadius = r0 * 1.1f;
            }

            Vector3 ring0Pos = chain[0].position - socketNormal * embedDepth;

            rings.Add(new RingDef {
                position = ring0Pos,
                right = socketRight,
                up = socketUp,
                radius = socketRadius,
                weight = new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });
            
            Vector3 ring1Pos = chain[0].position;

            rings.Add(new RingDef {
                position = ring1Pos,
                right = socketRight,
                up = socketUp,
                radius = r0,
                weight = new BoneWeight { boneIndex0 = 0, weight0 = 0.5f, boneIndex1 = 1, weight1 = 0.5f }
            });
            
            Vector3 thighDir = (chain[1].position - chain[0].position);
            if (thighDir.sqrMagnitude < 0.0001f) thighDir = chain[0].forward * 0.1f;
            
            Quaternion limbRot = Quaternion.LookRotation(chain[0].forward, chain[0].up);
            Quaternion transRot = Quaternion.Slerp(socketRot, limbRot, 0.5f);
            
            Vector3 ring2Pos = chain[0].position + thighDir * 0.25f;

            rings.Add(new RingDef {
                position = ring2Pos,
                right = transRot * Vector3.right,
                up = transRot * Vector3.up,
                radius = Mathf.Lerp(r0, GetRadius(1, chain, false, dna), 0.25f),
                weight = new BoneWeight { boneIndex0 = 1, weight0 = 1f }
            });
            
            for (int i = 1; i < chain.Count; i++)
            {
                Vector3 ringPos = chain[i].position;

                rings.Add(new RingDef {
                    position = ringPos,
                    right = chain[i].right,
                    up = chain[i].up,
                    radius = GetRadius(i, chain, false, dna),
                    weight = new BoneWeight { boneIndex0 = i + 1, weight0 = 1f }
                });
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = name + "_Mesh";
        
        int radialSegments = 6;
        int numRings = rings.Count;
        
        List<Vector3> vertices = new List<Vector3>();
        List<BoneWeight> weights = new List<BoneWeight>();
        List<Vector2> uvs = new List<Vector2>(); 
        List<int> triangles = new List<int>();
        List<Matrix4x4> bindPoses = new List<Matrix4x4>();
        
        for (int i = 0; i < bones.Count; i++)
        {
            bindPoses.Add(bones[i].worldToLocalMatrix * skinObj.transform.localToWorldMatrix);
        }
        
        // Start Cap
        int firstCenter = vertices.Count;
        Vector3 ring0Normal = Vector3.Cross(rings[0].right, rings[0].up).normalized;
        Vector3 startCapPos = rings[0].position - ring0Normal * (rings[0].radius * 0.2f);
        vertices.Add(skinObj.transform.InverseTransformPoint(startCapPos));
        weights.Add(rings[0].weight);
        uvs.Add(new Vector2(0.5f, 0f));
        
        int vertexOffset = vertices.Count;
        
        // Ring Vertices 
        for (int i = 0; i < numRings; i++)
        {
            RingDef ring = rings[i];
            float v = (float)i / (numRings > 1 ? numRings - 1 : 1);
            
            for (int j = 0; j <= radialSegments; j++) 
            {
                float u = (float)j / radialSegments;
                float angle = u * Mathf.PI * 2f;
                
                Vector3 localDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                Vector3 worldPos = ring.position + ring.right * localDir.x * ring.radius + ring.up * localDir.y * ring.radius;
                
                vertices.Add(skinObj.transform.InverseTransformPoint(worldPos));
                weights.Add(ring.weight);
                uvs.Add(new Vector2(u, v));
            }
        }

        // Start Cap Triangles (Standard Outward)
        for (int j = 0; j < radialSegments; j++)
        {
            triangles.Add(firstCenter);
            triangles.Add(vertexOffset + j + 1);
            triangles.Add(vertexOffset + j);
        }
        
        // Body Triangles (Standard Outward)
        for (int i = 0; i < numRings - 1; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int currentRing = vertexOffset + i * (radialSegments + 1);
                int nextRing = vertexOffset + (i + 1) * (radialSegments + 1);
                
                int a = currentRing + j;
                int b = currentRing + j + 1;
                int c = nextRing + j;
                int d = nextRing + j + 1;
                
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }
        }
        
        // End Cap Triangles (Standard Outward)
        int lastCenter = vertices.Count;
        Vector3 ringLastNormal = Vector3.Cross(rings[numRings - 1].right, rings[numRings - 1].up).normalized;
        Vector3 endCapPos = rings[numRings - 1].position + ringLastNormal * (rings[numRings - 1].radius * 0.2f);
        vertices.Add(skinObj.transform.InverseTransformPoint(endCapPos));
        weights.Add(rings[numRings - 1].weight);
        uvs.Add(new Vector2(0.5f, 1f));
        
        int lastRingOffset = vertexOffset + (numRings - 1) * (radialSegments + 1);
        for (int j = 0; j < radialSegments; j++)
        {
            triangles.Add(lastCenter);
            triangles.Add(lastRingOffset + j);
            triangles.Add(lastRingOffset + j + 1);
        }
        
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.boneWeights = weights.ToArray();
        mesh.SetTriangles(triangles, 0);
        mesh.bindposes = bindPoses.ToArray();
        
        Color32 idColor32 = new Color32(
            (byte)((creatureId) & 0xFF),
            (byte)((creatureId >> 8) & 0xFF),
            (byte)((creatureId >> 16) & 0xFF),
            (byte)((creatureId >> 24) & 0xFF)
        );
        
        Color32[] colors32 = new Color32[vertices.Count];
        for (int i = 0; i < colors32.Length; i++) colors32[i] = idColor32;
        mesh.SetColors(colors32);
        
        // Unity calculates the normals automatically
        mesh.RecalculateNormals(); 
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        
        smr.bones = bones.ToArray();
        smr.sharedMesh = mesh;
        smr.rootBone = bones[0];
        smr.updateWhenOffscreen = true; 
    }
}
