# Cinematic Color Palette Cycling — Design

## Summary

When cinematic mode is active, the rendered gradient palette is randomized at every pattern transition. The palette is chosen from the six built-in `GradientPresets`, never repeats back-to-back, and the user's pre-cinematic palette is restored when cinematic mode stops.

## Goals

- Each new pattern in cinematic mode renders with a different built-in palette.
- No back-to-back palette repeats.
- The user's palette (built-in preset or custom) is preserved across a cinematic session: snapshot on Start, restore on Stop.
- Zero new persisted state (no session JSON schema change).
- No user-facing toggle — always-on during cinematic mode.

## Non-goals

- No new palettes or "cinematic-only" curated subset (uses the existing six presets verbatim).
- No transition animation between palettes (snap is masked by the existing pattern reset + fade-in).
- No mid-cinematic palette editing behavior beyond what falls out of the design (user edits during a cycle are ephemeral; see Edge Cases).
- No tests (project has no test harness; manual verification only).

## Eligible palettes

All six entries in `GradientPresets.Presets`:

1. `Classic`
2. `Sunset`
3. `Ocean`
4. `Aurora`
5. `Mono-glow`
6. `Heat`

## User-visible behavior

| Action | Result |
| --- | --- |
| Start cinematic with a built-in preset selected (e.g., Classic) | First cinematic palette is uniformly chosen from the other five; no back-to-back repeats thereafter. UI gradient combo label tracks the active cinematic palette. |
| Start cinematic with a custom palette | First cinematic palette is uniformly chosen from all six. UI gradient combo label tracks the active cinematic palette. |
| Stop cinematic | The pre-cinematic palette (built-in or custom) is restored; UI gradient combo label re-matches accordingly. |
| Edit the gradient panel during cinematic mode | The edit takes effect immediately, but is overwritten at the next pattern transition (≤ ~25 s) and is *not* preserved across Stop — the pre-cinematic palette is what restores. |

## Architecture

One controller owns the behavior; one UI helper keeps the combo label honest.

### `CinematicController` (modified)

New private state:

```csharp
private List<Vector3>? _savedGradientStops;   // null when cinematic is inactive
private int _lastPaletteIndex = -1;           // -1 = no previous (user had custom or first start)
```

#### `Start(double currentTime)` — additions before existing body

Snapshot the user's stops and seed the "previous" index *before* the existing call to `StartNewCycle`:

```csharp
_savedGradientStops = new List<Vector3>(_renderer.Settings.GradientStops);
_lastPaletteIndex = ResolveCurrentPaletteIndex(_renderer.Settings.GradientStops);
```

`ResolveCurrentPaletteIndex` runs `GradientPresets.Match(stops)`; if it returns a name, find that name's index in `Presets`; otherwise return `-1`. Order matters: snapshot must happen before `StartNewCycle` so the snapshot captures the user's palette, not a freshly-chosen cinematic one.

#### `StartNewCycle(double currentTime)` — applied per cycle

At the top of `StartNewCycle`, before the pattern-loading loop, apply a fresh palette every time the method runs (i.e., every pattern transition):

```csharp
ApplyNextPalette();
```

`ApplyNextPalette()`:

```csharp
int next = PickNextPaletteIndex();
var stops = GradientPresets.Presets[next].Stops;
_renderer.Settings.GradientStops = new List<Vector3>(stops);
_lastPaletteIndex = next;
_ui.SyncGradientPresetLabel();
```

#### `PickNextPaletteIndex()`

Uniform random over `[0, Presets.Length)`, excluding `_lastPaletteIndex` when ≥ 0:

```csharp
private int PickNextPaletteIndex()
{
    int n = GradientPresets.Presets.Length;
    if (n <= 1) return 0;
    if (_lastPaletteIndex < 0) return Random.Shared.Next(n);
    int next = Random.Shared.Next(n - 1);
    if (next >= _lastPaletteIndex) next++;
    return next;
}
```

This is the standard "pick uniformly from {0..n-1} \ {k}" trick: sample from a smaller range and shift past the excluded slot.

#### `Stop()` — additions after existing body

Restore the snapshot if one exists:

```csharp
if (_savedGradientStops is not null)
{
    _renderer.Settings.GradientStops = new List<Vector3>(_savedGradientStops);
    _ui.SyncGradientPresetLabel();
    _savedGradientStops = null;
    _lastPaletteIndex = -1;
}
```

### `ImGuiUI` (modified)

Add one public method that mirrors the existing in-file pattern (already used at lines 198, 1171, 1189, 1590):

```csharp
public void SyncGradientPresetLabel()
{
    _gradientPreset = GradientPresets.Match(_renderer.Settings.GradientStops);
}
```

`ImGuiUI` already holds a reference to the renderer, so the method reads `_renderer.Settings.GradientStops` directly rather than taking a parameter — keeps the call sites in `CinematicController` short. Called whenever `CinematicController` mutates `GradientStops` from outside the UI's normal edit path, so the gradient combo label reflects the currently rendered palette.

