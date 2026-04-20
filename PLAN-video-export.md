# Plan: Video / Animation Export with Keyframe Editor

## Goal
Let users record the simulation as a video file (MP4 or WebM) with a custom camera path, generation range, and playback speed. Reuses the existing cinematic/flythrough infrastructure so that the camera work users already see in cinematic mode can be captured and shared.

## Current State (reference)
- Frame loop: `App.OnRender()` (src/GameOfLife3D.NET/App.cs, lines 222–276) drives each frame — updates camera, calls `_renderer.UpdateGenerations()` (264), `_renderer.Render()` (271), then ImGui.
- Post-process pipeline already renders to an FBO: `Rendering/PostProcessPipeline.cs` + `BloomEffect.cs`.
- Screenshot capture: `IO/ScreenshotCapture.cs` reads the framebuffer, encodes PNG with a custom zlib encoder (`SaveToDesktop()` line 13, `WritePng()` line 27). Triggered from `App.TakeScreenshot()` (F12 / UI button).
- Camera keyframes already exist:
  - `Camera/FlythroughPath.cs` line 5 — holds `PositionWaypoints`, `LookAtWaypoints`, `TotalDuration`.
  - `Camera/CatmullRomSpline.cs` — cubic interpolation.
  - `Camera/FlythroughPathGenerator.cs` — builds auto-paths.
  - `CameraController.StartFlythrough()` (Camera/CameraController.cs line 89).
- Cinematic mode: `CinematicController.cs` reveals 50 pre-computed generations one per 0.5s.

## Design

### 1. Frame source
Add `IO/VideoFrameCapturer.cs`:
- Wraps the existing FBO read-back used by `ScreenshotCapture`.
- Exposes `byte[] CaptureBgraFrame(out int width, out int height)` — BGRA layout (preferred by most encoders).
- Reuses the PostProcessPipeline's color attachment so bloom/tonemap are baked into frames.

### 2. Deterministic rendering
Normal frame loop is vsync-bound real time. For recording, the app must advance time in fixed increments matching the video frame rate.
- Add `RecordingClock` in `App.cs`: when active, overrides `deltaTime` with `1.0 / TargetFps` and ignores wall-clock.
- Skip ImGui rendering when writing a frame (toggleable so HUD can be shown if user wants).
- Runs the render loop as fast as possible while recording (render → capture → encode → repeat).

### 3. Encoder
Two-option approach, picked at runtime:

**Option A: FFmpeg pipe (preferred)**
- Add `IO/FfmpegEncoder.cs` — spawns `ffmpeg` as a child process via `System.Diagnostics.Process`.
- Command:
  ```
  ffmpeg -y -f rawvideo -pix_fmt bgra -s WxH -r FPS -i -
         -c:v libx264 -pix_fmt yuv420p -crf 18 output.mp4
  ```
- Stream BGRA bytes into `process.StandardInput.BaseStream`.
- Detect ffmpeg with `FfmpegEncoder.TryLocate()` (PATH + common install locations). Show a help dialog with install instructions if missing.

**Option B: PNG frame sequence fallback**
- If ffmpeg is unavailable, write numbered PNGs to a chosen directory using existing `ScreenshotCapture.WritePng()` — user can encode later.
- Simpler to ship, zero dependencies.

Ship Option B first (it's purely code we already have), add Option A behind a feature flag.

### 4. Keyframe editor UI
Add a new ImGui window `UI/RecordingPanel.cs`, opened from the main control panel via a `Record` button:

Sections:
1. **Output** — path picker (NativeFileDialogSharp already referenced), filename, resolution dropdown (current window / 1080p / 1440p / 4K), FPS (24/30/60), encoder (ffmpeg mp4 / webm / PNG sequence).
2. **Timeline** — generation range `[startGen, endGen]`, playback speed (generations per second).
3. **Camera keyframes** — list of `CameraKeyframe { time, CameraState }`. Buttons: `Add at current camera`, `Remove`, `Clear`. Reorder by drag.
4. **Actions** — `Preview` (plays back without encoding), `Start Recording` (progress bar + cancel).

Serialize keyframes into the same `FlythroughPath` structure already used by `CameraController.StartFlythrough()` so preview and recording share a code path.

### 5. Keyframe data model
Create `Camera/CameraKeyframe.cs`:
```csharp
public sealed record CameraKeyframe(double TimeSeconds, CameraState State);
```
Add `FlythroughPath.FromKeyframes(IEnumerable<CameraKeyframe>)` — already near-equivalent to existing waypoint builder, just takes explicit times instead of auto-computing them.

### 6. Recording pipeline
```
OnLoad() → RecordingController created (idle)
User configures + clicks Start
RecordingController.Begin():
    - set RecordingClock active
    - seek to startGen
    - set CameraController to follow the keyframe path
    - open FfmpegEncoder or PngSequence writer
App.OnRender() (recording mode):
    - advance sim by fixed step
    - render to FBO (skip ImGui composite if hidden)
    - VideoFrameCapturer.CaptureBgraFrame → encoder
    - increment frame counter
    - if frame >= totalFrames → RecordingController.Finish()
```

## Implementation steps
1. Extract shared FBO read-back helper from `ScreenshotCapture` into `IO/FramebufferReader.cs`; have both screenshot + recording use it.
2. Add `CameraKeyframe` + `FlythroughPath.FromKeyframes()`.
3. Add `RecordingClock` and plumb deterministic delta-time into `App.OnRender()`.
4. Add `PngSequenceWriter` (reuses existing PNG encoder) — ship this as MVP.
5. Add `RecordingController` orchestrating sim + camera + encoder.
6. Add `UI/RecordingPanel.cs` with keyframe editor + progress UI.
7. Add `FfmpegEncoder` (detection + pipe) as Phase 2.
8. Add progress indicator in status bar (`UI/StatusBar.cs`) during recording.

## Out of scope (future)
- Audio tracks.
- GPU-side H.264 encode (NVENC/VideoToolbox) — significantly reduces CPU load but complex.
- In-app frame editor (crop, color grading).

## Risks
- `glReadPixels` on every frame is expensive at 4K60. Use PBO double-buffering (`GL_PIXEL_PACK_BUFFER`) so the GPU→CPU transfer overlaps the next frame's render.
- Fixed-step sim means users may see jerkier preview if FPS target is lower than monitor refresh — this is acceptable because output quality is what matters.
- FFmpeg licensing: shipping the binary is LGPL/GPL-sensitive; easier to require users to install it themselves and just invoke it.

## Estimate
~4–5 days: 1 day clock + frame capture, 1 day keyframe data + UI editor, 1 day PNG sequence recording end-to-end, 1 day FFmpeg pipe, 0.5 day polish + PBO optimization.
