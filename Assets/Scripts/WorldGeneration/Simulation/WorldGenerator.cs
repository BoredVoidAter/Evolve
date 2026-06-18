using UnityEngine;
using System.Text;
using System.IO;

public class WorldGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapRadius = 50;
    public float hexOuterRadius = 1f;

    [Header("Terrain Settings")]
    public float heightMultiplier = 15f;
    [Range(0f, 1f)]
    public float waterLevel = 0.35f;

    [Header("Debug Export")]
    public bool saveToCSV = true;
    public bool printToConsole = false;
    public string exportFilename = "BiomeDebugData.csv";

    public HexGridData gridData { get; private set; }

    public void GenerateWorld()
    {
        gridData = new HexGridData();
        float mapMaxRadius = mapRadius * hexOuterRadius * 1.5f;

        for (int q = -mapRadius; q <= mapRadius; q++)
        {
            int r1 = Mathf.Max(-mapRadius, -q - mapRadius);
            int r2 = Mathf.Min(mapRadius, -q + mapRadius);

            for (int r = r1; r <= r2; r++)
            {
                HexCoordinates coords = new HexCoordinates(q, r);
                HexCell cell = new HexCell(coords);

                Vector3 worldPos = coords.ToWorldPosition(hexOuterRadius);
                
                float nx = worldPos.x + 12345.67f;
                float nz = worldPos.z + 54321.89f;

                float continentNoise = Mathf.PerlinNoise(nx * 0.02f, nz * 0.02f);
                float baseNoise = Mathf.PerlinNoise(nx * 0.08f, nz * 0.08f);
                float elevationNoise = (continentNoise * 0.55f) + (baseNoise * 0.45f);

                float mountainNoise = Mathf.PerlinNoise(nx * 0.15f, nz * 0.15f);
                mountainNoise = 1f - Mathf.Abs(mountainNoise * 2f - 1f);
                mountainNoise = Mathf.Pow(mountainNoise, 3f);

                if (elevationNoise > waterLevel)
                {
                    elevationNoise += mountainNoise * 0.4f * ((elevationNoise - waterLevel) * 2f);
                }

                float ravineNoise = Mathf.PerlinNoise(nx * 0.2f, nz * 0.2f);
                ravineNoise = Mathf.Abs(ravineNoise * 2f - 1f);
                
                // FIX: Corrected GLSL smoothstep to Unity C# equivalent
                float tRavine = Mathf.InverseLerp(0.01f, 0.15f, ravineNoise);
                ravineNoise = Mathf.SmoothStep(0f, 1f, tRavine);
                
                elevationNoise *= ravineNoise;

                float distFromCenter = new Vector2(worldPos.x, worldPos.z).magnitude;
                float distRatio = distFromCenter / mapMaxRadius;
                
                // FIX: Corrected GLSL smoothstep to Unity C# equivalent
                float tEdge = Mathf.InverseLerp(0.6f, 1.0f, distRatio);
                float edgeGradient = 1f - Mathf.SmoothStep(0f, 1f, tEdge);
                
                float normalizedElevation = elevationNoise * edgeGradient;

                if (normalizedElevation < waterLevel)
                {
                    float depthRatio = normalizedElevation / waterLevel;
                    normalizedElevation = waterLevel * Mathf.Pow(depthRatio, 2f);
                }

                cell.elevation = normalizedElevation * heightMultiplier;

                float latitude = Mathf.Clamp01(Mathf.Abs(worldPos.z) / mapMaxRadius);
                float baseTemp = 1.0f - latitude;
                
                float tempNoise = Mathf.PerlinNoise(nx * 0.05f, nz * 0.05f) * 0.4f - 0.2f;
                float finalTemp = Mathf.Clamp01(baseTemp + tempNoise);

                if (normalizedElevation > waterLevel) {
                    float heightAboveWater = (normalizedElevation - waterLevel) / (1f - waterLevel);
                    finalTemp = Mathf.Clamp01(finalTemp - (heightAboveWater * 0.6f));
                }

                cell.temperature = finalTemp;

                float mNoise = Mathf.PerlinNoise((nx + 3333.33f) * 0.04f, (nz + 3333.33f) * 0.04f);
                cell.moisture = Mathf.Clamp01(mNoise * 1.2f - 0.1f);

                float actualWaterLevel = heightMultiplier * waterLevel;

                if (cell.elevation <= actualWaterLevel)
                {
                    cell.biome = cell.temperature < 0.25f ? BiomeType.IceCap : BiomeType.Ocean;
                }
                else if (cell.elevation <= actualWaterLevel + (heightMultiplier * 0.06f))
                {
                    cell.biome = cell.temperature < 0.3f ? BiomeType.Tundra : BiomeType.Coast;
                }
                else if (normalizedElevation > 0.65f)
                {
                    cell.biome = cell.temperature < 0.4f ? BiomeType.IceCap : BiomeType.Mountain;
                }
                else
                {
                    if (cell.temperature > 0.65f)
                    {
                        if (cell.moisture < 0.35f) cell.biome = BiomeType.Desert;
                        else if (cell.moisture < 0.65f) cell.biome = BiomeType.Savanna;
                        else cell.biome = BiomeType.TropicalRainforest;
                    }
                    else if (cell.temperature > 0.35f)
                    {
                        if (cell.moisture < 0.45f) cell.biome = BiomeType.Grassland;
                        else cell.biome = BiomeType.Forest;
                    }
                    else
                    {
                        if (cell.moisture < 0.5f) cell.biome = BiomeType.Tundra;
                        else cell.biome = BiomeType.Taiga;
                    }
                }

                gridData.AddCell(cell);
            }
        }

        Debug.Log($"Generated World Data with {gridData.cells.Count} cells.");

        if (saveToCSV || printToConsole)
        {
            ExportBiomeData();
        }
    }

    private void ExportBiomeData()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Q,R,Elevation,Temperature,Moisture,Biome");

        foreach (var cell in gridData.cells.Values)
        {
            sb.AppendLine($"{cell.coordinates.q},{cell.coordinates.r},{cell.elevation:F3},{cell.temperature:F3},{cell.moisture:F3},{cell.biome}");
        }

        if (printToConsole)
            Debug.Log("--- START OF BIOME LAYOUT ---\n" + sb.ToString() + "\n--- END OF BIOME LAYOUT ---");

        if (saveToCSV)
        {
            try
            {
                string path = Path.Combine(Application.dataPath, "..", exportFilename);
                File.WriteAllText(path, sb.ToString());
                Debug.Log($"Saved biome debug data to file: {path}");
            }
            catch (System.Exception e) { Debug.LogError($"Failed to save: {e.Message}"); }
        }
    }
}
