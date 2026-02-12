# GameOfLife3D.NET

A 3D visualization of Conway's Game of Life built with .NET 10, Silk.NET, and OpenGL 3.3. Each alive cell is rendered as a cube in 3D space, with generations stacking along the Y axis to create a sculptural time history of the simulation.

![GameOfLife3D.NET in action](resources/GameOfLife3D.NET.png)

## Features

- **3D generation stacking** — 2D Game of Life computed across generations, rendered as cubes (X=column, Y=generation, Z=row)
- **Instanced rendering** — OpenGL instanced draw calls handle up to 4 million cubes efficiently
- **Color cycling gradient** — HSL-based color animation across generations with configurable hue angles
- **Wireframe overlay** — togglable edge rendering with independent color cycling
- **ImGui control panel** — pattern selection, rule editing (9 presets + custom B/S rules), grid size, toroidal wrapping
- **Timeline transport** — play/pause, speed control, and generation range scrubbing
- **Session persistence** — save/load full state (pattern, camera, colors, display range) as JSON
- **RLE import** — load patterns from standard .rle files
- **Built-in patterns** — R-pentomino, Glider, Blinker, Pulsar, Gosper Glider Gun

## Controls

| Input | Action |
|-------|--------|
| Left mouse drag | Orbit camera |
| Right mouse drag | Pan camera |
| Scroll wheel | Zoom |
| WASD | Move camera |
| Q / E | Rotate camera |
| R / F | Camera up / down |
| Space | Play / Pause |

## Download

Pre-built self-contained executables (no .NET runtime required):

- [**Windows x64**](https://github.com/hybridmachine/ClaudeAssistedProjects/releases/tag/v1.0.0) — `GameOfLife3D.NET.exe`
- [**Linux x64**](https://github.com/hybridmachine/ClaudeAssistedProjects/releases/tag/v1.0.0) — `GameOfLife3D.NET-linux-x64`

## Build from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build                                    # Debug build
dotnet run --project src/GameOfLife3D.NET/      # Run
dotnet publish src/GameOfLife3D.NET/ -c Release  # Publish self-contained exe
```

The RuntimeIdentifier is auto-detected (`win-x64` on Windows, `linux-x64` on Linux).

## License

See repository root for license information.
