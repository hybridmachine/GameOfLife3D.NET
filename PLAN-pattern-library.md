# Plan: Pattern Library Browser with Search

## Goal
Replace the current hand-coded list of ~5–12 built-in patterns (shown as flat buttons in the ImGui panel) with a searchable, filterable library that scales to hundreds of patterns. Users should be able to browse by category, filter by type/period/size, preview the pattern, and load it into the simulation.

## Current State (reference)
- Hand-coded `bool[,]` patterns live in `src/GameOfLife3D.NET/Engine/PatternLoader.cs` (lines 12–145), each with a `PatternInfo` record.
- RLE parsing already exists: `PatternLoader.ParseRLE()` (lines 158–226) and `ExportRLE()` (228–303).
- Pattern picker UI: `UI/ImGuiUI.cs` `RenderPatternSection()` (lines 612–637) — iterates `_patternLoader.GetBuiltInPatternMap()` as buttons, calls `_engine.InitializeFromPattern()` on click.
- Patterns currently ship only as code constants; there is no on-disk library.

## Design

### 1. Bundled pattern library
- Add a `resources/patterns/` directory containing `.rle` files organized in subfolders by category: `oscillators/`, `spaceships/`, `guns/`, `methuselahs/`, `still-lifes/`, `puffers/`, `misc/`.
- Seed with ~50–100 well-known patterns in the public domain (LifeWiki CC-BY-SA attribution file at `resources/patterns/ATTRIBUTION.md`).
- Mark the folder as `CopyToOutputDirectory=PreserveNewest` in `GameOfLife3D.NET.csproj`.

### 2. Pattern metadata
Create `Engine/PatternMetadata.cs`:
```csharp
public sealed record PatternMetadata(
    string Id,          // "gosper-glider-gun"
    string Name,        // "Gosper Glider Gun"
    string Category,    // "guns"
    int Width,
    int Height,
    int? Period,        // null for non-periodic
    string? Author,
    string? Description,
    string RlePath);    // relative path into resources/patterns/
```
- Extract metadata from RLE header comments (`#N name`, `#O author`, `#C comment`) already commonly present in LifeWiki files — extend `ParseRLE()` to surface these (currently it ignores header lines except dimensions).
- Infer `Period` from the `#C` comment when a `period N` token is present; otherwise compute lazily by simulating up to N generations and hashing state (re-uses hash work from the statistics feature).

### 3. Library index
Create `Engine/PatternLibrary.cs`:
- `PatternLibrary.LoadFromDirectory(string root)` — walks `resources/patterns/`, parses each RLE header only (cheap), builds `IReadOnlyList<PatternMetadata>`.
- Cached in a singleton on the `App` instance, passed into `ImGuiUI` via constructor (mirrors how `_patternLoader` is injected today).
- Full `bool[,]` grid is parsed lazily the first time a pattern is selected (avoids loading 100 grids upfront).

### 4. Browser UI
Rework `ImGuiUI.RenderPatternSection()` (612–637) into a two-panel layout inside the existing control window:
- **Top**: search box (`ImGui.InputTextWithHint`), category dropdown, period filter (min/max sliders), size filter (max width/height sliders).
- **Middle**: scrollable list (`ImGui.BeginChild` with fixed height ~250px) of matching patterns — one row per pattern, showing name, category, size, and period.
- **Bottom**: preview box — a small 2D mini-grid (drawn with ImGui's `DrawList` primitives) showing the currently highlighted pattern's cells, plus an `Load` button.

Keep the existing built-in category available as a quick-access pinned group above the search results.

### 5. Load path
- Reuse `_engine.InitializeFromPattern(bool[,])` — no engine changes needed.
- Add a recently-used list (last 8 patterns) persisted in `imgui.ini` or a new `library.state.json` under the user profile.

## Implementation steps
1. Create `resources/patterns/` directory and add ~20 seed RLEs (copy from simple LifeWiki patterns; commit attribution file).
2. Add `PatternMetadata` record.
3. Extend `PatternLoader.ParseRLE()` to capture `#N`/`#O`/`#C` comments into an output metadata struct.
4. Add `PatternLibrary` class with directory scan + search/filter methods.
5. Wire `PatternLibrary` into `App.OnLoad()` (instantiated alongside `_patternLoader`).
6. Rewrite `ImGuiUI.RenderPatternSection()` with search + list + preview.
7. Add mini-grid preview helper (`UI/PatternPreview.cs`) using `ImGui.GetWindowDrawList()`.
8. Add recently-used persistence.

## Out of scope (future)
- Live HTTP fetch from LifeWiki / conwaylife.com (requires network layer, user prefs, caching).
- Pattern upload/submission.
- Hashlife-style large pattern support.

## Risks
- RLE files vary in header conventions — metadata extraction must be defensive (default to "unknown").
- Large libraries (>500 patterns) may need an index file (`patterns.json`) pre-generated at build time instead of scanning on startup.

## Estimate
~2–3 days for a single contributor: 0.5 day data + metadata parsing, 1 day library + search logic, 1 day UI, 0.5 day polish.
