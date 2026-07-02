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
            if (child.name.EndsWith("_Flesh") || child.name == "BodyFlesh" || child.name == "MembraneFlesh" || child.name == "FeatureFlesh")
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

        // Phase 2: Grow Membranes
        if (dna.Membranes.WebbingAmount > 0)
        {
            BuildMembranes(dna, rootBoneTransform, creatureId);
        }

        // Phase 2: Grow Surface Features
        if (dna.Features.Type != SurfaceFeatureType.None && dna.Features.Density > 0)
        {
            BuildSurfaceFeatures(dna, spineChain, rootBoneTransform, creatureId);
        }

        LineRenderer[] lrs = rootBoneTransform.GetComponentsInChildren<LineRenderer>();
        foreach (var lr in lrs) lr.enabled = false;

        TextMesh[] tms = rootBoneTransform.GetComponentsInChildren<TextMesh>();
        foreach (var tm in tms) tm.gameObject.SetActive(false);
    }

    private Material CreateFleshMaterial(AnimalDNA dna)
    {
        Shader toonShader = Shader.Find("Shader Graphs/SH_UniversalToon");
        if (toonShader == null) toonShader = Shader.Find("Standard");

        Material mat;
        if (fleshMaterial != null) 
        {
            mat = new Material(fleshMaterial);
            if (mat.shader.name == "Standard" && toonShader.name != "Standard") 
                mat.shader = toonShader;
        }
        else 
        {
            mat = new Material(toonShader);
        }

        Color c1 = dna.Skin.PrimaryColor.a == 0 ? new Color(0.8f, 0.4f, 0.3f, 1f) : dna.Skin.PrimaryColor;
        Color c2 = dna.Skin.SecondaryColor.a == 0 ? new Color(0.6f, 0.2f, 0.2f, 1f) : dna.Skin.SecondaryColor;

        if (mat.HasProperty("_Col1")) mat.SetColor("_Col1", c1);
        if (mat.HasProperty("_Col2")) mat.SetColor("_Col2", c2);
        if (mat.HasProperty("_Color")) mat.color = c1;

        if (dna.Skin.PatternMask != null)
        {
            if (mat.HasProperty("_Tex")) 
            {
                mat.SetTexture("_Tex", dna.Skin.PatternMask);
                mat.mainTextureScale = new Vector2(2, 4); 
            }
            if (mat.HasProperty("_USE_TEXTURE")) mat.SetFloat("_USE_TEXTURE", 1f);
        }
        else
        {
            if (mat.HasProperty("_USE_TEXTURE")) mat.SetFloat("_USE_TEXTURE", 0f);
        }

        return mat;
    }

    private void SetupIDAndLayer(GameObject obj, uint creatureId, Mesh mesh)
    {
        obj.layer = LayerMask.NameToLayer("ID");
        IDMapObject idObj = obj.AddComponent<IDMapObject>();
        idObj.groupId = creatureId;

        Color32 idColor32 = new Color32(
            (byte)((creatureId) & 0xFF),
            (byte)((creatureId >> 8) & 0xFF),
            (byte)((creatureId >> 16) & 0xFF),
            (byte)((creatureId >> 24) & 0xFF)
        );

        Color32[] colors32 = new Color32[mesh.vertexCount];
        for (int i = 0; i < colors32.Length; i++) colors32[i] = idColor32;
        mesh.colors32 = colors32;
    }

    private List<Transform> GetSpineChain(Transform rootBone)
    {
        List<Transform> spineBones = new List<Transform>();
        List<Transform> tailBones = new List<Transform>();
        
        void FindBones(Transform t)
        {
            if (t.name.StartsWith("Spine_")) spineBones.Add(t);
            else if (t.name.StartsWith("Tail_")) tailBones.Add(t);
            foreach (Transform child in t) FindBones(child);
        }
        FindBones(rootBone);

        int ExtractNumber(string name)
        {
            var match = Regex.Match(name, @"\d+");
            return match.Success ? int.Parse(match.Value) : 0;
        }

        spineBones.Sort((a, b) => {
            int numA = ExtractNumber(a.name);
            int numB = ExtractNumber(b.name);
            if (numA != numB) return numA.CompareTo(numB);
            if (a.name.Contains("_Tip")) return 1;
            if (b.name.Contains("_Tip")) return -1;
            return 0;
        });
        
        tailBones.Sort((a, b) => {
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
                if (child.name.Contains("_J0")) limbRoots.Add(child);
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
        
        float tissueBulk = 1.0f + (dna.Tissue.MuscleMass * 0.15f) + (dna.Tissue.FatMass * 0.25f);
        float regionMultiplier = dna.Morphogenesis.GlobalGrowthRate > 0 ? dna.Morphogenesis.GlobalGrowthRate : 1.0f;
        
        BodyRegionType currentRegion = BodyRegionType.Thorax;

        if (isBody)
        {
            if (t.name.StartsWith("Tail_"))
            {
                currentRegion = BodyRegionType.Tail;
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

                if (t_norm < 0.3f) 
                {
                    currentRegion = BodyRegionType.Neck;
                    // Thickened base neck radius slightly so it doesn't break
                    baseRadius = 0.25f + 0.1f * spineProfile; 
                } 
                else if (t_norm > 0.65f) 
                {
                    currentRegion = BodyRegionType.Abdomen;
                    baseRadius = 0.35f + 0.1f * spineProfile;
                } 
                else 
                {
                    currentRegion = BodyRegionType.Thorax;
                    baseRadius = 0.45f + 0.05f * spineProfile;
                }
            }
        }
        else
        {
            float t_norm = chain.Count > 1 ? (float)index / (chain.Count - 1) : 0f;
            BoneTag tag = t.GetComponent<BoneTag>();
            LimbType type = tag != null ? tag.bone.Type : LimbType.Leg;
            
            currentRegion = BodyRegionType.Limb;

            if (type == LimbType.Leg) baseRadius = 0.25f;
            else if (type == LimbType.Manipulator) baseRadius = 0.15f;
            else if (type == LimbType.Horn) baseRadius = 0.12f;
            else if (type == LimbType.Tentacle) baseRadius = 0.2f;
            else if (type == LimbType.Head)
            {
                currentRegion = BodyRegionType.Head;
                float profile = 1.0f;
                // Shape it like a skull: narrow at connection, thick in middle, narrow at snout
                if (t_norm < 0.4f) profile = Mathf.Lerp(0.3f, 1.0f, t_norm / 0.4f);
                else profile = Mathf.Lerp(1.0f, 0.2f, (t_norm - 0.4f) / 0.6f);
                baseRadius = 0.4f * profile; 
            } 
            else baseRadius = 0.2f;
            
            baseRadius *= Mathf.Lerp(1.0f, 0.2f, t_norm);
        }

        if (dna.Morphogenesis.Regions != null)
        {
            foreach (var region in dna.Morphogenesis.Regions)
            {
                if (region.RegionType == currentRegion)
                {
                    float regSize = region.RelativeSize > 0 ? region.RelativeSize : 1f;
                    float regGrowth = region.GrowthRate > 0 ? region.GrowthRate : 1f;
                    regionMultiplier *= (regSize * regGrowth);
                    break;
                }
            }
        }

        return baseRadius * tissueBulk * regionMultiplier;
    }

    private void BuildChainMesh(string name, List<Transform> chain, AnimalDNA dna, bool isBody, uint creatureId, Transform creatureRoot)
    {
        if (chain.Count < 2) return;
        
        GameObject skinObj = new GameObject(name);
        skinObj.transform.SetParent(creatureRoot);
        skinObj.transform.localPosition = Vector3.zero;
        skinObj.transform.localRotation = Quaternion.identity;
        
        SkinnedMeshRenderer smr = skinObj.AddComponent<SkinnedMeshRenderer>();
        smr.material = CreateFleshMaterial(dna);
        
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
            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 forward = Vector3.zero;
                if (i < chain.Count - 1) forward = chain[i + 1].position - chain[i].position;
                else if (i > 0) forward = chain[i].position - chain[i - 1].position;
                
                if (forward.sqrMagnitude < 0.001f) forward = chain[i].forward;
                forward = forward.normalized;

                Vector3 up = (i == 0) ? Vector3.ProjectOnPlane(chain[i].up, forward).normalized : Vector3.ProjectOnPlane(lastUp, forward).normalized;
                if (up.sqrMagnitude < 0.001f) up = i == 0 ? Vector3.up : lastUp;

                Vector3 right = Vector3.Cross(up, forward).normalized;
                lastUp = up;

                rings.Add(new RingDef {
                    position = chain[i].position, right = right, up = up,
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
            if (Mathf.Abs(Vector3.Dot(socketNormal, socketUp)) > 0.95f) socketUp = chain[0].parent.forward;
                
            Quaternion socketRot = Quaternion.LookRotation(socketNormal, socketUp);
            Vector3 socketRight = socketRot * Vector3.right;
            socketUp = socketRot * Vector3.up;
            
            float embedDepth = 0f;
            float socketRadius = r0;
            
            // FIXED HEAD SEAM: Creates a standard spherical dome socket embedding cleanly into the neck
            if (isHead)
            {
                socketRadius = r0 * 1.1f; 
                embedDepth = r0 * 1.0f;
            }
            else
            {
                socketRadius = Mathf.Min(r0 * 1.6f, r0 + distToSpine * 0.5f);
                embedDepth = Mathf.Min(r0 * 2.5f, distToSpine * 0.9f);
            }

            rings.Add(new RingDef {
                position = chain[0].position - socketNormal * embedDepth,
                right = socketRight, up = socketUp, radius = socketRadius,
                weight = new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });
            
            // Calculate direction and clamp pivot offsets early to prevent overlapping/folding
            Vector3 thighDir = (chain[1].position - chain[0].position);
            if (thighDir.sqrMagnitude < 0.0001f) thighDir = chain[0].forward * 0.1f;
            float thighLen = thighDir.magnitude;
            Vector3 thighDirNorm = thighDir / thighLen;

            float pivotOffset = (chain[0].position - chain[0].parent.position).magnitude < 0.001f ? Mathf.Min(r0 * 0.5f, thighLen * 0.15f) : 0f;

            rings.Add(new RingDef {
                position = chain[0].position + socketNormal * pivotOffset,
                right = socketRight, up = socketUp, radius = r0,
                weight = pivotOffset > 0f 
                         ? new BoneWeight { boneIndex0 = 0, weight0 = 0.2f, boneIndex1 = 1, weight1 = 0.8f }
                         : new BoneWeight { boneIndex0 = 0, weight0 = 0.5f, boneIndex1 = 1, weight1 = 0.5f }
            });
            
            Quaternion transRot = Quaternion.Slerp(socketRot, Quaternion.LookRotation(chain[0].forward, chain[0].up), 0.5f);
            
            // Ensure the transition ring is strictly further along the bone than the pivot ring
            float transDist = Mathf.Max(thighLen * 0.25f, pivotOffset + 0.05f);

            rings.Add(new RingDef {
                position = chain[0].position + thighDirNorm * transDist,
                right = transRot * Vector3.right, up = transRot * Vector3.up,
                radius = Mathf.Lerp(r0, GetRadius(1, chain, false, dna), 0.25f),
                weight = new BoneWeight { boneIndex0 = 1, weight0 = 1f }
            });
            
            for (int i = 1; i < chain.Count; i++)
            {
                float r = GetRadius(i, chain, false, dna);
                
                if (i < chain.Count - 1)
                {
                    Vector3 dirFromPrev = (chain[i].position - chain[i-1].position).normalized;
                    if (dirFromPrev.sqrMagnitude < 0.001f) dirFromPrev = chain[i].parent.forward;
                    
                    Vector3 dirToNext = (chain[i+1].position - chain[i].position).normalized;
                    if (dirToNext.sqrMagnitude < 0.001f) dirToNext = chain[i].forward;

                    float prevDist = Vector3.Distance(chain[i].position, chain[i-1].position);
                    float nextDist = Vector3.Distance(chain[i+1].position, chain[i].position);
                    
                    float blendOffsetPrev = Mathf.Min(r * 0.6f, prevDist * 0.25f);
                    float blendOffsetNext = Mathf.Min(r * 0.6f, nextDist * 0.25f);
                    
                    rings.Add(new RingDef {
                        position = chain[i].position - dirFromPrev * blendOffsetPrev,
                        right = chain[i].right, up = chain[i].up,
                        radius = Mathf.Lerp(rings[rings.Count-1].radius, r, 0.8f),
                        weight = new BoneWeight { boneIndex0 = i, weight0 = 1f }
                    });
                    
                    rings.Add(new RingDef {
                        position = chain[i].position,
                        right = chain[i].right, up = chain[i].up,
                        radius = r,
                        weight = new BoneWeight { boneIndex0 = i, weight0 = 0.5f, boneIndex1 = i + 1, weight1 = 0.5f }
                    });
                    
                    rings.Add(new RingDef {
                        position = chain[i].position + dirToNext * blendOffsetNext,
                        right = chain[i].right, up = chain[i].up,
                        radius = Mathf.Lerp(r, GetRadius(i+1, chain, false, dna), 0.2f),
                        weight = new BoneWeight { boneIndex0 = i + 1, weight0 = 1f }
                    });
                }
                else
                {
                    rings.Add(new RingDef {
                        position = chain[i].position,
                        right = chain[i].right, up = chain[i].up,
                        radius = r,
                        weight = new BoneWeight { boneIndex0 = i + 1, weight0 = 1f }
                    });
                }
            }
        }

        Mesh mesh = new Mesh { name = name + "_Mesh" };
        int radialSegments = 6;
        int numRings = rings.Count;
        
        List<Vector3> vertices = new List<Vector3>();
        List<BoneWeight> weights = new List<BoneWeight>();
        List<Vector2> uvs = new List<Vector2>(); 
        List<int> triangles = new List<int>();
        List<Matrix4x4> bindPoses = new List<Matrix4x4>();
        
        for (int i = 0; i < bones.Count; i++) bindPoses.Add(bones[i].worldToLocalMatrix * skinObj.transform.localToWorldMatrix);
        
        int firstCenter = vertices.Count;
        Vector3 ring0Normal = Vector3.Cross(rings[0].right, rings[0].up).normalized;
        vertices.Add(skinObj.transform.InverseTransformPoint(rings[0].position - ring0Normal * (rings[0].radius * 0.2f)));
        weights.Add(rings[0].weight); uvs.Add(new Vector2(0.5f, 0f));
        
        int vertexOffset = vertices.Count;
        
        for (int i = 0; i < numRings; i++)
        {
            float v = (float)i / (numRings > 1 ? numRings - 1 : 1);
            for (int j = 0; j <= radialSegments; j++) 
            {
                float angle = ((float)j / radialSegments) * Mathf.PI * 2f;
                Vector3 worldPos = rings[i].position + rings[i].right * Mathf.Cos(angle) * rings[i].radius + rings[i].up * Mathf.Sin(angle) * rings[i].radius;
                vertices.Add(skinObj.transform.InverseTransformPoint(worldPos));
                weights.Add(rings[i].weight); uvs.Add(new Vector2((float)j / radialSegments, v));
            }
        }

        for (int j = 0; j < radialSegments; j++) { triangles.Add(firstCenter); triangles.Add(vertexOffset + j + 1); triangles.Add(vertexOffset + j); }
        
        for (int i = 0; i < numRings - 1; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int cR = vertexOffset + i * (radialSegments + 1), nR = vertexOffset + (i + 1) * (radialSegments + 1);
                triangles.Add(cR + j); triangles.Add(cR + j + 1); triangles.Add(nR + j);
                triangles.Add(cR + j + 1); triangles.Add(nR + j + 1); triangles.Add(nR + j);
            }
        }
        
        int lastCenter = vertices.Count;
        Vector3 ringLastNormal = Vector3.Cross(rings[numRings - 1].right, rings[numRings - 1].up).normalized;
        vertices.Add(skinObj.transform.InverseTransformPoint(rings[numRings - 1].position + ringLastNormal * (rings[numRings - 1].radius * 0.2f)));
        weights.Add(rings[numRings - 1].weight); uvs.Add(new Vector2(0.5f, 1f));
        
        int lastRingOffset = vertexOffset + (numRings - 1) * (radialSegments + 1);
        for (int j = 0; j < radialSegments; j++) { triangles.Add(lastCenter); triangles.Add(lastRingOffset + j); triangles.Add(lastRingOffset + j + 1); }
        
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs);
        mesh.boneWeights = weights.ToArray(); mesh.SetTriangles(triangles, 0); mesh.bindposes = bindPoses.ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        
        SetupIDAndLayer(skinObj, creatureId, mesh);

        smr.bones = bones.ToArray(); smr.sharedMesh = mesh; smr.rootBone = bones[0];
    }

    private void BuildMembranes(AnimalDNA dna, Transform rootBoneTransform, uint creatureId)
    {
        List<Transform> branchingJoints = new List<Transform>();
        void TraverseForBranches(Transform t)
        {
            int childLimbs = 0;
            foreach (Transform child in t)
            {
                if (child.name.Contains("_J0") || child.name.Contains("Sub_J0")) childLimbs++;
            }
            if (childLimbs > 1) branchingJoints.Add(t);
            foreach (Transform child in t) TraverseForBranches(child);
        }
        TraverseForBranches(rootBoneTransform);

        foreach (var joint in branchingJoints)
        {
            List<Transform> siblingLimbs = new List<Transform>();
            foreach (Transform child in joint)
            {
                if (child.name.Contains("_J0") || child.name.Contains("Sub_J0")) siblingLimbs.Add(child);
            }
            
            for (int i = 0; i < siblingLimbs.Count - 1; i++)
            {
                BuildWebBetween(siblingLimbs[i], siblingLimbs[i + 1], dna, joint, rootBoneTransform, creatureId);
            }
            if (dna.BodyPlan.Symmetry == SymmetryType.Radial && siblingLimbs.Count > 2)
            {
                BuildWebBetween(siblingLimbs[siblingLimbs.Count - 1], siblingLimbs[0], dna, joint, rootBoneTransform, creatureId);
            }
        }
    }

    private void BuildWebBetween(Transform rootA, Transform rootB, AnimalDNA dna, Transform parentJoint, Transform creatureRoot, uint creatureId)
    {
        List<Transform> chainA = GetLimbChain(rootA);
        List<Transform> chainB = GetLimbChain(rootB);
        int length = Mathf.Min(chainA.Count, chainB.Count);
        if (length < 2) return;

        GameObject webObj = new GameObject("MembraneFlesh");
        webObj.transform.SetParent(creatureRoot);
        webObj.transform.localPosition = Vector3.zero;
        
        SkinnedMeshRenderer smr = webObj.AddComponent<SkinnedMeshRenderer>();
        
        Material webMat = CreateFleshMaterial(dna);
        if (webMat.HasProperty("_Col1"))
        {
            Color c = webMat.GetColor("_Col1");
            webMat.SetColor("_Col1", new Color(c.r * 1.2f, c.g * 0.8f, c.b * 0.8f, 1f));
        }
        if (webMat.HasProperty("_Color"))
        {
            Color c = webMat.color;
            webMat.color = new Color(c.r * 1.2f, c.g * 0.8f, c.b * 0.8f, 1f);
        }
        smr.material = webMat;

        List<Transform> bones = new List<Transform>();
        bones.AddRange(chainA);
        bones.AddRange(chainB);

        Mesh mesh = new Mesh { name = "MembraneMesh" };
        List<Vector3> verts = new List<Vector3>();
        List<BoneWeight> weights = new List<BoneWeight>();
        List<int> tris = new List<int>();
        List<Matrix4x4> bindPoses = new List<Matrix4x4>();

        for (int i = 0; i < bones.Count; i++) bindPoses.Add(bones[i].worldToLocalMatrix * webObj.transform.localToWorldMatrix);

        for (int i = 0; i < length; i++)
        {
            Vector3 midPoint = (chainA[i].position + chainB[i].position) * 0.5f;

            verts.Add(webObj.transform.InverseTransformPoint(chainA[i].position));
            weights.Add(new BoneWeight { boneIndex0 = i, weight0 = 1f });

            verts.Add(webObj.transform.InverseTransformPoint(midPoint));
            weights.Add(new BoneWeight { boneIndex0 = i, weight0 = 0.5f, boneIndex1 = chainA.Count + i, weight1 = 0.5f });

            verts.Add(webObj.transform.InverseTransformPoint(chainB[i].position));
            weights.Add(new BoneWeight { boneIndex0 = chainA.Count + i, weight0 = 1f });
        }

        for (int i = 0; i < length - 1; i++)
        {
            int row1 = i * 3;
            int row2 = (i + 1) * 3;
            
            tris.Add(row1); tris.Add(row2); tris.Add(row1 + 1);
            tris.Add(row1 + 1); tris.Add(row2); tris.Add(row2 + 1);
            
            tris.Add(row1 + 1); tris.Add(row2 + 1); tris.Add(row1 + 2);
            tris.Add(row1 + 2); tris.Add(row2 + 1); tris.Add(row2 + 2);
            
            tris.Add(row1); tris.Add(row1 + 1); tris.Add(row2);
            tris.Add(row1 + 1); tris.Add(row2 + 1); tris.Add(row2);
            tris.Add(row1 + 1); tris.Add(row1 + 2); tris.Add(row2 + 1);
            tris.Add(row1 + 2); tris.Add(row2 + 2); tris.Add(row2 + 1);
        }

        mesh.SetVertices(verts);
        mesh.boneWeights = weights.ToArray();
        mesh.SetTriangles(tris, 0);
        mesh.bindposes = bindPoses.ToArray();
        mesh.RecalculateNormals();

        SetupIDAndLayer(webObj, creatureId, mesh);

        smr.bones = bones.ToArray();
        smr.sharedMesh = mesh;
        smr.rootBone = bones[0];
    }

    private void BuildSurfaceFeatures(AnimalDNA dna, List<Transform> spineChain, Transform creatureRoot, uint creatureId)
    {
        GameObject featureObj = new GameObject("FeatureFlesh");
        featureObj.transform.SetParent(creatureRoot);
        featureObj.transform.localPosition = Vector3.zero;

        SkinnedMeshRenderer smr = featureObj.AddComponent<SkinnedMeshRenderer>();
        
        Material featMat = CreateFleshMaterial(dna);
        if (dna.Features.Type == SurfaceFeatureType.Spike || dna.Features.Type == SurfaceFeatureType.Plate)
        {
            if (featMat.HasProperty("_Col1")) featMat.SetColor("_Col1", new Color(0.2f, 0.2f, 0.2f, 1f));
            if (featMat.HasProperty("_Col2")) featMat.SetColor("_Col2", new Color(0.1f, 0.1f, 0.1f, 1f));
            if (featMat.HasProperty("_Color")) featMat.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            if (featMat.HasProperty("_USE_TEXTURE")) featMat.SetFloat("_USE_TEXTURE", 0f);
        }
        smr.material = featMat;

        Mesh mesh = new Mesh { name = "FeaturesMesh" };
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<BoneWeight> weights = new List<BoneWeight>();
        List<int> tris = new List<int>();
        List<Matrix4x4> bindPoses = new List<Matrix4x4>();

        for (int i = 0; i < spineChain.Count; i++) bindPoses.Add(spineChain[i].worldToLocalMatrix * featureObj.transform.localToWorldMatrix);

        float featLength = dna.Features.Length > 0 ? dna.Features.Length : 0.5f;
        float featThick = dna.Features.Thickness > 0 ? dna.Features.Thickness : 0.1f;

        Vector3 lastUp = Vector3.zero;

        for (int i = 0; i < spineChain.Count; i++)
        {
            Transform bone = spineChain[i];
            
            // Note: in our spine logic, 'forward' points toward the tail!
            Vector3 forward = Vector3.zero;
            if (i < spineChain.Count - 1) forward = spineChain[i + 1].position - spineChain[i].position;
            else if (i > 0) forward = spineChain[i].position - spineChain[i - 1].position;
            
            if (forward.sqrMagnitude < 0.001f) forward = bone.forward;
            forward = forward.normalized;

            Vector3 up = (i == 0) ? Vector3.ProjectOnPlane(bone.up, forward).normalized : Vector3.ProjectOnPlane(lastUp, forward).normalized;
            if (up.sqrMagnitude < 0.001f) up = i == 0 ? Vector3.up : lastUp;
            lastUp = up;

            if (Random.value > dna.Features.Density && dna.Features.Type != SurfaceFeatureType.Ridge && dna.Features.Type != SurfaceFeatureType.Frill) continue;

            int bIdx = i;
            float radius = GetRadius(i, spineChain, true, dna);
            
            Vector3 baseCenter = bone.position + up * (radius * 0.85f);
            Vector3 right = Vector3.Cross(up, forward).normalized;

            if (dna.Features.Type == SurfaceFeatureType.Spike)
            {
                Vector3 tip = baseCenter + up * featLength;
                Vector3 fwdOffset = forward * featThick;
                Vector3 rgtOffset = right * featThick;

                int vStart = verts.Count;
                verts.Add(featureObj.transform.InverseTransformPoint(tip)); uvs.Add(new Vector2(0.5f, 1f));
                verts.Add(featureObj.transform.InverseTransformPoint(baseCenter + fwdOffset)); uvs.Add(new Vector2(0.5f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(baseCenter + rgtOffset)); uvs.Add(new Vector2(1f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(baseCenter - fwdOffset)); uvs.Add(new Vector2(0.5f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(baseCenter - rgtOffset)); uvs.Add(new Vector2(0f, 0f));

                for (int v = 0; v < 5; v++) weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });

                tris.AddRange(new int[] { 
                    vStart, vStart+2, vStart+1, 
                    vStart, vStart+3, vStart+2, 
                    vStart, vStart+4, vStart+3, 
                    vStart, vStart+1, vStart+4 
                });
            }
            else if (dna.Features.Type == SurfaceFeatureType.Plate)
            {
                Vector3 baseR = baseCenter + right * featThick;
                Vector3 baseL = baseCenter - right * featThick;
                Vector3 frontTip = baseCenter + forward * (featLength * 0.6f) + up * (featLength * 0.2f);
                Vector3 backTip = baseCenter - forward * (featLength * 0.6f) + up * (featLength * 0.2f);
                Vector3 topTip = baseCenter + up * featLength;

                int vStart = verts.Count;
                verts.Add(featureObj.transform.InverseTransformPoint(baseR)); uvs.Add(new Vector2(1f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(baseL)); uvs.Add(new Vector2(0f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(frontTip)); uvs.Add(new Vector2(0.5f, 1f));
                verts.Add(featureObj.transform.InverseTransformPoint(backTip)); uvs.Add(new Vector2(0.5f, 1f));
                verts.Add(featureObj.transform.InverseTransformPoint(topTip)); uvs.Add(new Vector2(0.5f, 1f));

                for (int v = 0; v < 5; v++) weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });

                tris.AddRange(new int[] { vStart, vStart+2, vStart+4, vStart, vStart+4, vStart+3 });
                tris.AddRange(new int[] { vStart+1, vStart+4, vStart+2, vStart+1, vStart+3, vStart+4 });
            }
            else if (dna.Features.Type == SurfaceFeatureType.Ridge)
            {
                Vector3 topTip = baseCenter + up * featLength;
                Vector3 fwdOffset = forward * (featLength * 0.6f); 
                
                int vStart = verts.Count;
                verts.Add(featureObj.transform.InverseTransformPoint(baseCenter + right * featThick)); uvs.Add(new Vector2(1f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(baseCenter - right * featThick)); uvs.Add(new Vector2(0f, 0f));
                verts.Add(featureObj.transform.InverseTransformPoint(topTip + fwdOffset)); uvs.Add(new Vector2(1f, 1f));
                verts.Add(featureObj.transform.InverseTransformPoint(topTip - fwdOffset)); uvs.Add(new Vector2(0f, 1f));

                for (int v = 0; v < 4; v++) weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });

                tris.AddRange(new int[] {
                    vStart, vStart+2, vStart+3,
                    vStart+1, vStart+3, vStart+2,
                    vStart, vStart+3, vStart+1,
                    vStart, vStart+1, vStart+2
                });
            }
            else if (dna.Features.Type == SurfaceFeatureType.Frill && i == 0) 
            {
                // BRAND NEW FRILL GENERATOR:
                // Generates a proper, segmented 3D fan shape that extends back over the neck.
                int segments = 8;
                float angleSpread = 100f; // -100 to 100 degree span (200 degrees over the back of head)
                
                Vector3 fwdThick = forward * featThick * 0.5f;
                int vStart = verts.Count;
                
                // 1. Back Base Loop (attached to skin)
                for(int s = 0; s <= segments; s++) {
                    float u = (float)s / segments;
                    float angle = Mathf.Lerp(-angleSpread, angleSpread, u);
                    Vector3 dir = Quaternion.AngleAxis(angle, forward) * up;
                    verts.Add(featureObj.transform.InverseTransformPoint(bone.position + dir * radius - fwdThick));
                    uvs.Add(new Vector2(u, 0f));
                    weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });
                }
                
                // 2. Back Edge Loop (the outer rim, tilted backward over the neck)
                for(int s = 0; s <= segments; s++) {
                    float u = (float)s / segments;
                    float angle = Mathf.Lerp(-angleSpread, angleSpread, u);
                    // (up + forward) points up and backwards over the spine
                    Vector3 dir = Quaternion.AngleAxis(angle, forward) * (up + forward * 0.4f).normalized;
                    verts.Add(featureObj.transform.InverseTransformPoint(bone.position + dir * (radius + featLength * 2f) - fwdThick));
                    uvs.Add(new Vector2(u, 1f));
                    weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });
                }
                
                // 3. Front Base Loop (attached to skin)
                for(int s = 0; s <= segments; s++) {
                    float u = (float)s / segments;
                    float angle = Mathf.Lerp(-angleSpread, angleSpread, u);
                    Vector3 dir = Quaternion.AngleAxis(angle, forward) * up;
                    verts.Add(featureObj.transform.InverseTransformPoint(bone.position + dir * radius + fwdThick));
                    uvs.Add(new Vector2(u, 0f));
                    weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });
                }
                
                // 4. Front Edge Loop (outer rim)
                for(int s = 0; s <= segments; s++) {
                    float u = (float)s / segments;
                    float angle = Mathf.Lerp(-angleSpread, angleSpread, u);
                    Vector3 dir = Quaternion.AngleAxis(angle, forward) * (up + forward * 0.4f).normalized;
                    verts.Add(featureObj.transform.InverseTransformPoint(bone.position + dir * (radius + featLength * 2f) + fwdThick));
                    uvs.Add(new Vector2(u, 1f));
                    weights.Add(new BoneWeight { boneIndex0 = bIdx, weight0 = 1f });
                }

                int bBack = vStart;
                int eBack = vStart + segments + 1;
                int bFront = vStart + (segments + 1) * 2;
                int eFront = vStart + (segments + 1) * 3;

                for(int s = 0; s < segments; s++) 
                {
                    // Back face
                    tris.AddRange(new int[] { 
                        bBack+s, eBack+s+1, eBack+s, 
                        bBack+s, bBack+s+1, eBack+s+1 
                    });
                    // Front face (reversed winding)
                    tris.AddRange(new int[] { 
                        bFront+s, eFront+s, eFront+s+1, 
                        bFront+s, eFront+s+1, bFront+s+1 
                    });
                    // Outer rim (connecting front and back faces)
                    tris.AddRange(new int[] {
                        eBack+s, eBack+s+1, eFront+s+1,
                        eBack+s, eFront+s+1, eFront+s
                    });
                }
            }
        }

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.boneWeights = weights.ToArray();
        mesh.SetTriangles(tris, 0);
        mesh.bindposes = bindPoses.ToArray();
        mesh.RecalculateNormals();

        SetupIDAndLayer(featureObj, creatureId, mesh);

        smr.bones = spineChain.ToArray();
        smr.sharedMesh = mesh;
        smr.rootBone = spineChain[0];
    }
}
