# Cell Shape Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a global "cell shape" picker so alive cells can render as one of nine 3D primitives (cube, beveled cube, tetrahedron, octahedron, square pyramid, dodecahedron, icosahedron, sphere, capsule), selected via an ImGui combo with thumbnail previews.

**Architecture:** A small `IInstancedMesh` interface unifies all shape meshes (same position+normal vertex layout as the existing cubes). `InstancedCellRenderer` (renamed from `InstancedCubeRenderer`) holds a `Dictionary<CellShape, IInstancedMesh>` and dispatches by `RenderSettings.Shape`. A `ShapeThumbnailRenderer` renders each mesh to a small offscreen FBO at startup for use as ImGui icon textures. No shader changes for the in-scene render path; thumbnails get their own minimal shader pair.

**Tech Stack:** C# / .NET 10, Silk.NET OpenGL 3.3 Core, ImGui.NET, `System.Numerics`.

**Spec:** `PLAN-cell-shapes.md` at repo root.

---

## Verification Approach

This project has **no automated test suite** (per `AGENTS.md`). The verification rhythm is:

- After every code change: `dotnet build` succeeds with no new errors/warnings.
- After every meaningful chunk: `dotnet run --project src/GameOfLife3D.NET/` and visually confirm the change.

Each task ends with a build step and (where applicable) a visual smoke test with exact instructions. Commit after each task passes verification.

---

## Task 1: Foundation — enum, interface, mesh builder

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/CellShape.cs`
- Create: `src/GameOfLife3D.NET/Rendering/IInstancedMesh.cs`
- Create: `src/GameOfLife3D.NET/Rendering/MeshBuilder.cs`

- [ ] **Step 1: Create the `CellShape` enum (only the two existing shapes for now)**

`src/GameOfLife3D.NET/Rendering/CellShape.cs`:
```csharp
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
}
```

- [ ] **Step 2: Create the `IInstancedMesh` interface**

`src/GameOfLife3D.NET/Rendering/IInstancedMesh.cs`:
```csharp
namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// A static GL mesh used as the per-instance template by InstancedCellRenderer.
/// All implementations share the same vertex layout: position (vec3) + normal
/// (vec3), stride 24 bytes. Instance attributes (aInstancePosition loc 2,
/// aGenerationT loc 3) are bound to every implementation's VAO at renderer
/// initialization time, not by the mesh itself.
/// </summary>
public interface IInstancedMesh : IDisposable
{
    uint Vao { get; }
    uint IndexCount { get; }
}
```

- [ ] **Step 3: Create `MeshBuilder` with the helpers currently inside `BeveledCubeMesh`**

`src/GameOfLife3D.NET/Rendering/MeshBuilder.cs`:
```csharp
namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Shared geometry helpers for building IInstancedMesh implementations. Each
/// AddQuad / AddTriangle call appends flat-shaded vertices (position + normal,
/// 6 floats each) and the matching indices to the supplied lists. Winding is
/// auto-corrected against the supplied normal hint, so callers can pass corners
/// in either CW or CCW order.
/// </summary>
public static class MeshBuilder
{
    public static void AddQuad(List<float> verts, List<uint> indices,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b,
        (float X, float Y, float Z) c, (float X, float Y, float Z) d,
        (float X, float Y, float Z) normal)
    {
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cross = (
            ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2,
            ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3,
            ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        float dot = cross.Item1 * normal.X + cross.Item2 * normal.Y + cross.Item3 * normal.Z;

        if (dot < 0)
            (b, d) = (d, b);

        uint baseIdx = (uint)(verts.Count / 6);

        AddVertex(verts, a, normal);
        AddVertex(verts, b, normal);
        AddVertex(verts, c, normal);
        AddVertex(verts, d, normal);

        indices.Add(baseIdx);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx + 3);
    }

    public static void AddTriangle(List<float> verts, List<uint> indices,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c,
        (float X, float Y, float Z) normal)
    {
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cross = (
            ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2,
            ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3,
            ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        float dot = cross.Item1 * normal.X + cross.Item2 * normal.Y + cross.Item3 * normal.Z;

        if (dot < 0)
            (b, c) = (c, b);

        uint baseIdx = (uint)(verts.Count / 6);

        AddVertex(verts, a, normal);
        AddVertex(verts, b, normal);
        AddVertex(verts, c, normal);

        indices.Add(baseIdx);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx + 2);
    }

