# Plan: Real-time Statistics & Periodicity Detection

## Goal
Build on the existing population graph in the Statistics section to add periodicity detection (oscillator/still-life/spaceship identification), per-generation births/deaths tracking, bounding-box analysis, classification badges, and CSV export. Turn the tool into an analysis aid, not just a visualizer.

## Current State (reference)
- Cell count displayed in `UI/StatusBar.cs` `Render()` (line 31), pulling `_renderer.GetVisibleCellCount()` (Rendering/Renderer3D.cs line 220).
- A Statistics section already exists: `UI/ImGuiUI.cs` `RenderStatsSection()` (lines 571–610). It rebuilds a `_populationData` float[] each time `GenerationCount` changes, displays Current/Min/Max/Average labels, and renders a single `ImGui.PlotLines` population graph. No births/deaths, no bounding box, no classification.
- `GameEngine.Generations` list (Engine/GameEngine.cs line 18) stores every computed generation as a `Generation` (sealed class — Engine/Generation.cs line 5) holding `Index`, `Cells` (`bool[,]`), and `LiveCells` (`IReadOnlyList<Vector2Int>`).
- `GenerationCount` (line 17). Max generation cap is 1000.
- Generations are appended via the private `GameEngine.AddGeneration(bool[,])` (line 188), called from `InitializeFromPattern`, `InitializeRandom`, `ComputeGenerations` (line 102), `ComputeSingleGeneration` (line 115), and `ImportState`. The per-step computation lives in private `ComputeNextGeneration(bool[,])` (line 199).
- No per-generation hash today; no births/deaths tracking; no pattern classification.

## Design

### 1. Per-generation statistics record
Create `Engine/GenerationStats.cs`:
```csharp
public sealed record GenerationStats(
    int Index,
    int LiveCount,
    int Births,           // cells alive this gen, dead last gen
    int Deaths,           // cells dead this gen, alive last gen
    (int MinX, int MinY, int MaxX, int MaxY) BoundingBox,
    ulong StateHash);     // 64-bit hash of (canonicalized) cell positions
```

Compute inside the private `GameEngine.AddGeneration(bool[,])` method (line 188) — the single chokepoint where every new generation is appended. All values derive from the cell array plus the previous generation. O(live cells) per generation, negligible overhead.

**Hash function**: FNV-1a or xxHash over the sorted `(x, y)` live-cell pairs. Use raw positions (not translated) as a first pass; see §3 for translation-invariant hashing.

Store in `GameEngine` as `IReadOnlyList<GenerationStats> Stats`.

### 2. Periodicity detection
Create `Engine/PeriodicityDetector.cs`:
- Maintain a dictionary `Dictionary<ulong, int> _hashToGeneration` updated each generation.
- When the current hash matches a prior generation's hash: period = `currentGen - priorGen`.
- Classify:
  - Period 1, same bounding box → **still life**.
  - Period N, same bounding box → **oscillator (period N)**.
  - Period N, bounding box translated by (dx, dy) → **spaceship (period N, displacement (dx,dy))**.
- To catch translated repeats, compute a second hash on cells normalized to their bounding-box origin (`cell - (minX, minY)`). A match on the normalized hash with a changed bounding box means a spaceship.
- Emit `PatternClassification` record on first detection; store in `GameEngine.Classifications`.

### 3. Translation-invariant hashing (spaceships)
Compute two hashes per generation:
- `RawHash`: positions as-is → detects stationary oscillators/still lifes.
- `NormalizedHash`: positions minus bounding-box min → detects any repeating shape regardless of position.

O(live cells × 2) — still cheap for grids up to 200x200.

### 4. UI: extend the existing Statistics section
Rework `ImGuiUI.RenderStatsSection()` (lines 571–610) in place rather than creating a separate window. The Current/Min/Max/Average labels and the existing `ImGui.PlotLines` population graph stay; replace the per-frame `_populationData` rebuild with a read of the new `_engine.Stats` list (avoids the existing O(N) rebuild on every generation change).

Add the following new elements below the current population graph:

1. **Births/Deaths plot** — second `ImGui.PlotLines` (or two stacked) showing per-generation births and deaths.
2. **Bounding box** — `LabelValue` rows showing width × height and centroid of the currently displayed generation.
3. **Classification** — colored label showing detected type: "Still life", "Oscillator (period 2)", "Spaceship (period 4, heading ↗)", or "Evolving".
4. **Export** — `Export CSV` button writing `gen, live, births, deaths, minX, minY, maxX, maxY, hash` to a user-chosen file (reuses NativeFileDialogSharp).

### 5. Status bar addition
Extend `UI/StatusBar.cs` to show a small classification badge next to the cell count:
- `● STILL`, `● P2`, `● SHIP P4` — short codes so they fit.
- Color-coded (green for stable, blue for oscillator, purple for spaceship).

### 6. Optional heatmap overlay
Add a toggle "Heatmap" in the render settings that color-codes cubes by how long they've been alive (cell age).
- Track `int[,] CellAge` in `GameEngine` updated per generation (reset to 0 on death, +1 on survival).
- Pass per-cell age as an instance attribute to `InstancedCubeRenderer` (extend existing per-instance buffer layout).
- Shader samples a viridis-style gradient based on age / maxAge.

This is a bonus — ship the panel/stats first.

## Implementation steps
1. Add `GenerationStats` record.
2. Add an `_stats` list on `GameEngine` and emit a `GenerationStats` from inside the existing private `AddGeneration(bool[,])` method (line 188) — the single chokepoint used by `InitializeFromPattern`, `InitializeRandom`, `ComputeGenerations`, `ComputeSingleGeneration`, and `ImportState`. Also clear `_stats` wherever `_generations.Clear()` is called (e.g. `SetGridSize`, `Clear`, and the gen-0 edit path in `SetCellInGen0`).
3. Add FNV-1a hash helper in `Engine/Hashing.cs`.
4. Add `PeriodicityDetector` called after each stats computation.
5. Extend `ImGuiUI.RenderStatsSection()` (lines 571–610) with births/deaths plot, bounding box, classification label, and export button. Drive it from `_engine.Stats` instead of the current `_populationData` rebuild.
6. Extend `StatusBar` with classification badge.
7. Add CSV export via `IO/StatisticsExporter.cs`.
8. (Phase 2) Cell-age tracking + heatmap shader variant.

## Out of scope (future)
- Detecting multiple independent patterns (e.g., a glider + separate still life) — would require connected-component analysis per generation.
- Long-range predictions / Hashlife-style memoization.
- Identifying named patterns from a library (e.g., "that's a Pulsar").

## Risks
- Hash collisions: 64-bit FNV-1a over sorted cell coords is good enough for <10⁶ generations. Add a slow-path exact-comparison fallback when a match is found (compare cell arrays directly) to eliminate false positives.
- Graph can get dense past a few hundred generations — downsample or use `ImPlot` if added later. `ImGui.PlotLines` handles the 1000-gen cap fine without that.
- Bounding-box classification fails for patterns that emit a glider (bounding box grows unboundedly). Mark these as "evolving" until they stabilize.

## Estimate
~2 days: 0.5 day stats + hash, 0.5 day periodicity detection, 0.5 day UI panel, 0.25 day CSV export, 0.25 day status-bar badge. Heatmap bonus adds ~0.5 day.
