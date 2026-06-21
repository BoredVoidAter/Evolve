using UnityEngine;

public class HexCell
{
    public HexCoordinates coordinates;
    public float elevation;
    public float temperature;
    public float moisture;
    public BiomeType biome;
    public float temporaryMoistureBuffer;
    public float cloudCover;
    public int[] riverEdges = new int[6];
    
    // NEW: Tracks the amount of water flowing through
    public int riverVolume = 0; 

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