    public static void AddVertex(List<float> verts,
        (float X, float Y, float Z) pos, (float X, float Y, float Z) normal)
    {
        verts.Add(pos.X);
        verts.Add(pos.Y);
        verts.Add(pos.Z);
        verts.Add(normal.X);
        verts.Add(normal.Y);
        verts.Add(normal.Z);
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build
```
Expected: succeeds, no new errors. The three new files compile but are not yet referenced.

- [ ] **Step 5: Commit**

```bash
git add src/GameOfLife3D.NET/Rendering/CellShape.cs \
        src/GameOfLife3D.NET/Rendering/IInstancedMesh.cs \
        src/GameOfLife3D.NET/Rendering/MeshBuilder.cs
git commit -m "Add CellShape enum, IInstancedMesh interface, and MeshBuilder helpers"
```

---

## Task 2: Adapt existing meshes to `IInstancedMesh`

**Files:**
- Modify: `src/GameOfLife3D.NET/Rendering/CubeMesh.cs`
- Modify: `src/GameOfLife3D.NET/Rendering/BeveledCubeMesh.cs`

- [ ] **Step 1: Make `CubeMesh` implement `IInstancedMesh`**

Edit `src/GameOfLife3D.NET/Rendering/CubeMesh.cs` line 5:
```csharp
public sealed class CubeMesh : IInstancedMesh
```

No other changes to `CubeMesh` — it already exposes `Vao` and `IndexCount` with the right signatures.

- [ ] **Step 2: Make `BeveledCubeMesh` implement `IInstancedMesh` and remove its private helpers**

In `src/GameOfLife3D.NET/Rendering/BeveledCubeMesh.cs`:

1. Change the class declaration at line 5 to:
```csharp
public sealed class BeveledCubeMesh : IInstancedMesh
```

2. Delete the private static methods `AddQuad`, `AddTriangle`, and `AddVertex` at the bottom of the file (lines 180-253 in the current file).

3. Inside `Generate()`, replace every `AddQuad(...)` and `AddTriangle(...)` call with `MeshBuilder.AddQuad(...)` and `MeshBuilder.AddTriangle(...)` — same argument order. Approximately 32 call sites.

- [ ] **Step 3: Build**

```bash
dotnet build
```
Expected: succeeds. No behavioral change yet.

- [ ] **Step 4: Run and confirm no visual regression**

```bash
dotnet run --project src/GameOfLife3D.NET/
```
Expected: app launches, alive cells render as beveled cubes (the existing default). Quit.

- [ ] **Step 5: Commit**

```bash
git add src/GameOfLife3D.NET/Rendering/CubeMesh.cs src/GameOfLife3D.NET/Rendering/BeveledCubeMesh.cs
git commit -m "Adopt IInstancedMesh on CubeMesh and BeveledCubeMesh; share helpers via MeshBuilder"
```

---

## Task 3: Shape-selection refactor (rename renderer, swap settings field, update persistence + UI)

This task is larger — it threads the new `Shape` field through every place `UseBeveledCubes` is read or written. After this task, the UI shows a "Cell shape" combo with two entries (Cube, BeveledCube) and changing it switches the mesh exactly as the old checkbox did. The combo is text-only for now; thumbnails come in Task 11.

**Files:**
- Modify: `src/GameOfLife3D.NET/Rendering/RenderSettings.cs`
- Rename + modify: `src/GameOfLife3D.NET/Rendering/InstancedCubeRenderer.cs` → `InstancedCellRenderer.cs`
- Modify: `src/GameOfLife3D.NET/Rendering/Renderer3D.cs`
- Modify: `src/GameOfLife3D.NET/IO/SessionManager.cs`
- Modify: `src/GameOfLife3D.NET/UI/ImGuiUI.cs`

- [ ] **Step 1: Update `RenderSettings.cs` — replace `UseBeveledCubes` with `Shape`**

In `src/GameOfLife3D.NET/Rendering/RenderSettings.cs`, replace lines 76-77:
```csharp
    // Beveled cubes
    public bool UseBeveledCubes { get; set; } = true;
```
with:
```csharp
    // Cell shape — chosen mesh for all alive cells. Default matches the old
    // first-run behavior when UseBeveledCubes defaulted to true.
    public CellShape Shape { get; set; } = CellShape.BeveledCube;
```

- [ ] **Step 2: Rename `InstancedCubeRenderer.cs` to `InstancedCellRenderer.cs` and refactor**

```bash
git mv src/GameOfLife3D.NET/Rendering/InstancedCubeRenderer.cs \
       src/GameOfLife3D.NET/Rendering/InstancedCellRenderer.cs
```

Now edit the renamed file `src/GameOfLife3D.NET/Rendering/InstancedCellRenderer.cs`:

Replace the class declaration and field block (the existing lines 14-30 region) so that the file's top half reads:
```csharp
namespace GameOfLife3D.NET.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct InstanceData
{
    public Vector3 Position;
    public float GenerationT;
}

public sealed class InstancedCellRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<CellShape, IInstancedMesh> _meshes = new();
    private uint _instanceVbo;
    private int _maxInstances;
    private int _instanceCount;
    private bool _dirty;

    // Pre-allocated buffer
    private InstanceData[] _instanceBuffer = [];

    // Performance guard: when the chosen shape is BeveledCube and the live
    // instance count exceeds this threshold, fall back to the plain cube mesh.
    // Other shapes have no fallback — they always render as themselves.
    private const int BeveledMaxInstances = 500_000;

    public int InstanceCount => _instanceCount;

    public InstancedCellRenderer(GL gl)
    {
        _gl = gl;
    }

    public void Initialize(int maxInstances = 4_000_000)
    {
        _maxInstances = maxInstances;
        _instanceBuffer = new InstanceData[maxInstances];

        _meshes[CellShape.Cube] = new CubeMesh(_gl);
        _meshes[CellShape.BeveledCube] = new BeveledCubeMesh(_gl);

        _instanceVbo = _gl.GenBuffer();

        // Allocate instance VBO
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(maxInstances * Marshal.SizeOf<InstanceData>()),
                null, BufferUsageARB.DynamicDraw);
        }

        // Bind instance attributes to every shape's VAO
        foreach (var mesh in _meshes.Values)
            BindInstanceAttributesToVAO(mesh.Vao);
    }
```

Replace the `GetActiveMesh` method body (current lines 104-117) with:
```csharp
    private void GetActiveMesh(RenderSettings settings, out uint vao, out uint indexCount)
    {
        CellShape effective = settings.Shape;
        // Beveled cube falls back to plain cube above the LOD threshold —
        // preserves the original BeveledMaxInstances behavior.
        if (effective == CellShape.BeveledCube && _instanceCount > BeveledMaxInstances)
            effective = CellShape.Cube;

        IInstancedMesh mesh = _meshes.TryGetValue(effective, out var m)
            ? m
            : _meshes[CellShape.Cube]; // defensive fallback for unknown enum values

        vao = mesh.Vao;
        indexCount = mesh.IndexCount;
    }
```

Replace the existing null-checks in `RenderSolid` and `RenderWireframe` so they check the dictionary instead. Specifically:

In `RenderSolid`, change line 121 (`if (_instanceCount == 0 || _cubeMesh == null) return;`) to:
```csharp
        if (_instanceCount == 0 || _meshes.Count == 0) return;
```

In `RenderWireframe`, change line 160 (`if (_instanceCount == 0 || _cubeMesh == null || !settings.ShowWireframe) return;`) to:
```csharp
        if (_instanceCount == 0 || _meshes.Count == 0 || !settings.ShowWireframe) return;
```

Replace `Dispose` (current lines 245-251) with:
```csharp
    public void Dispose()
    {
        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();
        if (_instanceVbo != 0)
            _gl.DeleteBuffer(_instanceVbo);
    }
```

- [ ] **Step 3: Update `Renderer3D.cs` references**

In `src/GameOfLife3D.NET/Rendering/Renderer3D.cs`:

Replace line 19:
```csharp
    private InstancedCubeRenderer? _instancedRenderer;
```
with:
```csharp
    private InstancedCellRenderer? _instancedRenderer;
```

Replace line 54:
```csharp
        _instancedRenderer = new InstancedCubeRenderer(_gl);
```
with:
```csharp
        _instancedRenderer = new InstancedCellRenderer(_gl);
```

Update the stale comment on line 10-11:
```csharp
    // Above this instance count we skip the reflection pass and render the
    // floor as flat tinted water. Matches InstancedCellRenderer's beveled-cube
    // cutoff so the visual fidelity drop happens at one consistent threshold.
```

- [ ] **Step 4: Update `SessionManager.cs` — add `Shape`, keep `UseBeveledCubes` as legacy fallback**

In `src/GameOfLife3D.NET/IO/SessionManager.cs`, in the `RenderSessionData` class, replace lines 81-82:
```csharp
    // Beveled cubes
    public bool UseBeveledCubes { get; set; }
```
with:
```csharp
    // Cell shape. Nullable so loaders can tell "field absent" (legacy) from
    // "explicit value". Legacy `UseBeveledCubes` is kept for one release as a
    // fallback for sessions saved before this feature landed.
    public int? Shape { get; set; }

    // Legacy field — only read on load when `Shape` is null. Never written
    // by `FromRenderSettings` anymore.
    public bool? UseBeveledCubes { get; set; }
```

In `FromRenderSettings` (around line 188-189), replace:
```csharp
        // Beveled cubes
        UseBeveledCubes = s.UseBeveledCubes,
```
with:
```csharp
        // Cell shape (new field; legacy UseBeveledCubes intentionally not written)
        Shape = (int)s.Shape,
```

In `ApplyRenderSettings` (around line 253-254), replace:
```csharp
        // Beveled cubes
        target.UseBeveledCubes = data.UseBeveledCubes;
