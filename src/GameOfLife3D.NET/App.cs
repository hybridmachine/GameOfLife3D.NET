using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Editing;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.IO;
using GameOfLife3D.NET.Recording;
using GameOfLife3D.NET.Rendering;
using GameOfLife3D.NET.UI;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GameOfLife3D.NET;

public sealed class App : IDisposable
{
    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;
    private ImGuiController? _imGuiController;

    private GameEngine? _engine;
    private PatternLoader? _patternLoader;
    private PatternLibrary? _patternLibrary;
    private Renderer3D? _renderer;
    private CameraController? _camera;
    private ImGuiUI? _ui;
    private EditingController? _editController;
    private GridRayCaster? _rayCaster;

    private float _dpiScale = 1.0f;
    private double _startTime;
    private bool _spaceWasDown;
    private bool _f12WasDown;

    // Key edge detection for edit mode
    private bool _eWasDown;
    private bool _leftBracketWasDown;
    private bool _rightBracketWasDown;
    private bool _rWasDown;
    private bool _escWasDown;
    private bool _zeroWasDown;
    private bool _fWasDown;

    // Cinematic mode
    private CinematicController? _cinematic;
    private bool _pWasDown;
    private bool _escCinematicWasDown;

    // Video recording
    private RecordingController? _recording;
    private bool _ctrlRWasDown;
    private string? _pendingTempPath;
    private VideoCodec _pendingCodec;
    private double _recordingStatusUntil;

