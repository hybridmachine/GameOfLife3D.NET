# Repository Guidelines

## Project Structure & Module Organization

GameOfLife3D.NET is a .NET 10 desktop app using Silk.NET, OpenGL 3.3, and ImGui. The solution entry point is `GameOfLife3D.NET.slnx`; the main project lives in `src/GameOfLife3D.NET/`.

- `Program.cs` and `App.cs` bootstrap the window, input, main loop, and high-level orchestration.
- `Engine/` contains Game of Life rules, pattern loading, generation state, and preset rules.
- `Rendering/`, `Shaders/`, and `Camera/` handle OpenGL rendering, embedded GLSL resources, and camera/flythrough behavior.
- `UI/`, `Editing/`, `IO/`, and `Recording/` contain ImGui controls, interactive editing, persistence/export, and ffmpeg recording support.
- PBR materials support texture maps via shader-side triplanar projection: `IO/MaterialImporter.cs` resolves `.mtlx` image-node connections and `.pbr.json` texture paths, and `Rendering/MaterialTextureCache.cs` loads/caches them; sample materials live in `resources/materials/` (regenerate textures with `./.venv/bin/python resources/materials/generate.py`).
- `resources/` stores bundled images and `.rle` patterns; `signing/` stores packaging and signing scripts.

There is currently no dedicated test project.

## Build, Test, and Development Commands

```bash
dotnet restore
dotnet build
dotnet run --project src/GameOfLife3D.NET/
dotnet build --configuration Release
dotnet publish src/GameOfLife3D.NET/ -c Release
```

`dotnet restore` installs NuGet packages. `dotnet build` compiles a Debug build. `dotnet run --project src/GameOfLife3D.NET/` starts the app locally. Release builds produce a self-contained single-file executable using the auto-detected runtime identifier (`win-x64`, `linux-x64`, or `osx-arm64`). Video recording requires `ffmpeg` on `PATH` or in common macOS/Linux install locations.

## Coding Style & Naming Conventions

Use C# with nullable reference types and implicit usings enabled. Follow the existing style: file-scoped namespaces, four-space indentation, PascalCase for public types and members, `_camelCase` for private fields, and records for immutable value objects. Keep OpenGL interop code explicit and local to rendering classes. Shader files are embedded resources; keep shared GLSL helpers in `Shaders/*.glsl` and load them through the existing include mechanism.

## Testing Guidelines

No automated test suite is present. Before submitting code, run `dotnet build` and manually exercise affected UI or rendering flows with `dotnet run --project src/GameOfLife3D.NET/`. For changes to patterns, sessions, screenshots, exports, or recording, verify the relevant save/load or file output path. Add future tests under a sibling test project, such as `tests/GameOfLife3D.NET.Tests/`, with test names that describe behavior.

## Commit & Pull Request Guidelines

Recent history uses short imperative commit messages, for example `Fix InvalidOperationException when clicking a Recent pattern` and `Refresh CLAUDE.md to cover added subsystems`. Keep commits focused and avoid mixing refactors with feature or bug fixes.

Pull requests should include a concise summary, the commands run, manual verification notes, and screenshots or short recordings for visible rendering/UI changes. Link related issues when applicable and call out dependency, signing, or runtime requirement changes.