```
with:
```csharp
        // Cell shape: prefer the new field, fall back to the legacy
        // UseBeveledCubes bool for sessions saved before this feature landed.
        if (data.Shape.HasValue)
        {
            target.Shape = (CellShape)Math.Clamp(data.Shape.Value, 0, 1);
        }
        else if (data.UseBeveledCubes.HasValue)
        {
            target.Shape = data.UseBeveledCubes.Value ? CellShape.BeveledCube : CellShape.Cube;
        }
```

The `Math.Clamp(..., 0, 1)` upper bound widens as new shapes are added in later tasks — update it each time. (Reminder noted in each future task.)

- [ ] **Step 5: Update `ImGuiUI.cs` — checkbox to combo**

In `src/GameOfLife3D.NET/UI/ImGuiUI.cs`:

Replace lines 79-80:
```csharp
    // Beveled cubes
    private bool _useBeveledCubes = true;
```
with:
```csharp
    // Cell shape — mirrors RenderSettings.Shape; the int form drives ImGui.Combo.
    private int _shape = (int)CellShape.BeveledCube;
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube" };
```

Add `using GameOfLife3D.NET.Rendering;` at the top of the file if it's not already there (the file may already import it via the `RenderSettings` reference — verify in the existing using block).

Replace lines 950-951:
```csharp
            if (ImGui.Checkbox("Rounded Cubes", ref _useBeveledCubes))
                settings.UseBeveledCubes = _useBeveledCubes;
```
with:
```csharp
            if (ImGui.Combo("Cell Shape", ref _shape, ShapeNames, ShapeNames.Length))
                settings.Shape = (CellShape)_shape;
```

Replace line 1616:
```csharp
        _useBeveledCubes = s.UseBeveledCubes;
```
with:
```csharp
        _shape = (int)s.Shape;
```

- [ ] **Step 6: Build**

```bash
dotnet build
```
Expected: succeeds. If a "field never used" warning appears for any stray `_useBeveledCubes` reference, search and clean it up:
```bash
grep -n "UseBeveledCubes\|_useBeveledCubes" src/GameOfLife3D.NET --include="*.cs" -r
```
Only the legacy `UseBeveledCubes` nullable property inside `RenderSessionData` should match.

- [ ] **Step 7: Smoke test — visual parity with prior behavior**

```bash
dotnet run --project src/GameOfLife3D.NET/
```

Verify:
1. App launches; alive cells render as **beveled cubes** (matches first-run before this change).
2. In the render settings panel, find the new "Cell Shape" combo. It should show "Rounded Cube" selected.
3. Change it to "Cube" — cells become plain cubes.
4. Change back to "Rounded Cube" — cells become beveled again.
5. Save a session (File → Save), restart the app, load the session — cells render with whichever shape was selected.
6. Quit.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Generalize cube/beveled toggle into CellShape selector

Replaces RenderSettings.UseBeveledCubes with a CellShape enum. Renames
InstancedCubeRenderer to InstancedCellRenderer and uses a shape→mesh
dictionary instead of two fixed fields. SessionManager keeps the legacy
UseBeveledCubes nullable for one release as a load-time fallback."
```

---

## Task 4: Tetrahedron mesh

The smallest polyhedron — 4 vertices, 4 triangular faces. Sharp gem-like silhouette.

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/TetrahedronMesh.cs`
- Modify: `src/GameOfLife3D.NET/Rendering/CellShape.cs`
- Modify: `src/GameOfLife3D.NET/Rendering/InstancedCellRenderer.cs`
- Modify: `src/GameOfLife3D.NET/IO/SessionManager.cs` (clamp upper bound)
- Modify: `src/GameOfLife3D.NET/UI/ImGuiUI.cs` (combo entry)

- [ ] **Step 1: Add enum member**

In `src/GameOfLife3D.NET/Rendering/CellShape.cs`, add `Tetrahedron = 2,` after `BeveledCube = 1,`. Final file:
```csharp
namespace GameOfLife3D.NET.Rendering;

