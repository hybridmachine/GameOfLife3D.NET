namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Selects which mesh the instanced cell renderer draws. New shapes will be
/// added in subsequent commits; the integer ordering is persisted to session
/// JSON so do not reorder existing members.
/// </summary>
public enum CellShape
{
    Cube = 0,
    BeveledCube = 1,
    Tetrahedron = 2,
    Octahedron = 3,
}
