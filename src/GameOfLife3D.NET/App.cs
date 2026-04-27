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
    private RecordingPanel? _recordingPanel;

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
        _recordingPanel = new RecordingPanel
        {
            CurrentCameraStateProvider = () => _camera!.GetState(),
            FramebufferSizeProvider = () => (_window!.FramebufferSize.X, _window.FramebufferSize.Y),
            Controller = _recording,
        };
        _recordingPanel.OnStartRecording = (settings, keyframes) =>
        {
            try
            {
                if (_cinematic?.IsActive == true)
                {
                    _cinematic.Stop();
                    _ui!.IsCinematicModeActive = false;
                }
                // Force capture dimensions to match the live composite FBO — the resolution
                // dropdown is informational; actually resizing the render target is a future step.
                int fbW = _renderer!.PostProcess!.Width;
                int fbH = _renderer.PostProcess.Height;
                _recording.Begin(settings with { Width = fbW, Height = fbH }, keyframes, _camera!, _engine!, _ui!);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to start recording: {ex.Message}");
                _recordingPanel.LastErrorMessage = ex.Message;
            }
        };
        _recordingPanel.OnCancelRecording = () => _recording.Cancel();
        _ui.OnOpenRecordingPanel = () => _recordingPanel!.Visible = true;

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
        double frameDelta = recording ? _recording!.Clock.FrameDelta : deltaTime;
        double currentTime = recording ? _recording!.CurrentRecordingTime : _window!.Time;

        // Update ImGui
        _imGuiController.Update((float)frameDelta);

        // Check ImGui capture state. During recording, suppress ImGui input capture so the camera
        // path drives unimpeded — the only ImGui surface allowed is the recording overlay.
        var io = ImGui.GetIO();
        if (recording)
        {
            _camera.SetImGuiCapture(false, false);
        }
        else
        {
            _camera.SetImGuiCapture(io.WantCaptureMouse, io.WantCaptureKeyboard);
        }

        if (!recording)
        {
            // Handle cinematic mode shortcuts (always active, even during WantCaptureKeyboard)
            HandleCinematicShortcuts(currentTime);

            // Handle keyboard shortcuts (blocked during cinematic mode)
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
            }
        }

        // Update systems
        if (recording)
        {
            _recording!.Tick();           // advances generation scrub
            _camera.Update((float)frameDelta);
            // Skip _ui.Tick (no playback) and _cinematic.Update (mutually exclusive with recording).
        }
        else
        {
            _camera.Update((float)frameDelta);
            _ui.Tick(currentTime);
            _cinematic?.Update(currentTime);
        }
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
                if (_recording.IsComplete)
                {
                    _recording.Finish();
                    Console.WriteLine($"Recording complete: {_recording.Settings?.OutputPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Recording frame failed: {ex.Message}");
                _recording?.Cancel();
            }
        }

        // Render ImGui UI (suppress main panel during recording when HUD is hidden)
        bool hideHud = recording && (_recording!.Settings?.HideHud ?? true);
        if (!hideHud)
        {
            _ui.Render(logicalSize.X, logicalSize.Y);
        }
        _recordingPanel?.Render(logicalSize.X, logicalSize.Y);
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
        }

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

        // R: rotate pattern
        if (rDown && !_rWasDown && _editController is { IsActive: true })
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
        }

        _gl?.Viewport(size);
        if (_camera != null && size.X > 0 && size.Y > 0)
            _camera.AspectRatio = (float)size.X / size.Y;
        if (_renderer != null && size.X > 0 && size.Y > 0)
            _renderer.ResizePostProcess(size.X, size.Y);
    }

    private void OnClosing()
    {
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
