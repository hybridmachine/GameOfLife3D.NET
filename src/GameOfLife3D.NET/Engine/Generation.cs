using System.Numerics;

namespace GameOfLife3D.NET.Engine;

public sealed class Generation
{
    public int Index { get; }
    public bool[,] Cells { get; }
    public IReadOnlyList<Vector2Int> LiveCells { get; }

    public Generation(int index, bool[,] cells, List<Vector2Int> liveCells)
    {
        Index = index;
        Cells = cells;
        LiveCells = liveCells;
    }
}

public readonly record struct Vector2Int(int X, int Y);
