using UnityEngine;
using System.Text;
using System.IO;
using System.Collections.Generic;

public class WorldGenerator : MonoBehaviour
{
    [Header("Map Settings")]
    public int mapRadius = 50;
    public float hexOuterRadius = 1f;

    [Header("Terrain Settings")]
    public float heightMultiplier = 15f;
    [Range(0f, 1f)]
    public float waterLevel = 0.35f;

    [Header("River Settings")]
    public int targetRiverCount = 15;
    [Range(0f, 1f)]
    [Tooltip("Higher values allow rivers to carve through hills to reach the sea, making them much longer.")]
    public float riverLengthBias = 0.7f; 

    [Header("Debug Export")]
    public bool saveToCSV = true;
    public bool printToConsole = false;
    public string exportFilename = "BiomeDebugData.csv";

    public HexGridData gridData { get; private set; }
    private System.Random rng;

    public void GenerateWorld()
    {
        gridData = new HexGridData();
        rng = new System.Random(12345);
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
                float tRavine = Mathf.InverseLerp(0.01f, 0.15f, ravineNoise);
                ravineNoise = Mathf.SmoothStep(0f, 1f, tRavine);
                elevationNoise *= ravineNoise;

                float distFromCenter = new Vector2(worldPos.x, worldPos.z).magnitude;
                float distRatio = distFromCenter / mapMaxRadius;
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

                if (normalizedElevation > waterLevel) 
                {
                    float heightAboveWater = (normalizedElevation - waterLevel) / (1f - waterLevel);
                    finalTemp = Mathf.Clamp01(finalTemp - (heightAboveWater * 0.6f));
                }
                cell.temperature = finalTemp;

                float mNoise = Mathf.PerlinNoise((nx + 3333.33f) * 0.04f, (nz + 3333.33f) * 0.04f);
                cell.moisture = Mathf.Clamp01(mNoise * 1.2f - 0.1f);

                gridData.AddCell(cell);
            }
        }

        GenerateRivers();
        CalculateRiverVolumes();
        AssignBiomes();

        Debug.Log($"Generated World Data with {gridData.cells.Count} cells.");

        if (saveToCSV || printToConsole) ExportBiomeData();
    }

    private void GenerateRivers()
    {
        float actualWaterLevel = heightMultiplier * waterLevel;
        List<HexCell> potentialSources = new List<HexCell>();

        foreach (var cell in gridData.cells.Values)
        {
            if (cell.elevation > actualWaterLevel + (heightMultiplier * 0.3f) && cell.moisture > 0.4f)
            {
                potentialSources.Add(cell);
            }
        }

        for (int i = 0; i < potentialSources.Count; i++)
        {
            HexCell temp = potentialSources[i];
            int randomIndex = rng.Next(i, potentialSources.Count);
            potentialSources[i] = potentialSources[randomIndex];
            potentialSources[randomIndex] = temp;
        }

        int generatedRivers = 0;
        foreach (var source in potentialSources)
        {
            if (generatedRivers >= targetRiverCount) break;

            bool hasRiver = false;
            foreach (int edge in source.riverEdges) if (edge != 0) hasRiver = true;
            if (hasRiver) continue;

            if (CreateRiver(source, actualWaterLevel))
            {
                generatedRivers++;
            }
        }
    }

    private bool CreateRiver(HexCell startCell, float oceanElevation)
    {
        Queue<HexCell> activeFrontier = new Queue<HexCell>();
        activeFrontier.Enqueue(startCell);
        int totalLength = 0;

        while (activeFrontier.Count > 0)
        {
            HexCell current = activeFrontier.Dequeue();
            if (current.elevation <= oceanElevation) continue;

            List<int> validDirections = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                HexCell neighbor = gridData.GetCell(current.coordinates.GetNeighbor(i));
                if (neighbor != null && neighbor.riverEdges[(i + 3) % 6] != 1)
                {
                    if (neighbor.elevation < current.elevation)
                    {
                        validDirections.Add(i);
                    }
                }
            }

            // RIVER CARVING LOGIC: If stuck in a pit, try to carve through a hill
            if (validDirections.Count == 0 && riverLengthBias > 0f)
            {
                int bestCarveDir = -1;
                float lowestUphill = float.MaxValue;

                for (int i = 0; i < 6; i++)
                {
                    HexCell neighbor = gridData.GetCell(current.coordinates.GetNeighbor(i));
                    if (neighbor != null && neighbor.riverEdges[(i + 3) % 6] != 1)
                    {
                        // Don't carve into another existing river to prevent loops
                        bool neighborHasRiver = false;
                        foreach (int edge in neighbor.riverEdges) if (edge != 0) neighborHasRiver = true;
                        
                        if (!neighborHasRiver && neighbor.elevation < lowestUphill)
                        {
                            lowestUphill = neighbor.elevation;
                            bestCarveDir = i;
                        }
                    }
                }

                float maxCarveHeight = heightMultiplier * 0.4f * riverLengthBias; // How high a hill it can bore through
                if (bestCarveDir != -1 && (lowestUphill - current.elevation) <= maxCarveHeight && rng.NextDouble() <= riverLengthBias)
                {
                    HexCell carveNeighbor = gridData.GetCell(current.coordinates.GetNeighbor(bestCarveDir));
                    carveNeighbor.elevation = current.elevation - 0.05f; // Carve a gorge!
                    validDirections.Add(bestCarveDir);
                }
                else
                {
                    continue; // Truly stuck
                }
            }
            else if (validDirections.Count == 0) continue;

            validDirections.Sort((a, b) => {
                float ea = gridData.GetCell(current.coordinates.GetNeighbor(a)).elevation;
                float eb = gridData.GetCell(current.coordinates.GetNeighbor(b)).elevation;
                return ea.CompareTo(eb);
            });

            int bestDir = validDirections[0];
            HexCell bestNeighbor = gridData.GetCell(current.coordinates.GetNeighbor(bestDir));
            
            current.riverEdges[bestDir] = 1; 
            bestNeighbor.riverEdges[(bestDir + 3) % 6] = -1; 
            bestNeighbor.moisture = Mathf.Clamp01(bestNeighbor.moisture + 0.2f);
            
            activeFrontier.Enqueue(bestNeighbor);
            totalLength++;

            if (validDirections.Count > 1 && rng.NextDouble() < 0.15)
            {
                int forkDir = validDirections[1];
                HexCell forkNeighbor = gridData.GetCell(current.coordinates.GetNeighbor(forkDir));
                
                current.riverEdges[forkDir] = 1;
                forkNeighbor.riverEdges[(forkDir + 3) % 6] = -1;
                forkNeighbor.moisture = Mathf.Clamp01(forkNeighbor.moisture + 0.2f);
                
                activeFrontier.Enqueue(forkNeighbor);
                totalLength++;
            }
        }
        return totalLength > 0;
    }

    private void CalculateRiverVolumes()
    {
        List<HexCell> sortedCells = new List<HexCell>(gridData.cells.Values);
        sortedCells.Sort((a, b) => b.elevation.CompareTo(a.elevation));

        foreach (var cell in sortedCells)
        {
            int incoming = 0;
            int outgoing = 0;
            for (int i = 0; i < 6; i++)
            {
                if (cell.riverEdges[i] == 1) outgoing++;
                if (cell.riverEdges[i] == -1) incoming++;
            }

            if (incoming > 0 || outgoing > 0)
            {
                if (cell.riverVolume == 0) cell.riverVolume = 1;

                if (outgoing > 0)
                {
                    int volumePerBranch = (cell.riverVolume / outgoing) + 1; 
                    for (int i = 0; i < 6; i++)
                    {
                        if (cell.riverEdges[i] == 1)
                        {
                            HexCell neighbor = gridData.GetCell(cell.coordinates.GetNeighbor(i));
                            if (neighbor != null)
                            {
                                neighbor.riverVolume += volumePerBranch;
                            }
                        }
                    }
                }
            }
        }
    }

    private void AssignBiomes()
    {
        float actualWaterLevel = heightMultiplier * waterLevel;
        foreach (var cell in gridData.cells.Values)
        {
            if (cell.elevation <= actualWaterLevel)
            {
                if (cell.temperature < 0.25f)
                {
                    cell.biome = BiomeType.IceCap;
                    cell.elevation = actualWaterLevel + 0.25f;
                }
                else cell.biome = BiomeType.Ocean;
            }
            else if (cell.elevation <= actualWaterLevel + (heightMultiplier * 0.06f)) cell.biome = cell.temperature < 0.3f ? BiomeType.Tundra : BiomeType.Coast;
            else if (cell.elevation > heightMultiplier * 0.65f) cell.biome = cell.temperature < 0.4f ? BiomeType.IceCap : BiomeType.Mountain;
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
