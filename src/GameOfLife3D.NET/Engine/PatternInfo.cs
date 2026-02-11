namespace GameOfLife3D.NET.Engine;

public sealed record PatternInfo(string Name, string Description, bool[,] Pattern, string? Author = null);
