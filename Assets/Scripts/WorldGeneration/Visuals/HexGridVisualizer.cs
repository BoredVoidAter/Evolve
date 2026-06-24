using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WorldGenerator))]
public class HexGridVisualizer : MonoBehaviour
{
    [Header("Generation Details")]
    [Tooltip("If true, generates a world immediately on Start instead of waiting for UI.")]
    public bool generateOnStart = false;

    [Header("Prefabs & Materials")]
    public GameObject hexPrefab;
    public Material hexMaterial;
    public Material waterMaterial;
    public Material riverMaterial;
    public Material waterfallParticleMaterial;

    [Header("Terrain Settings")]
    public float bedrockElevation = -10f;

    [Header("River Visual Settings")]
    [Tooltip("How high above the hex terrain the river mesh hovers to prevent Z-fighting")]
    public float riverElevationOffset = 0.02f;
    [Tooltip("Global multiplier to easily make all rivers wider or thinner")]
    public float riverWidthMultiplier = 1f;

    private WorldGenerator _generator;
    private List<GameObject> _spawnedHexes = new List<GameObject>();

    [Header("Decorations")]
    public List<BiomeDecoration> biomeDecorations;
    private Dictionary<BiomeType, GameObject> _decorationDict;

    void Start()
    {
        _generator = GetComponent<WorldGenerator>();

        if (generateOnStart)
        {
            _generator.GenerateWorld();
            RenderGrid(_generator.gridData);
        }

        _decorationDict = new Dictionary<BiomeType, GameObject>();
        foreach (var dec in biomeDecorations)
        {
            _decorationDict[dec.biome] = dec.decorationPrefab;
        }
    }

    public void ClearGrid()
    {
        foreach (var hex in _spawnedHexes) Destroy(hex);
        _spawnedHexes.Clear();
    }

    public void RenderGrid(HexGridData data)
    {
        ClearGrid();

        foreach (var kvp in data.cells)
        {
            HexCell cell = kvp.Value;
            GameObject hexGO = Instantiate(hexPrefab, transform);

            float totalHeight = cell.elevation - bedrockElevation;
            hexGO.transform.localScale = new Vector3(_generator.hexOuterRadius, totalHeight, _generator.hexOuterRadius);

            Vector3 worldPos = cell.coordinates.ToWorldPosition(_generator.hexOuterRadius);
            float yPos = bedrockElevation + (totalHeight / 2f);
            hexGO.transform.position = new Vector3(worldPos.x, yPos, worldPos.z);

            Renderer rend = hexGO.GetComponentInChildren<Renderer>();
            MeshFilter mf = hexGO.GetComponentInChildren<MeshFilter>();

            Vector3 anchorPos = new Vector3(worldPos.x, cell.elevation, worldPos.z);

            if (_decorationDict.TryGetValue(cell.biome, out GameObject prefab) && prefab != null)
            {
                GameObject decoration = Instantiate(prefab, anchorPos, Quaternion.identity, hexGO.transform);
            }

            MeshCollider mc = hexGO.GetComponent<MeshCollider>();
            if (mc == null) mc = hexGO.AddComponent<MeshCollider>();

            HexTileInfo info = hexGO.GetComponent<HexTileInfo>();
            if (info == null) info = hexGO.AddComponent<HexTileInfo>();
            info.coordinates = cell.coordinates;

            if (rend != null && mf != null)
            {
                rend.sharedMaterial = hexMaterial;
                Mesh instancedMesh = mf.mesh;

                Vector4[] uv1 = new Vector4[instancedMesh.vertexCount];
                Vector4[] uv2 = new Vector4[instancedMesh.vertexCount];

                for (int i = 0; i < instancedMesh.vertexCount; i++)
                {
                    uv1[i] = new Vector4((float)cell.biome, 0, 0, 0);
                    uv2[i] = new Vector4(cell.elevation, 0, 0, 0);
                }

                instancedMesh.SetUVs(1, uv1);
                instancedMesh.SetUVs(2, uv2);
            }

            _spawnedHexes.Add(hexGO);
        }

        if (waterMaterial != null) CreateGlobalWater();
        RenderRiversAsMesh(data);
        CullFoliageInWater();
    }

