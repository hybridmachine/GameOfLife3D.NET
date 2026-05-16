using System.Numerics;
using System.Runtime.InteropServices;
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
    private bool _showCustomRule;
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

    // Cell shape — mirrors RenderSettings.Shape; the int form drives ImGui.Combo.
    private int _shape = (int)CellShape.BeveledCube;
    private static readonly string[] ShapeNames = { "Cube", "Rounded Cube", "Tetrahedron", "Octahedron", "Pyramid" };

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

    // Control panel
    private bool _isControlPanelOpen;
    private float _controlPanelSlide;
    private bool _isTimelineVisible = true;
    private float _fontSize = UiSettingsState.BaseFontSize;
    private float _automaticFontSize = UiSettingsState.BaseFontSize;
    private bool _isFontSizeAutomatic = true;
    private const float ControlPanelMargin = 10f;
    private const float ControlPanelSlideSpeed = 8f;
    private const float ControlPanelToggleSize = 34f;
    private const float ControlPanelToggleGap = 25f;
    private const float TimelineToggleSize = 30f;
    private const float TimelineToggleMargin = 10f;

    private static readonly string[] GridSizes = ["25", "50", "75", "100", "150", "200"];
    private static readonly int[] GridSizeValues = [25, 50, 75, 100, 150, 200];
    private static readonly string[] RuleNames;
    private static readonly string[] RuleKeys;

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

    public ImGuiUI(GameEngine engine, Renderer3D renderer, CameraController camera, PatternLoader patternLoader, PatternLibrary patternLibrary, EditingController? editController = null)
    {
        _engine = engine;
        _renderer = renderer;
        _camera = camera;
        _patternLoader = patternLoader;
        _patternLibrary = patternLibrary;
        _patternLibState = PatternLibraryState.Load();
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

    private void OnReset()
    {
        _isPlaying = false;
        _timeline.SetPlaying(false);
        _engine.Clear();
        _engine.SetRule("conway");
        _engine.SetToroidal(true);
        _selectedRuleIdx = 0;
        _toroidal = true;
        _showCustomRule = false;

        var pattern = _patternLoader.GetBuiltInPattern("r-pentomino");
        if (pattern != null)
            _engine.InitializeFromPattern(pattern);

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

        UpdateControlPanelAnimation();
        RenderControlPanel(windowWidth, windowHeight);
        if (_isTimelineVisible)
            _timeline.Render(windowWidth, windowHeight);
        _statusBar.ShowEditBadge = _editController?.IsActive ?? false;
        _statusBar.Render(_displayStart, _displayEnd, _engine.RuleString,
            _renderer.GetVisibleCellCount(), windowWidth, windowHeight);
        RenderControlPanelToggle();
        RenderTimelineToggle(windowHeight);
    }

    private void RenderRecordingIndicator()
    {
        var drawList = ImGui.GetForegroundDrawList();

        // Position: just right of the gear toggle, vertically centered with it.
        float gearRightX = ControlPanelMargin + ControlPanelToggleSize;
        float gearCenterY = ControlPanelMargin + ControlPanelToggleSize * 0.5f;
        float radius = 7f;
        float gap = 10f;
        var center = new Vector2(gearRightX + gap + radius, gearCenterY);

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

    private void RenderControlPanel(int windowWidth, int windowHeight)
    {
        if (!_isControlPanelOpen && _controlPanelSlide <= 0.001f)
            return;

        float panelY = ControlPanelMargin + ControlPanelToggleSize + ControlPanelToggleGap;
        float minPanelWidth = Math.Max(280f, ImGui.GetTextLineHeight() * 16f);
        float panelWidth = Math.Clamp(windowWidth * 0.24f, minPanelWidth, 480);
        float maxPanelHeight = Math.Max(200f, windowHeight - panelY - 20f);
        float minPanelHeight = Math.Min(300f, maxPanelHeight);
        float panelHeight = Math.Clamp(windowHeight * 0.7f, minPanelHeight, maxPanelHeight);
        float maxPanelWidth = Math.Max(minPanelWidth, windowWidth * 0.35f);
        float openX = ControlPanelMargin;
        float closedX = -panelWidth - 2f;
        float panelX = closedX + (openX - closedX) * _controlPanelSlide;

        ImGui.SetNextWindowPos(new Vector2(panelX, panelY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(panelWidth, panelHeight), ImGuiCond.Always);
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(minPanelWidth, 200),
            new Vector2(maxPanelWidth, Math.Max(200f, windowHeight - panelY - 20f)));

        if (ImGui.Begin("Game of Life 3D", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove))
        {
            RenderSimulationSection();
            ImGui.Spacing();
            RenderStatsSection();
            ImGui.Spacing();
            RenderPatternSection();
            ImGui.Spacing();
            RenderVisualSection();
            ImGui.Spacing();
            RenderEditingSection();
            ImGui.Spacing();
            RenderFileSection();
            ImGui.Spacing();
            RenderCameraSection();
        }
        ImGui.End();
    }

    private void RenderControlPanelToggle()
    {
        ImGui.SetNextWindowPos(
            new Vector2(ControlPanelMargin, ControlPanelMargin),
            ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(ControlPanelToggleSize, ControlPanelToggleSize), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin("##ControlPanelToggle", flags))
        {
            Vector4 buttonColor = _isControlPanelOpen ? Theme.AccentMuted : Theme.BgSurface;
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.FrameHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.FrameActive);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);

            if (ImGui.Button("##control_panel_toggle_btn", new Vector2(ControlPanelToggleSize, ControlPanelToggleSize)))
                _isControlPanelOpen = !_isControlPanelOpen;

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_isControlPanelOpen ? "Hide controls" : "Show controls");

            var drawList = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var center = (min + max) * 0.5f;
            float ringRadius = ControlPanelToggleSize * 0.22f;
            float hubRadius = ringRadius * 0.45f;
            float spokeInner = ringRadius * 1.05f;
            float spokeOuter = ringRadius * 1.45f;
            uint iconColor = ImGui.ColorConvertFloat4ToU32(Theme.TextPrimary);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * (MathF.PI / 4f);
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                drawList.AddLine(
                    center + dir * spokeInner,
                    center + dir * spokeOuter,
                    iconColor,
                    2f);
            }

            drawList.AddCircle(center, ringRadius, iconColor, 24, 2f);
            drawList.AddCircleFilled(center, hubRadius, iconColor, 20);

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
        }
        ImGui.End();
        ImGui.PopStyleVar();
    }

    private void UpdateControlPanelAnimation()
    {
        float target = _isControlPanelOpen ? 1f : 0f;
        float dt = MathF.Max(ImGui.GetIO().DeltaTime, 1f / 240f);
        float step = ControlPanelSlideSpeed * dt;

        if (_controlPanelSlide < target)
            _controlPanelSlide = MathF.Min(target, _controlPanelSlide + step);
        else if (_controlPanelSlide > target)
            _controlPanelSlide = MathF.Max(target, _controlPanelSlide - step);
    }

    private void RenderTimelineToggle(int windowHeight)
    {
        float toggleY = windowHeight - UILayoutMetrics.StatusBarHeight - TimelineToggleSize - TimelineToggleMargin;
        if (_isTimelineVisible)
            toggleY -= UILayoutMetrics.TimelineBarHeight;

        ImGui.SetNextWindowPos(new Vector2(TimelineToggleMargin, toggleY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(TimelineToggleSize, TimelineToggleSize), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0f);

        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin("##TimelineToggle", flags))
        {
            Vector4 buttonColor = _isTimelineVisible ? Theme.AccentMuted : Theme.BgSurface;
            ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.FrameHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.FrameActive);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);

            if (ImGui.Button("##timeline_toggle_btn", new Vector2(TimelineToggleSize, TimelineToggleSize)))
                _isTimelineVisible = !_isTimelineVisible;

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_isTimelineVisible ? "Hide play bar" : "Show play bar");

            var drawList = ImGui.GetWindowDrawList();
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var center = (min + max) * 0.5f;
            float halfWidth = TimelineToggleSize * 0.16f;
            float halfHeight = TimelineToggleSize * 0.12f;
            uint iconColor = ImGui.ColorConvertFloat4ToU32(Theme.TextPrimary);

            Vector2 left;
            Vector2 middle;
            Vector2 right;

            if (_isTimelineVisible)
            {
                left = new Vector2(center.X - halfWidth, center.Y - halfHeight);
                middle = new Vector2(center.X, center.Y + halfHeight);
                right = new Vector2(center.X + halfWidth, center.Y - halfHeight);
            }
            else
            {
                left = new Vector2(center.X - halfWidth, center.Y + halfHeight);
                middle = new Vector2(center.X, center.Y - halfHeight);
                right = new Vector2(center.X + halfWidth, center.Y + halfHeight);
            }

            drawList.AddLine(left, middle, iconColor, 2f);
            drawList.AddLine(middle, right, iconColor, 2f);

            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
        }
        ImGui.End();
        ImGui.PopStyleVar();
    }

    private void RenderSimulationSection()
    {
        if (UIHelpers.SectionHeader(Icons.Gear, "Simulation"))
        {
            float fullWidth = ImGui.GetContentRegionAvail().X;

            // Grid size
            ImGui.SetNextItemWidth(fullWidth * 0.45f);
            if (ImGui.Combo("Grid Size", ref _selectedGridSizeIdx, GridSizes, GridSizes.Length))
            {
                int newSize = GridSizeValues[_selectedGridSizeIdx];
                _engine.SetGridSize(newSize);
                _renderer.SetGridSize(newSize);
                SyncDisplayRange();
            }

            // Rule preset
            ImGui.SetNextItemWidth(fullWidth * 0.65f);
            if (ImGui.Combo("Rule", ref _selectedRuleIdx, RuleNames, RuleNames.Length))
            {
                string key = RuleKeys[_selectedRuleIdx];
                _showCustomRule = key == "custom";
                if (!_showCustomRule)
                {
                    _engine.SetRule(key);
                    RecomputeGenerations();
                }
            }

            // Custom rule
            if (_showCustomRule)
            {
                UIHelpers.BeginGroup("custom_rule");
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
                ImGui.Text("Custom Rule Definition");
                ImGui.PopStyleColor();
                ImGui.SetNextItemWidth(fullWidth * 0.35f);
                ImGui.InputText("Birth", ref _customBirth, 9);
                ImGui.SetNextItemWidth(fullWidth * 0.35f);
                ImGui.InputText("Survival", ref _customSurvival, 9);
                if (UIHelpers.AccentButton("Apply"))
                {
                    var birth = _customBirth.Where(c => c >= '0' && c <= '8')
                        .Select(c => c - '0').Distinct().ToArray();
                    var survival = _customSurvival.Where(c => c >= '0' && c <= '8')
                        .Select(c => c - '0').Distinct().ToArray();
                    _engine.SetCustomRule(birth, survival);
                    RecomputeGenerations();
                }
                UIHelpers.EndGroup();
            }

            // Toroidal
            if (ImGui.Checkbox("Toroidal", ref _toroidal))
            {
                _engine.SetToroidal(_toroidal);
                RecomputeGenerations();
            }
            UIHelpers.Tooltip("Wrap grid edges so cells connect across boundaries");

            UIHelpers.ThinSeparator();

            // Generation count display
            UIHelpers.LabelValue("Generations:", _engine.GenerationCount.ToString());

            // Compute buttons
            int computeClicked = UIHelpers.ButtonRow(["+ 10", "+ 50", "+ 100"]);
            if (computeClicked >= 0)
            {
                int[] amounts = [10, 50, 100];
                _engine.ComputeGenerations(_engine.GenerationCount + amounts[computeClicked]);
                SyncDisplayRange();
            }

            UIHelpers.ThinSeparator();

            // Random init
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Random Seed");
            ImGui.PopStyleColor();
            ImGui.SetNextItemWidth(fullWidth - 70);
            ImGui.SliderFloat("##density", ref _randomDensity, 5f, 80f, "Density: %.0f%%");
            ImGui.SameLine();
            if (UIHelpers.AccentButton("Go"))
            {
                _engine.InitializeRandom(_randomDensity / 100f);
                _renderer.InvalidateState();
                SyncDisplayRange();
            }
        }
    }

    private void RenderStatsSection()
    {
        if (UIHelpers.SectionHeader(Icons.ChartBar, "Statistics", defaultOpen: false))
        {
            // Rebuild population array if generation count changed
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
                float current = _populationData.Length > 0 ? _populationData[^1] : 0;
                float min = _populationData.Min();
                float max = _populationData.Max();
                float avg = _populationData.Average();

                UIHelpers.LabelValue("Current:", ((int)current).ToString());
                UIHelpers.LabelValue("Min / Max:", $"{(int)min} / {(int)max}");
                UIHelpers.LabelValue("Average:", $"{avg:F0}");

                float fullWidth = ImGui.GetContentRegionAvail().X;
                ImGui.PlotLines("##pop", ref _populationData[0], _populationData.Length,
                    0, $"Population ({_populationData.Length} gens)",
                    min * 0.9f, max * 1.1f, new Vector2(fullWidth, 60));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                ImGui.Text("No generations computed.");
                ImGui.PopStyleColor();
            }
        }
    }

    private void RenderPatternSection()
    {
        if (!UIHelpers.SectionHeader(Icons.Grid, "Patterns"))
            return;

        float fullWidth = ImGui.GetContentRegionAvail().X;

        // ── Recently used (pinned above the search UI) ──
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

        // ── Search + filters ──
        ImGui.SetNextItemWidth(fullWidth);
        ImGui.InputTextWithHint("##pattern-search", "Search patterns...", ref _patternSearch, 64);

        // Category dropdown (All + unique categories from library)
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
        ImGui.SliderInt("##pat-period-min", ref _patternPeriodMin, 0, 32, "Period ≥ %d");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(fullWidth * 0.48f);
        ImGui.SliderInt("##pat-period-max", ref _patternPeriodMax, 0, 32, "Period ≤ %d");
        if (_patternPeriodMin > _patternPeriodMax)
            _patternPeriodMax = _patternPeriodMin;

        ImGui.SetNextItemWidth(fullWidth);
        ImGui.SliderInt("##pat-max-size", ref _patternMaxSize, 3, 200, "Max size: %d cells");

        UIHelpers.ThinSeparator();

        // ── Results list ──
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
                    ? $"{p.Name}  ({p.Width}×{p.Height}, p{p.Period})"
                    : $"{p.Name}  ({p.Width}×{p.Height})";

                if (ImGui.Selectable($"{label}##sel-{p.Id}", isSelected))
                    _selectedPatternId = p.Id;

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    LoadPattern(p.Id);
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();

        // ── Preview + Load ──
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
    }

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

    private void RenderVisualSection()
    {
        if (UIHelpers.SectionHeader(Icons.Palette, "Appearance"))
        {
            var settings = _renderer.Settings;
            float fullWidth = ImGui.GetContentRegionAvail().X;

            RenderFontSizeControls(fullWidth);
            UIHelpers.ThinSeparator();

            // ── Geometry ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Geometry");
            ImGui.PopStyleColor();

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##padding", ref _cellPadding, 0f, 50f, "Cell Padding: %.0f%%"))
            {
                settings.CellPadding = _cellPadding / 100f;
                _renderer.InvalidateState();
            }

            if (ImGui.Checkbox("Wireframe", ref _showWireframe))
                settings.ShowWireframe = _showWireframe;

            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Floor");
            ImGui.PopStyleColor();
            string[] floorModes = ["Off", "Grid Lines", "Reflective"];
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.Combo("##floormode", ref _floorModeIdx, floorModes, floorModes.Length))
                settings.FloorMode = (FloorMode)_floorModeIdx;
            UIHelpers.Tooltip("Off — no floor; Grid Lines — classic grid; Reflective — animated water surface that reflects the cubes.");

            if (settings.FloorMode == FloorMode.Reflective)
            {
                RenderReflectiveFloorControls(settings, fullWidth);
            }

            if (ImGui.Checkbox("Generation Labels", ref _showGenerationLabels))
                settings.ShowGenerationLabels = _showGenerationLabels;

            if (ImGui.Combo("Cell Shape", ref _shape, ShapeNames, ShapeNames.Length))
                settings.Shape = (CellShape)_shape;

            UIHelpers.ThinSeparator();

            // ── Colors ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Colors");
            ImGui.PopStyleColor();

            if (ImGui.Checkbox("Face Color Cycling", ref _faceColorCycling))
                settings.FaceColorCycling = _faceColorCycling;
            UIHelpers.Tooltip("Animate face colors based on generation using a gradient");

            if (_faceColorCycling)
            {
                RenderGradientEditor(settings, fullWidth);
            }
            else
            {
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.ColorEdit3("##cellcolor", ref _cellColor))
                    settings.CellColor = _cellColor;
            }

            if (_showWireframe)
            {
                if (ImGui.Checkbox("Edge Color Cycling", ref _edgeColorCycling))
                    settings.EdgeColorCycling = _edgeColorCycling;
                UIHelpers.Tooltip("Animate edge colors with hue rotation");

                if (_edgeColorCycling)
                {
                    ImGui.SetNextItemWidth(fullWidth);
                    if (ImGui.SliderFloat("##hue", ref _edgeColorAngle, 0f, 360f, "Hue Offset: %.0f\u00B0"))
                        settings.EdgeColorAngle = _edgeColorAngle;
                    UIHelpers.Tooltip("Rotates the face gradient hue (in HSL) to derive wireframe colors. Dark stops are auto-brightened to keep wires visible.");
                }
                else
                {
                    ImGui.SetNextItemWidth(fullWidth);
                    if (ImGui.ColorEdit3("##edgecolor", ref _edgeColor))
                        settings.EdgeColor = _edgeColor;
                }
            }

            UIHelpers.ThinSeparator();

            // ── Fog ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Depth Fog");
            ImGui.PopStyleColor();

            if (ImGui.Checkbox("Enable Fog", ref _fogEnabled))
                settings.FogEnabled = _fogEnabled;
            UIHelpers.Tooltip("Fade distant cubes to the background color for better depth perception");

            if (_fogEnabled)
            {
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.SliderFloat("##fogstart", ref _fogStart, 1f, 200f, "Start: %.0f"))
                    settings.FogStart = _fogStart;

                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.SliderFloat("##fogend", ref _fogEnd, 10f, 500f, "End: %.0f"))
                    settings.FogEnd = _fogEnd;

                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.ColorEdit3("##fogcolor", ref _fogColor))
                    settings.FogColor = _fogColor;
            }

            UIHelpers.ThinSeparator();

            // ── Cross-Section ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Cross-Section");
            ImGui.PopStyleColor();

            if (ImGui.Checkbox("Enable Clip Plane", ref _clipEnabled))
                settings.ClipEnabled = _clipEnabled;
            UIHelpers.Tooltip("Clip cells above a Y threshold to see inside dense structures");

            if (_clipEnabled)
            {
                float maxY = Math.Max(_engine.GenerationCount, 1);
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.SliderFloat("##clipy", ref _clipY, 0f, maxY, "Clip Y: %.0f"))
                    settings.ClipY = _clipY;
            }

            UIHelpers.ThinSeparator();

            // ── Background ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Background");
            ImGui.PopStyleColor();

            string[] bgModes = ["Solid", "Gradient", "Starfield"];
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.Combo("##bgmode", ref _backgroundMode, bgModes, bgModes.Length))
                settings.BackgroundMode = (BackgroundMode)_backgroundMode;

            if (_backgroundMode > 0)
            {
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.ColorEdit3("##bgtop", ref _bgTopColor))
                    settings.BackgroundTopColor = _bgTopColor;
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.ColorEdit3("##bgbottom", ref _bgBottomColor))
                    settings.BackgroundBottomColor = _bgBottomColor;
            }

            UIHelpers.ThinSeparator();

            // ── Bloom ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Bloom");
            ImGui.PopStyleColor();

            if (ImGui.Checkbox("Enable Bloom", ref _bloomEnabled))
                settings.BloomEnabled = _bloomEnabled;
            UIHelpers.Tooltip("Makes bright color-cycling areas glow");

            if (_bloomEnabled)
            {
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.SliderFloat("##bloomthresh", ref _bloomThreshold, 0.1f, 1.5f, "Threshold: %.2f"))
                    settings.BloomThreshold = _bloomThreshold;

                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.SliderFloat("##bloomintensity", ref _bloomIntensity, 0.1f, 2.0f, "Intensity: %.2f"))
                    settings.BloomIntensity = _bloomIntensity;
            }
        }
    }

    /// <summary>
    /// Sub-controls shown when the reflective floor is active. Tucked into a
    /// collapsible tree node so the Appearance section doesn't grow noisy for
    /// users who just want the default water look.
    /// </summary>
    private void RenderReflectiveFloorControls(RenderSettings settings, float fullWidth)
    {
        ImGui.Indent();
        if (ImGui.TreeNodeEx("Water Tuning", ImGuiTreeNodeFlags.None))
        {
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##wavestr", ref _waveStrength, 0f, 1f, "Wave Strength: %.2f"))
                settings.WaveStrength = _waveStrength;
            UIHelpers.Tooltip("0 = perfect mirror; higher values give choppier ripples.");

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##wavespeed", ref _waveSpeed, 0f, 2f, "Wave Speed: %.2f"))
                settings.WaveSpeed = _waveSpeed;

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##refl", ref _reflectivity, 0f, 1f, "Reflectivity: %.2f"))
                settings.Reflectivity = _reflectivity;
            UIHelpers.Tooltip("Schlick F0: how much the surface reflects when looked at straight-on. Glancing angles always reflect strongly.");

            // Just the color swatch — clicking it pops the full picker.
            // The inline R/G/B numeric inputs ColorEdit3 shows by default
            // crowd the panel and the picker popup falls off-screen.
            if (ImGui.ColorEdit3("Water Tint", ref _waterTint, ImGuiColorEditFlags.NoInputs))
                settings.WaterTint = _waterTint;
            UIHelpers.Tooltip("Base water color blended underneath the reflection.");

            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderFloat("##reflres", ref _reflectionResolutionScale, 0.25f, 1f, "Reflection Resolution: %.2fx"))
            {
                settings.ReflectionResolutionScale = _reflectionResolutionScale;
            }
            UIHelpers.Tooltip("Reflection texture size relative to the main view. Lower is faster but blurrier.");

            ImGui.TreePop();
        }
        ImGui.Unindent();
    }

    /// <summary>
    /// Editor for the user-selectable face-cycling gradient. Source of truth is
    /// <see cref="RenderSettings.GradientStops"/>; we mutate it in place via
    /// <see cref="CollectionsMarshal.AsSpan"/> so the renderer picks up edits the
    /// next frame without a copy-back step.
    /// </summary>
    private void RenderGradientEditor(RenderSettings settings, float fullWidth)
    {
        ImGui.Spacing();

        // ── Preset combo ──
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
        UIHelpers.Tooltip("Choose a built-in palette. Editing any stop switches to Custom.");

        // ── Live preview strip ──
        DrawGradientPreview(settings.GradientStops, fullWidth);

        // ── Per-stop pickers + remove ──
        var span = CollectionsMarshal.AsSpan(settings.GradientStops);
        int removeIdx = -1;
        bool canRemove = settings.GradientStops.Count > RenderSettings.MinGradientStops;

        for (int i = 0; i < settings.GradientStops.Count; i++)
        {
            ImGui.PushID(i);

            // Color square + dropdown picker. NoLabel/NoInputs keep the row compact.
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
            UIHelpers.Tooltip(canRemove
                ? "Remove this stop"
                : $"Minimum {RenderSettings.MinGradientStops} stops required");

            ImGui.PopID();
        }

        if (removeIdx >= 0)
        {
            settings.GradientStops.RemoveAt(removeIdx);
            _gradientPreset = GradientPresets.Match(settings.GradientStops);
        }

        // ── Add Stop / Reset row ──
        bool canAdd = settings.GradientStops.Count < RenderSettings.MaxGradientStops;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float halfWidth = (fullWidth - spacing) * 0.5f;

        if (!canAdd) ImGui.BeginDisabled();
        if (ImGui.Button("+ Add Stop", new Vector2(halfWidth, 0)))
        {
            // Duplicate the last color so the new stop blends seamlessly until edited.
            var last = settings.GradientStops[^1];
            settings.GradientStops.Add(last);
            _gradientPreset = GradientPresets.Match(settings.GradientStops);
        }
        if (!canAdd) ImGui.EndDisabled();
        UIHelpers.Tooltip(canAdd
            ? "Append a new color stop (max 8)"
            : $"Maximum {RenderSettings.MaxGradientStops} stops reached");

        ImGui.SameLine();
        if (ImGui.Button("Reset", new Vector2(halfWidth, 0)))
        {
            settings.ResetGradient();
            _gradientPreset = GradientPresets.Match(settings.GradientStops);
        }
        UIHelpers.Tooltip("Restore the default Classic palette");
    }

    /// <summary>
    /// Draws a 1D preview of the cyclic gradient as a strip of multi-color rects.
    /// One extra segment from the last stop back to the first is appended so the
    /// wrap point is visible.
    /// </summary>
    private static void DrawGradientPreview(IReadOnlyList<Vector3> stops, float width)
    {
        if (stops.Count < 2) return;

        float height = MathF.Max(10f, ImGui.GetTextLineHeight() * 0.9f);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();

        int segments = stops.Count; // includes wrap segment
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

        // Subtle border so the strip reads as one element.
        drawList.AddRect(origin,
            new Vector2(origin.X + width, origin.Y + height),
            Theme.BorderU32, 2f);

        ImGui.Dummy(new Vector2(width, height + 4f));
    }

    private void RenderFontSizeControls(float fullWidth)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text("UI");
        ImGui.PopStyleColor();

        string mode = _isFontSizeAutomatic ? "auto" : "custom";
        UIHelpers.LabelValue("Font Size:", $"{_fontSize:F0}px ({mode})");

        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = (fullWidth - spacing * 2f) / 3f;

        bool atMin = _fontSize <= UiSettingsState.MinFontSize + 0.001f;
        if (atMin) ImGui.BeginDisabled();
        if (ImGui.Button("-##font_size", new Vector2(buttonWidth, 0)))
            OnFontSizeChanged?.Invoke(_fontSize - UiSettingsState.FontSizeStep);
        if (atMin) ImGui.EndDisabled();
        UIHelpers.Tooltip("Decrease UI font size");

        ImGui.SameLine();
        bool atMax = _fontSize >= UiSettingsState.MaxFontSize - 0.001f;
        if (atMax) ImGui.BeginDisabled();
        if (ImGui.Button("+##font_size", new Vector2(buttonWidth, 0)))
            OnFontSizeChanged?.Invoke(_fontSize + UiSettingsState.FontSizeStep);
        if (atMax) ImGui.EndDisabled();
        UIHelpers.Tooltip("Increase UI font size");

        ImGui.SameLine();
        if (_isFontSizeAutomatic) ImGui.BeginDisabled();
        if (ImGui.Button("Auto##font_size", new Vector2(buttonWidth, 0)))
            OnFontSizeReset?.Invoke();
        if (_isFontSizeAutomatic) ImGui.EndDisabled();
        UIHelpers.Tooltip("Reset to resolution-based automatic font size");

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        ImGui.TextWrapped($"Auto for this resolution: {_automaticFontSize:F0}px");
        ImGui.PopStyleColor();
    }

    private void RenderEditingSection()
    {
        if (_editController == null) return;

        if (UIHelpers.SectionHeader(Icons.Pencil, "Editing", defaultOpen: false))
        {
            float fullWidth = ImGui.GetContentRegionAvail().X;

            bool isActive = _editController.IsActive;
            if (isActive)
            {
                if (ImGui.Button("Exit Edit Mode", new Vector2(fullWidth, 0)))
                    _editController.Deactivate();
            }
            else
            {
                bool canEdit = !_isPlaying && _displayStart == 0;
                if (!canEdit) ImGui.BeginDisabled();
                if (UIHelpers.AccentButton("Enter Edit Mode", new Vector2(fullWidth, 0)))
                    _editController.TryActivate(_isPlaying, _displayStart, _engine.GridSize);
                if (!canEdit)
                {
                    ImGui.EndDisabled();
                    ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                    ImGui.Text("Pause & view gen 0 to edit");
                    ImGui.PopStyleColor();
                }
            }

            if (isActive)
            {
                UIHelpers.ThinSeparator();

                // Tool selector
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
                ImGui.Text("Tool");
                ImGui.PopStyleColor();

                string[] tools = ["Toggle", "Draw", "Erase"];
                int currentTool = (int)_editController.CurrentTool;
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.Combo("##edittool", ref currentTool, tools, tools.Length))
                    _editController.CurrentTool = (EditTool)currentTool;

                // Brush size
                int brushSize = _editController.BrushSize;
                ImGui.SetNextItemWidth(fullWidth);
                if (ImGui.SliderInt("##brushsize", ref brushSize, 1, 10, "Brush: %d"))
                    _editController.BrushSize = brushSize;

                // Rotation
                if (ImGui.Button($"Rotate ({_editController.PatternRotation}\u00B0)", new Vector2(fullWidth, 0)))
                    _editController.RotatePattern();

                UIHelpers.ThinSeparator();
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                ImGui.Text("E=Toggle  [/]=Size  R=Rotate");
                ImGui.PopStyleColor();
            }
        }
    }

    private void RenderFileSection()
    {
        if (UIHelpers.SectionHeader(Icons.FloppyDisk, "File", defaultOpen: false))
        {
            float fullWidth = ImGui.GetContentRegionAvail().X;
            float btnWidth = (fullWidth - ImGui.GetStyle().ItemSpacing.X) * 0.5f;

            if (ImGui.Button("Load Pattern", new Vector2(btnWidth, 0)))
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
            UIHelpers.Tooltip("Load a pattern from an RLE file");

            ImGui.SameLine();
            if (ImGui.Button("Load Session", new Vector2(btnWidth, 0)))
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
            UIHelpers.Tooltip("Load a previously saved session (JSON)");

            if (UIHelpers.AccentButton("Save Session", new Vector2(fullWidth, 0)))
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
            UIHelpers.Tooltip("Save current session to a JSON file");

            UIHelpers.ThinSeparator();

            // Screenshot
            if (ImGui.Button("Screenshot (F12)", new Vector2(fullWidth, 0)))
                OnScreenshotRequested?.Invoke();
            UIHelpers.Tooltip("Save the current view as a PNG to your Desktop");

            // Video recording
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Record Video");
            ImGui.PopStyleColor();

            int duration = RecordingDurationSeconds;
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.SliderInt("##rec_duration", ref duration, 1, 120, "Duration: %d s"))
                RecordingDurationSeconds = Math.Clamp(duration, 1, 120);

            string[] codecLabels = ["H.264 MP4", "VP9 WebM"];
            int codecIdx = RecordingCodec == VideoCodec.H264Mp4 ? 0 : 1;
            ImGui.SetNextItemWidth(fullWidth);
            if (ImGui.Combo("##rec_codec", ref codecIdx, codecLabels, codecLabels.Length))
                RecordingCodec = codecIdx == 0 ? VideoCodec.H264Mp4 : VideoCodec.Vp9Webm;

            if (IsRecording)
            {
                ImGui.ProgressBar((float)RecordingProgress01, new Vector2(fullWidth, 0),
                    $"Recording {RecordingProgress01 * RecordingDurationSeconds:F1} / {RecordingDurationSeconds} s");
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
                ImGui.TextWrapped("Press Ctrl+R to start recording");
                ImGui.PopStyleColor();
            }

            if (!string.IsNullOrEmpty(RecordingStatusMessage))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
                ImGui.TextWrapped(RecordingStatusMessage);
                ImGui.PopStyleColor();
            }

            UIHelpers.ThinSeparator();

            // Export
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("3D Export");
            ImGui.PopStyleColor();

            if (ImGui.Button("Export STL", new Vector2(btnWidth, 0)))
            {
                var path = FileDialogHelper.SaveFile("stl");
                if (path != null)
                    OnExportSTL?.Invoke(path);
            }
            UIHelpers.Tooltip("Export visible cubes as binary STL for 3D printing");

            ImGui.SameLine();
            if (ImGui.Button("Export OBJ", new Vector2(btnWidth, 0)))
            {
                var path = FileDialogHelper.SaveFile("obj");
                if (path != null)
                    OnExportOBJ?.Invoke(path);
            }
            UIHelpers.Tooltip("Export visible cubes as OBJ for Blender/etc.");

            UIHelpers.ThinSeparator();

            // Pattern Export
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Pattern Export");
            ImGui.PopStyleColor();

            if (ImGui.Button("Export RLE", new Vector2(btnWidth, 0)))
            {
                var path = FileDialogHelper.SaveFile("rle");
                if (path != null)
                    OnExportRLE?.Invoke(path);
            }
            UIHelpers.Tooltip("Export generation 0 pattern as RLE file");
        }
    }

    private void RenderCameraSection()
    {
        if (UIHelpers.SectionHeader(Icons.Camera, "Camera", defaultOpen: false))
        {
            if (ImGui.Button("Reset Camera", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                if (_camera.IsFlythroughActive)
                    _camera.StopFlythrough();
                _camera.Reset();
            }

            ImGui.Spacing();
            UIHelpers.BeginGroup("camera_help");
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
            UIHelpers.LabelValue("  F12", "Screenshot");
            UIHelpers.LabelValue("  E", "Toggle Edit");
            UIHelpers.LabelValue("  Esc", "Exit Edit");
            UIHelpers.EndGroup();
        }
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
    }
}