    public void Run()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1600, 900);
        options.Title = "Game of Life 3D";
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
        options.VSync = true;

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += OnClosing;

        _window.Run();
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window!);
        _input = _window!.CreateInput();

        // Detect DPI scale from framebuffer vs logical window size
        _dpiScale = (float)_window!.FramebufferSize.X / _window.Size.X;
        if (_dpiScale <= 1.0f)
            _dpiScale = DpiHelper.GetSystemDpiScale();
        _dpiScale = Math.Max(1.0f, _dpiScale);

        _imGuiController = new ImGuiController(_gl, _window, _input, onConfigureIO: () =>
        {
            var io = ImGui.GetIO();
            io.Fonts.Clear();
            float fontSize = 14.0f * _dpiScale;
            unsafe
            {
                // Load the primary system font with basic Unicode coverage
                string? fontPath = FindSystemFont();
                if (fontPath != null)
                {
                    var config = ImGuiNative.ImFontConfig_ImFontConfig();
                    config->OversampleH = 2;
                    config->OversampleV = 2;

                    var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
                    builder.AddRanges(io.Fonts.GetGlyphRangesDefault());
                    builder.AddChar((char)0x00B0); // °
                    builder.AddRange(0x2010, 0x2030); // General punctuation (–, —, etc.)
                    builder.BuildRanges(out var ranges);

                    io.Fonts.AddFontFromFileTTF(fontPath, fontSize, config, ranges.Data);
                    builder.Destroy();
                    ImGuiNative.ImFontConfig_destroy(config);
                }
                else
                {
                    var config = ImGuiNative.ImFontConfig_ImFontConfig();
                    config->SizePixels = fontSize;
                    config->OversampleH = 2;
                    config->OversampleV = 2;
                    io.Fonts.AddFontDefault(config);
                    ImGuiNative.ImFontConfig_destroy(config);
                }

                // Merge Font Awesome icons into the font atlas
                MergeIconFont(io, fontSize);
            }
        });

        // Scale font rendering back to logical pixel size (atlas is high-res for crispness)
        ImGui.GetIO().FontGlobalScale = 1.0f / _dpiScale;

        // Apply the centralized UI theme (colors and base geometry, no DPI scaling needed)
        Theme.Apply();

        // Initialize engine
        _engine = new GameEngine(50);
        _patternLoader = new PatternLoader();
        _patternLibrary = new PatternLibrary();

        // Load default pattern and compute some generations
        var pattern = _patternLoader.GetBuiltInPattern("r-pentomino");
        if (pattern != null)
        {
            _engine.InitializeFromPattern(pattern);
            _engine.ComputeGenerations(50);
        }

        // Initialize renderer
        _renderer = new Renderer3D(_gl);
        _renderer.Initialize();
        _renderer.SetGridSize(_engine.GridSize);

        // Initialize post-process pipeline
        var fbSize = _window.FramebufferSize;
        _renderer.InitializePostProcess(fbSize.X, fbSize.Y);

        // Initialize camera
        _camera = new CameraController();
        _camera.Initialize(_input);
        _camera.AspectRatio = (float)_window!.Size.X / _window.Size.Y;

        // Initialize editing
        _rayCaster = new GridRayCaster();
        _editController = new EditingController(_engine, _renderer, _rayCaster, _camera);

        // Initialize UI
        _ui = new ImGuiUI(_engine, _renderer, _camera, _patternLoader, _patternLibrary, _editController);
        _ui.SyncDisplayRange();
        _ui.OnScreenshotRequested = TakeScreenshot;
        _ui.OnExportSTL = path => ExportModel(path, "stl");
        _ui.OnExportOBJ = path => ExportModel(path, "obj");
        _ui.OnExportRLE = ExportRLE;

        // Initialize cinematic controller
        _cinematic = new CinematicController(_engine, _camera, _ui, _renderer);

        // Initialize video recording
        _recording = new RecordingController();

        // Wire mouse clicks for editing
        foreach (var mouse in _input.Mice)
        {
            mouse.MouseDown += OnMouseDownForEditing;
            mouse.MouseMove += OnMouseMoveForEditing;
        }

        // OpenGL setup
        _gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);

        _startTime = _window.Time;

        // Start cinematic mode on launch
        _ui.IsCinematicModeActive = true;
        _ui.StartCinematicHint(_startTime);
        _cinematic!.Start(_startTime);
    }

    private void OnMouseDownForEditing(IMouse mouse, MouseButton button)
    {
        if (_editController == null || _camera == null || !_editController.IsActive) return;
        if (_camera.IsFlythroughActive) return;

        var io = ImGui.GetIO();
        if (io.WantCaptureMouse) return;

        if (button == MouseButton.Left)
        {
            var pos = mouse.Position;
            _editController.HandleClick(
                pos.X, pos.Y,
                _camera.ViewMatrix, _camera.ProjectionMatrix,
                _window!.Size.X, _window.Size.Y,
                _engine!.GridSize);
        }
    }

    private void OnMouseMoveForEditing(IMouse mouse, Vector2 position)
    {
        if (_editController == null || _camera == null || !_editController.IsActive) return;
        if (_camera.IsFlythroughActive) return;

        var io = ImGui.GetIO();
        if (io.WantCaptureMouse) return;

        _editController.HandleMouseMove(
            position.X, position.Y,
            _camera.ViewMatrix, _camera.ProjectionMatrix,
            _window!.Size.X, _window.Size.Y,
            _engine!.GridSize);
    }

    private void OnRender(double deltaTime)
    {
        if (_gl == null || _engine == null || _renderer == null || _camera == null || _ui == null || _imGuiController == null)
            return;

        bool recording = _recording?.IsActive == true;
        // During recording, drive the world with a deterministic 1/fps clock so output is frame-accurate.
        // Otherwise camera/playback/cinematic all behave normally — the recording captures whatever the user does.
        double frameDelta = recording ? _recording!.Clock.FrameDelta : deltaTime;
        double currentTime = recording ? _recording!.CurrentRecordingTime : _window!.Time;

        // Update ImGui
        _imGuiController.Update((float)frameDelta);

        var io = ImGui.GetIO();
        _camera.SetImGuiCapture(io.WantCaptureMouse, io.WantCaptureKeyboard);

        // Handle cinematic mode shortcuts (always active, even during WantCaptureKeyboard)
        HandleCinematicShortcuts(currentTime);

        // Handle keyboard shortcuts (blocked during cinematic mode and ImGui keyboard capture)
        if (!io.WantCaptureKeyboard && !(_cinematic?.IsActive ?? false))
        {
            HandleKeyboardShortcuts();
        }
        else
        {
            _spaceWasDown = false;
            _f12WasDown = false;
            _eWasDown = false;
            _leftBracketWasDown = false;
            _rightBracketWasDown = false;
            _rWasDown = false;
            _escWasDown = false;
            _zeroWasDown = false;
            _fWasDown = false;
            _ctrlRWasDown = false;
        }

        // Update systems — same path whether recording or not.
        _camera.Update((float)frameDelta);
        _ui.Tick(currentTime);
        _cinematic?.Update(currentTime);
        _ui.StatusBar.UpdateFPS(currentTime);

        // Update renderer with current generations
        _renderer.UpdateGenerations(_engine.Generations, _ui.DisplayStart, _ui.DisplayEnd);

        // Render
        var view = _camera.ViewMatrix;
        var proj = _camera.ProjectionMatrix;
        var fbSize = _window!.FramebufferSize;
        var logicalSize = _window.Size;
        _renderer.Render(view, proj, fbSize.X, fbSize.Y, currentTime, logicalSize.X, logicalSize.Y);

        // CAPTURE — must happen after EndSceneAndComposite (post-bloom) and before any ImGui draw.
        if (recording)
        {
            try
            {
                var pixels = _renderer.PostProcess!.ReadFinalPixels();
                _recording!.WriteFrame(pixels);
                _recording.AdvanceFrame();

                _ui.IsRecording = _recording.IsActive;
                _ui.RecordingProgress01 = _recording.TotalFrames > 0
                    ? Math.Clamp(_recording.CurrentFrame / (double)_recording.TotalFrames, 0.0, 1.0)
                    : 0.0;

                // WriteFrame catches encoder errors internally and deactivates. Surface that here.
                if (!_recording.IsActive && !_recording.IsComplete)
                {
                    string err = _recording.LastError ?? "encoder failed";
                    Console.Error.WriteLine($"Recording aborted: {err}");
                    _ui.IsRecording = false;
                    _ui.RecordingProgress01 = 0.0;
                    _pendingTempPath = null; // encoder.Cancel already deleted the file
                    SetRecordingStatus($"Recording failed: {err}", currentTime, 8.0);
                }
                else if (_recording.IsComplete)
                {
                    _recording.Finish();
                    _ui.IsRecording = false;
                    _ui.RecordingProgress01 = 0.0;
                    PromptSaveAndMove();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Recording frame failed: {ex.Message}");
                _recording?.Cancel();
                _ui.IsRecording = false;
                _ui.RecordingProgress01 = 0.0;
                CleanupTempFile();
                SetRecordingStatus($"Recording failed: {ex.Message}", currentTime, 8.0);
            }
        }

        // Expire status messages after their TTL.
        if (_ui.RecordingStatusMessage != null && currentTime > _recordingStatusUntil)
            _ui.RecordingStatusMessage = null;

        // Render ImGui UI. Capture happens before ImGui draws, so the HUD is never in the recording.
        _ui.Render(logicalSize.X, logicalSize.Y);
        _imGuiController.Render();
    }

    private void HandleKeyboardShortcuts()
    {
        bool spaceDown = false;
        bool f12Down = false;
        bool eDown = false;
        bool leftBracketDown = false;
        bool rightBracketDown = false;
        bool rDown = false;
        bool escDown = false;
        bool zeroDown = false;
        bool ctrlDown = false;

        foreach (var keyboard in _input!.Keyboards)
        {
            if (keyboard.IsKeyPressed(Key.Space)) spaceDown = true;
            if (keyboard.IsKeyPressed(Key.F12)) f12Down = true;
            if (keyboard.IsKeyPressed(Key.E)) eDown = true;
            if (keyboard.IsKeyPressed(Key.LeftBracket)) leftBracketDown = true;
            if (keyboard.IsKeyPressed(Key.RightBracket)) rightBracketDown = true;
            if (keyboard.IsKeyPressed(Key.R)) rDown = true;
            if (keyboard.IsKeyPressed(Key.Escape)) escDown = true;
            if (keyboard.IsKeyPressed(Key.Number0) || keyboard.IsKeyPressed(Key.Keypad0)) zeroDown = true;
            if (keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight) ||
                keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight))
                ctrlDown = true;
        }

        // Ctrl+R: start recording (one-press capture). Suppresses the edit-mode 'R' rotation while held.
        bool ctrlR = ctrlDown && rDown;
        if (ctrlR && !_ctrlRWasDown)
            StartRecording();
        _ctrlRWasDown = ctrlR;

        // Space: play/pause
        if (spaceDown && !_spaceWasDown)
            _ui!.TogglePlayPause();
        _spaceWasDown = spaceDown;

        // F12: screenshot
        if (f12Down && !_f12WasDown)
            TakeScreenshot();
        _f12WasDown = f12Down;

        // E: toggle edit mode
        if (eDown && !_eWasDown && _editController != null)
        {
            if (_editController.IsActive)
                _editController.Deactivate();
            else
                _editController.TryActivate(_ui!.IsPlaying, _ui!.DisplayStart, _engine!.GridSize);
        }
        _eWasDown = eDown;

        // [/]: brush size
        if (leftBracketDown && !_leftBracketWasDown && _editController is { IsActive: true })
            _editController.BrushSize = Math.Max(1, _editController.BrushSize - 1);
        _leftBracketWasDown = leftBracketDown;

        if (rightBracketDown && !_rightBracketWasDown && _editController is { IsActive: true })
            _editController.BrushSize = Math.Min(10, _editController.BrushSize + 1);
        _rightBracketWasDown = rightBracketDown;

        // R (without Ctrl): rotate pattern in edit mode
        if (rDown && !_rWasDown && !ctrlDown && _editController is { IsActive: true })
            _editController.RotatePattern();
        _rWasDown = rDown;

        // Escape: exit edit mode
        if (escDown && !_escWasDown && _editController is { IsActive: true })
            _editController.Deactivate();
        _escWasDown = escDown;

        // 0: restart auto orbit camera
        if (zeroDown && !_zeroWasDown)
            _camera?.StartAutoOrbit();
        _zeroWasDown = zeroDown;

        // F: toggle flythrough
        bool fDown = false;
        foreach (var keyboard in _input!.Keyboards)
        {
            if (keyboard.IsKeyPressed(Key.F)) fDown = true;
        }

        if (fDown && !_fWasDown && _camera != null)
        {
            if (_camera.IsFlythroughActive)
            {
                _camera.StopFlythrough();
            }
            else
            {
                // Deactivate edit mode if active
                if (_editController is { IsActive: true })
                    _editController.Deactivate();

                // Pause playback
                _ui!.Pause();

                // Generate and start flythrough with continuous looping
                var path = FlythroughPathGenerator.Generate(
                    _engine!.Generations,
                    _ui.DisplayStart, _ui.DisplayEnd,
                    _engine.GridSize,
                    _camera.Position,
                    _camera.Target);

                if (path != null)
                {
                    _camera.StartFlythrough(path, (pos, lookAt) =>
                        FlythroughPathGenerator.Generate(
                            _engine.Generations,
                            _ui.DisplayStart, _ui.DisplayEnd,
                            _engine.GridSize, pos, lookAt));
                }
            }
        }
        _fWasDown = fDown;
    }

    private void HandleCinematicShortcuts(double currentTime)
    {
        if (_cinematic == null || _input == null) return;

        bool pDown = false;
        bool escDown = false;
        foreach (var keyboard in _input.Keyboards)
        {
            if (keyboard.IsKeyPressed(Key.P)) pDown = true;
            if (keyboard.IsKeyPressed(Key.Escape)) escDown = true;
        }

        // P toggles cinematic mode on/off (always active, even during ImGui keyboard capture)
        if (pDown && !_pWasDown)
        {
            if (_cinematic.IsActive)
            {
                _cinematic.Stop();
                _ui!.IsCinematicModeActive = false;
            }
            else
            {
                // Deactivate edit mode if active
                if (_editController is { IsActive: true })
                    _editController.Deactivate();

                _ui!.IsCinematicModeActive = true;
                _ui.StartCinematicHint(currentTime);
                _cinematic.Start(currentTime);
            }
        }

        // Escape exits cinematic mode (if active)
        if (escDown && !_escCinematicWasDown && _cinematic.IsActive)
        {
            _cinematic.Stop();
            _ui!.IsCinematicModeActive = false;
        }

        _pWasDown = pDown;
        _escCinematicWasDown = escDown;
    }

    private void ExportModel(string path, string format)
    {
        if (_engine == null || _ui == null) return;

        try
        {
            int cellCount = _renderer!.GetVisibleCellCount();
            long estimatedSize = ModelExporter.EstimateSTLSize(cellCount);

            if (estimatedSize > 100_000_000)
                Console.WriteLine($"Warning: Export will be ~{estimatedSize / 1_000_000}MB");

            if (format == "stl")
            {
                ModelExporter.ExportBinarySTL(path, _engine.Generations,
                    _ui.DisplayStart, _ui.DisplayEnd, _engine.GridSize, _renderer.Settings.CellPadding);
            }
            else
            {
                ModelExporter.ExportOBJ(path, _engine.Generations,
                    _ui.DisplayStart, _ui.DisplayEnd, _engine.GridSize, _renderer.Settings.CellPadding);
            }

            Console.WriteLine($"Exported to: {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Export failed: {ex.Message}");
        }
    }

    private void ExportRLE(string path)
    {
        if (_engine == null) return;

        try
        {
            var gen0 = _engine.GetGeneration(0);
            if (gen0 == null)
            {
                Console.Error.WriteLine("Export failed: no generation 0");
                return;
            }

            string rle = PatternLoader.ExportRLE(gen0.Cells, _engine.RuleString);
            File.WriteAllText(path, rle);
            Console.WriteLine($"Exported RLE to: {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"RLE export failed: {ex.Message}");
        }
    }

    private void StartRecording()
    {
        if (_recording == null || _ui == null || _renderer?.PostProcess == null) return;
        if (_recording.IsActive) return;

        if (FfmpegEncoder.LocateBinary() == null)
        {
            SetRecordingStatus(FfmpegEncoder.InstallInstructions(), _window!.Time, 12.0);
            Console.Error.WriteLine(FfmpegEncoder.InstallInstructions());
            return;
        }

        VideoCodec codec = _ui.RecordingCodec;
        string ext = FfmpegEncoder.CodecExtension(codec);
        string tempPath = Path.Combine(
            Path.GetTempPath(),
            $"GameOfLife3D-recording-{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}");

        var settings = new RecordingSettings
        {
            Codec = codec,
            Fps = 60,
            Width = _renderer.PostProcess.Width,
            Height = _renderer.PostProcess.Height,
            OutputPath = tempPath,
            DurationSeconds = Math.Max(1, _ui.RecordingDurationSeconds),
        };

        try
        {
            _recording.Begin(settings);
            _pendingTempPath = tempPath;
            _pendingCodec = codec;
            _ui.IsRecording = true;
            _ui.RecordingProgress01 = 0.0;
            SetRecordingStatus($"Recording {settings.DurationSeconds:F0} s…", _window!.Time, settings.DurationSeconds + 2.0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start recording: {ex.Message}");
            SetRecordingStatus($"Failed to start recording: {ex.Message}", _window!.Time, 8.0);
            try { File.Delete(tempPath); } catch { }
        }
    }

    private void PromptSaveAndMove()
    {
        if (_pendingTempPath == null) return;
        string tempPath = _pendingTempPath;
        _pendingTempPath = null;

        string ext = FfmpegEncoder.CodecExtension(_pendingCodec).TrimStart('.');
        string? dest = null;
        try
        {
            dest = FileDialogHelper.SaveFile(ext);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Save dialog failed: {ex.Message}");
        }

        if (dest == null)
        {
            try { File.Delete(tempPath); } catch { }
            SetRecordingStatus("Recording discarded.", _window!.Time, 4.0);
            return;
        }

        // Ensure correct extension on the destination.
        string expectedExt = "." + ext;
        if (!dest.EndsWith(expectedExt, StringComparison.OrdinalIgnoreCase))
            dest += expectedExt;

        try
        {
            File.Move(tempPath, dest, overwrite: true);
            Console.WriteLine($"Recording saved: {dest}");
            SetRecordingStatus($"Saved: {Path.GetFileName(dest)}", _window!.Time, 6.0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save recording: {ex.Message}");
            SetRecordingStatus($"Save failed: {ex.Message}", _window!.Time, 8.0);
            try { File.Delete(tempPath); } catch { }
        }
    }

    private void CleanupTempFile()
    {
        if (_pendingTempPath == null) return;
        try { File.Delete(_pendingTempPath); } catch { }
        _pendingTempPath = null;
    }

    private void SetRecordingStatus(string message, double now, double ttlSeconds)
    {
        if (_ui == null) return;
        _ui.RecordingStatusMessage = message;
        _recordingStatusUntil = now + ttlSeconds;
    }

    public void TakeScreenshot()
    {
        if (_renderer?.PostProcess == null) return;

        try
        {
            var pixels = _renderer.PostProcess.ReadFinalPixels();
            string path = ScreenshotCapture.SaveToDesktop(pixels, _renderer.PostProcess.Width, _renderer.PostProcess.Height);
            Console.WriteLine($"Screenshot saved: {path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Screenshot failed: {ex.Message}");
        }
    }

    private void OnResize(Vector2D<int> size)
    {
        // Resizing the framebuffer mid-recording would change the encoder's expected dimensions.
        // Cancel cleanly rather than corrupt the stream.
        if (_recording?.IsActive == true)
        {
            Console.Error.WriteLine("Window resized during recording — cancelling.");
            _recording.Cancel();
            if (_ui != null) { _ui.IsRecording = false; _ui.RecordingProgress01 = 0.0; }
            _pendingTempPath = null;
            SetRecordingStatus("Recording cancelled (window resized).", _window?.Time ?? 0.0, 6.0);
        }

        _gl?.Viewport(size);
        if (_camera != null && size.X > 0 && size.Y > 0)
            _camera.AspectRatio = (float)size.X / size.Y;
        if (_renderer != null && size.X > 0 && size.Y > 0)
            _renderer.ResizePostProcess(size.X, size.Y);
    }

    private void OnClosing()
    {
        if (_recording?.IsActive == true) _recording.Cancel();
        CleanupTempFile();

        _renderer?.Dispose();
        _imGuiController?.Dispose();
        _input?.Dispose();
        _gl?.Dispose();
    }

    private static unsafe void MergeIconFont(ImGuiIOPtr io, float fontSize)
    {
        byte[] fontData = LoadEmbeddedFont("fa-solid-900.ttf");
        // Allocate unmanaged memory — ImGui atlas takes ownership
        IntPtr fontPtr = Marshal.AllocHGlobal(fontData.Length);
        Marshal.Copy(fontData, 0, fontPtr, fontData.Length);

        var config = ImGuiNative.ImFontConfig_ImFontConfig();
        config->MergeMode = 1;
        config->OversampleH = 2;
        config->OversampleV = 2;
        config->GlyphMinAdvanceX = fontSize; // uniform icon width

        // Font Awesome uses the Unicode Private Use Area
        var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        builder.AddRange(0xF000, 0xF900);
        builder.BuildRanges(out var ranges);

        io.Fonts.AddFontFromMemoryTTF(fontPtr, fontData.Length, fontSize, config, ranges.Data);
        builder.Destroy();
        ImGuiNative.ImFontConfig_destroy(config);
    }

    private static byte[] LoadEmbeddedFont(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string fullName = $"GameOfLife3D.NET.Fonts.{name.Replace('/', '.').Replace('\\', '.')}";
        using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new FileNotFoundException($"Embedded font not found: {fullName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string? FindSystemFont()
    {
        // Prefer fonts with good Unicode symbol coverage
        string[] candidates =
        [
            // macOS
            "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
            "/System/Library/Fonts/SFNS.ttf",
            "/Library/Fonts/Arial Unicode.ttf",
            // Linux
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",         // Arch
            "/usr/share/fonts/dejavu/DejaVuSans.ttf",       // Fedora
            // Windows
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\arial.ttf",
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    public void Dispose()
    {
        _window?.Dispose();
    }
}

static class ImFontGlyphRangesBuilderExtensions
{
    public static void AddRange(this ImFontGlyphRangesBuilderPtr builder, int start, int end)
    {
        for (int c = start; c <= end; c++)
            builder.AddChar((char)c);
    }
}
