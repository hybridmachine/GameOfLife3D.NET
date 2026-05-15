# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GameOfLife3D.NET is a .NET 10 cross-platform desktop port of the GameOfLife3D TypeScript/Three.js web app. It uses Silk.NET + OpenGL 3.3 Core for 3D rendering and ImGui.NET for all UI controls. The app computes 2D Conway's Game of Life across generations and renders each alive cell as a cube in 3D space (X=column, Y=generation/time, Z=row).

## Build and Run Commands

```bash
dotnet build                                     # Debug build
dotnet run --project src/GameOfLife3D.NET/       # Run
dotnet build --configuration Release             # Self-contained single-file Release
dotnet publish src/GameOfLife3D.NET/ -c Release  # Publish single-file exe
```

No test project. `RuntimeIdentifier` auto-detects `win-x64` / `linux-x64` / `osx-arm64`. `AllowUnsafeBlocks` is on for OpenGL interop. `DpiHelper` P/Invokes `user32.dll` on Windows and falls back to 1.0 elsewhere. Video recording requires an `ffmpeg` binary on `PATH` (or common macOS/Linux install paths) — it is detected at startup and is **not** bundled.

## macOS Signing & Notarization

End-to-end script: `./signing/Publish-And-Sign-macOS.zsh [optional-config-path]`. It publishes Release/osx-arm64 (self-contained, **not** single-file), assembles `build/macOS/Game of Life 3D.app`, generates `.icns` from `logo.png` via `sips` + `iconutil`, signs inner-to-outer (`.dylib` → `.dll` → other files → main exe → bundle, all with `--options runtime --timestamp` and the entitlements file), verifies via `codesign --verify --deep --strict` + `spctl --assess`, notarizes with `xcrun notarytool submit --wait`, staples with `xcrun stapler staple`, and rezips the stapled bundle to `build/macOS/GameOfLife3D.NET-macOS-arm64.zip`.

Prerequisites (one-time setup):
- `Developer ID Application` certificate installed in the login keychain.
- Notarytool credentials stored under a keychain profile, e.g. `xcrun notarytool store-credentials "GameOfLife3D-notarize" --apple-id ... --team-id ... --password <app-specific-password>`.
- `signing/macOS/macos-signing-config.json` populated from the tracked `.template.json` (fields: `TeamId`, `CertificateIdentity`, `NotarizationKeychainProfile`, `BundleIdentifier`). The populated config is **gitignored** — never commit it.

Things to preserve:
- The entitlements at `signing/macOS/GameOfLife3D.entitlements` (`allow-jit`, `allow-unsigned-executable-memory`, `disable-library-validation`, `allow-dyld-environment-variables`) are required for the .NET runtime + Silk.NET native libs to load under the hardened runtime; do not strip them.
- The release version lives **only** in `signing/macOS/Info.plist` — both `CFBundleVersion` and `CFBundleShortVersionString` must be bumped together (e.g. `1.0.1` → `1.0.2`). The `.csproj` has no version field.
- `LSMinimumSystemVersion` is `13.0`. Bundle ID is `com.softcentral.gameoflife3d`; keep it consistent across `Info.plist` and the signing config.

## Architecture

### Component Data Flow

