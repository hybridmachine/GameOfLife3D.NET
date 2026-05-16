# PLAN — Cell Shape Variants

**Status:** design approved, not yet implemented
**Date:** 2026-05-15

## Goal

Add a global "cell shape" picker so alive cells can be rendered as one of several 3D primitives — not just cubes. The user picks one shape; every alive cell renders as that shape. Shapes are chosen to cover faceted, pyramidal, and oblong silhouettes while staying cheap to render.

## Shape Roster

All meshes fit the unit-cube footprint (centered at origin, ±0.5 extent) so the existing grid/raycast/editing systems keep working unchanged. Triangle counts are approximate.

| Shape | Tris | Notes |
|---|---|---|
| Cube | 12 | Existing `CubeMesh`. |
| Beveled cube | ~44 | Existing `BeveledCubeMesh`. **Default** (preserves today's first-run look). |
| Tetrahedron | 4 | 4 triangular faces. |
| Octahedron | 8 | 8 triangular faces, point up/down. |
| Square pyramid | 6 | 4 side triangles + square base; base sits on the grid plane. |
| Dodecahedron | 36 | 12 pentagons, triangulated. Faceted-globe look. |
| Icosahedron | 20 | 20 triangles. Geodesic feel. |
| Sphere (icosphere, 1 subdiv) | 80 | Subdivided icosahedron. No UV-sphere pole pinch. |
| Capsule | ~96 | Cylinder body + hemisphere caps (8 segments around, 3 latitude rings per cap). Long axis along **Y** (generation axis) so vertical cell stacks merge into pill-like columns. |

**Orientation.** One fixed pose per shape, chosen to look balanced on the grid. No per-cell rotation.

## Architecture

### Selection model

Global. One shape applies to every alive cell. Mirrors the existing `UseBeveledCubes` toggle generalized to N options.

### Mesh abstraction

A small `IInstancedMesh` interface exposing `Vao` and `IndexCount`. Every shape mesh implements it with the same vertex layout used today — position (vec3) + normal (vec3), stride 24 bytes. The instance buffer and instance attribute layout (`aInstancePosition` loc 2, `aGenerationT` loc 3) stay exactly as they are, bound to each shape's VAO at init.

`MeshBuilder` is a static helper extracted from `BeveledCubeMesh`'s private `AddQuad`/`AddTriangle`/`AddVertex` methods. New polyhedron meshes use it. Sphere and capsule have their own small parametric generators.

### Renderer changes

- `InstancedCubeRenderer` is renamed to `InstancedCellRenderer`. Call sites in `Renderer3D` get a one-line rename.
- The renderer holds `Dictionary<CellShape, IInstancedMesh>` populated at `Initialize()`. Instance attributes are bound to every shape's VAO at startup.
- Per-frame draw picks the active mesh from `settings.Shape` instead of branching on `UseBeveledCubes`.
- **LOD behavior is preserved exactly as today** for the beveled cube — at 500k+ instances it still falls back to the plain cube mesh. The new shapes get **no fallback**: they always render at their chosen mesh, regardless of cell count. Most new shapes are at or below cube complexity (tetrahedron 4, pyramid 6, octahedron 8, icosahedron 20); the heavier ones (dodecahedron 36, sphere 80, capsule 96) are still in range of the GPU at 4M instances on the hardware this app already targets. A per-shape threshold can be added later if a real perf problem surfaces in profiling.

### Shaders

Unchanged. `cube.vert` / `cube.frag` already operate on any mesh with position + normal — they don't care whether the mesh is a cube. Wireframe (`polygon-mode line`) is mesh-agnostic and continues to work for every shape.

### Settings

`RenderSettings`:

```csharp
public CellShape Shape { get; set; } = CellShape.BeveledCube;
// UseBeveledCubes is removed.
```

`CellShape` enum members: `Cube, BeveledCube, Octahedron, Tetrahedron, SquarePyramid, Dodecahedron, Icosahedron, Sphere, Capsule`.

### Persistence and backward compatibility

`RenderSessionData` (the JSON-persisted record):

1. Add `CellShape? Shape` (nullable, following the project's existing "absent in legacy save vs explicit value" pattern).
2. Keep `bool? UseBeveledCubes` for one release as a legacy fallback.
3. On load: if `Shape` is present, use it. Otherwise if `UseBeveledCubes == true` → `BeveledCube`, else → `Cube`. Sessions saved post-feature only write `Shape`.

This guarantees existing session files reload pixel-for-pixel identically.

## UI

In the existing render-settings panel, replace the "Use beveled cubes" checkbox with a "Cell shape" combo.

**Combo rows.** Each row shows a small thumbnail (≈24×24 px) followed by the shape name. The selected-item header at the top of the closed combo also shows the thumbnail. Implemented with `ImGui.BeginCombo` + `Selectable` + `Image` (ImGui's default `Combo` doesn't render images in dropdown rows).

**Thumbnail source.** A new `Rendering/ShapeThumbnailRenderer` renders each mesh once at startup to a private 32×32 RGBA framebuffer using the existing cube shader (fixed camera + light direction). Output is a `Dictionary<CellShape, uint>` of GL texture handles consumed by the ImGui combo. Disposed alongside the renderer. Cost: 9 sub-millisecond draws at startup.

Thumbnails always match the actual rendered shape — no asset pipeline, and any future mesh tweak is reflected automatically.

## Files

**Modified**
- `Rendering/RenderSettings.cs` — replace `UseBeveledCubes` with `Shape`.
- `Rendering/InstancedCubeRenderer.cs` → renamed `InstancedCellRenderer.cs`, holds shape dictionary, switches mesh by `settings.Shape`. The `BeveledMaxInstances = 500_000` LOD branch is preserved exactly — beveled-cube → plain-cube fallback at 500k+ stays untouched. The new shapes are not subject to any fallback.
- `Rendering/BeveledCubeMesh.cs` — implement `IInstancedMesh`. The private `AddQuad`/`AddTriangle`/`AddVertex` helpers move to `MeshBuilder`.
- `Rendering/CubeMesh.cs` — implement `IInstancedMesh`.
- `Rendering/Renderer3D.cs` — one-line type rename.
- `IO/SessionManager.cs` — add nullable `Shape` field; keep `UseBeveledCubes` legacy fallback for one load cycle.
- `UI/ImGuiUI.cs` — replace the beveled-cubes checkbox with the new combo + thumbnail rows.

**Created**
- `Rendering/CellShape.cs` — enum.
- `Rendering/IInstancedMesh.cs` — interface.
- `Rendering/MeshBuilder.cs` — shared `AddQuad`/`AddTriangle`/`AddVertex` helpers.
- `Rendering/Meshes/TetrahedronMesh.cs`
- `Rendering/Meshes/OctahedronMesh.cs`
- `Rendering/Meshes/SquarePyramidMesh.cs`
- `Rendering/Meshes/DodecahedronMesh.cs`
- `Rendering/Meshes/IcosahedronMesh.cs`
- `Rendering/Meshes/IcosphereMesh.cs` — sphere via 1 subdivision of icosahedron.
- `Rendering/Meshes/CapsuleMesh.cs` — Y-axis cylinder + hemisphere caps, 8-segment rings.
- `Rendering/ShapeThumbnailRenderer.cs` — offscreen FBO that produces the icon textures.

(The `Rendering/Meshes/` subfolder is a new organizational nicety; if you'd prefer to keep meshes flat in `Rendering/`, that's fine — one-line change.)

## Performance notes

At the current 4M max-instance ceiling, the heaviest shape (capsule, ~96 tris) costs ~384M tris per draw, which modern GPUs handle. Realistic max-occupancy at high cell counts is typically much lower than 4M alive cells, so real-world tris/frame is well under that.

The existing beveled-cube 500k LOD fallback is kept untouched to preserve current behavior for users who have already tuned their experience around it. New shapes have no fallback — they render as themselves at every cell count. If a real perf issue shows up in profiling (especially for sphere/capsule at very high counts), a per-shape threshold can be added later without changing the user-facing API.

## Out of scope (deliberately deferred)

- Per-cell shape variation.
- Per-cell rotation.
- Animated shape morphing.
- Shape cycling per generation.
- User-imported custom meshes.
- Hexagonal prism / other shapes beyond the 9 in the roster.

Easy to revisit any of these once the global-shape feature is in.
