using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Editing;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.IO;
using GameOfLife3D.NET.Rendering;
using ImGuiNET;

namespace GameOfLife3D.NET.UI;

public sealed class ImGuiUI
{
    private readonly GameEngine _engine;
    private readonly Renderer3D _renderer;
    private readonly CameraController _camera;
    private readonly PatternLoader _patternLoader;
    private readonly PatternLibrary _patternLibrary;
    private readonly PatternLibraryState _patternLibState;
    private readonly EditingController? _editController;
    private readonly TimelineBar _timeline;
    private readonly StatusBar _statusBar;

    // Pattern library UI state
    private string _patternSearch = "";
    private int _patternCategoryIdx;
    private int _patternPeriodMin;
    private int _patternPeriodMax = 32;
    private int _patternMaxSize = 200;
    private string? _selectedPatternId;

    // UI state
    private int _selectedGridSizeIdx = 2; // 50
    private int _selectedRuleIdx;
    private string _customBirth = "3";
    private string _customSurvival = "23";
    private float _cellPadding;
    private Vector3 _cellColor;
    private Vector3 _edgeColor;
    private bool _faceColorCycling;
    private bool _edgeColorCycling;
    private float _edgeColorAngle;
    // Active preset name (e.g. "Classic", "Sunset"); null when the user has
    // edited the stops away from any built-in preset. The actual stop list
    // lives on RenderSettings.GradientStops — we don't mirror it here.
    private string? _gradientPreset;
    private int _floorModeIdx; // Mirrors (int)RenderSettings.FloorMode: 0=Off, 1=Grid, 2=Reflective
    private float _waveStrength;
    private float _waveSpeed;
    private Vector3 _waterTint;
    private float _reflectivity;
    private float _reflectionResolutionScale;
    private bool _showGenerationLabels;
    private bool _showWireframe;
    private bool _toroidal = true;
    private float _randomDensity = 30f; // Stored as a percentage (5-80), not a normalized 0-1 density.

    // Fog
    private bool _fogEnabled;
    private float _fogStart = 20f;
    private float _fogEnd = 100f;
    private Vector3 _fogColor = new(0.05f, 0.05f, 0.08f);

    // Clip
    private bool _clipEnabled;
    private float _clipY = 25f;

    // Background
    private int _backgroundMode;
    private Vector3 _bgTopColor = new(0.08f, 0.08f, 0.15f);
    private Vector3 _bgBottomColor = new(0.02f, 0.02f, 0.04f);

    // Bloom
    private bool _bloomEnabled;
    private float _bloomThreshold = 0.6f;
    private float _bloomIntensity = 0.5f;

    // PBR Material
    private bool _showMaterialPopup;
    private MaterialLibraryState _materialLibState;
    // Rename state: index into _materialLibState.Materials being renamed, and its draft name.
    private int _materialRenameIdx = -1;
    private string _materialRenameDraft = "";
    private string? _materialImportError;

    // Cell shape — mirrors RenderSettings.Shape; the int form drives ImGui.Combo.
    private int _shape = (int)CellShape.BeveledCube;
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid", "Icosahedron", "Dodecahedron", "Sphere", "Capsule" };

    // Population stats
    private float[] _populationData = [];
    private int _lastPopulationGenCount;

    // Display state
    private int _displayStart;
    private int _displayEnd;

    // Cinematic mode
    private double _lastTickTime;
    private double _cinematicHintStartTime;
    private double _cinematicPatternLabelStartTime;
    private string? _cinematicPatternLabel;

    // Animation
    private bool _isPlaying;
    private float _animationSpeed = 200f;
    private double _lastAnimationTime;

    // Menu bar state
    private bool _isTimelineVisible = true;
    private bool _showPatternLibraryWindow;
    private bool _showRandomSeedPopup;
    private bool _showRecordingPopup;
    private bool _showGradientPopup;
    private bool _showCellColorPopup;
    private bool _showFogPopup;
    private bool _showBloomPopup;
    private bool _showClipPlanePopup;
    private bool _showBackgroundPopup;
    private bool _showCellPaddingPopup;
    private bool _showReflectiveFloorPopup;
    private bool _showStatisticsWindow;
    private bool _showShortcutsPopup;
    private bool _showAboutPopup;
    private bool _showCustomRulePopup;
    private float _fontSize = UiSettingsState.BaseFontSize;
    private float _automaticFontSize = UiSettingsState.BaseFontSize;
    private bool _isFontSizeAutomatic = true;

    private static readonly string[] GridSizes = ["25", "50", "75", "100", "150", "200"];
    private static readonly int[] GridSizeValues = [25, 50, 75, 100, 150, 200];

    // Width to reserve in the material library list for the rename/remove button pair.
    private const float MaterialLibraryButtonsReservedWidth = 56f;
    private static readonly string[] RuleNames;
    private static readonly string[] RuleKeys;
    private static readonly string AppVersion = LoadAppVersion();

    static ImGuiUI()
    {
        var rules = RulePresets.All;
        var names = new List<string>();
        var keys = new List<string>();
        foreach (var kvp in rules)
        {
            keys.Add(kvp.Key);
            names.Add(kvp.Value.Name);
        }
        names.Add("Custom");
        keys.Add("custom");
        RuleNames = [.. names];
        RuleKeys = [.. keys];
    }

    public TimelineBar Timeline => _timeline;
    public StatusBar StatusBar => _statusBar;
    public int DisplayStart => _displayStart;
    public int DisplayEnd => _displayEnd;
    public bool IsPlaying => _isPlaying;
    public bool IsCinematicModeActive { get; set; }

    // Screenshot callback
    public Action? OnScreenshotRequested { get; set; }

    // Exit callback
    public Action? OnExitRequested { get; set; }

    // Cinematic mode callback
    public Action? OnCinematicToggleRequested { get; set; }

    // Recording callback
    public Action? OnRecordingStartRequested { get; set; }

    // Recording settings (read by App when Ctrl+R is pressed)
    public int RecordingDurationSeconds { get; set; } = 10;
    public VideoCodec RecordingCodec { get; set; } = VideoCodec.H264Mp4;

    // Recording status (written by App each frame)
    public bool IsRecording { get; set; }
    public double RecordingProgress01 { get; set; }
    public string? RecordingStatusMessage { get; set; }

    // Export callbacks
    public Action<string>? OnExportSTL { get; set; }
    public Action<string>? OnExportOBJ { get; set; }
    public Action<string>? OnExportRLE { get; set; }
    public Action<float>? OnFontSizeChanged { get; set; }
    public Action? OnFontSizeReset { get; set; }

    /// <summary>
    /// Invoked when the active PBR material changes (load or clear).
    /// <c>null</c> means "revert to the legacy shader".
    /// </summary>
    public Action<CellMaterial?>? OnMaterialChanged { get; set; }

