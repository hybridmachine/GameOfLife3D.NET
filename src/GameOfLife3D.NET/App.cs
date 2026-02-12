using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;
using GameOfLife3D.NET.UI;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

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

    private float _dpiScale = 1.0f;
    private double _startTime;
    private bool _spaceWasDown;

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

        // Apply the centralized UI theme (handles both sizing and colors)
        Theme.Apply(_dpiScale);

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

        // Initialize camera
        _camera = new CameraController();
        _camera.Initialize(_input);
        _camera.AspectRatio = (float)_window!.Size.X / _window.Size.Y;

        // Initialize UI
        _ui = new ImGuiUI(_engine, _renderer, _camera, _patternLoader, _dpiScale);
        _ui.SyncDisplayRange();

        // OpenGL setup
        _gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);

        _startTime = _window.Time;
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

        // Handle Space key for play/pause with edge detection
        if (!io.WantCaptureKeyboard)
        {
            bool spaceDown = false;
            foreach (var keyboard in _input!.Keyboards)
            {
                if (keyboard.IsKeyPressed(Key.Space))
                    spaceDown = true;
            }

            if (spaceDown && !_spaceWasDown)
            {
                _ui.TogglePlayPause();
            }
            _spaceWasDown = spaceDown;
        }
        else
        {
            _spaceWasDown = false;
        }

        // Update systems
        _camera.Update((float)deltaTime);
        _ui.Tick(currentTime);
        _ui.StatusBar.UpdateFPS(currentTime);

        // Update animation speed from timeline
        float speed = _ui.Timeline.SpeedMultiplier;
        // Speed multiplier: higher = faster, base is 200ms
        // Already handled in Tick via timeline

        // Update renderer with current generations
        _renderer.UpdateGenerations(_engine.Generations, _ui.DisplayStart, _ui.DisplayEnd);

        // Clear and render
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var view = _camera.ViewMatrix;
        var proj = _camera.ProjectionMatrix;
        var fbSize = _window.FramebufferSize;
        var logicalSize = _window.Size;
        _renderer.Render(view, proj, fbSize.X, fbSize.Y, currentTime, logicalSize.X, logicalSize.Y);

        // Render ImGui UI (uses logical pixel coordinates)
        _ui.Render(logicalSize.X, logicalSize.Y);
        _imGuiController.Render();
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
        if (_camera != null && size.X > 0 && size.Y > 0)
            _camera.AspectRatio = (float)size.X / size.Y;
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