### Untouched components

- `GradientPresets` — read-only consumer; no changes.
- `RenderSettings` — no new fields, no schema change.
- `SessionManager` — unchanged. Cinematic palette state is purely in-memory; restore happens before any save can capture it.
- Shaders and `Renderer3D` — unchanged. They already read `RenderSettings.GradientStops` each frame.

## Data flow

### Start

```
User clicks Start Cinematic
  └─ CinematicController.Start
       ├─ _savedGradientStops ← copy of Settings.GradientStops
       ├─ _lastPaletteIndex ← index of matched preset, or -1 if custom
       ├─ _ui.Pause
       └─ StartNewCycle
            ├─ ApplyNextPalette
            │    ├─ next ← PickNextPaletteIndex (excludes _lastPaletteIndex)
            │    ├─ Settings.GradientStops ← copy of Presets[next].Stops
            │    ├─ _lastPaletteIndex ← next
            │    └─ _ui.SyncGradientPresetLabel
            └─ (existing) load pattern, prepare flythrough, reset reveal range
```

### Per pattern transition (during Update)

```
currentTime − _cycleStartTime ≥ CycleDurationSeconds
  └─ StartNewCycle
       ├─ ApplyNextPalette  ← excludes the just-shown palette
       └─ (existing) load next pattern, reset reveal range, restart fade-in
```

The visual transition is masked because the existing code resets `_revealedEnd = 0` and `FadeOpacity = 0` at the same moment — there are no fully-faded-in cubes on screen to "pop" color.

### Stop

```
User clicks Stop Cinematic (or escape, or pattern-load failure path)
  └─ CinematicController.Stop
       ├─ (existing) clear fade, stop flythrough, sync display range
       └─ Settings.GradientStops ← copy of _savedGradientStops
            ├─ _ui.SyncGradientPresetLabel
            └─ clear _savedGradientStops and _lastPaletteIndex
```

## Error handling

- **Single-element preset table.** `PickNextPaletteIndex` short-circuits to `0`. Cannot occur in shipped code (six entries are hard-coded immutables), but the guard is cheap.
- **Stop called when Start never ran.** `_savedGradientStops` is null; restore block is skipped. The existing `if (!_isActive) return;` early-exit also covers this.
- **All curated patterns fail.** Existing code already calls `Stop()` in this branch, which now also restores the palette. No additional handling needed.
- **Process crash during cinematic.** Only in-memory state is mutated; session JSON on disk still holds the user's original palette. Next launch loads cleanly.
- **User edits gradient mid-cinematic.** Edit is ephemeral by design — overwritten at next transition, not preserved on Stop. Documented in user-visible behavior.

## Edge cases & rationale

- **Why snapshot before `StartNewCycle`?** `StartNewCycle` applies a palette immediately. If the snapshot ran after, it would capture a cinematic palette, defeating restore.
- **Why exclude the user's matched preset on first cycle?** The user explicitly asked for a different palette than what's on screen at the moment cinematic starts. When the user has a custom palette, no preset matches, so no exclusion applies — every preset is a "different palette" from a custom one.
- **Why reset `_lastPaletteIndex` to -1 on Stop?** Not strictly required (the field is unused while inactive), but pairs with `_savedGradientStops = null` to leave the controller in a clean idle state.
- **Why `new List<Vector3>(...)` on both snapshot and restore?** `RenderSettings.GradientStops` is a mutable `List<Vector3>`. Sharing a reference between the snapshot and the live list would let one corrupt the other. Copies keep them independent.

## Files changed

| File | Change |
| --- | --- |
| `src/GameOfLife3D.NET/CinematicController.cs` | Add `_savedGradientStops`, `_lastPaletteIndex`; add `ApplyNextPalette`, `PickNextPaletteIndex`, `ResolveCurrentPaletteIndex`; modify `Start`, `Stop`, `StartNewCycle`. |
| `src/GameOfLife3D.NET/UI/ImGuiUI.cs` | Add public parameterless `SyncGradientPresetLabel()` that reads `_renderer.Settings.GradientStops`. |

No new files. No project / dependency changes.

## Manual verification

After implementation:

1. Default state (Classic preset) → Start cinematic → first cycle renders a non-Classic palette. Observe ≥ 5 cycles; no back-to-back duplicates.
2. Pick Sunset in UI → Start cinematic → first cycle is non-Sunset; observe ≥ 5 cycles, no repeats; Stop → Sunset reappears and combo label says "Sunset".
3. Build a custom palette (edit one stop so `GradientPresets.Match` returns null) → Start cinematic → cycles proceed normally; Stop → custom palette restored, combo label reads "Custom".
4. Open the gradient panel while cinematic is active → combo label tracks the cycling palette (changes at each transition).
5. Stop cinematic via the same control used to start it → palette and combo label both return to the pre-cinematic state.