```
Program.cs → App.cs (orchestrator, ~870 lines: window/input/loop, mode toggles)
    ├── Engine/
    │   ├── GameEngine.cs       → 2D CA computation, capped at 1000 generations
    │   ├── PatternLoader.cs    → RLE parsing
    │   ├── PatternLibrary.cs   → Bundled + user RLE patterns + metadata
    │   └── RulePresets.cs      → 9 named B/S rules + custom
    ├── Camera/
    │   ├── CameraController.cs       → Spherical orbit (Silk.NET input)
    │   └── FlythroughPath/Generator/CatmullRomSpline → Cinematic camera path
    ├── CinematicController.cs        → Auto flythrough mode
    ├── Editing/
    │   ├── EditingController.cs      → Interactive cell paint/erase on gen 0
    │   └── GridRayCaster.cs          → Mouse-ray → grid cell hit test
    ├── Rendering/Renderer3D.cs       → Frame coordinator
    │   ├── InstancedCubeRenderer     → 4M-instance VBO, BufferSubData uploads
    │   ├── BeveledCubeMesh/CubeMesh  → Two cube LODs (beveled below 500k cells)
    │   ├── GridRenderer              → Base plane grid lines
    │   ├── ReflectiveFloorRenderer   → Off / Grid / Reflective floor; reflection
    │   │                               pass skipped above 500k instances
    │   ├── PostProcessPipeline       → HDR offscreen → tone-map composite
    │   ├── BloomEffect               → Bright-pass + separable blur + composite
    │   ├── TextRenderer              → Gen labels via ImGui foreground draw list
    │   └── ShaderProgram             → Embedded .vert/.frag with `#include` glsl
    ├── Recording/
    │   ├── RecordingController.cs    → Frame pump + clock
    │   ├── RecordingClock.cs         → Fixed-step time for deterministic capture
    │   └── RecordingSettings.cs      → Duration, codec, resolution
    ├── IO/
    │   ├── SessionManager.cs         → Versioned JSON (game + camera + render)
    │   ├── UiSettingsState.cs        → Persisted UI prefs (font size, etc.)
    │   ├── PatternLibraryState.cs    → User pattern library on disk
    │   ├── FfmpegEncoder.cs          → Spawns ffmpeg, pipes raw frames
    │   ├── ScreenshotCapture.cs      → glReadPixels → PNG via StbImageSharp
    │   ├── ModelExporter.cs          → Mesh export
    │   └── FileDialogHelper.cs       → NativeFileDialog wrapper
    └── UI/ImGuiUI.cs (~1600 lines: control panel, dialogs)
        ├── TimelineBar / StatusBar / PatternPreview
        ├── Theme / Icons / UIHelpers / UILayoutMetrics
        └── DpiHelper                 → System DPI for ImGui font + style scale
```

### Key Technical Decisions

- **Math**: `System.Numerics` (`Vector3`, `Matrix4x4`) for SIMD.
- **Shaders**: `Shaders/*.{vert,frag,glsl}` are `EmbeddedResource`s. `ShaderProgram.LoadEmbeddedResource` resolves `#include "filename"` directives so `gradient.glsl`, `water_normal.glsl` can be shared.
- **Embedded assets**: `resources/2k_stars_milky_way.jpg` (skybox), `resources/patterns/*.rle`, and `Fonts/fa-solid-900.ttf` are linked into the assembly via `EmbeddedResource Link=...` in the .csproj.
- **Instance buffer**: pre-allocated 4M-cell VBO; `BufferSubData` per upload; dirty flags on `Renderer3D` (`_lastDisplayStart/End`, `_lastGenerationCount`) avoid redundant GPU uploads.
- **LOD threshold**: above 500k instances, the renderer drops the beveled cube mesh and the reflective floor pass — keep this constant in sync if either side changes (`ReflectionMaxInstances` in `Renderer3D.cs`).
- **Input arbitration**: `ImGui.GetIO().WantCaptureMouse/WantCaptureKeyboard` is checked before camera, edit, and cinematic controllers consume input.
- **Wireframe**: same instance buffer, `glPolygonMode(GL_LINE)` + `glPolygonOffset` to avoid z-fighting.
- **Generation labels**: world-to-screen projection then `ImGui.GetForegroundDrawList().AddText()` — they live in the ImGui overlay, not the GL scene.
- **DPI scaling**: derived from `framebuffer / window` size ratio with `DpiHelper.GetSystemDpiScale()` fallback; applied to ImGui font atlas and style.
- **Session JSON forward-compat**: `RenderSessionData` uses nullable fields (`ShowGridLines`, `FloorMode`) to distinguish "absent in legacy save" from "explicitly false/0" — preserve this pattern when adding new persisted fields.
- **Recording**: `RecordingClock` advances simulation in fixed steps so captured video is independent of real frame rate; frames are streamed to ffmpeg via stdin.

### NuGet Dependencies

- Silk.NET 2.23.0 (OpenGL, windowing, input)
- Silk.NET.OpenGL.Extensions.ImGui 2.23.0
- NativeFileDialogSharp 0.5.0 (OS file dialogs)
- StbImageSharp 2.30.15 (PNG/JPG decode for textures + screenshots)

### Repo Layout Notes

- `PLAN-*.md` at repo root are design docs for in-flight features (pattern library, statistics, video export). They describe intent, not necessarily current implementation — verify against code before relying on them.
- `resources/patterns/*.rle` is the source of truth for bundled patterns; adding a new `.rle` here makes it available to the library at next build.

## Coding Conventions

- C# with nullable enabled, implicit usings.
- PascalCase for public members, `_camelCase` for private fields.
- File-scoped namespaces.
- Records for immutable value types (e.g., `Rule`, `Vector2Int`, `CameraState`).