public enum CellShape
{
    Cube = 0,
    BeveledCube = 1,
    Tetrahedron = 2,
}
```

- [ ] **Step 2: Create the mesh class**

`src/GameOfLife3D.NET/Rendering/Meshes/TetrahedronMesh.cs`:
```csharp
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Regular tetrahedron inscribed in the unit cube (±0.5 extent). Uses the four
/// "alternating" corners of the cube as vertices, which guarantees the result
/// fits the cell footprint exactly. Flat-shaded — each face owns its own
/// vertex triples with the face normal.
/// </summary>
public sealed class TetrahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public TetrahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private unsafe void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        // Four alternating corners of the unit cube
        var a = ( 0.5f,  0.5f,  0.5f);
        var b = ( 0.5f, -0.5f, -0.5f);
        var c = (-0.5f,  0.5f, -0.5f);
        var d = (-0.5f, -0.5f,  0.5f);

        // Normal hints = direction from origin to face centroid (tetrahedron is
        // convex and centered at origin, so centroid direction is outward).
        // MeshBuilder.AddTriangle corrects winding to match.
        MeshBuilder.AddTriangle(verts, idx, a, b, c, Centroid(a, b, c));
        MeshBuilder.AddTriangle(verts, idx, a, d, b, Centroid(a, d, b));
        MeshBuilder.AddTriangle(verts, idx, a, c, d, Centroid(a, c, d));
        MeshBuilder.AddTriangle(verts, idx, b, d, c, Centroid(b, d, c));

        IndexCount = (uint)idx.Count;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        var vertArr = verts.ToArray();
        var idxArr = idx.ToArray();

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* p = vertArr)
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertArr.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* p = idxArr)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(idxArr.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    private static (float, float, float) Centroid(
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c)
        => ((a.X + b.X + c.X) / 3f, (a.Y + b.Y + c.Y) / 3f, (a.Z + b.Z + c.Z) / 3f);

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

- [ ] **Step 3: Register in the renderer**

In `src/GameOfLife3D.NET/Rendering/InstancedCellRenderer.cs`, inside `Initialize`, after the existing mesh registrations:
```csharp
        _meshes[CellShape.Tetrahedron] = new Meshes.TetrahedronMesh(_gl);
```

Add `using GameOfLife3D.NET.Rendering.Meshes;` at the top of the file if you prefer; either works.

- [ ] **Step 4: Widen the SessionManager clamp upper bound to 2**

In `src/GameOfLife3D.NET/IO/SessionManager.cs`, `ApplyRenderSettings`:
```csharp
        if (data.Shape.HasValue)
        {
            target.Shape = (CellShape)Math.Clamp(data.Shape.Value, 0, 2);
        }
```

- [ ] **Step 5: Add UI combo entry**

In `src/GameOfLife3D.NET/UI/ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron" };
```

- [ ] **Step 6: Build + smoke test**

```bash
dotnet build && dotnet run --project src/GameOfLife3D.NET/
```

In the running app, open the Cell Shape combo and pick "Tetrahedron". Alive cells should render as small four-faced gems with sharp points. Rotate the camera to confirm shading looks right (no obviously dark or flipped faces).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Add Tetrahedron cell shape"
```

---

## Task 5: Octahedron mesh

8 triangular faces meeting at 6 axis-aligned vertices. Crystal/diamond silhouette.

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/OctahedronMesh.cs`
- Modify: `CellShape.cs`, `InstancedCellRenderer.cs`, `SessionManager.cs`, `ImGuiUI.cs`

- [ ] **Step 1: Add enum member**

`CellShape.cs`: append `Octahedron = 3,`.

- [ ] **Step 2: Create the mesh class**

`src/GameOfLife3D.NET/Rendering/Meshes/OctahedronMesh.cs`:
```csharp
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Octahedron with vertices at ±0.5 along each axis. 8 triangular faces, flat-
/// shaded. Reads as a sharp crystal — points along Y up/down line up with the
/// generation axis.
/// </summary>
public sealed class OctahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public OctahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private unsafe void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        var px = ( 0.5f,  0f,  0f);
        var nx = (-0.5f,  0f,  0f);
        var py = ( 0f,  0.5f,  0f);
        var ny = ( 0f, -0.5f,  0f);
        var pz = ( 0f,  0f,  0.5f);
        var nz = ( 0f,  0f, -0.5f);

        // 8 triangular faces, one per (±x,±y,±z) octant. The third vertex on
        // each face is the axis-Y vertex matching the y-sign; normal hint is
        // the octant-corner direction.
        void Face(
            (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c,
            float nxs, float nys, float nzs)
            => MeshBuilder.AddTriangle(verts, idx, a, b, c, (nxs, nys, nzs));

        Face(px, pz, py,  1f,  1f,  1f);
        Face(px, py, nz,  1f,  1f, -1f);
        Face(px, ny, pz,  1f, -1f,  1f);
        Face(px, nz, ny,  1f, -1f, -1f);
        Face(nx, py, pz, -1f,  1f,  1f);
        Face(nx, nz, py, -1f,  1f, -1f);
        Face(nx, pz, ny, -1f, -1f,  1f);
        Face(nx, ny, nz, -1f, -1f, -1f);

        IndexCount = (uint)idx.Count;
        UploadAndBind(verts.ToArray(), idx.ToArray(), out _vao, out _vbo, out _ebo, _gl);
    }

    internal static unsafe void UploadAndBind(float[] vertArr, uint[] idxArr,
        out uint vao, out uint vbo, out uint ebo, GL gl)
    {
        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = vertArr)
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertArr.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* p = idxArr)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(idxArr.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

The `internal static UploadAndBind` is reusable — subsequent meshes (pyramid, dodec, ico, icosphere, capsule) call into it instead of duplicating the GL setup boilerplate.

- [ ] **Step 3: Register, widen clamp, add UI entry**

In `InstancedCellRenderer.cs` `Initialize`:
```csharp
        _meshes[CellShape.Octahedron] = new Meshes.OctahedronMesh(_gl);
```

In `SessionManager.ApplyRenderSettings`, clamp upper bound → `3`.

In `ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron" };
```

- [ ] **Step 4: Build + smoke test**

`dotnet build && dotnet run --project src/GameOfLife3D.NET/`. Select Octahedron from the combo — alive cells should be diamond-shaped, with points along Y up/down.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add Octahedron cell shape"
```

---

## Task 6: Square pyramid mesh

Apex up, square base on the bottom of the cell. 4 side triangles + a quad base (split into 2 tris) = 6 tris.

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/SquarePyramidMesh.cs`
- Modify: `CellShape.cs`, `InstancedCellRenderer.cs`, `SessionManager.cs`, `ImGuiUI.cs`

- [ ] **Step 1: Add enum member**

`CellShape.cs`: append `SquarePyramid = 4,`.

- [ ] **Step 2: Create the mesh class**

`src/GameOfLife3D.NET/Rendering/Meshes/SquarePyramidMesh.cs`:
```csharp
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Square-base pyramid. Apex at (0, 0.5, 0), base square on Y=-0.5 spanning
/// ±0.5 in X and Z. Base is included so the underside isn't see-through.
/// </summary>
public sealed class SquarePyramidMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public SquarePyramidMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        var apex = (0f, 0.5f, 0f);
        var bl = (-0.5f, -0.5f, -0.5f);
        var br = ( 0.5f, -0.5f, -0.5f);
        var tr = ( 0.5f, -0.5f,  0.5f);
        var tl = (-0.5f, -0.5f,  0.5f);

        // Side triangles — normal hint points outward-and-slightly-up.
        MeshBuilder.AddTriangle(verts, idx, apex, bl, br, ( 0f,  0.5f, -1f));  // front (-Z)
        MeshBuilder.AddTriangle(verts, idx, apex, br, tr, ( 1f,  0.5f,  0f));  // right (+X)
        MeshBuilder.AddTriangle(verts, idx, apex, tr, tl, ( 0f,  0.5f,  1f));  // back (+Z)
        MeshBuilder.AddTriangle(verts, idx, apex, tl, bl, (-1f,  0.5f,  0f));  // left (-X)

        // Base — single quad facing -Y
        MeshBuilder.AddQuad(verts, idx, bl, br, tr, tl, (0f, -1f, 0f));

        IndexCount = (uint)idx.Count;
        OctahedronMesh.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

- [ ] **Step 3: Register, widen clamp, add UI entry**

`InstancedCellRenderer.cs` `Initialize`:
```csharp
        _meshes[CellShape.SquarePyramid] = new Meshes.SquarePyramidMesh(_gl);
```

`SessionManager.ApplyRenderSettings` clamp upper bound → `4`.

`ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid" };
```

- [ ] **Step 4: Build + smoke test**

Select "Pyramid". Alive cells should look like pyramids resting on the grid plane with apexes pointing up along Y.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add SquarePyramid cell shape"
```

---

## Task 7: Icosahedron mesh

12 vertices, 20 triangular faces. Uses the canonical golden-ratio construction.

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/IcosahedronMesh.cs`
- Modify: `CellShape.cs`, `InstancedCellRenderer.cs`, `SessionManager.cs`, `ImGuiUI.cs`

- [ ] **Step 1: Add enum member**

`CellShape.cs`: append `Icosahedron = 5,`.

- [ ] **Step 2: Create the mesh class**

`src/GameOfLife3D.NET/Rendering/Meshes/IcosahedronMesh.cs`:
```csharp
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Regular icosahedron: 12 vertices, 20 triangular faces. Vertices are the
/// canonical golden-ratio construction, scaled so the bounding sphere radius
/// equals 0.5 (max coord magnitude is φ × scale = 0.5).
/// </summary>
public sealed class IcosahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    // Pre-built unit-cube-scaled vertex coordinates and face index triples,
    // exposed for IcosphereMesh which subdivides them.
    internal static readonly (float X, float Y, float Z)[] BaseVertices;
    internal static readonly int[] BaseFaces =
    {
        0, 11,  5,    0,  5,  1,    0,  1,  7,    0,  7, 10,   0, 10, 11,
        1,  5,  9,    5, 11,  4,   11, 10,  2,   10,  7,  6,   7,  1,  8,
        3,  9,  4,    3,  4,  2,    3,  2,  6,    3,  6,  8,   3,  8,  9,
        4,  9,  5,    2,  4, 11,    6,  2, 10,    8,  6,  7,   9,  8,  1,
    };

    static IcosahedronMesh()
    {
        const float Phi = 1.6180339887f;
        const float S = 0.5f / Phi; // unscaled `1` maps to S; unscaled `φ` maps to 0.5
        BaseVertices = new (float, float, float)[]
        {
            (-S, S * Phi, 0f),  // 0
            ( S, S * Phi, 0f),  // 1
            (-S,-S * Phi, 0f),  // 2
            ( S,-S * Phi, 0f),  // 3
            (0f,-S,  S * Phi),  // 4
            (0f, S,  S * Phi),  // 5
            (0f,-S, -S * Phi),  // 6
            (0f, S, -S * Phi),  // 7
            ( S * Phi, 0f, -S), // 8
            ( S * Phi, 0f,  S), // 9
            (-S * Phi, 0f, -S), // 10
            (-S * Phi, 0f,  S), // 11
        };
    }

    public IcosahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        for (int f = 0; f < BaseFaces.Length; f += 3)
        {
            var a = BaseVertices[BaseFaces[f]];
            var b = BaseVertices[BaseFaces[f + 1]];
            var c = BaseVertices[BaseFaces[f + 2]];
            // Icosahedron is convex around origin → centroid direction is outward.
            var n = ((a.X + b.X + c.X) / 3f,
                     (a.Y + b.Y + c.Y) / 3f,
                     (a.Z + b.Z + c.Z) / 3f);
            MeshBuilder.AddTriangle(verts, idx, a, b, c, n);
        }

        IndexCount = (uint)idx.Count;
        OctahedronMesh.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

- [ ] **Step 3: Register, widen clamp, add UI entry**

`InstancedCellRenderer.cs` `Initialize`:
```csharp
        _meshes[CellShape.Icosahedron] = new Meshes.IcosahedronMesh(_gl);
```

`SessionManager.ApplyRenderSettings` clamp upper bound → `5`.

`ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid", "Icosahedron" };
```

- [ ] **Step 4: Build + smoke test**

Select "Icosahedron". Alive cells should be 20-faced polyhedra reading as geodesic balls. Rotate camera; verify all faces shade as expected (no obviously inverted faces).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add Icosahedron cell shape"
```

---

## Task 8: Dodecahedron mesh

20 vertices, 12 pentagonal faces (fan-triangulated → 36 tris).

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/DodecahedronMesh.cs`
- Modify: `CellShape.cs`, `InstancedCellRenderer.cs`, `SessionManager.cs`, `ImGuiUI.cs`

- [ ] **Step 1: Add enum member**

`CellShape.cs`: append `Dodecahedron = 6,`.

- [ ] **Step 2: Create the mesh class**

The dodecahedron is built as the **dual of the icosahedron** built in Task 7:
each dodecahedron face is the pentagon connecting the centroids of the five icosahedron faces meeting at one icosahedron vertex. This avoids hand-typing a 12-row pentagon-vertex table that is hard to verify, and reuses the already-correct icosahedron data.

`src/GameOfLife3D.NET/Rendering/Meshes/DodecahedronMesh.cs`:
```csharp
using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Regular dodecahedron built as the dual of IcosahedronMesh: each dodec
/// vertex is the centroid of an icosa face, and each dodec face is the
/// pentagon connecting the 5 icosa-face centroids that meet at one icosa
/// vertex. 12 pentagons × 3 fan triangles = 36 triangles. The mesh is
/// uniformly rescaled so the largest coordinate magnitude equals 0.5
/// (fits the unit cell).
/// </summary>
public sealed class DodecahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public DodecahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        int faceCount = IcosahedronMesh.BaseFaces.Length / 3;
        int vertCount = IcosahedronMesh.BaseVertices.Length;

        // 1. Compute the centroid of each icosa face — these are the 20 dodec
        //    vertices. Track the max coordinate magnitude so we can rescale.
        var centroids = new Vector3[faceCount];
        float maxComp = 0f;
        for (int f = 0; f < faceCount; f++)
        {
            var (ax, ay, az) = IcosahedronMesh.BaseVertices[IcosahedronMesh.BaseFaces[f * 3 + 0]];
            var (bx, by, bz) = IcosahedronMesh.BaseVertices[IcosahedronMesh.BaseFaces[f * 3 + 1]];
            var (cx, cy, cz) = IcosahedronMesh.BaseVertices[IcosahedronMesh.BaseFaces[f * 3 + 2]];
            centroids[f] = new Vector3(
                (ax + bx + cx) / 3f,
                (ay + by + cy) / 3f,
                (az + bz + cz) / 3f);
            maxComp = MathF.Max(maxComp, MathF.Max(
                MathF.Abs(centroids[f].X),
                MathF.Max(MathF.Abs(centroids[f].Y), MathF.Abs(centroids[f].Z))));
        }
        float scale = 0.5f / maxComp;
        for (int i = 0; i < faceCount; i++) centroids[i] *= scale;

        // 2. For each icosa vertex, collect the 5 face indices that contain
        //    it. Each vertex of a regular icosahedron is shared by exactly 5
        //    faces.
        var facesAtVertex = new int[vertCount][];
        {
            var counts = new int[vertCount];
            for (int f = 0; f < faceCount; f++)
                for (int k = 0; k < 3; k++)
                    counts[IcosahedronMesh.BaseFaces[f * 3 + k]]++;
            for (int v = 0; v < vertCount; v++) facesAtVertex[v] = new int[counts[v]];
            for (int v = 0; v < vertCount; v++) counts[v] = 0;
            for (int f = 0; f < faceCount; f++)
                for (int k = 0; k < 3; k++)
                {
                    int vi = IcosahedronMesh.BaseFaces[f * 3 + k];
                    facesAtVertex[vi][counts[vi]++] = f;
                }
        }

        // 3. For each icosa vertex, sort the 5 surrounding face-centroids by
        //    their angular position in the plane perpendicular to that vertex.
        //    Then fan-triangulate the resulting pentagon.
        var verts = new List<float>();
        var idx = new List<uint>();

        for (int v = 0; v < vertCount; v++)
        {
            var faceIdxs = facesAtVertex[v];
            if (faceIdxs.Length != 5) continue;  // should always be 5 for a valid icosa

            var (vx, vy, vz) = IcosahedronMesh.BaseVertices[v];
            Vector3 axis = Vector3.Normalize(new Vector3(vx, vy, vz));

            // Project each centroid onto the plane through origin perpendicular
            // to `axis`. The projected vectors lie in a 2D plane; we sort by
            // angle in that plane.
            var projected = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = centroids[faceIdxs[i]];
                projected[i] = c - axis * Vector3.Dot(c, axis);
            }
            Vector3 uAxis = Vector3.Normalize(projected[0]);
            Vector3 wAxis = Vector3.Cross(axis, uAxis);

            var angled = new (float angle, int faceIdx)[5];
            for (int i = 0; i < 5; i++)
            {
                float angle = MathF.Atan2(
                    Vector3.Dot(projected[i], wAxis),
                    Vector3.Dot(projected[i], uAxis));
                angled[i] = (angle, faceIdxs[i]);
            }
            Array.Sort(angled, (a, b) => a.angle.CompareTo(b.angle));

            // Fan-triangulate from angled[0]. Normal hint = axis (outward from
            // origin — same direction as the icosa vertex this face is dual to).
            var p0 = centroids[angled[0].faceIdx];
            for (int i = 1; i < 4; i++)
            {
                var pa = centroids[angled[i].faceIdx];
                var pb = centroids[angled[i + 1].faceIdx];
                MeshBuilder.AddTriangle(verts, idx,
                    (p0.X, p0.Y, p0.Z),
                    (pa.X, pa.Y, pa.Z),
                    (pb.X, pb.Y, pb.Z),
                    (axis.X, axis.Y, axis.Z));
            }
        }

        IndexCount = (uint)idx.Count;
        OctahedronMesh.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