    public ImGuiUI(GameEngine engine, Renderer3D renderer, CameraController camera, PatternLoader patternLoader, PatternLibrary patternLibrary, EditingController? editController = null)
    {
        _engine = engine;
        _renderer = renderer;
        _camera = camera;
        _patternLoader = patternLoader;
        _patternLibrary = patternLibrary;
        _patternLibState = PatternLibraryState.Load();
        _materialLibState = MaterialLibraryState.Load();
        _editController = editController;
        _timeline = new TimelineBar();
        _statusBar = new StatusBar();

        // Restore last-used category if it still exists in the library
        if (_patternLibState.LastCategory != null)
        {
            int idx = _patternLibrary.Categories.ToList().FindIndex(c =>
                string.Equals(c, _patternLibState.LastCategory, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _patternCategoryIdx = idx + 1; // +1 for "All" at index 0
        }

        // Sync initial state from render settings
        var settings = renderer.Settings;
        _cellPadding = settings.CellPadding * 100f;
        _cellColor = settings.CellColor;
        _edgeColor = settings.EdgeColor;
        _faceColorCycling = settings.FaceColorCycling;
        _edgeColorCycling = settings.EdgeColorCycling;
        _edgeColorAngle = settings.EdgeColorAngle;
        _floorModeIdx = (int)settings.FloorMode;
        _waveStrength = settings.WaveStrength;
        _waveSpeed = settings.WaveSpeed;
        _waterTint = settings.WaterTint;
        _reflectivity = settings.Reflectivity;
        _reflectionResolutionScale = settings.ReflectionResolutionScale;
        _showGenerationLabels = settings.ShowGenerationLabels;
        _showWireframe = settings.ShowWireframe;
        _gradientPreset = GradientPresets.Match(settings.GradientStops);

        _timeline.RangeChanged += OnRangeChanged;
        _timeline.PlayToggled += playing => _isPlaying = playing;
        _timeline.ResetRequested += OnReset;
    }

    public void TogglePlayPause()
    {
        _isPlaying = !_isPlaying;
        _timeline.SetPlaying(_isPlaying);
    }

    public void Pause()
    {
        _isPlaying = false;
        _timeline.SetPlaying(false);
    }

    public void ToggleTimeline()
    {
        _isTimelineVisible = !_isTimelineVisible;
    }

    public void OpenRecordingWindow()
    {
        _showRecordingPopup = true;
    }

    public void Tick(double currentTime)
    {
        // Defensive: if the clock jumps backward (e.g. wall-clock → recording-clock at start of
        // a recording, or back at the end), re-anchor so the next tick interval is correct.
        if (currentTime < _lastAnimationTime || currentTime < _lastTickTime - 1.0)
            _lastAnimationTime = currentTime;
        _lastTickTime = currentTime;
        if (!_isPlaying) return;

        // Base speed 200ms, divided by speed multiplier
        double interval = (_animationSpeed / _timeline.SpeedMultiplier) / 1000.0;
        if (currentTime - _lastAnimationTime > interval)
        {
            bool computed = _engine.ComputeSingleGeneration();
            if (computed)
            {
                _displayEnd = _engine.GenerationCount - 1;
                _timeline.SetTotalGenerations(_engine.GenerationCount);
                _timeline.SetEndGeneration(_displayEnd);
            }
            else
            {
                _isPlaying = false;
                _timeline.SetPlaying(false);
            }
            _lastAnimationTime = currentTime;
        }
    }

    public void SyncDisplayRange()
    {
        int maxGen = Math.Max(0, _engine.GenerationCount - 1);
        _displayStart = 0;
        _displayEnd = maxGen;
        _timeline.SetTotalGenerations(_engine.GenerationCount);
        _timeline.SetRange(0, maxGen);
    }

    public void SyncGradientPresetLabel()
    {
        _gradientPreset = GradientPresets.Match(_renderer.Settings.GradientStops);
    }

    public void SetDisplayEnd(int end)
    {
        int maxGen = Math.Max(0, _engine.GenerationCount - 1);
        _displayEnd = Math.Clamp(end, _displayStart, maxGen);
        _timeline.SetTotalGenerations(_engine.GenerationCount);
        _timeline.SetEndGeneration(_displayEnd);
    }

    public void SetDisplayRange(int start, int end)
    {
        int maxGen = Math.Max(0, _engine.GenerationCount - 1);
        _displayStart = Math.Clamp(start, 0, maxGen);
        _displayEnd = Math.Clamp(end, _displayStart, maxGen);
        _timeline.SetTotalGenerations(_engine.GenerationCount);
        _timeline.SetRange(_displayStart, _displayEnd);
    }

    public void SetFontSizeState(float currentFontSize, float automaticFontSize, bool isAutomatic)
    {
        _fontSize = UiSettingsState.ClampFontSize(currentFontSize);
        _automaticFontSize = UiSettingsState.ClampFontSize(automaticFontSize);
        _isFontSizeAutomatic = isAutomatic;
    }

    public void StartCinematicHint(double currentTime)
    {
        _cinematicHintStartTime = currentTime;
    }

    public void StartCinematicPatternLabel(string patternName, double currentTime)
    {
        _cinematicPatternLabel = patternName;
        _cinematicPatternLabelStartTime = currentTime;
    }

    private void OnRangeChanged(int start, int end)
    {
        if (!IsCinematicModeActive && _camera.IsFlythroughActive)
            _camera.StopFlythrough();

        _displayStart = start;
        _displayEnd = end;
    }

    private void SeekDisplayEnd(int end)
    {
        int maxGen = Math.Max(0, _engine.GenerationCount - 1);
        int clampedEnd = Math.Clamp(end, 0, maxGen);
        int clampedStart = Math.Min(_displayStart, clampedEnd);

        _timeline.SetTotalGenerations(_engine.GenerationCount);
        _timeline.SetRange(clampedStart, clampedEnd);
        OnRangeChanged(clampedStart, clampedEnd);
    }

    private void OnReset()
    {
        _isPlaying = false;
        _timeline.SetPlaying(false);
        _engine.Clear();
        _engine.SetRule("conway");
        _engine.SetToroidal(true);
        _selectedRuleIdx = 0;
        _toroidal = true;

        var pattern = _patternLoader.GetBuiltInPattern("r-pentomino");
        if (pattern != null)
            _engine.InitializeFromPattern(pattern);

        _renderer.InvalidateState();
        SyncDisplayRange();
    }

    public void Render(int windowWidth, int windowHeight)
    {
        // Recording indicator is drawn on the foreground draw list, which renders after the
        // post-bloom composite is captured — so it is visible to the user but never in the file.
        if (IsRecording)
            RenderRecordingIndicator();

        if (IsCinematicModeActive)
        {
            RenderCinematicHint(windowWidth, windowHeight);
            return;
        }

        RenderMenuBar();
        RenderFloatingWindows();

        if (_isTimelineVisible)
            _timeline.Render(windowWidth, windowHeight);
        _statusBar.ShowEditBadge = _editController?.IsActive ?? false;
        _statusBar.Render(_displayStart, _displayEnd, _engine.RuleString,
            _renderer.GetVisibleCellCount(), windowWidth, windowHeight);
    }

    private void RenderRecordingIndicator()
    {
        var drawList = ImGui.GetForegroundDrawList();

        // Position: top-right corner of the window, below the menu bar
        float menuBarHeight = ImGui.GetFrameHeight();
        float margin = 12f;
        float radius = 7f;
        float centerX = ImGui.GetIO().DisplaySize.X - margin - radius;
        float centerY = menuBarHeight + margin + radius;
        var center = new Vector2(centerX, centerY);

        // ~1 Hz blink driven by the active clock (wall-clock or recording clock — either is fine).
        bool on = (int)(_lastTickTime * 2.0) % 2 == 0;
        if (on)
        {
            uint red = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.18f, 0.18f, 1f));
            drawList.AddCircleFilled(center, radius, red);
        }
        // Subtle outline so the dot is visible against bright scenes when blinked off.
        uint outline = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f));
        drawList.AddCircle(center, radius + 0.5f, outline, 16, 1.2f);

        // Countdown: seconds remaining, centered under the dot.
        double remaining = Math.Max(0.0,
            RecordingDurationSeconds * (1.0 - Math.Clamp(RecordingProgress01, 0.0, 1.0)));
        string label = $"{remaining:F0}s";
        var textSize = ImGui.CalcTextSize(label);
        var textPos = new Vector2(center.X - textSize.X * 0.5f, center.Y + radius + 4f);
        uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.9f));
        uint shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.7f));
        drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 1), shadow, label);
        drawList.AddText(textPos, textColor, label);
    }

    private void RenderCinematicHint(int windowWidth, int windowHeight)
    {
        var drawList = ImGui.GetForegroundDrawList();
        double elapsed = _lastTickTime - _cinematicHintStartTime;
        RenderCinematicPatternLabel(drawList, windowWidth);

        // Main "Cinematic Mode" text fades out over 3 seconds (visible for first 1s, then fades)
        if (elapsed < 4.0)
        {
            float alpha = elapsed < 1.0 ? 1.0f : Math.Max(0f, 1.0f - (float)(elapsed - 1.0) / 3.0f);
            uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, alpha));

            string title = "Cinematic Mode";
            var titleSize = ImGui.CalcTextSize(title);
            string subtitle = "Press Escape to Stop";
            var subtitleSize = ImGui.CalcTextSize(subtitle);

            float totalHeight = titleSize.Y + 8f + subtitleSize.Y;
            float topY = (windowHeight - totalHeight) * 0.5f;

            drawList.AddText(
                new Vector2((windowWidth - titleSize.X) * 0.5f, topY),
                color, title);
            drawList.AddText(
                new Vector2((windowWidth - subtitleSize.X) * 0.5f, topY + titleSize.Y + 8f),
                color, subtitle);
        }

        // Subtle persistent exit hint at bottom
        {
            string hint = "Press P or Esc to exit";
            var hintSize = ImGui.CalcTextSize(hint);
            var hintPos = new Vector2(
                (windowWidth - hintSize.X) * 0.5f,
                windowHeight - hintSize.Y - 20f);
            uint hintColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.15f));
            drawList.AddText(hintPos, hintColor, hint);
        }
    }

    private void RenderCinematicPatternLabel(ImDrawListPtr drawList, int windowWidth)
    {
        if (string.IsNullOrWhiteSpace(_cinematicPatternLabel))
            return;

        double elapsed = _lastTickTime - _cinematicPatternLabelStartTime;
        if (elapsed >= 4.0)
            return;

        float alpha = Math.Max(0f, 1.0f - (float)(elapsed / 4.0));
        string label = $"Pattern: {_cinematicPatternLabel}";
        var labelSize = ImGui.CalcTextSize(label);
        var labelPos = new Vector2((windowWidth - labelSize.X) * 0.5f, 72f);
        uint shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, alpha * 0.7f));
        uint color = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, alpha));

        drawList.AddText(new Vector2(labelPos.X + 1f, labelPos.Y + 1f), shadow, label);
        drawList.AddText(labelPos, color, label);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Menu Bar
    // ────────────────────────────────────────────────────────────────────────

    private void RenderMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return;

        RenderFileMenu();
        RenderEditMenu();
        RenderSimulationMenu();
        RenderViewMenu();
        RenderAppearanceMenu();
        RenderPatternsMenu();
        RenderHelpMenu();

        ImGui.EndMainMenuBar();
    }

    private void RenderFloatingWindows()
    {
        if (_showStatisticsWindow) RenderStatisticsWindow();
        if (_showPatternLibraryWindow) RenderPatternLibraryWindow();
        if (_showRandomSeedPopup) RenderRandomSeedPopup();
        if (_showRecordingPopup) RenderRecordingPopup();
        if (_showCustomRulePopup) RenderCustomRulePopup();
        if (_showGradientPopup) RenderGradientPopup();
        if (_showCellColorPopup) RenderCellColorPopup();
        if (_showFogPopup) RenderFogPopup();
        if (_showBloomPopup) RenderBloomPopup();
        if (_showClipPlanePopup) RenderClipPlanePopup();
        if (_showBackgroundPopup) RenderBackgroundPopup();
        if (_showCellPaddingPopup) RenderCellPaddingPopup();
        if (_showReflectiveFloorPopup) RenderReflectiveFloorPopup();
        if (_showMaterialPopup) RenderMaterialPopup();
        if (_showShortcutsPopup) RenderShortcutsPopup();
        if (_showAboutPopup) RenderAboutPopup();
    }

    // ────────────────────────────────────────────────────────────────────────
    // File Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderFileMenu()
    {
        if (!ImGui.BeginMenu("File"))
            return;

        if (ImGui.MenuItem("Load Pattern..."))
        {
            var path = FileDialogHelper.OpenFile("rle");
            if (path != null)
            {
                try
                {
                    string content = File.ReadAllText(path);
                    var pattern = PatternLoader.ParseRLE(content);
                    _engine.InitializeFromPattern(pattern);
                    _renderer.InvalidateState();
                    SyncDisplayRange();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error loading pattern: {ex.Message}");
                }
            }
        }

        if (ImGui.MenuItem("Load Session..."))
        {
            if (_camera.IsFlythroughActive)
                _camera.StopFlythrough();
            var path = FileDialogHelper.OpenFile("json");
            if (path != null)
            {
                try
                {
                    var session = SessionManager.Load(path);
                    if (session?.GameState != null)
                    {
                        _engine.ImportState(session.GameState);
                        _renderer.SetGridSize(session.GameState.GridSize);

                        if (session.Camera != null)
                            _camera.SetState(SessionManager.ToCameraState(session.Camera));

                        if (session.RenderSettings != null)
                        {
                            SessionManager.ApplyRenderSettings(session.RenderSettings, _renderer.Settings);
                            SyncUIFromSettings();
                        }

                        _displayStart = session.DisplayStart;
                        _displayEnd = Math.Min(session.DisplayEnd, _engine.GenerationCount - 1);
                        _timeline.SetTotalGenerations(_engine.GenerationCount);
                        _timeline.SetRange(_displayStart, _displayEnd);
                        _renderer.InvalidateState();
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error loading session: {ex.Message}");
                }
            }
        }

        if (ImGui.MenuItem("Save Session..."))
        {
            if (_camera.IsFlythroughActive)
                _camera.StopFlythrough();
            var path = FileDialogHelper.SaveFile("json");
            if (path != null)
            {
                try
                {
                    SessionManager.Save(path, _engine, _camera, _renderer.Settings,
                        _displayStart, _displayEnd);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error saving session: {ex.Message}");
                }
            }
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Screenshot", "F12"))
            OnScreenshotRequested?.Invoke();

        if (ImGui.MenuItem(IsRecording ? "Recording..." : "Record Video..."))
            _showRecordingPopup = true;

        ImGui.Separator();

        if (ImGui.BeginMenu("Export"))
        {
            if (ImGui.MenuItem("Export STL..."))
            {
                var path = FileDialogHelper.SaveFile("stl");
                if (path != null)
                    OnExportSTL?.Invoke(path);
            }

            if (ImGui.MenuItem("Export OBJ..."))
            {
                var path = FileDialogHelper.SaveFile("obj");
                if (path != null)
                    OnExportOBJ?.Invoke(path);
            }

            if (ImGui.MenuItem("Export RLE..."))
            {
                var path = FileDialogHelper.SaveFile("rle");
                if (path != null)
                    OnExportRLE?.Invoke(path);
            }

            ImGui.EndMenu();
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Exit"))
            OnExitRequested?.Invoke();

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edit Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderEditMenu()
    {
        if (!ImGui.BeginMenu("Edit"))
            return;

        if (_editController == null)
        {
            ImGui.EndMenu();
            return;
        }

        bool isActive = _editController.IsActive;
        bool canEdit = !_isPlaying && _displayStart == 0;

        if (isActive)
        {
            if (ImGui.MenuItem("Exit Edit Mode", "E"))
                _editController.Deactivate();
        }
        else
        {
            if (ImGui.MenuItem("Enter Edit Mode", "E", false, canEdit))
                _editController.TryActivate(_isPlaying, _displayStart, _engine.GridSize);
        }

        ImGui.Separator();

        if (ImGui.BeginMenu("Tool", isActive))
        {
            string[] tools = ["Toggle", "Draw", "Erase"];
            int currentTool = (int)_editController.CurrentTool;
            for (int i = 0; i < tools.Length; i++)
            {
                if (ImGui.MenuItem(tools[i], "", i == currentTool))
                    _editController.CurrentTool = (EditTool)i;
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Brush Size", isActive))
        {
            int brushSize = _editController.BrushSize;
            for (int i = 1; i <= 10; i++)
            {
                if (ImGui.MenuItem($"{i}", "", i == brushSize))
                    _editController.BrushSize = i;
            }
            ImGui.EndMenu();
        }

        if (ImGui.MenuItem("Rotate Pattern", "R", false, isActive))
            _editController.RotatePattern();

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Simulation Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderSimulationMenu()
    {
        if (!ImGui.BeginMenu("Simulation"))
            return;

        int maxGen = Math.Max(0, _engine.GenerationCount - 1);
        bool canStepBack = _displayEnd > 0;
        bool canStepForward = _displayEnd < maxGen;

        if (ImGui.MenuItem(_isPlaying ? "Pause" : "Play", "Space"))
            TogglePlayPause();

        if (ImGui.MenuItem("Previous Generation", "", false, canStepBack))
            SeekDisplayEnd(_displayEnd - 1);

        if (ImGui.MenuItem("Next Generation", "", false, canStepForward))
            SeekDisplayEnd(_displayEnd + 1);

        if (ImGui.MenuItem("First Generation", "", false, canStepBack))
            SeekDisplayEnd(0);

        if (ImGui.MenuItem("Last Generation", "", false, canStepForward))
            SeekDisplayEnd(maxGen);

        if (ImGui.MenuItem("Reset Simulation"))
            OnReset();

        ImGui.Separator();

        if (ImGui.BeginMenu("Grid Size"))
        {
            for (int i = 0; i < GridSizes.Length; i++)
            {
                bool selected = i == _selectedGridSizeIdx;
                if (ImGui.MenuItem(GridSizes[i], "", selected))
                {
                    _selectedGridSizeIdx = i;
                    int newSize = GridSizeValues[i];
                    _engine.SetGridSize(newSize);
                    _renderer.SetGridSize(newSize);
                    SyncDisplayRange();
                }
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Rule"))
        {
            for (int i = 0; i < RuleNames.Length; i++)
            {
                bool selected = i == _selectedRuleIdx;
                if (ImGui.MenuItem(RuleNames[i], "", selected))
                {
                    string key = RuleKeys[i];
                    if (key == "custom")
                    {
                        _showCustomRulePopup = true;
                    }
                    else
                    {
                        _selectedRuleIdx = i;
                        _engine.SetRule(key);
                        RecomputeGenerations();
                    }
                }
            }
            ImGui.EndMenu();
        }

        if (ImGui.MenuItem("Toroidal", "", _toroidal))
        {
            _toroidal = !_toroidal;
            _engine.SetToroidal(_toroidal);
            RecomputeGenerations();
        }

        ImGui.Separator();

        if (ImGui.BeginMenu("Compute", _engine.GenerationCount > 0))
        {
            if (ImGui.MenuItem("+10 Generations"))
            {
                _engine.ComputeGenerations(_engine.GenerationCount + 10);
                SyncDisplayRange();
            }
            if (ImGui.MenuItem("+50 Generations"))
            {
                _engine.ComputeGenerations(_engine.GenerationCount + 50);
                SyncDisplayRange();
            }
            if (ImGui.MenuItem("+100 Generations"))
            {
                _engine.ComputeGenerations(_engine.GenerationCount + 100);
                SyncDisplayRange();
            }
            ImGui.EndMenu();
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Random Seed..."))
            _showRandomSeedPopup = true;

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // View Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderViewMenu()
    {
        if (!ImGui.BeginMenu("View"))
            return;

        if (ImGui.MenuItem("Show Timeline", "T", _isTimelineVisible))
            _isTimelineVisible = !_isTimelineVisible;

        if (ImGui.MenuItem("Show Statistics", "", _showStatisticsWindow))
            _showStatisticsWindow = !_showStatisticsWindow;

        if (ImGui.MenuItem("Show Perf Stats", "", _statusBar.ShowPerfStats))
            _statusBar.ShowPerfStats = !_statusBar.ShowPerfStats;

        ImGui.Separator();

        if (ImGui.MenuItem("Reset Camera"))
        {
            if (_camera.IsFlythroughActive)
                _camera.StopFlythrough();
            _camera.Reset();
        }

        if (ImGui.MenuItem("Flythrough Mode", "F", _camera.IsFlythroughActive))
        {
            if (_camera.IsFlythroughActive)
            {
                _camera.StopFlythrough();
            }
            else
            {
                if (_editController is { IsActive: true })
                    _editController.Deactivate();

                _isPlaying = false;
                _timeline.SetPlaying(false);

                var path = FlythroughPathGenerator.Generate(
                    _engine.Generations,
                    _displayStart, _displayEnd,
                    _engine.GridSize,
                    _camera.Position,
                    _camera.Target);

                if (path != null)
                {
                    _camera.StartFlythrough(path, (pos, lookAt) =>
                        FlythroughPathGenerator.Generate(
                            _engine.Generations,
                            _displayStart, _displayEnd,
                            _engine.GridSize, pos, lookAt));
                }
            }
        }

        if (ImGui.MenuItem("Cinematic Mode", "P", IsCinematicModeActive))
            OnCinematicToggleRequested?.Invoke();

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Appearance Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderAppearanceMenu()
    {
        if (!ImGui.BeginMenu("Appearance"))
            return;

        var settings = _renderer.Settings;

        if (ImGui.BeginMenu("Cell Shape"))
        {
            for (int i = 0; i < ShapeNames.Length; i++)
            {
                bool selected = i == _shape;
                if (ImGui.MenuItem(ShapeNames[i], "", selected))
                {
                    _shape = i;
                    settings.Shape = (CellShape)i;
                }
            }
            ImGui.EndMenu();
        }

        if (ImGui.MenuItem("Cell Padding..."))
            _showCellPaddingPopup = true;

        if (ImGui.MenuItem("Wireframe", "", _showWireframe))
        {
            _showWireframe = !_showWireframe;
            settings.ShowWireframe = _showWireframe;
        }

        ImGui.Separator();

        if (ImGui.BeginMenu("Floor Mode"))
        {
            string[] floorModes = ["Off", "Grid Lines", "Reflective"];
            for (int i = 0; i < floorModes.Length; i++)
            {
                bool selected = i == _floorModeIdx;
                if (ImGui.MenuItem(floorModes[i], "", selected))
                {
                    _floorModeIdx = i;
                    settings.FloorMode = (FloorMode)i;
                }
            }
            ImGui.EndMenu();
        }

        if (settings.FloorMode == FloorMode.Reflective)
        {
            if (ImGui.MenuItem("Water Tuning..."))
                _showReflectiveFloorPopup = true;
        }

        if (ImGui.MenuItem("Generation Labels", "", _showGenerationLabels))
        {
            _showGenerationLabels = !_showGenerationLabels;
            settings.ShowGenerationLabels = _showGenerationLabels;
        }

        ImGui.Separator();

        if (ImGui.BeginMenu("Colors"))
        {
            bool hasMaterial = settings.ActiveMaterial != null;

            // Color cycling is mutually exclusive with PBR materials — the
            // material's base_color and lighting define the cell appearance.
            if (hasMaterial) ImGui.BeginDisabled();
            if (ImGui.MenuItem("Face Color Cycling", "", _faceColorCycling))
            {
                _faceColorCycling = !_faceColorCycling;
                settings.FaceColorCycling = _faceColorCycling;
            }

            if (_faceColorCycling)
            {
                if (ImGui.MenuItem("Gradient Editor..."))
                    _showGradientPopup = true;
            }
            else
            {
                if (ImGui.MenuItem("Cell Color..."))
                    _showCellColorPopup = true;
            }
            if (hasMaterial) ImGui.EndDisabled();

            if (hasMaterial)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                ImGui.Text("Overridden by material");
                ImGui.PopStyleColor();
            }

            if (_showWireframe)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Edge Color Cycling", "", _edgeColorCycling))
                {
                    _edgeColorCycling = !_edgeColorCycling;
                    settings.EdgeColorCycling = _edgeColorCycling;
                }

                if (_edgeColorCycling)
                {
                    if (ImGui.SliderFloat("##hue_menu", ref _edgeColorAngle, 0f, 360f, "Hue Offset: %.0f\u00B0"))
                        settings.EdgeColorAngle = _edgeColorAngle;
                }
                else
                {
                    if (ImGui.ColorEdit3("Edge Color", ref _edgeColor))
                        settings.EdgeColor = _edgeColor;
                }
            }

            ImGui.EndMenu();
        }

        ImGui.Separator();

        if (ImGui.BeginMenu("Effects"))
        {
            if (ImGui.MenuItem("Fog..."))
                _showFogPopup = true;

            if (ImGui.MenuItem("Bloom..."))
                _showBloomPopup = true;

            if (ImGui.MenuItem("Clip Plane..."))
                _showClipPlanePopup = true;

            if (ImGui.MenuItem("Background..."))
                _showBackgroundPopup = true;

            ImGui.Separator();

            if (ImGui.MenuItem("Materials (PBR)..."))
                _showMaterialPopup = true;

            ImGui.EndMenu();
        }

        ImGui.Separator();

        if (ImGui.BeginMenu("Font Size"))
        {
            bool atMin = _fontSize <= UiSettingsState.MinFontSize + 0.001f;
            bool atMax = _fontSize >= UiSettingsState.MaxFontSize - 0.001f;

            if (ImGui.MenuItem("Decrease", "", false, !atMin))
                OnFontSizeChanged?.Invoke(_fontSize - UiSettingsState.FontSizeStep);

            if (ImGui.MenuItem("Increase", "", false, !atMax))
                OnFontSizeChanged?.Invoke(_fontSize + UiSettingsState.FontSizeStep);

            ImGui.Separator();

            if (ImGui.MenuItem("Automatic", "", _isFontSizeAutomatic, !_isFontSizeAutomatic))
                OnFontSizeReset?.Invoke();

            ImGui.EndMenu();
        }

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Patterns Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderPatternsMenu()
    {
        if (!ImGui.BeginMenu("Patterns"))
            return;

        if (ImGui.MenuItem("Pattern Library..."))
            _showPatternLibraryWindow = true;

        if (_patternLibState.RecentIds.Count > 0)
        {
            ImGui.Separator();
            if (ImGui.BeginMenu("Recent"))
            {
                foreach (var recentId in _patternLibState.RecentIds.ToArray())
                {
                    var metadata = _patternLibrary.Get(recentId);
                    if (metadata == null) continue;

                    if (ImGui.MenuItem(metadata.Name))
                        LoadPattern(metadata.Id);
                }
                ImGui.EndMenu();
            }
        }

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Help Menu
    // ────────────────────────────────────────────────────────────────────────

    private void RenderHelpMenu()
    {
        if (!ImGui.BeginMenu("Help"))
            return;

        if (ImGui.MenuItem("Keyboard Shortcuts"))
            _showShortcutsPopup = true;

        ImGui.Separator();

        if (ImGui.MenuItem("About"))
            _showAboutPopup = true;

        ImGui.EndMenu();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Floating Windows & Popups
    // ────────────────────────────────────────────────────────────────────────

    private void RenderStatisticsWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(320, 280), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Statistics", ref _showStatisticsWindow))
        {
            ImGui.End();
            return;
        }

        if (_engine.GenerationCount != _lastPopulationGenCount)
        {
            _populationData = new float[_engine.GenerationCount];
            for (int i = 0; i < _engine.GenerationCount; i++)
            {
                var gen = _engine.GetGeneration(i);
                _populationData[i] = gen?.LiveCells.Count ?? 0;
            }
            _lastPopulationGenCount = _engine.GenerationCount;
        }

        if (_populationData.Length > 0)
        {
            float current = _populationData[^1];
            float min = _populationData.Min();
            float max = _populationData.Max();
            float avg = _populationData.Average();

            UIHelpers.LabelValue("Generations:", _engine.GenerationCount.ToString());
            UIHelpers.LabelValue("Current:", ((int)current).ToString());
            UIHelpers.LabelValue("Min / Max:", $"{(int)min} / {(int)max}");
            UIHelpers.LabelValue("Average:", $"{avg:F0}");

            float fullWidth = ImGui.GetContentRegionAvail().X;
            ImGui.PlotLines("##pop", ref _populationData[0], _populationData.Length,
                0, $"Population ({_populationData.Length} gens)",
                min * 0.9f, max * 1.1f, new Vector2(fullWidth, 80));
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
            ImGui.Text("No generations computed.");
            ImGui.PopStyleColor();
        }

        ImGui.End();
    }

    private void RenderPatternLibraryWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(420, 550), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Pattern Library", ref _showPatternLibraryWindow))
        {
            ImGui.End();
            return;
        }

        float fullWidth = ImGui.GetContentRegionAvail().X;

        // Recently used
        if (_patternLibState.RecentIds.Count > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Recent");
            ImGui.PopStyleColor();

            float currentX = 0;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            var recentIdsSnapshot = _patternLibState.RecentIds.ToArray();
            foreach (var recentId in recentIdsSnapshot)
            {
                var metadata = _patternLibrary.Get(recentId);
                if (metadata == null) continue;

                float btnWidth = ImGui.CalcTextSize(metadata.Name).X + ImGui.GetStyle().FramePadding.X * 2;
                if (currentX > 0 && currentX + btnWidth > fullWidth)
                    currentX = 0;
                else if (currentX > 0)
                    ImGui.SameLine();

                if (ImGui.Button($"{metadata.Name}##recent-{metadata.Id}"))
                    LoadPattern(metadata.Id);

                currentX += btnWidth + spacing;
            }

            UIHelpers.ThinSeparator();
        }

        // Search + filters
        ImGui.SetNextItemWidth(fullWidth);
        ImGui.InputTextWithHint("##pattern-search", "Search patterns...", ref _patternSearch, 64);

        var categories = _patternLibrary.Categories;
        string[] categoryLabels = new string[categories.Count + 1];
        categoryLabels[0] = "All categories";
        for (int i = 0; i < categories.Count; i++)
            categoryLabels[i + 1] = ToTitleCase(categories[i]);

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.Combo("##pattern-cat", ref _patternCategoryIdx, categoryLabels, categoryLabels.Length))
        {
            _patternLibState.LastCategory = _patternCategoryIdx > 0 ? categories[_patternCategoryIdx - 1] : null;
            _patternLibState.Save();
        }

        ImGui.SetNextItemWidth(fullWidth * 0.48f);
        ImGui.SliderInt("##pat-period-min", ref _patternPeriodMin, 0, 32, "Period >= %d");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(fullWidth * 0.48f);
        ImGui.SliderInt("##pat-period-max", ref _patternPeriodMax, 0, 32, "Period <= %d");
        if (_patternPeriodMin > _patternPeriodMax)
            _patternPeriodMax = _patternPeriodMin;

        ImGui.SetNextItemWidth(fullWidth);
        ImGui.SliderInt("##pat-max-size", ref _patternMaxSize, 3, 200, "Max size: %d cells");

        UIHelpers.ThinSeparator();

        // Results list
        string? activeCategory = _patternCategoryIdx > 0 ? categories[_patternCategoryIdx - 1] : null;
        int? periodMin = _patternPeriodMin > 0 ? _patternPeriodMin : null;
        int? periodMax = _patternPeriodMax < 32 ? _patternPeriodMax : null;
        int? maxSize = _patternMaxSize < 200 ? _patternMaxSize : null;

        var results = _patternLibrary.Search(
            query: _patternSearch,
            category: activeCategory,
            periodMin: periodMin,
            periodMax: periodMax,
            maxSize: maxSize).ToList();

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.BgSurface);
        ImGui.BeginChild("##pat-results", new Vector2(fullWidth, 160), ImGuiChildFlags.Border);

        if (results.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
            ImGui.Text("No patterns match.");
            ImGui.PopStyleColor();
        }
        else
        {
            foreach (var p in results)
            {
                bool isSelected = p.Id == _selectedPatternId;
                string label = p.Period.HasValue
                    ? $"{p.Name}  ({p.Width}x{p.Height}, p{p.Period})"
                    : $"{p.Name}  ({p.Width}x{p.Height})";

                if (ImGui.Selectable($"{label}##sel-{p.Id}", isSelected))
                    _selectedPatternId = p.Id;

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    LoadPattern(p.Id);
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();

        // Preview + Load
        var selected = _selectedPatternId != null ? _patternLibrary.Get(_selectedPatternId) : null;
        if (selected != null)
        {
            UIHelpers.ThinSeparator();

            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text(selected.Category);
            ImGui.PopStyleColor();

            ImGui.Text(selected.Name);
            if (selected.Author != null)
                UIHelpers.LabelValue("Author:", selected.Author);
            if (selected.Description != null)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                ImGui.TextWrapped(selected.Description);
                ImGui.PopStyleColor();
            }

            var preview = _patternLibrary.GetPattern(selected.Id);
            PatternPreview.Draw(preview, new Vector2(fullWidth, 100));

            if (UIHelpers.AccentButton("Load pattern", new Vector2(fullWidth, 0)))
                LoadPattern(selected.Id);
        }

        ImGui.End();
    }

    private void RenderRecordingPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(340, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Record Video", ref _showRecordingPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        float fullWidth = ImGui.GetContentRegionAvail().X;

        int duration = RecordingDurationSeconds;
        if (IsRecording)
            ImGui.BeginDisabled();

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderInt("##rec-duration", ref duration, 1, 120, "Duration: %d s"))
            RecordingDurationSeconds = Math.Clamp(duration, 1, 120);

        string[] codecLabels = ["H.264 MP4", "VP9 WebM"];
        int codecIdx = RecordingCodec == VideoCodec.H264Mp4 ? 0 : 1;
        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.Combo("##rec-codec", ref codecIdx, codecLabels, codecLabels.Length))
            RecordingCodec = codecIdx == 0 ? VideoCodec.H264Mp4 : VideoCodec.Vp9Webm;

        if (IsRecording)
            ImGui.EndDisabled();

        if (IsRecording)
        {
            float progress = (float)Math.Clamp(RecordingProgress01, 0.0, 1.0);
            ImGui.ProgressBar(progress, new Vector2(fullWidth, 0),
                $"{progress * RecordingDurationSeconds:F1} / {RecordingDurationSeconds} s");
        }
        else if (UIHelpers.AccentButton("Start Recording", new Vector2(fullWidth, 0)))
        {
            OnRecordingStartRequested?.Invoke();
        }

        if (!string.IsNullOrWhiteSpace(RecordingStatusMessage))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.TextWrapped(RecordingStatusMessage);
            ImGui.PopStyleColor();
        }

        ImGui.End();
    }

    private void RenderRandomSeedPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Random Seed", ref _showRandomSeedPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        float fullWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(fullWidth - 70);
        ImGui.SliderFloat("##density", ref _randomDensity, 5f, 80f, "Density: %.0f%%");
        ImGui.SameLine();
        if (UIHelpers.AccentButton("Go"))
        {
            _engine.InitializeRandom(_randomDensity / 100f);
            _renderer.InvalidateState();
            SyncDisplayRange();
        }

        ImGui.End();
    }

    private void RenderCustomRulePopup()
    {
        ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Custom Rule", ref _showCustomRulePopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        float fullWidth = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(fullWidth * 0.45f);
        ImGui.InputText("Birth", ref _customBirth, 9);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(fullWidth * 0.45f);
        ImGui.InputText("Survival", ref _customSurvival, 9);

        if (UIHelpers.AccentButton("Apply", new Vector2(fullWidth, 0)))
        {
            var birth = _customBirth.Where(c => c >= '0' && c <= '8')
                .Select(c => c - '0').Distinct().ToArray();
            var survival = _customSurvival.Where(c => c >= '0' && c <= '8')
                .Select(c => c - '0').Distinct().ToArray();
            _engine.SetCustomRule(birth, survival);
            _selectedRuleIdx = Array.IndexOf(RuleKeys, "custom");
            RecomputeGenerations();
            _showCustomRulePopup = false;
        }

        ImGui.End();
    }

    private void RenderGradientPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(340, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Gradient Editor", ref _showGradientPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        // Preset combo
        string presetLabel = _gradientPreset ?? GradientPresets.CustomLabel;
        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.BeginCombo("##gradpreset", presetLabel))
        {
            foreach (var (name, preset) in GradientPresets.Presets)
            {
                bool isSelected = _gradientPreset == name;
                if (ImGui.Selectable(name, isSelected))
                {
                    settings.GradientStops = new List<Vector3>(preset);
                    _gradientPreset = name;
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        // Live preview strip
        DrawGradientPreview(settings.GradientStops, fullWidth);

        // Per-stop pickers + remove
        var span = CollectionsMarshal.AsSpan(settings.GradientStops);
        int removeIdx = -1;
        bool canRemove = settings.GradientStops.Count > RenderSettings.MinGradientStops;

        for (int i = 0; i < settings.GradientStops.Count; i++)
        {
            ImGui.PushID(i);

            ImGui.SetNextItemWidth(fullWidth - 30f);
            if (ImGui.ColorEdit3($"##stop{i}", ref span[i],
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
            {
                _gradientPreset = GradientPresets.Match(settings.GradientStops);
            }

            ImGui.SameLine();
            if (!canRemove) ImGui.BeginDisabled();
            if (ImGui.Button(Icons.Trash, new Vector2(24f, 0)))
                removeIdx = i;
            if (!canRemove) ImGui.EndDisabled();

            ImGui.PopID();
        }

        if (removeIdx >= 0)
        {
            settings.GradientStops.RemoveAt(removeIdx);
            _gradientPreset = GradientPresets.Match(settings.GradientStops);
        }

        // Add Stop / Reset row
        bool canAdd = settings.GradientStops.Count < RenderSettings.MaxGradientStops;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float halfWidth = (fullWidth - spacing) * 0.5f;

        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button("+ Add Stop", new Vector2(halfWidth, 0)))
        {
            var last = settings.GradientStops[^1];
            settings.GradientStops.Add(last);
            _gradientPreset = GradientPresets.Match(settings.GradientStops);
        }
        if (!canAdd) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Reset", new Vector2(halfWidth, 0)))
        {
            settings.ResetGradient();
            _gradientPreset = GradientPresets.Match(settings.GradientStops);
        }

        ImGui.End();
    }

    private void RenderCellColorPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Cell Color", ref _showCellColorPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.ColorEdit3("Cell Color", ref _cellColor))
            settings.CellColor = _cellColor;

        ImGui.End();
    }

    private void RenderFogPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Depth Fog", ref _showFogPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        if (ImGui.Checkbox("Enable Fog", ref _fogEnabled))
            settings.FogEnabled = _fogEnabled;

        if (_fogEnabled)
        {
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##fogstart", ref _fogStart, 1f, 200f, "Start: %.0f"))
                settings.FogStart = _fogStart;

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##fogend", ref _fogEnd, 10f, 500f, "End: %.0f"))
                settings.FogEnd = _fogEnd;

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.ColorEdit3("Fog Color", ref _fogColor))
                settings.FogColor = _fogColor;
        }

        ImGui.End();
    }

    private void RenderBloomPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Bloom", ref _showBloomPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        if (ImGui.Checkbox("Enable Bloom", ref _bloomEnabled))
            settings.BloomEnabled = _bloomEnabled;

        if (_bloomEnabled)
        {
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##bloomthresh", ref _bloomThreshold, 0.1f, 1.5f, "Threshold: %.2f"))
                settings.BloomThreshold = _bloomThreshold;

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##bloomintensity", ref _bloomIntensity, 0.1f, 2.0f, "Intensity: %.2f"))
                settings.BloomIntensity = _bloomIntensity;
        }

        ImGui.End();
    }

    private void RenderClipPlanePopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Clip Plane", ref _showClipPlanePopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        if (ImGui.Checkbox("Enable Clip Plane", ref _clipEnabled))
            settings.ClipEnabled = _clipEnabled;

        if (_clipEnabled)
        {
            float maxY = Math.Max(_engine.GenerationCount, 1);
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##clipy", ref _clipY, 0f, maxY, "Clip Y: %.0f"))
                settings.ClipY = _clipY;
        }

        ImGui.End();
    }

    private void RenderBackgroundPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Background", ref _showBackgroundPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        string[] bgModes = ["Solid", "Gradient", "Starfield"];
        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.Combo("Mode", ref _backgroundMode, bgModes, bgModes.Length))
            settings.BackgroundMode = (BackgroundMode)_backgroundMode;

        if (_backgroundMode > 0)
        {
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.ColorEdit3("Top Color", ref _bgTopColor))
                settings.BackgroundTopColor = _bgTopColor;
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.ColorEdit3("Bottom Color", ref _bgBottomColor))
                settings.BackgroundBottomColor = _bgBottomColor;
        }

        ImGui.End();
    }

    private void RenderCellPaddingPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Cell Padding", ref _showCellPaddingPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderFloat("##padding", ref _cellPadding, 0f, 50f, "Cell Padding: %.0f%%"))
        {
            settings.CellPadding = _cellPadding / 100f;
            _renderer.InvalidateState();
        }

        ImGui.End();
    }

    private void RenderReflectiveFloorPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(320, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Water Tuning", ref _showReflectiveFloorPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        var settings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderFloat("##wavestr", ref _waveStrength, 0f, 1f, "Wave Strength: %.2f"))
            settings.WaveStrength = _waveStrength;

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderFloat("##wavespeed", ref _waveSpeed, 0f, 2f, "Wave Speed: %.2f"))
            settings.WaveSpeed = _waveSpeed;

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderFloat("##refl", ref _reflectivity, 0f, 1f, "Reflectivity: %.2f"))
            settings.Reflectivity = _reflectivity;

        if (ImGui.ColorEdit3("Water Tint", ref _waterTint, ImGuiColorEditFlags.NoInputs))
            settings.WaterTint = _waterTint;

        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderFloat("##reflres", ref _reflectionResolutionScale, 0.25f, 1f, "Reflection Resolution: %.2fx"))
            settings.ReflectionResolutionScale = _reflectionResolutionScale;

        ImGui.End();
    }

    private void RenderMaterialPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(380, 0), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Materials (PBR)", ref _showMaterialPopup))
        {
            ImGui.End();
            return;
        }

        var renderSettings = _renderer.Settings;
        float fullWidth = ImGui.GetContentRegionAvail().X;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float halfWidth = (fullWidth - spacing) * 0.5f;

        // ── Active material status ────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("Active Material");
        ImGui.PopStyleColor();

        if (renderSettings.ActiveMaterial != null)
        {
            string label = renderSettings.MaterialFilePath != null
                ? Path.GetFileNameWithoutExtension(renderSettings.MaterialFilePath)
                : "Custom (imported)";
            ImGui.TextWrapped(label);

            ImGui.Spacing();
            if (UIHelpers.AccentButton("Clear Material", new Vector2(fullWidth, 0)))
            {
                renderSettings.ActiveMaterial = null;
                renderSettings.MaterialFilePath = null;
                OnMaterialChanged?.Invoke(null);
            }
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
            ImGui.Text("None — Legacy Lambertian shader");
            ImGui.PopStyleColor();
        }

        UIHelpers.ThinSeparator();

        // ── Import buttons ────────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("Import");
        ImGui.PopStyleColor();

        if (ImGui.Button("Load .mtlx...", new Vector2(halfWidth, 0)))
        {
            var path = FileDialogHelper.OpenFile("mtlx");
            if (path != null)
                TryImportMaterial(path, renderSettings);
        }
        ImGui.SameLine();
        if (ImGui.Button("Load .pbr.json...", new Vector2(halfWidth, 0)))
        {
            var path = FileDialogHelper.OpenFile("json");
            if (path != null)
                TryImportMaterial(path, renderSettings);
        }

        if (_materialImportError != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
            ImGui.TextWrapped(_materialImportError);
            ImGui.PopStyleColor();
        }

        UIHelpers.ThinSeparator();

        // ── IBL / environment intensity ───────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("Environment");
        ImGui.PopStyleColor();

        float envIntensity = renderSettings.EnvIntensity;
        ImGui.SetNextItemWidth(fullWidth);
        if (ImGui.SliderFloat("##envintensity", ref envIntensity, 0f, 2f, "IBL Intensity: %.2f"))
            renderSettings.EnvIntensity = envIntensity;

        UIHelpers.ThinSeparator();

        // ── Material library ──────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("Library");
        ImGui.PopStyleColor();

        if (_materialLibState.Materials.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
            ImGui.Text("No saved materials. Load a file above to add one.");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.BgSurface);
            ImGui.BeginChild("##mat-lib", new Vector2(fullWidth, 130), ImGuiChildFlags.Border);

            for (int i = 0; i < _materialLibState.Materials.Count; i++)
            {
                var entry = _materialLibState.Materials[i];
                ImGui.PushID(i);

                if (!entry.FileExists)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                    ImGui.Text($"[Missing] {entry.Name}");
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    if (ImGui.SmallButton(Icons.Trash))
                        _materialLibState.Remove(entry.FilePath);
                }
                else if (_materialRenameIdx == i)
                {
                    // Inline rename field
                    ImGui.SetNextItemWidth(fullWidth - MaterialLibraryButtonsReservedWidth);
                    if (ImGui.InputText("##rename", ref _materialRenameDraft, 128,
                            ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        _materialLibState.Rename(entry.FilePath, _materialRenameDraft.Trim());
                        _materialRenameIdx = -1;
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("OK"))
                    {
                        _materialLibState.Rename(entry.FilePath, _materialRenameDraft.Trim());
                        _materialRenameIdx = -1;
                    }
                }
                else
                {
                    bool isActive = string.Equals(renderSettings.MaterialFilePath,
                        entry.FilePath, StringComparison.OrdinalIgnoreCase);

                    if (ImGui.Selectable(entry.Name, isActive,
                            ImGuiSelectableFlags.AllowDoubleClick,
                            new Vector2(fullWidth - MaterialLibraryButtonsReservedWidth, 0)))
                    {
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                            TryImportMaterial(entry.FilePath, renderSettings);
                    }

                    ImGui.SameLine();
                    if (ImGui.SmallButton(Icons.Pencil))
                    {
                        _materialRenameIdx = i;
                        _materialRenameDraft = entry.Name;
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton(Icons.Trash))
                        _materialLibState.Remove(entry.FilePath);
                }

                ImGui.PopID();
            }

            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        // ── Active material inspector (read-only) ─────────────────────────────
        if (renderSettings.ActiveMaterial != null)
        {
            UIHelpers.ThinSeparator();
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Inspector (read-only)");
            ImGui.PopStyleColor();

            var mat = renderSettings.ActiveMaterial;

            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
            UIHelpers.LabelValue("Base Metalness:", $"{mat.BaseMetalness:F3}");
            UIHelpers.LabelValue("Base Diffuse Roughness:", $"{mat.BaseDiffuseRoughness:F3}");
            UIHelpers.LabelValue("Base Weight:", $"{mat.BaseWeight:F3}");
            UIHelpers.LabelValue("Specular Weight:", $"{mat.SpecularWeight:F3}");
            UIHelpers.LabelValue("Specular Color:", $"({mat.SpecularColor.X:F2}, {mat.SpecularColor.Y:F2}, {mat.SpecularColor.Z:F2})");
            UIHelpers.LabelValue("Specular Roughness:", $"{mat.SpecularRoughness:F3}");
            UIHelpers.LabelValue("Specular Anisotropy:", $"{mat.SpecularAnisotropy:F3}");
            UIHelpers.LabelValue("Specular IOR:", $"{mat.SpecularIor:F3}");
            UIHelpers.LabelValue("Emission Luminance:", $"{mat.EmissionLuminance:F3}");
            UIHelpers.LabelValue("Coat Weight:", $"{mat.CoatWeight:F3}");
            if (mat.CoatWeight > 0f)
            {
                UIHelpers.LabelValue("Coat Color:", $"({mat.CoatColor.X:F2}, {mat.CoatColor.Y:F2}, {mat.CoatColor.Z:F2})");
                UIHelpers.LabelValue("Coat Roughness:", $"{mat.CoatRoughness:F3}");
                UIHelpers.LabelValue("Coat Anisotropy:", $"{mat.CoatAnisotropy:F3}");
                UIHelpers.LabelValue("Coat IOR:", $"{mat.CoatIor:F3}");
                UIHelpers.LabelValue("Coat Darkening:", $"{mat.CoatDarkening:F3}");
            }
            UIHelpers.LabelValue("Fuzz Weight:", $"{mat.FuzzWeight:F3}");
            if (mat.FuzzWeight > 0f)
            {
                UIHelpers.LabelValue("Fuzz Color:", $"({mat.FuzzColor.X:F2}, {mat.FuzzColor.Y:F2}, {mat.FuzzColor.Z:F2})");
                UIHelpers.LabelValue("Fuzz Roughness:", $"{mat.FuzzRoughness:F3}");
            }
            UIHelpers.LabelValue("Thin Film Weight:", $"{mat.ThinFilmWeight:F3}");
            if (mat.ThinFilmWeight > 0f)
            {
                UIHelpers.LabelValue("Thin Film Thickness:", $"{mat.ThinFilmThickness:F1} nm");
                UIHelpers.LabelValue("Thin Film IOR:", $"{mat.ThinFilmIor:F3}");
            }
            UIHelpers.LabelValue("Geometry Opacity:", $"{mat.GeometryOpacity:F3}");
            UIHelpers.LabelValue("Texture Scale:", $"{mat.TextureScale:F3}");

            // Texture slots — file name when bound, em dash for constant-only.
            UIHelpers.LabelValue("Base Color Texture:", mat.BaseColorTexture != null ? Path.GetFileName(mat.BaseColorTexture) : "—");
            UIHelpers.LabelValue("Metalness Texture:", mat.MetalnessTexture != null ? Path.GetFileName(mat.MetalnessTexture) : "—");
            UIHelpers.LabelValue("Roughness Texture:", mat.RoughnessTexture != null ? Path.GetFileName(mat.RoughnessTexture) : "—");
            UIHelpers.LabelValue("Normal Texture:", mat.NormalTexture != null ? Path.GetFileName(mat.NormalTexture) : "—");
            UIHelpers.LabelValue("Emission Texture:", mat.EmissionTexture != null ? Path.GetFileName(mat.EmissionTexture) : "—");
            UIHelpers.LabelValue("Opacity Texture:", mat.OpacityTexture != null ? Path.GetFileName(mat.OpacityTexture) : "—");
            ImGui.PopStyleColor();
        }

        ImGui.End();
    }

    private void TryImportMaterial(string path, RenderSettings renderSettings)
    {
        _materialImportError = null;
        var result = MaterialImporter.ImportFile(path);

        if (!result.IsSuccess)
        {
            _materialImportError = result.Error ?? "Unknown import error.";
            return;
        }

        renderSettings.ActiveMaterial = result.Material;
        renderSettings.MaterialFilePath = path;
        _faceColorCycling = false;
        OnMaterialChanged?.Invoke(result.Material);

        // Add to library using the file stem as default name.
        string name = Path.GetFileNameWithoutExtension(path);
        _materialLibState.AddOrUpdate(name, path);

        if (result.UnsupportedTexturedParams.Count > 0)
        {
            string skipped = string.Join(", ", result.UnsupportedTexturedParams);
            _materialImportError = $"Note: unsupported or missing texture inputs (constants used): {skipped}";
        }
    }

    private void RenderShortcutsPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(320, 0), ImGuiCond.Always);
        if (!ImGui.Begin("Keyboard Shortcuts", ref _showShortcutsPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("Mouse");
        ImGui.PopStyleColor();
        UIHelpers.LabelValue("  LMB", "Orbit");
        UIHelpers.LabelValue("  RMB", "Pan");
        UIHelpers.LabelValue("  Scroll", "Zoom");

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("Keyboard");
        ImGui.PopStyleColor();
        UIHelpers.LabelValue("  WASD", "Move");
        UIHelpers.LabelValue("  QE", "Rotate");
        UIHelpers.LabelValue("  RC", "Up / Down");
        UIHelpers.LabelValue("  0", "Restart Auto Orbit");
        UIHelpers.LabelValue("  F", "Toggle Flythrough");
        UIHelpers.LabelValue("  P", "Toggle Cinematic");
        UIHelpers.LabelValue("  Space", "Play / Pause");
        UIHelpers.LabelValue("  T", "Toggle Timeline");
        UIHelpers.LabelValue("  F12", "Screenshot");
        UIHelpers.LabelValue("  Ctrl+R", "Record Video");
        UIHelpers.LabelValue("  E", "Toggle Edit");
        UIHelpers.LabelValue("  [/]", "Brush Size");
        UIHelpers.LabelValue("  Esc", "Exit Edit/Cinematic");

        ImGui.End();
    }

    private void RenderAboutPopup()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 0), ImGuiCond.Always);
        if (!ImGui.Begin("About", ref _showAboutPopup,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextPrimary);
        ImGui.Text("Game of Life 3D");
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text($"Version {AppVersion}");
        ImGui.Text("Created by Brian Tabone");
        ImGui.PopStyleColor();

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        ImGui.TextWrapped("A 3D visualization of Conway's Game of Life and other cellular automata.");
        ImGui.TextWrapped("Built with Silk.NET, OpenGL 3.3, and ImGui.");
        ImGui.PopStyleColor();

        ImGui.End();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private void LoadPattern(string id)
    {
        var pattern = _patternLibrary.GetPattern(id);
        if (pattern == null) return;

        _engine.InitializeFromPattern(pattern);
        _renderer.InvalidateState();
        SyncDisplayRange();
        _selectedPatternId = id;
        _patternLibState.MarkUsed(id);
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var parts = s.Split('-', '_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0) continue;
            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        }
        return string.Join(' ', parts);
    }

    private static string LoadAppVersion()
    {
        foreach (string plistPath in GetVersionPlistCandidates())
        {
            string? version = ReadPlistString(plistPath, "CFBundleShortVersionString");
            if (!string.IsNullOrWhiteSpace(version))
                return version;
        }

        return "Unknown";
    }

    private static IEnumerable<string> GetVersionPlistCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(root);
            for (int i = 0; i < 8 && directory != null; i++, directory = directory.Parent)
            {
                foreach (string candidate in new[]
                {
                    Path.Combine(directory.FullName, "Info.plist"),
                    Path.Combine(directory.FullName, "signing", "macOS", "Info.plist"),
                })
                {
                    string fullPath = Path.GetFullPath(candidate);
                    if (seen.Add(fullPath))
                        yield return fullPath;
                }
            }
        }
    }

    private static string? ReadPlistString(string path, string key)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var elements = XDocument.Load(path).Root?.Element("dict")?.Elements().ToArray();
            if (elements == null)
                return null;

            for (int i = 0; i < elements.Length - 1; i++)
            {
                if (elements[i].Name.LocalName == "key" &&
                    elements[i].Value == key &&
                    elements[i + 1].Name.LocalName == "string")
                {
                    return elements[i + 1].Value;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static void DrawGradientPreview(IReadOnlyList<Vector3> stops, float width)
    {
        if (stops.Count < 2) return;

        float height = MathF.Max(10f, ImGui.GetTextLineHeight() * 0.9f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();

        int segments = stops.Count;
        float segWidth = width / segments;

        for (int i = 0; i < segments; i++)
        {
            Vector3 a = stops[i];
            Vector3 b = stops[(i + 1) % stops.Count];
            uint colA = ImGui.ColorConvertFloat4ToU32(new Vector4(a.X, a.Y, a.Z, 1f));
            uint colB = ImGui.ColorConvertFloat4ToU32(new Vector4(b.X, b.Y, b.Z, 1f));
            var p0 = new Vector2(origin.X + i * segWidth, origin.Y);
            var p1 = new Vector2(origin.X + (i + 1) * segWidth, origin.Y + height);
            drawList.AddRectFilledMultiColor(p0, p1, colA, colB, colB, colA);
        }

        drawList.AddRect(origin,
            new Vector2(origin.X + width, origin.Y + height),
            Theme.BorderU32, 2f);

        ImGui.Dummy(new Vector2(width, height + 4f));
    }

    private void RecomputeGenerations()
    {
        if (_engine.GenerationCount > 1)
        {
            var gen0 = _engine.GetGeneration(0);
            if (gen0 != null)
            {
                int count = _engine.GenerationCount;
                _engine.Clear();
                _engine.InitializeFromPattern(gen0.Cells);
                _engine.ComputeGenerations(count);
            }
        }
        SyncDisplayRange();
        _renderer.InvalidateState();
    }

    private void SyncUIFromSettings()
    {
        var s = _renderer.Settings;
        _cellPadding = s.CellPadding * 100f;
        _cellColor = s.CellColor;
        _edgeColor = s.EdgeColor;
        _faceColorCycling = s.FaceColorCycling;
        _edgeColorCycling = s.EdgeColorCycling;
        _edgeColorAngle = s.EdgeColorAngle;
        _gradientPreset = GradientPresets.Match(s.GradientStops);
        _floorModeIdx = (int)s.FloorMode;
        _waveStrength = s.WaveStrength;
        _waveSpeed = s.WaveSpeed;
        _waterTint = s.WaterTint;
        _reflectivity = s.Reflectivity;
        _reflectionResolutionScale = s.ReflectionResolutionScale;
        _showGenerationLabels = s.ShowGenerationLabels;
        _showWireframe = s.ShowWireframe;
        _fogEnabled = s.FogEnabled;
        _fogStart = s.FogStart;
        _fogEnd = s.FogEnd;
        _fogColor = s.FogColor;
        _clipEnabled = s.ClipEnabled;
        _clipY = s.ClipY;
        _backgroundMode = (int)s.BackgroundMode;
        _bgTopColor = s.BackgroundTopColor;
        _bgBottomColor = s.BackgroundBottomColor;
        _bloomEnabled = s.BloomEnabled;
        _bloomThreshold = s.BloomThreshold;
        _bloomIntensity = s.BloomIntensity;
        _shape = Math.Clamp((int)s.Shape, 0, ShapeNames.Length - 1);
        // Clear any import error from a previous session so it doesn't persist.
        _materialImportError = null;
    }
}
