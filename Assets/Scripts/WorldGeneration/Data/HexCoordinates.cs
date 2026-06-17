using UnityEngine;
using System;

// We use a struct because coordinates are simple values created frequently.
[Serializable]
public struct HexCoordinates : IEquatable<HexCoordinates>
{
    public int q;
    public int r;
    
    // Cube coordinate 's' is useful for algorithms (distance, line drawing)
    // In a hex grid, q + r + s always equals 0
    public int s => -q - r;

    public HexCoordinates(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    // Converts axial coordinates to 3D Unity World Space (Pointy-topped hexes)
    public Vector3 ToWorldPosition(float hexOuterRadius)
    {
        float x = hexOuterRadius * Mathf.Sqrt(3f) * (q + r / 2f);
        float z = hexOuterRadius * (3f / 2f) * r;
        return new Vector3(x, 0f, z);
    }

    // Standard 6 directions on a hex grid
    private static HexCoordinates[] directions = new HexCoordinates[]
    {
        new HexCoordinates(1, 0), new HexCoordinates(1, -1), new HexCoordinates(0, -1),
        new HexCoordinates(-1, 0), new HexCoordinates(-1, 1), new HexCoordinates(0, 1)
    };

    public HexCoordinates GetNeighbor(int directionIndex)
    {
        HexCoordinates dir = directions[directionIndex % 6];
        return new HexCoordinates(q + dir.q, r + dir.r);
    }

    // Boilerplate for using this struct in Dictionaries
    public bool Equals(HexCoordinates other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoordinates other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(q, r);
}