- [ ] **Step 3: Register, widen clamp, add UI entry**

`InstancedCellRenderer.cs` `Initialize`:
```csharp
        _meshes[CellShape.Dodecahedron] = new Meshes.DodecahedronMesh(_gl);
```

`SessionManager.ApplyRenderSettings` clamp upper bound → `6`.

`ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid", "Icosahedron", "Dodecahedron" };
```

- [ ] **Step 4: Build + smoke test**

Select "Dodecahedron". Alive cells should be 12-sided faceted spheres with pentagonal facets clearly visible. Rotate camera to check all faces shade correctly.

If any face appears unlit or inverted, the pentagon index list above is the suspect — verify each pentagon traverses its 5 vertices in a single planar loop (no skipping across the polyhedron).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add Dodecahedron cell shape"
```

---

## Task 9: Icosphere mesh (subdivided icosahedron, sphere stand-in)

Take each icosahedron face, split it into 4 sub-triangles by inserting a midpoint on each edge, then push every vertex out to the bounding sphere. Smooth-shaded — vertex normals point radially. Result: 80 triangles, reads as "round".

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/IcosphereMesh.cs`
- Modify: `CellShape.cs`, `InstancedCellRenderer.cs`, `SessionManager.cs`, `ImGuiUI.cs`

- [ ] **Step 1: Add enum member**

