using UnityEngine;
using System;

[Serializable]
public struct HexCoordinates : IEquatable<HexCoordinates>
{
    public int q;
    public int r;
    public int s => -q - r;

    public HexCoordinates(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    public Vector3 ToWorldPosition(float hexOuterRadius)
    {
        float x = hexOuterRadius * Mathf.Sqrt(3f) * (q + r / 2f);
        float z = hexOuterRadius * (3f / 2f) * r;
        return new Vector3(x, 0f, z);
    }

    // NEW: Converts a world space point back into a Hex Grid Coordinate
    public static HexCoordinates FromPosition(Vector3 position, float hexOuterRadius)
    {
        float r = position.z * 2f / (3f * hexOuterRadius);
        float q = (position.x / (hexOuterRadius * Mathf.Sqrt(3f))) - (r / 2f);
        float s = -q - r;

        // Round to nearest integer hex
        int iQ = Mathf.RoundToInt(q);
        int iR = Mathf.RoundToInt(r);
        int iS = Mathf.RoundToInt(s);

        // Fix rounding errors
        float qDiff = Mathf.Abs(iQ - q);
        float rDiff = Mathf.Abs(iR - r);
        float sDiff = Mathf.Abs(iS - s);

        if (qDiff > rDiff && qDiff > sDiff) iQ = -iR - iS;
        else if (rDiff > sDiff) iR = -iQ - iS;

        return new HexCoordinates(iQ, iR);
    }

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

    public bool Equals(HexCoordinates other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoordinates other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(q, r);
}