    private Vector3 GetCorner(int index, float radius)
    {
        float angle = (-index * 60f - 30f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
    }

    private Vector3 GetEdgeMidpoint(int index, float radius)
    {
        float angle = -index * 60f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (radius * Mathf.Sqrt(3f) / 2f);
    }

    private float GetRiverWidth(HexCell c)
    {
        if (c == null || c.riverVolume == 0) return 0f;
        float R = _generator.hexOuterRadius;
        float baseWidth = R * 0.1f;
        float addedWidth = Mathf.Log(c.riverVolume + 1f) * R * 0.06f;
        return Mathf.Min(baseWidth + addedWidth, R * 0.45f) * riverWidthMultiplier;
    }

    private void RenderRiversAsMesh(HexGridData data)
    {
        GameObject riverSystem = new GameObject("RiverSystem");
        riverSystem.layer = 4;
        riverSystem.transform.SetParent(transform);
        _spawnedHexes.Add(riverSystem);

        MeshFilter mf = riverSystem.AddComponent<MeshFilter>();
        MeshRenderer mr = riverSystem.AddComponent<MeshRenderer>();
        mr.sharedMaterial = riverMaterial != null ? riverMaterial : waterMaterial;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> flowDirs = new List<Vector3>();

        float R = _generator.hexOuterRadius;
        float yOffset = riverElevationOffset;
        float oceanLevel = _generator.heightMultiplier * _generator.waterLevel;

        foreach (var kvp in data.cells)
        {
            HexCell cell = kvp.Value;
            if (cell.riverVolume == 0 || cell.elevation <= oceanLevel) continue;

            float wC = GetRiverWidth(cell);
            Vector3 center = cell.GetWorldPosition(R) + Vector3.up * yOffset;

            Vector3 totalFlow = Vector3.zero;
            for (int i = 0; i < 6; i++)
            {
                if (cell.riverEdges[i] == 1) totalFlow += GetEdgeMidpoint(i, 1f).normalized;
                if (cell.riverEdges[i] == -1) totalFlow -= GetEdgeMidpoint(i, 1f).normalized;
            }
            Vector3 hubFlow = totalFlow.normalized;
            if (hubFlow == Vector3.zero) hubFlow = Vector3.forward;

            int cIndex = vertices.Count;
            vertices.Add(center);
            uvs.Add(new Vector2(0.5f, 0.5f));
            flowDirs.Add(hubFlow);

            int hubStartIndex = vertices.Count;
            for (int i = 0; i < 6; i++)
            {
                vertices.Add(center + GetCorner(i, wC));
                uvs.Add(new Vector2(0.5f, 0.5f));
                flowDirs.Add(hubFlow);
            }

            List<int> activeArms = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                if (cell.riverEdges[i] != 0) activeArms.Add(i);
            }

            HashSet<int> hubTrianglesToRender = new HashSet<int>();
            bool isStraightRiver = activeArms.Count == 2 && (activeArms[0] + 3) % 6 == activeArms[1];

            if (isStraightRiver)
            {
            }
            else if (activeArms.Count <= 1)
            {
                for (int i = 0; i < 6; i++) hubTrianglesToRender.Add(i);
            }
            else
            {
                List<int> activeT = new List<int>();
                foreach (int a in activeArms)
                {
                    activeT.Add((a + 5) % 6);
                }
                activeT.Sort();

                int maxGap = -1;
                int maxGapStart = -1;
                int maxGapEnd = -1;

                for (int i = 0; i < activeT.Count; i++)
                {
                    int current = activeT[i];
                    int next = activeT[(i + 1) % activeT.Count];
                    int gap = (next - current + 6) % 6;

                    if (gap > maxGap)
                    {
                        maxGap = gap;
                        maxGapStart = current;
                        maxGapEnd = next;
                    }
                }

                int currT = maxGapEnd;
                while (true)
                {
                    hubTrianglesToRender.Add(currT);
                    if (currT == maxGapStart) break;
                    currT = (currT + 1) % 6;
                }
            }

            for (int i = 0; i < 6; i++)
            {
                if (hubTrianglesToRender.Contains(i))
                {
                    triangles.Add(cIndex);
                    triangles.Add(hubStartIndex + ((i + 1) % 6));
                    triangles.Add(hubStartIndex + i);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                if (cell.riverEdges[i] != 0)
                {
                    int iPrev = (i + 5) % 6;
                    HexCell neighbor = data.GetCell(cell.coordinates.GetNeighbor(i));

                    float wN = (neighbor != null && neighbor.riverVolume > 0) ? GetRiverWidth(neighbor) : wC;
                    float wEdge = (wC + wN) * 0.5f;

                    Vector3 cornerLeft = center + GetCorner(iPrev, R);
                    Vector3 cornerRight = center + GetCorner(i, R);
                    Vector3 edgeMid = (cornerLeft + cornerRight) * 0.5f;
                    Vector3 edgeDir = (cornerRight - cornerLeft).normalized;

                    Vector3 edgeLeft = edgeMid - edgeDir * (wEdge * 0.5f);
                    Vector3 edgeRight = edgeMid + edgeDir * (wEdge * 0.5f);

                    Vector3 armFlow = (cell.riverEdges[i] == 1) ? (edgeMid - center).normalized : (center - edgeMid).normalized;

                    Vector3 hubLeft;
                    Vector3 hubRight;

                    if (isStraightRiver)
                    {
                        hubLeft = center - edgeDir * (wC * 0.5f);
                        hubRight = center + edgeDir * (wC * 0.5f);
                    }
                    else
                    {
                        hubLeft = center + GetCorner(iPrev, wC);
                        hubRight = center + GetCorner(i, wC);
                    }

                    int start = vertices.Count;
                    vertices.Add(hubLeft);   uvs.Add(new Vector2(0f, 0f)); flowDirs.Add(armFlow);
                    vertices.Add(hubRight);  uvs.Add(new Vector2(1f, 0f)); flowDirs.Add(armFlow);
                    vertices.Add(edgeLeft);  uvs.Add(new Vector2(0f, 0f)); flowDirs.Add(armFlow);
                    vertices.Add(edgeRight); uvs.Add(new Vector2(1f, 0f)); flowDirs.Add(armFlow);

                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                    triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 1);

                    if (cell.riverEdges[i] == 1 && neighbor != null && cell.elevation > neighbor.elevation)
                    {
                        float bottomY = neighbor.elevation + yOffset;
                        if (bottomY < oceanLevel + yOffset) bottomY = oceanLevel + yOffset;

                        Vector3 wfBotLeft = edgeLeft; wfBotLeft.y = bottomY;
                        Vector3 wfBotRight = edgeRight; wfBotRight.y = bottomY;

                        Vector3 outDir = (neighbor.GetWorldPosition(R) - cell.GetWorldPosition(R)).normalized;
                        wfBotLeft += outDir * 0.05f;
                        wfBotRight += outDir * 0.05f;
                        Vector3 wfFlow = (wfBotLeft - edgeLeft).normalized;

                        int wfStart = vertices.Count;
                        vertices.Add(edgeLeft);   uvs.Add(new Vector2(0f, 0f)); flowDirs.Add(wfFlow);
                        vertices.Add(edgeRight);  uvs.Add(new Vector2(1f, 0f)); flowDirs.Add(wfFlow);
                        vertices.Add(wfBotLeft);  uvs.Add(new Vector2(0f, 0f)); flowDirs.Add(wfFlow);
                        vertices.Add(wfBotRight); uvs.Add(new Vector2(1f, 0f)); flowDirs.Add(wfFlow);

                        triangles.Add(wfStart); triangles.Add(wfStart + 2); triangles.Add(wfStart + 3);
                        triangles.Add(wfStart); triangles.Add(wfStart + 3); triangles.Add(wfStart + 1);

                        float dropHeight = cell.elevation - bottomY;
                        if (dropHeight > 0f)
                        {
                            Vector3 splashCenter = (wfBotLeft + wfBotRight) * 0.5f;
                            CreateWaterfallParticles(splashCenter, outDir, dropHeight, cell.riverVolume);
                        }
                    }
                }
            }
        }

        Mesh riverMesh = new Mesh();
        riverMesh.name = "ProceduralRivers";
        riverMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        riverMesh.SetVertices(vertices);
        riverMesh.SetTriangles(triangles, 0);
        riverMesh.SetUVs(0, uvs);
        riverMesh.SetUVs(1, flowDirs);
        riverMesh.RecalculateNormals();

        mf.mesh = riverMesh;
        MeshCollider rmc = riverSystem.AddComponent<MeshCollider>();
        rmc.sharedMesh = riverMesh;
    }

