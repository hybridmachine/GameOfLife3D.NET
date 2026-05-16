namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// A static GL mesh used as the per-instance template by InstancedCellRenderer.
/// All implementations share the same vertex layout: position (vec3) + normal
/// (vec3), stride 24 bytes. Instance attributes (aInstancePosition loc 2,
/// aGenerationT loc 3) are bound to every implementation's VAO at renderer
/// initialization time, not by the mesh itself.
///
/// <c>CubeMesh</c> and <c>BeveledCubeMesh</c> both implement this interface;
/// additional cell-shape meshes follow the same pattern so they slot into the
/// renderer's shape registry uniformly.
/// </summary>
public interface IInstancedMesh : IDisposable
{
    uint Vao { get; }
    uint IndexCount { get; }
}
