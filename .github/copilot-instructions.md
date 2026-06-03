# Copilot instructions for GameOfLife3D.NET

## Commands

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet run --project src/GameOfLife3D.NET/
dotnet build --configuration Release
dotnet publish src/GameOfLife3D.NET/ -c Release
```

There is currently no dedicated test project or lint command. Use `dotnet build` as the main automated validation, then manually exercise affected desktop flows with `dotnet run --project src/GameOfLife3D.NET/`. For changes to sessions, screenshots, exports, patterns, or recording, verify the corresponding save/load or file output path.

macOS packaging/signing uses:

```bash
./signing/Publish-And-Sign-macOS.zsh [optional-config-path]
```

The signing config with real credentials is gitignored; use `signing/macOS/macos-signing-config.template.json` as the shape.

## Architecture

GameOfLife3D.NET is a .NET 10 desktop app built on Silk.NET, OpenGL 3.3 Core, and ImGui.NET. It computes 2D cellular automata generations and renders alive cells as instanced 3D shapes where X/Z are grid coordinates and Y is generation/time.

`Program.cs` only creates and runs `App`. `App.cs` is the orchestration layer: it creates the Silk.NET window/input context, configures ImGui and DPI/font state, initializes `GameEngine`, `Renderer3D`, `CameraController`, `EditingController`, `ImGuiUI`, `CinematicController`, and `RecordingController`, then arbitrates the main render/input loop.

The main subsystem boundaries are:

- `Engine/`: Game of Life state, rule presets/custom B/S rules, RLE parsing, embedded pattern library, and generation computation. `GameEngine` caps computed generations at 1000 and edits to generation 0 clear derived generations.
- `Rendering/` and `Shaders/`: OpenGL rendering. `Renderer3D` coordinates scene passes, floor/reflection/post-process/bloom/labels, while `InstancedCellRenderer` owns the shared instance buffer and shape meshes.
- `Camera/`, `CinematicController.cs`, and `Editing/`: orbit/flythrough camera behavior, automatic cinematic paths, and mouse-ray grid editing. Respect ImGui input capture before consuming camera/editing/cinematic input.
- `UI/`: ImGui controls, timeline/status/pattern preview, theme, icons, DPI helpers, and callbacks into `App`.
- `IO/` and `Recording/`: session JSON, user UI/pattern state, screenshots, model/RLE export, native file dialogs, ffmpeg encoding, and fixed-step recording capture.

Video recording depends on an external `ffmpeg` executable detected at startup from `PATH` or common macOS/Linux install locations; it is not bundled.

## Codebase-specific conventions

- C# uses nullable reference types and implicit usings. Match the existing style: file-scoped namespaces, four-space indentation, PascalCase public members, `_camelCase` private fields, and records for immutable value objects.
- Keep OpenGL interop explicit and local to rendering classes. `AllowUnsafeBlocks` is enabled for this project.
- Shaders are embedded resources under `src/GameOfLife3D.NET/Shaders/*.{vert,frag,glsl}`. Load them through `ShaderProgram.FromEmbeddedResources`; shared GLSL helpers are included with `#include "filename"`.
- Bundled assets are embedded by the `.csproj`: the starfield image, `resources/patterns/*.rle` linked as `Patterns/%(Filename).rle`, and `Fonts/fa-solid-900.ttf`. Adding a bundled RLE pattern under `resources/patterns/` makes it available through `PatternLibrary`.
- `PatternLibrary` indexes embedded RLE headers eagerly at startup and decodes full grids lazily. Resource names must contain `.Patterns.` and end in `.rle`.
- The instanced renderer preallocates a 4,000,000-cell VBO and uploads with `BufferSubData` only when dirty. `Renderer3D` tracks display range/generation count to avoid redundant uploads.
- Keep the 500,000-instance performance threshold synchronized: `Renderer3D.ReflectionMaxInstances` skips the reflection pass above it, and `InstancedCellRenderer.BeveledMaxInstances` falls back from beveled cube to plain cube above it.
- `RenderSettings.Shape` selects one mesh for all alive cells. Only beveled cube has the high-instance fallback; other shapes render as selected.
- Session JSON is forward/backward compatible through nullable fields in `RenderSessionData`. When adding persisted render settings, use nullable fields when absence needs to differ from explicit false/zero and preserve legacy load paths until a deliberate migration removes them.
- `RecordingClock` advances capture in fixed steps so video output is independent of real frame rate; do not couple recording progression to live frame timing.
- Root `PLAN-*.md` and `docs/superpowers/plans/` files are design/history notes. Verify against current code before treating them as implemented behavior.

## Packaging notes

- Release builds auto-detect runtime identifiers for `win-x64`, `linux-x64`, or `osx-arm64` and publish self-contained single-file executables by default.
- The macOS signing flow publishes a non-single-file app bundle, signs inner files before the bundle, notarizes, staples, and zips the result.
- Preserve `signing/macOS/GameOfLife3D.entitlements`; the .NET runtime and Silk.NET native libraries require those hardened-runtime allowances.
- macOS release version lives only in `signing/macOS/Info.plist`. Bump `CFBundleVersion` and `CFBundleShortVersionString` together, and keep bundle ID consistency with the signing config.