`CellShape.cs`: append `Sphere = 7,`.

- [ ] **Step 2: Create the mesh class**

`src/GameOfLife3D.NET/Rendering/Meshes/IcosphereMesh.cs`:
```csharp
using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Sphere built by subdividing an icosahedron once (each triangle → 4) and
/// projecting every vertex onto the bounding sphere (radius 0.5). 80 tris.
/// Smooth-shaded: vertex normal = normalize(vertex position) since the sphere
/// is centered at the origin.
/// </summary>
public sealed class IcosphereMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public IcosphereMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        const float Radius = 0.5f;

        // Start with the base icosahedron's 12 vertices (already scaled so
        // their magnitudes match Radius). Each face is 3 indices in BaseFaces.
        var positions = new List<Vector3>();
        foreach (var (x, y, z) in IcosahedronMesh.BaseVertices)
            positions.Add(new Vector3(x, y, z));

        var faces = new List<(int a, int b, int c)>();
        for (int f = 0; f < IcosahedronMesh.BaseFaces.Length; f += 3)
            faces.Add((IcosahedronMesh.BaseFaces[f],
                       IcosahedronMesh.BaseFaces[f + 1],
                       IcosahedronMesh.BaseFaces[f + 2]));

        // One subdivision pass.
        var midpointCache = new Dictionary<(int, int), int>();
        int Midpoint(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (midpointCache.TryGetValue(key, out var idx)) return idx;
            var mid = Vector3.Normalize((positions[a] + positions[b]) * 0.5f) * Radius;
            positions.Add(mid);
            int newIdx = positions.Count - 1;
            midpointCache[key] = newIdx;
            return newIdx;
        }

        var subdivided = new List<(int, int, int)>(faces.Count * 4);
        foreach (var (a, b, c) in faces)
        {
            int ab = Midpoint(a, b);
            int bc = Midpoint(b, c);
            int ca = Midpoint(c, a);
            subdivided.Add((a, ab, ca));
            subdivided.Add((b, bc, ab));
            subdivided.Add((c, ca, bc));
            subdivided.Add((ab, bc, ca));
        }

        // Build the flat vertex buffer with smooth normals (radial).
        var verts = new List<float>(positions.Count * 6);
        foreach (var p in positions)
        {
            Vector3 n = Vector3.Normalize(p);
            verts.Add(p.X); verts.Add(p.Y); verts.Add(p.Z);
            verts.Add(n.X); verts.Add(n.Y); verts.Add(n.Z);
        }

        var idx = new List<uint>(subdivided.Count * 3);
        foreach (var (a, b, c) in subdivided)
        {
            idx.Add((uint)a);
            idx.Add((uint)b);
            idx.Add((uint)c);
        }

        IndexCount = (uint)idx.Count;
        OctahedronMesh.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

- [ ] **Step 3: Register, widen clamp, add UI entry**

`InstancedCellRenderer.cs` `Initialize`:
```csharp
        _meshes[CellShape.Sphere] = new Meshes.IcosphereMesh(_gl);
```

`SessionManager.ApplyRenderSettings` clamp upper bound → `7`.

`ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid", "Icosahedron", "Dodecahedron", "Sphere" };
```

- [ ] **Step 4: Build + smoke test**

Select "Sphere". Alive cells should look round and smooth-shaded — no visible facet edges at moderate camera distances. The wireframe toggle, if turned on, reveals the 80-triangle subdivision pattern.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add Sphere (icosphere) cell shape"
```

---

## Task 10: Capsule mesh

Cylinder body along Y between Y = -0.25 and Y = +0.25, capped by hemispheres of radius 0.25 at each end, so the full extent is ±0.5 along Y and ±0.25 along X/Z. Smooth-shaded.

**Files:**
- Create: `src/GameOfLife3D.NET/Rendering/Meshes/CapsuleMesh.cs`
- Modify: `CellShape.cs`, `InstancedCellRenderer.cs`, `SessionManager.cs`, `ImGuiUI.cs`

- [ ] **Step 1: Add enum member**

`CellShape.cs`: append `Capsule = 8,`.

- [ ] **Step 2: Create the mesh class**

