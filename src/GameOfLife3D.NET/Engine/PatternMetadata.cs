namespace GameOfLife3D.NET.Engine;

public sealed record PatternMetadata(
    string Id,
    string Name,
    string Category,
    int Width,
    int Height,
    int? Period,
    string? Author,
    string? Description,
    string ResourcePath);