    private void CreateWaterfallParticles(Vector3 bottomPos, Vector3 flowDirXZ, float dropHeight, int volume)
    {
        GameObject psObj = new GameObject("WaterfallSplash");
        psObj.transform.SetParent(transform);
        psObj.transform.position = bottomPos;
        psObj.transform.rotation = Quaternion.LookRotation(Vector3.up + flowDirXZ * 0.5f);
        _spawnedHexes.Add(psObj);

        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = psObj.GetComponent<ParticleSystemRenderer>();
        if (waterfallParticleMaterial != null) psr.material = waterfallParticleMaterial;

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startSpeed = 2f + (dropHeight * 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(_generator.hexOuterRadius * 0.03f, _generator.hexOuterRadius * 0.08f);
        main.startColor = new Color(0.8f, 0.95f, 1f, 0.85f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 40f + (dropHeight * 15f) + (volume * 5f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_generator.hexOuterRadius * 0.3f, 0.1f, 0.1f);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0f)));
    }

    private void CreateGlobalWater()
    {
        GameObject waterPlane = new GameObject("GlobalWaterPlane");
        waterPlane.layer = 4;
        waterPlane.transform.SetParent(transform);

        float maxWaterHeight = _generator.heightMultiplier * _generator.waterLevel;
        waterPlane.transform.position = new Vector3(0, maxWaterHeight + 0.05f, 0);

        MeshFilter mf = waterPlane.AddComponent<MeshFilter>();
        MeshRenderer rend = waterPlane.AddComponent<MeshRenderer>();

        Mesh waterMesh = new Mesh();
        Vector3[] vertices = new Vector3[7];
        Vector2[] uvs = new Vector2[7];
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        float giantRadius = Mathf.Sqrt(3f) * _generator.hexOuterRadius * (_generator.mapRadius + 0.75f);

        for (int i = 0; i < 6; i++)
        {
            float angleRad = i * 60f * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(giantRadius * Mathf.Cos(angleRad), 0, giantRadius * Mathf.Sin(angleRad));
            uvs[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(angleRad), 0.5f + 0.5f * Mathf.Sin(angleRad));
        }

        int[] triangles = new int[18];
        for (int i = 0; i < 6; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i == 5) ? 1 : i + 2;
            triangles[i * 3 + 2] = i + 1;
        }

        waterMesh.vertices = vertices;
        waterMesh.uv = uvs;
        waterMesh.triangles = triangles;
        waterMesh.RecalculateNormals();

        mf.mesh = waterMesh;
        MeshCollider wmc = waterPlane.AddComponent<MeshCollider>();
        wmc.sharedMesh = waterMesh;

        rend.sharedMaterial = waterMaterial;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        _spawnedHexes.Add(waterPlane);
    }

    private struct RiverSegment
    {
        public Vector3 a;
        public Vector3 b;
        public float radius;
    }

    private void CullFoliageInWater()
    {
        float oceanLevel = _generator.heightMultiplier * _generator.waterLevel;
        float R = _generator.hexOuterRadius;

        List<RiverSegment> riverSegments = new List<RiverSegment>();
        foreach (var cell in _generator.gridData.cells.Values)
        {
            if (cell.elevation <= oceanLevel) continue;
            
            if (cell.riverVolume > 0)
            {
                Vector3 center = cell.GetWorldPosition(R);
                // Fixed: The GetRiverWidth returns the FULL width. For the capsule checking, 
                // we want the radius (half width) to prevent removing extra foliage miles from the river.
                float riverRadius = GetRiverWidth(cell) * 0.5f; 
                
                for (int i = 0; i < 6; i++)
                {
                    if (cell.riverEdges[i] != 0)
                    {
                        Vector3 edgeMid = center + GetEdgeMidpoint(i, R);
                        riverSegments.Add(new RiverSegment {
                            a = center,
                            b = edgeMid,
                            radius = riverRadius
                        });
                    }
                }
            }
        }

        ParticleSystem[] allPS = GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in allPS)
        {
            if (ps.gameObject.name.Contains("WaterfallSplash")) continue;

            // Ensures all initial burst emissions run before doing our calculations
            ps.Simulate(ps.main.duration, false, true);

            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[ps.main.maxParticles];
            int count = ps.GetParticles(particles);
            int keptCount = 0;
            
            ParticleSystemSimulationSpace simSpace = ps.main.simulationSpace;
            Transform customTransform = ps.main.customSimulationSpace;

            for (int i = 0; i < count; i++)
            {
                Vector3 worldPos;
                if (simSpace == ParticleSystemSimulationSpace.World)
                    worldPos = particles[i].position;
                else if (simSpace == ParticleSystemSimulationSpace.Custom && customTransform != null)
                    worldPos = customTransform.TransformPoint(particles[i].position);
                else
                    worldPos = ps.transform.TransformPoint(particles[i].position);

                if (worldPos.y <= oceanLevel + 0.05f) continue;

                Vector3 p2D = new Vector3(worldPos.x, 0, worldPos.z);
                bool inRiver = false;
                
                foreach (var seg in riverSegments)
                {
                    Vector3 a2D = new Vector3(seg.a.x, 0, seg.a.z);
                    Vector3 b2D = new Vector3(seg.b.x, 0, seg.b.z);
                    float dist = DistancePointLine(p2D, a2D, b2D);
                    
                    if (dist < seg.radius + 0.05f) // Padding to stop foliage scaling right on the bank bounds
                    {
                        inRiver = true;
                        break;
                    }
                }

                if (!inRiver)
                {
                    particles[keptCount] = particles[i];
                    keptCount++;
                }
            }

            ps.SetParticles(particles, keptCount);

            // FIX: You MUST turn off emission. 
            // Leaving emission on combined with looping particles means new ones will just 
            // re-burst inside the river spaces on the very next simulation frame.
            var emission = ps.emission;
            emission.enabled = false;

            ps.Play(false);
        }
    }

    private float DistancePointLine(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 pa = p - a;
        Vector3 ba = b - a;
        float sqrLen = Vector3.Dot(ba, ba);
        if (sqrLen == 0f) return Vector3.Distance(p, a);
        float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / sqrLen);
        return Vector3.Distance(p, a + ba * h);
    }
}
