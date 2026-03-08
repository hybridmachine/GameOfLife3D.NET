using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Editing;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.IO;
using GameOfLife3D.NET.Rendering;
using GameOfLife3D.NET.UI;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Numerics;

namespace GameOfLife3D.NET;

public sealed class App : IDisposable
{
    private IWindow? _window;
    private GL? _gl;
    private IInputContext? _input;
    private ImGuiController? _imGuiController;

    private GameEngine? _engine;
    private PatternLoader? _patternLoader;
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
                // Try to load a system font with good Unicode symbol coverage
                string? fontPath = FindSystemFont();
                if (fontPath != null)
                {
                    var config = ImGuiNative.ImFontConfig_ImFontConfig();
                    config->OversampleH = 2;
                    config->OversampleV = 2;

                    // Build glyph ranges: default + geometric shapes + misc symbols + arrows
                    var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
                    builder.AddRanges(io.Fonts.GetGlyphRangesDefault());
                    builder.AddChar((char)0x00B0); // °
                    builder.AddRange(0x2010, 0x2030); // General punctuation (–, —, etc.)
                    builder.AddRange(0x2190, 0x21FF); // Arrows
                    builder.AddRange(0x2500, 0x257F); // Box drawing
                    builder.AddRange(0x2580, 0x259F); // Block elements
                    builder.AddRange(0x25A0, 0x25FF); // Geometric shapes (▶◀◉▦ etc.)
                    builder.AddRange(0x2600, 0x26FF); // Misc symbols (⚙ etc.)
                    builder.AddRange(0x2700, 0x27BF); // Dingbats (❚ etc.)
                    builder.AddRange(0x27C0, 0x27FF); // Supplemental arrows (⟳ etc.)
                    builder.BuildRanges(out var ranges);

                    io.Fonts.AddFontFromFileTTF(fontPath, fontSize, config, ranges.Data);
                    builder.Destroy();
                    ImGuiNative.ImFontConfig_destroy(config);
                }
                else
                {
                    // Fallback to ImGui default font
                    var config = ImGuiNative.ImFontConfig_ImFontConfig();
                    config->SizePixels = fontSize;
                    config->OversampleH = 2;
                    config->OversampleV = 2;
                    io.Fonts.AddFontDefault(config);
                    ImGuiNative.ImFontConfig_destroy(config);
                }
            }
        });

        // Scale font rendering back to logical pixel size (atlas is high-res for crispness)
        ImGui.GetIO().FontGlobalScale = 1.0f / _dpiScale;

        // Apply the centralized UI theme (colors and base geometry, no DPI scaling needed)
        Theme.Apply();

        // Initialize engine
        _engine = new GameEngine(50);
        _patternLoader = new PatternLoader();

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
        _editController = new EditingController(_engine, _renderer, _rayCaster);

        // Initialize UI
        _ui = new ImGuiUI(_engine, _renderer, _camera, _patternLoader, _editController);
        _ui.SyncDisplayRange();
        _ui.OnScreenshotRequested = TakeScreenshot;
        _ui.OnExportSTL = path => ExportModel(path, "stl");
        _ui.OnExportOBJ = path => ExportModel(path, "obj");

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
    }

    private void OnMouseDownForEditing(IMouse mouse, MouseButton button)
    {
        if (_editController == null || _camera == null || !_editController.IsActive) return;

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

        double currentTime = _window!.Time;

        // Update ImGui
        _imGuiController.Update((float)deltaTime);

        // Check ImGui capture state
        var io = ImGui.GetIO();
        _camera.SetImGuiCapture(io.WantCaptureMouse, io.WantCaptureKeyboard);

        // Handle keyboard shortcuts
        if (!io.WantCaptureKeyboard)
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
        }

        // Update systems
        _camera.Update((float)deltaTime);
        _ui.Tick(currentTime);
        _ui.StatusBar.UpdateFPS(currentTime);

        // Update renderer with current generations
        _renderer.UpdateGenerations(_engine.Generations, _ui.DisplayStart, _ui.DisplayEnd);

        // Render
        var view = _camera.ViewMatrix;
        var proj = _camera.ProjectionMatrix;
        var fbSize = _window.FramebufferSize;
        var logicalSize = _window.Size;
        _renderer.Render(view, proj, fbSize.X, fbSize.Y, currentTime, logicalSize.X, logicalSize.Y);

        // Render ImGui UI (uses logical pixel coordinates)
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
                _editController.TryActivate(_ui!.IsPlaying, _ui!.DisplayStart);
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

    public void TakeScreenshot()
    {
        if (_renderer?.PostProcess == null) return;

        try
        {
            var pixels = _renderer.PostProcess.ReadPixels();
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
