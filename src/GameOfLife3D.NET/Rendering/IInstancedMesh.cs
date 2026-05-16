namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// A static GL mesh used as the per-instance template by InstancedCellRenderer.
/// All implementations share the same vertex layout: position (vec3) + normal
/// (vec3), stride 24 bytes. Instance attributes (aInstancePosition loc 2,
/// aGenerationT loc 3) are bound to every implementation's VAO at renderer
/// initialization time, not by the mesh itself.
///
/// The existing <c>CubeMesh</c> and <c>BeveledCubeMesh</c> classes are adapted
/// to implement this interface in the following commit; they expose the right
/// surface (Vao, IndexCount) already, so the adaptation is a one-word change
/// per class.
/// </summary>
public interface IInstancedMesh : IDisposable
{
    uint Vao { get; }
    uint IndexCount { get; }
}
