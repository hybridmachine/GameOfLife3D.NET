using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Capsule (pill) along the Y axis. 8 segments around the circumference and
/// 3 latitude rings per hemisphere cap (≈96 non-degenerate tris). Long axis
/// is Y so vertical generation stacks merge visually. Smooth normals: each
/// vertex's normal points from the nearest cap center radially outward — at
/// the two equator rings that direction is purely in the XZ plane, which
/// also matches the cylinder body's normal, so smooth shading is continuous
/// across the cap/body joint.
/// </summary>
public sealed class CapsuleMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    private const int Segments = 8;      // around the circumference
    private const int CapRings = 3;      // latitude rings per hemisphere
    private const float Radius = 0.25f;  // cap and body radius
    private const float HalfBody = 0.25f; // half-height of the cylinder body
    private const float Tau = MathF.PI * 2f;

    public CapsuleMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();

        // ── Top cap ────────────────────────────────────────────────────
        // Center at (0, +HalfBody, 0). Latitude i = 0 is the north pole;
        // i = CapRings is the top equator (Y = +HalfBody, r = Radius).
        for (int i = 0; i <= CapRings; i++)
        {
            float phi = (MathF.PI * 0.5f) * (float)i / CapRings;
            float y = Radius * MathF.Cos(phi);
            float r = Radius * MathF.Sin(phi);

            for (int j = 0; j < Segments; j++)
            {
                float theta = Tau * j / Segments;
                Vector3 dirOnCap = new Vector3(
                    r * MathF.Cos(theta), y, r * MathF.Sin(theta));
                Vector3 capCenter = new Vector3(0f, HalfBody, 0f);
                positions.Add(capCenter + dirOnCap);
                normals.Add(Vector3.Normalize(dirOnCap));
            }
        }

        // ── Bottom cap ─────────────────────────────────────────────────
        // Center at (0, -HalfBody, 0). Latitude i = 0 is the bottom equator
        // (Y = -HalfBody, r = Radius); i = CapRings is the south pole. The
        // band between the previous loop's last ring (top equator at +HalfBody)
        // and this loop's first ring (bottom equator at -HalfBody) is the
        // cylinder body section — both rings have radius=Radius, so the band
        // is a true uniform-radius cylinder strip.
        for (int i = 0; i <= CapRings; i++)
        {
            float phi = (MathF.PI * 0.5f) * (float)i / CapRings;
            float y = -Radius * MathF.Sin(phi);
            float r = Radius * MathF.Cos(phi);

            for (int j = 0; j < Segments; j++)
            {
                float theta = Tau * j / Segments;
                Vector3 dirOnCap = new Vector3(
                    r * MathF.Cos(theta), y, r * MathF.Sin(theta));
                Vector3 capCenter = new Vector3(0f, -HalfBody, 0f);
                positions.Add(capCenter + dirOnCap);
                normals.Add(Vector3.Normalize(dirOnCap));
            }
        }

        // Total rings: (CapRings+1) for top cap + (CapRings+1) for bottom cap.
        int totalRings = 2 * (CapRings + 1);

        // Build the quad-strip indices between consecutive rings. The strip
        // between top-cap's last ring and bottom-cap's first ring is the
        // cylinder body — this is automatic given the ring layout.
        var idx = new List<uint>();
        for (int ring = 0; ring < totalRings - 1; ring++)
        {
            for (int j = 0; j < Segments; j++)
            {
                int jNext = (j + 1) % Segments;
                int a = ring * Segments + j;
                int b = ring * Segments + jNext;
                int c = (ring + 1) * Segments + jNext;
                int d = (ring + 1) * Segments + j;

                idx.Add((uint)a); idx.Add((uint)b); idx.Add((uint)c);
                idx.Add((uint)a); idx.Add((uint)c); idx.Add((uint)d);
            }
        }

        // Pack into the position+normal interleaved float buffer.
        var verts = new List<float>(positions.Count * 6);
        for (int i = 0; i < positions.Count; i++)
        {
            verts.Add(positions[i].X);
            verts.Add(positions[i].Y);
            verts.Add(positions[i].Z);
            verts.Add(normals[i].X);
            verts.Add(normals[i].Y);
            verts.Add(normals[i].Z);
        }

        IndexCount = (uint)idx.Count;
        MeshBuilder.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
