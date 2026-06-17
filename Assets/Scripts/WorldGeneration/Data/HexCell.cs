using UnityEngine;

// Pure data class representing a single hex tile
public class HexCell
{
    public HexCoordinates coordinates;
    
    // Core Simulation Data
    public float elevation;
    public float temperature;
    public float moisture;
    public BiomeType biome;

    // --- FUTURE PROOFING ---
    // For cellular automata (weather/water simulation)
    public float temporaryMoistureBuffer; 
    public float cloudCover;

    // For Catan-style rivers flowing on edges (0 to 5 for the 6 edges)
    // 0 = no river, 1 = small river, 2 = large river, etc.
    public int[] riverEdges = new int[6]; 

    public HexCell(HexCoordinates coordinates)
    {
        this.coordinates = coordinates;
    }

    public Vector3 GetWorldPosition(float hexRadius)
    {
        Vector3 pos = coordinates.ToWorldPosition(hexRadius);
        pos.y = elevation;
        return pos;
    }
}
