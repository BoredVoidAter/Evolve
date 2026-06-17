using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapRadius = 30; // Increased slightly to give room for multiple islands
    public float hexOuterRadius = 1f;
    
    [Header("Terrain Settings")]
    public float noiseScale = 0.05f;
    public float heightMultiplier = 10f;
    [Range(0f, 1f)]
    public float waterLevel = 0.25f; // Elevation percentage that acts as sea level

    public HexGridData gridData { get; private set; }

    public void GenerateWorld()
    {
        gridData = new HexGridData();
        
        // Approximate max extent of the map for coordinate masking
        float maxDist = mapRadius * hexOuterRadius * Mathf.Sqrt(3f) * 0.8f; 

        for (int q = -mapRadius; q <= mapRadius; q++)
        {
            int r1 = Mathf.Max(-mapRadius, -q - mapRadius);
            int r2 = Mathf.Min(mapRadius, -q + mapRadius);

            for (int r = r1; r <= r2; r++)
            {
                HexCoordinates coords = new HexCoordinates(q, r);
                HexCell cell = new HexCell(coords);
                Vector3 worldPos = coords.ToWorldPosition(hexOuterRadius);

                // 1. Archipelago / Multiple Islands Noise (Layered Noise)
                float nx = worldPos.x + 10000;
                float nz = worldPos.z + 10000;
                
                float elevationNoise = Mathf.PerlinNoise(nx * noiseScale, nz * noiseScale) * 1f;
                elevationNoise += Mathf.PerlinNoise(nx * noiseScale * 2f, nz * noiseScale * 2f) * 0.5f;
                elevationNoise /= 1.5f; // Normalize back to 0-1

                // Create a falloff ONLY at the outer edges of the map so borders are always water
                float distFromCenter = new Vector2(worldPos.x, worldPos.z).magnitude;
                float edgeGradient = 1f;
                float safeZone = maxDist * 0.6f; // Inner 60% of the map has no edge constraints
                
                if (distFromCenter > safeZone)
                {
                    edgeGradient = Mathf.Clamp01(1f - ((distFromCenter - safeZone) / (maxDist - safeZone)));
                }

                // Apply gradient and calculate final elevation
                float normalizedElevation = elevationNoise * edgeGradient;
                cell.elevation = normalizedElevation * heightMultiplier;

                // 2. Generate Moisture & Temperature using highly offset noise coords
                cell.moisture = Mathf.PerlinNoise((worldPos.x + 20000) * noiseScale, (worldPos.z + 20000) * noiseScale);
                float tempNoise = Mathf.PerlinNoise((worldPos.x + 30000) * noiseScale, (worldPos.z + 30000) * noiseScale);
                
                // Drop temperature based on poles (Z distance) and elevation
                float latitude = Mathf.Clamp01(Mathf.Abs(worldPos.z) / maxDist);
                cell.temperature = Mathf.Clamp01(tempNoise * 0.6f + 0.4f - latitude * 0.5f - normalizedElevation * 0.4f);

                // 3. Complete Biome Assignment
                float actualWaterLevel = heightMultiplier * waterLevel;

                if (cell.elevation < actualWaterLevel) 
                {
                    cell.biome = cell.temperature < 0.2f ? BiomeType.IceCap : BiomeType.Ocean;
                }
                else if (cell.elevation < actualWaterLevel + (heightMultiplier * 0.08f)) 
                {
                    cell.biome = BiomeType.Coast;
                }
                else if (cell.elevation > heightMultiplier * 0.75f) 
                {
                    cell.biome = BiomeType.Mountain;
                }
                else 
                {
                    // Whittaker Biome Mapping
                    if (cell.temperature > 0.6f) 
                    {
                        if (cell.moisture < 0.3f) cell.biome = BiomeType.Desert;
                        else if (cell.moisture < 0.6f) cell.biome = BiomeType.Savanna;
                        else cell.biome = BiomeType.TropicalRainforest;
                    }
                    else if (cell.temperature > 0.3f) 
                    {
                        if (cell.moisture < 0.4f) cell.biome = BiomeType.Grassland;
                        else cell.biome = BiomeType.Forest;
                    }
                    else 
                    {
                        if (cell.moisture < 0.4f) cell.biome = BiomeType.Tundra;
                        else cell.biome = BiomeType.Taiga;
                    }
                }

                gridData.AddCell(cell);
            }
        }
        Debug.Log($"Generated World Data with {gridData.cells.Count} cells.");
    }
}
