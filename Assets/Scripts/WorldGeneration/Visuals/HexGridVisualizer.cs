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

                Color biomeColor = GetColorForBiome(cell.biome);
                
                // Dynamically shade the ocean floor darker based on depth!
                if (cell.biome == BiomeType.Ocean)
                {
                    float maxWaterHeight = _generator.heightMultiplier * _generator.waterLevel;
                    float depthPercent = Mathf.Clamp01(cell.elevation / maxWaterHeight);
                    
                    // Multiply color so deeper waters appear darker blue
                    biomeColor *= Mathf.Lerp(0.3f, 1.0f, depthPercent);
                }

                Vector4[] uv1 = new Vector4[instancedMesh.vertexCount];
                Vector4[] uv2 = new Vector4[instancedMesh.vertexCount];

                for (int i = 0; i < instancedMesh.vertexCount; i++)
                {
                    uv1[i] = new Vector4((float)cell.biome, biomeColor.r, biomeColor.g, biomeColor.b);
                    uv2[i] = new Vector4(cell.elevation, 0, 0, 0);
                }
                instancedMesh.SetUVs(1, uv1);
                instancedMesh.SetUVs(2, uv2);
            }

            _spawnedHexes.Add(hexGO);
        }

        if (waterMaterial != null)
        {
            GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            waterPlane.name = "GlobalWaterPlane";
            waterPlane.transform.SetParent(transform);

            float maxWaterHeight = _generator.heightMultiplier * _generator.waterLevel;
            float yPos = maxWaterHeight + 0.05f;

            waterPlane.transform.position = new Vector3(0, yPos, 0); 
            waterPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Scale to comfortably cover the entire generated hex radius area
            float mapSize = _generator.mapRadius * _generator.hexOuterRadius * 4f;
            waterPlane.transform.localScale = new Vector3(mapSize, mapSize, 1f);

            Renderer rend = waterPlane.GetComponent<Renderer>();
            rend.sharedMaterial = waterMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            _spawnedHexes.Add(waterPlane);
        }
    }

    private Color GetColorForBiome(BiomeType biome)
    {
        switch (biome)
        {
            case BiomeType.Ocean: return new Color(0.1f, 0.4f, 0.9f); // Base Deep Blue
            case BiomeType.Coast: return new Color(0.9f, 0.8f, 0.5f);
            case BiomeType.Mountain: return new Color(0.6f, 0.6f, 0.6f);
            case BiomeType.Grassland: return new Color(0.3f, 0.7f, 0.2f);
            case BiomeType.Desert: return new Color(0.8f, 0.7f, 0.3f);
            case BiomeType.Tundra: return new Color(0.7f, 0.8f, 0.8f);
            case BiomeType.Savanna: return new Color(0.7f, 0.8f, 0.3f);
            case BiomeType.TropicalRainforest: return new Color(0.1f, 0.5f, 0.2f);
            case BiomeType.Forest: return new Color(0.2f, 0.6f, 0.2f);
            case BiomeType.Taiga: return new Color(0.2f, 0.5f, 0.4f);
            case BiomeType.IceCap: return new Color(0.9f, 0.95f, 1f);
            default: return Color.magenta;
        }
    }
}
