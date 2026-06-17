using System.Collections.Generic;

// Holds the entire world's data
public class HexGridData
{
    // Dictionary allows us to create maps of any shape (circular, jagged, etc.) easily
    public Dictionary<HexCoordinates, HexCell> cells = new Dictionary<HexCoordinates, HexCell>();

    public void AddCell(HexCell cell)
    {
        cells[cell.coordinates] = cell;
    }

    public HexCell GetCell(HexCoordinates coords)
    {
        cells.TryGetValue(coords, out HexCell cell);
        return cell;
    }

    public List<HexCell> GetNeighbors(HexCell cell)
    {
        List<HexCell> neighbors = new List<HexCell>();
        for (int i = 0; i < 6; i++)
        {
            HexCell neighbor = GetCell(cell.coordinates.GetNeighbor(i));
            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }
}