`src/GameOfLife3D.NET/Rendering/Meshes/CapsuleMesh.cs`:
```csharp
using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Capsule (pill) along the Y axis. 8 segments around the circumference and
/// 3 latitude rings per hemisphere cap (≈96 tris). Long axis is Y so vertical
/// generation stacks merge visually. Smooth normals: each vertex's normal is
/// the direction from the nearest cap center (for caps) or the axis (for the
/// cylinder).
/// </summary>
public sealed class CapsuleMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    private const int Segments = 8;     // around the circumference
    private const int CapRings = 3;     // latitude rings per hemisphere
    private const float Radius = 0.25f; // cap radius
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
        // Latitude i = 0 is the pole; i = CapRings is the equator.
        for (int i = 0; i <= CapRings; i++)
        {
            float phi = (MathF.PI * 0.5f) * (float)i / CapRings;  // 0 .. π/2
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
        // Mirror of the top. Latitude i = 0 is the equator (matches top's
        // equator ring); i = CapRings is the pole.
        for (int i = 1; i <= CapRings; i++)  // start at 1 — equator already added above
        {
            float phi = (MathF.PI * 0.5f) * (float)i / CapRings;
            float y = -Radius * MathF.Sin(phi);  // negative on bottom
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

        // The top cap's equator ring is at index TopRingStart = CapRings * Segments.
        // The bottom hemisphere starts at index (CapRings+1)*Segments.
        // We connect the top-cap equator to the bottom-cap equator directly —
        // there are no separate body rings; the cylinder body is the quad band
        // between those two equator rings.

        int totalRings = 2 * CapRings + 1; // top: CapRings+1 rings; bottom adds CapRings more
        // (equator shared, so 2*CapRings+1 unique rings)

        var idx = new List<uint>();
        // Indices: rings are stacked. Connect ring r to ring r+1 with Segments
        // quads.
        for (int r = 0; r < totalRings - 1; r++)
        {
            for (int j = 0; j < Segments; j++)
            {
                int jNext = (j + 1) % Segments;
                int a = r * Segments + j;
                int b = r * Segments + jNext;
                int c = (r + 1) * Segments + jNext;
                int d = (r + 1) * Segments + j;

                idx.Add((uint)a); idx.Add((uint)b); idx.Add((uint)c);
                idx.Add((uint)a); idx.Add((uint)c); idx.Add((uint)d);
            }
        }

        // Pole closures: top pole is the first ring (all vertices coincide —
        // the i=0 ring has y=Radius, r=0, so each "vertex" is at (0, R, 0)).
        // The pole strip is already covered by the loop above because we
        // treated the pole as a degenerate ring of Segments vertices.

        // Build vertex buffer.
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
        OctahedronMesh.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}
```

**Note on the pole degenerate-ring approach:** The top of the capsule technically has a single pole vertex, but we emit `Segments` coincident vertices instead so the connectivity is uniform — every ring has exactly `Segments` vertices and every band is a clean quad strip. This costs a few extra vertices but eliminates a fan-around-pole special case. Triangles at the pole degenerate to zero area but render harmlessly.

- [ ] **Step 3: Register, widen clamp, add UI entry**

`InstancedCellRenderer.cs` `Initialize`:
```csharp
        _meshes[CellShape.Capsule] = new Meshes.CapsuleMesh(_gl);
```

`SessionManager.ApplyRenderSettings` clamp upper bound → `8`.

`ImGuiUI.cs`:
```csharp
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid", "Icosahedron", "Dodecahedron", "Sphere", "Capsule" };
```

- [ ] **Step 4: Build + smoke test**

Select "Capsule". Alive cells should be vertical pill shapes — extended along Y, narrower in X/Z. Vertical stacks of alive cells (multiple generations of the same X,Z) should read as continuous columns. Wireframe should show 8-segment ring structure.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add Capsule cell shape"
```

---

## Task 11: Thumbnail renderer and combo UI with icons

Replace the text-only `ImGui.Combo` with a `BeginCombo` / `Selectable` flow that renders a 24×24 thumbnail next to each name. Thumbnails are generated once at startup by rendering each mesh into a small offscreen FBO using a minimal dedicated shader.

**Files:**
- Create: `src/GameOfLife3D.NET/Shaders/thumb.vert`
- Create: `src/GameOfLife3D.NET/Shaders/thumb.frag`
- Create: `src/GameOfLife3D.NET/Rendering/ShapeThumbnailRenderer.cs`
- Modify: `src/GameOfLife3D.NET/Rendering/Renderer3D.cs` (initialize thumbs at startup, expose to UI)
- Modify: `src/GameOfLife3D.NET/UI/ImGuiUI.cs` (combo with images)
- Modify: `src/GameOfLife3D.NET/GameOfLife3D.NET.csproj` (embed the two new shaders)

- [ ] **Step 1: Write the thumb vertex shader**

`src/GameOfLife3D.NET/Shaders/thumb.vert`:
```glsl
#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 uMvp;

out vec3 vNormal;

void main()
{
    // Mesh has no model transform and the light direction is supplied in
    // object space, so the normal can be passed through unchanged.
    vNormal = aNormal;
    gl_Position = uMvp * vec4(aPosition, 1.0);
}
```

- [ ] **Step 2: Write the thumb fragment shader**

`src/GameOfLife3D.NET/Shaders/thumb.frag`:
```glsl
#version 330 core

in vec3 vNormal;

uniform vec3 uColor;
uniform vec3 uLightDir;

out vec4 FragColor;

void main()
{
    float ambient = 0.45;
    float diffuse = max(dot(normalize(vNormal), normalize(uLightDir)), 0.0) * 0.55;
    FragColor = vec4(uColor * (ambient + diffuse), 1.0);
}
```

- [ ] **Step 3: Embed the new shaders in the project file**

In `src/GameOfLife3D.NET/GameOfLife3D.NET.csproj`, find the `<EmbeddedResource>` items for the other shaders (e.g., `cube.vert`) and add two analogous lines for `thumb.vert` and `thumb.frag`. They should follow the same pattern (`Include="Shaders\thumb.vert"` etc., or via the existing wildcard if there is one).

If the csproj uses a wildcard like `<EmbeddedResource Include="Shaders\*" />`, no change is needed — verify by searching:
```bash
grep -n "EmbeddedResource\|Shaders" src/GameOfLife3D.NET/GameOfLife3D.NET.csproj
```

- [ ] **Step 4: Create `ShapeThumbnailRenderer`**

`src/GameOfLife3D.NET/Rendering/ShapeThumbnailRenderer.cs`:
```csharp
using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Renders each registered CellShape mesh once at startup into a small RGBA
/// texture and exposes the GL texture handles for ImGui Image() calls. The
/// thumbnails are flat-shaded with a neutral mid-gray so the shape itself is
/// readable regardless of the user's current cell color.
/// </summary>
public sealed class ShapeThumbnailRenderer : IDisposable
{
    private const int Size = 32;
    private static readonly Vector3 ThumbColor = new(0.65f, 0.78f, 0.90f);

    private readonly GL _gl;
    private readonly Dictionary<CellShape, uint> _textures = new();
    private ShaderProgram? _shader;

    public ShapeThumbnailRenderer(GL gl)
    {
        _gl = gl;
    }

    public uint? GetTexture(CellShape shape)
        => _textures.TryGetValue(shape, out var id) ? id : null;

