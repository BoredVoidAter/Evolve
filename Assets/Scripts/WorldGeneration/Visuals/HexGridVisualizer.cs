using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WorldGenerator))]
public class HexGridVisualizer : MonoBehaviour
{
    [Header("Prefabs & Materials")]
    public GameObject hexPrefab;
    public Material hexMaterial;
    public Material waterMaterial;
    
    [Header("Terrain Settings")]
    public float bedrockElevation = -10f;
    
    private WorldGenerator _generator;
    private List<GameObject> _spawnedHexes = new List<GameObject>();
    
    void Start()
    {
        _generator = GetComponent<WorldGenerator>();
        _generator.GenerateWorld();
        RenderGrid(_generator.gridData);
    }
    
    public void RenderGrid(HexGridData data)
    {
        foreach (var hex in _spawnedHexes) Destroy(hex);
        _spawnedHexes.Clear();
        
        foreach (var kvp in data.cells)
        {
            HexCell cell = kvp.Value;
            GameObject hexGO = Instantiate(hexPrefab, transform);
            float totalHeight = cell.elevation - bedrockElevation;
            
            hexGO.transform.localScale = new Vector3(
                _generator.hexOuterRadius * 2f,
                totalHeight,
                _generator.hexOuterRadius * 2f
            );
            
            Vector3 worldPos = cell.coordinates.ToWorldPosition(_generator.hexOuterRadius);
            float yPos = bedrockElevation + (totalHeight / 2f);
            hexGO.transform.position = new Vector3(worldPos.x, yPos, worldPos.z);
            
            Renderer rend = hexGO.GetComponentInChildren<Renderer>();
            MeshFilter mf = hexGO.GetComponentInChildren<MeshFilter>();
            if (rend != null && mf != null)
            {
                rend.sharedMaterial = hexMaterial;
                Mesh instancedMesh = mf.mesh;
                
                if (cell.biome == BiomeType.Ocean)
                {
                    float maxWaterHeight = _generator.heightMultiplier * _generator.waterLevel;
                    float depthPercent = Mathf.Clamp01(cell.elevation / maxWaterHeight);
                }
                
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
        
        if (waterMaterial != null)
        {
            // Instead of a Quad, we generate a giant hexagon mesh to perfectly fit the map boundary
            GameObject waterPlane = new GameObject("GlobalWaterPlane");
            waterPlane.transform.SetParent(transform);
            
            float maxWaterHeight = _generator.heightMultiplier * _generator.waterLevel;
            float yPos = maxWaterHeight + 0.05f;
            waterPlane.transform.position = new Vector3(0, yPos, 0);
            
            MeshFilter mf = waterPlane.AddComponent<MeshFilter>();
            MeshRenderer rend = waterPlane.AddComponent<MeshRenderer>();
            
            Mesh waterMesh = new Mesh();
            waterMesh.name = "WaterHexagon";
            
            Vector3[] vertices = new Vector3[7];
            Vector2[] uvs = new Vector2[7];
            
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            
            // Calculate a radius slightly larger than the outermost hex to ensure tight coverage
            float giantRadius = Mathf.Sqrt(3f) * _generator.hexOuterRadius * (_generator.mapRadius + 0.75f);
            
            // Generate the 6 corners of the giant bounding flat-topped hexagon
            for (int i = 0; i < 6; i++)
            {
                float angleRad = i * 60f * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(giantRadius * Mathf.Cos(angleRad), 0, giantRadius * Mathf.Sin(angleRad));
                uvs[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(angleRad), 0.5f + 0.5f * Mathf.Sin(angleRad));
            }
            
            // 6 triangles connecting the center to the edges
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
            rend.sharedMaterial = waterMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            
            _spawnedHexes.Add(waterPlane);
        }
    }
    
}
