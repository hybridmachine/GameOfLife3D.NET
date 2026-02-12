# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GameOfLife3D.NET is a .NET 10 Windows desktop port of the GameOfLife3D TypeScript/Three.js web app. It uses Silk.NET + OpenGL 3.3 Core for 3D rendering and ImGui.NET for all UI controls. The app computes 2D Conway's Game of Life across generations and renders each alive cell as a cube in 3D space (X=column, Y=generation/time, Z=row).

## Build and Run Commands

```bash
dotnet build                                    # Build
dotnet run --project src/GameOfLife3D.NET/      # Run
dotnet build --configuration Release            # Release build (self-contained single-file exe)
```

No test project exists yet. RuntimeIdentifier is auto-detected per OS (`win-x64` on Windows, `linux-x64` on Linux). `AllowUnsafeBlocks` is enabled for OpenGL interop. `DpiHelper` P/Invokes `user32.dll` on Windows and falls back to 1.0 on Linux.

## Architecture

### Component Data Flow

```
Program.cs → App.cs (orchestrator)
    ├── Engine/GameEngine.cs      → Game of Life computation (max 1000 generations)
    ├── Engine/PatternLoader.cs   → RLE parsing + 5 built-in patterns
    ├── Rendering/Renderer3D.cs   → OpenGL instanced cube rendering coordinator
    │   ├── InstancedCubeRenderer → Instanced draw calls + instance buffer
    │   ├── GridRenderer          → Base plane grid lines
    │   └── TextRenderer          → Gen labels via ImGui foreground overlay
    ├── Camera/CameraController   → Spherical orbit camera (Silk.NET input)
    ├── IO/SessionManager.cs      → JSON session save/load (game state + camera + render settings)
    │   └── FileDialogHelper      → NativeFileDialog wrapper
    └── UI/ImGuiUI.cs             → Full ImGui control panel
        ├── TimelineBar           → Generation range slider + transport
        └── StatusBar             → FPS, gen range, cell count overlay
```

### Key Technical Decisions

- **Math**: System.Numerics (Vector3, Matrix4x4) with SIMD support
- **Shaders**: Embedded as EmbeddedResource in .csproj; `ShaderProgram.LoadEmbeddedResource` resolves `#include "filename"` directives for shared code (e.g., `gradient.glsl`)
- **Instance buffer**: Pre-allocated 4M instances, BufferSubData for updates; dirty-flag pattern avoids redundant GPU uploads
- **Input conflict**: ImGui.GetIO().WantCaptureMouse/WantCaptureKeyboard checked before camera input
- **Wireframe**: Same instances with glPolygonMode(GL_LINE) + glPolygonOffset
- **Generation labels**: World-to-screen projection + ImGui.GetForegroundDrawList().AddText()
- **DPI scaling**: Auto-detected from framebuffer/window size ratio, applied to ImGui fonts and style

### NuGet Dependencies

- Silk.NET 2.23.0 (OpenGL, windowing, input)
- Silk.NET.OpenGL.Extensions.ImGui 2.23.0 (ImGui integration)
- NativeFileDialogSharp 0.5.0 (OS file dialogs)

## Coding Conventions

- C# with nullable enabled, implicit usings
- PascalCase for public members, _camelCase for private fields
- File-scoped namespaces
- Records for immutable data types (e.g., `Rule`, `Vector2Int`, `CameraState`)