    public unsafe void Render(IReadOnlyDictionary<CellShape, IInstancedMesh> meshes)
    {
        _shader = ShaderProgram.FromEmbeddedResources(_gl, "thumb.vert", "thumb.frag");

        // Save GL state we're about to perturb so the caller's render setup
        // isn't disturbed. We only need viewport + currently-bound FBO; the
        // shader binds are cleaned up by ShaderProgram.Use() being called
        // again before the next draw.
        int[] prevViewport = new int[4];
        _gl.GetInteger(GLEnum.Viewport, prevViewport);
        _gl.GetInteger(GLEnum.FramebufferBinding, out int prevFbo);

        // One small FBO + color texture + depth renderbuffer, reused across shapes.
        uint fbo = _gl.GenFramebuffer();
        uint depthRbo = _gl.GenRenderbuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
            InternalFormat.DepthComponent24, Size, Size);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRbo);

        Matrix4x4 view = Matrix4x4.CreateLookAt(
            new Vector3(0.9f, 0.8f, 1.4f), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4f, 1f, 0.1f, 5f);
        Matrix4x4 mvp = view * proj;

        // Light direction is supplied in **object space** to match the thumb
        // shader's pass-through normal — the mesh has no model transform, so
        // object/world space coincide for the model, and we don't need to
        // transform the normal to view space.
        Vector3 lightObjectSpace = Vector3.Normalize(new Vector3(0.6f, 0.9f, 0.4f));

        _gl.Viewport(0, 0, Size, Size);
        _gl.Enable(EnableCap.DepthTest);

        _shader.Use();
        _shader.SetUniform("uMvp", mvp);
        _shader.SetUniform("uColor", ThumbColor);
        _shader.SetUniform("uLightDir", lightObjectSpace);

        foreach (var kv in meshes)
        {
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
                Size, Size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.ClampToEdge);

            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);

            _gl.ClearColor(0.10f, 0.12f, 0.16f, 1f); // dark slate background
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _gl.BindVertexArray(kv.Value.Vao);
            _gl.DrawElements(PrimitiveType.Triangles, kv.Value.IndexCount,
                DrawElementsType.UnsignedInt, null);

            _textures[kv.Key] = tex;
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)prevFbo);
        _gl.Viewport(prevViewport[0], prevViewport[1],
            (uint)prevViewport[2], (uint)prevViewport[3]);

        _gl.DeleteFramebuffer(fbo);
        _gl.DeleteRenderbuffer(depthRbo);
    }

    public void Dispose()
    {
        foreach (var tex in _textures.Values)
            _gl.DeleteTexture(tex);
        _textures.Clear();
        _shader?.Dispose();
    }
}
```

**No new ShaderProgram helpers needed.** The thumb shader uses only `uMvp` (Matrix4x4), `uColor` (Vector3), and `uLightDir` (Vector3), all of which `ShaderProgram` already supports via existing `SetUniform` overloads (visible in the cube/wireframe/floor shader call paths).

- [ ] **Step 5: Initialize the thumbnail renderer from `Renderer3D` and expose it**

In `src/GameOfLife3D.NET/Rendering/Renderer3D.cs`:

Add a field:
```csharp
    private ShapeThumbnailRenderer? _shapeThumbnails;
```

In `Initialize()`, after the existing mesh renderer is initialized:
```csharp
        _shapeThumbnails = new ShapeThumbnailRenderer(_gl);
        _shapeThumbnails.Render(_instancedRenderer.GetMeshes());
```

`GetMeshes()` needs to be added to `InstancedCellRenderer`:
```csharp
    public IReadOnlyDictionary<CellShape, IInstancedMesh> GetMeshes() => _meshes;
```

Add a public accessor on `Renderer3D`:
```csharp
    public ShapeThumbnailRenderer? ShapeThumbnails => _shapeThumbnails;
```

In `Dispose()`:
```csharp
        _shapeThumbnails?.Dispose();
```

- [ ] **Step 6: Pass the thumbnail renderer into `ImGuiUI` and update the combo**

Find where `ImGuiUI` receives `RenderSettings` or `Renderer3D` from `App.cs`. (Grep:
```bash
grep -n "new ImGuiUI\|ImGuiUI(" src/GameOfLife3D.NET/App.cs src/GameOfLife3D.NET/UI/ImGuiUI.cs
```
)

Pass the `ShapeThumbnailRenderer` (or the whole `Renderer3D`) into the existing entry point — the simplest change is to thread it through whichever method renders the settings panel, since that's where the combo lives.

Replace the existing simple `ImGui.Combo` call from Task 3 with an image-aware combo:

```csharp
            // Cell shape — combo rows show a thumbnail next to each name.
            var thumbnails = renderer3D.ShapeThumbnails;  // however it's threaded in
            if (ImGui.BeginCombo("Cell Shape", ShapeNames[_shape]))
            {
                for (int i = 0; i < ShapeNames.Length; i++)
                {
                    bool isSelected = (i == _shape);
                    var thumb = thumbnails?.GetTexture((CellShape)i);
                    if (thumb.HasValue)
                    {
                        ImGui.Image((IntPtr)thumb.Value, new System.Numerics.Vector2(24, 24));
                        ImGui.SameLine();
                    }
                    if (ImGui.Selectable(ShapeNames[i], isSelected))
                    {
                        _shape = i;
                        settings.Shape = (CellShape)i;
                    }
                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
```

If your `ImGuiUI` method that renders this section doesn't currently receive the `Renderer3D`, add the parameter and update the single call site in `App.cs`.

- [ ] **Step 7: Build**

```bash
dotnet build
```
Expected: succeeds. If the `SetUniformMatrix3` method was missing, you'll get a compile error pointing to `ShapeThumbnailRenderer.cs`; add the helper to `ShaderProgram.cs` and rebuild.

- [ ] **Step 8: Smoke test**

```bash
dotnet run --project src/GameOfLife3D.NET/
```

1. Open the render settings panel.
2. Click the "Cell Shape" combo. Verify the dropdown shows nine rows, each with a small 3D-rendered thumbnail next to the name.
3. Each thumbnail should clearly depict its shape (cube, beveled cube, etc.).
4. Select each shape in turn — confirm the alive cells update to match.
5. Quit.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Add ShapeThumbnailRenderer and image-rich cell shape combo"
```

---

## Final verification

- [ ] **Run a full session save/load cycle to verify backward compat**

```bash
dotnet run --project src/GameOfLife3D.NET/
```

1. Pick a non-default shape (e.g., Dodecahedron). Save the session. Quit.
2. Re-launch. Load the session. Verify the dodecahedron loads correctly.
3. Find an older session JSON file you have around (or create one before this branch by checking out master, saving, then coming back). Load it on the new branch — verify the cells render as cubes or beveled cubes (whichever the legacy `UseBeveledCubes` boolean implied).

- [ ] **Run `dotnet build --configuration Release`**

Make sure release builds still produce a self-contained single-file binary without errors.

---

## Notes for the implementer

- **Parallelizable tasks:** Tasks 4, 5, 6, 7, and 10 (tetrahedron, octahedron, pyramid, icosahedron, capsule) are independent of each other and can be dispatched in parallel after Task 3 completes. Tasks 8 (dodecahedron) and 9 (icosphere) **both depend on Task 7's `IcosahedronMesh.BaseVertices`/`BaseFaces`** and must wait for it. Each task only touches its own new mesh file plus one-line edits in four shared files (`CellShape.cs`, `InstancedCellRenderer.cs.Initialize`, `SessionManager.cs` clamp, `ImGuiUI.cs ShapeNames`). Merge-conflict risk on the shared files is real but trivially resolvable.

- **Mesh visual correctness:** Without a test harness, visual inspection is the only check. After each mesh task, rotate the camera 360° around a single cell, and toggle wireframe on/off. Watch for: faces shaded dark (likely an inverted face normal), edges that don't meet (missing face), or shapes that fall outside the cell grid (scale off).

- **The `UseBeveledCubes` legacy field** stays in `RenderSessionData` for one release. After confirming session migration works for at least one release, it can be removed in a follow-up PR — out of scope for this plan.
