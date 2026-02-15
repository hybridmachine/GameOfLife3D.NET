using System.Numerics;
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
    private readonly EditingController? _editController;
    private readonly TimelineBar _timeline;
    private readonly StatusBar _statusBar;
    private readonly float _dpiScale;

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
    private bool _showGridLines;
    private bool _showGenerationLabels;
    private bool _showWireframe;
    private bool _toroidal;
    private float _randomDensity = 0.3f;

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

    // Beveled cubes
    private bool _useBeveledCubes;

    // Population stats
    private float[] _populationData = [];
    private int _lastPopulationGenCount;

    // Display state
    private int _displayStart;
    private int _displayEnd;

    // Animation
    private bool _isPlaying;
    private float _animationSpeed = 200f;
    private double _lastAnimationTime;

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

    // Screenshot callback
    public Action? OnScreenshotRequested { get; set; }

    // Export callbacks
    public Action<string>? OnExportSTL { get; set; }
    public Action<string>? OnExportOBJ { get; set; }

    public ImGuiUI(GameEngine engine, Renderer3D renderer, CameraController camera, PatternLoader patternLoader, EditingController? editController = null, float dpiScale = 1.0f)
    {
        _engine = engine;
        _renderer = renderer;
        _camera = camera;
        _patternLoader = patternLoader;
        _editController = editController;
        _dpiScale = dpiScale;
        _timeline = new TimelineBar(dpiScale);
        _statusBar = new StatusBar(dpiScale);

        // Sync initial state from render settings
        var settings = renderer.Settings;
        _cellPadding = settings.CellPadding * 100f;
        _cellColor = settings.CellColor;
        _edgeColor = settings.EdgeColor;
        _faceColorCycling = settings.FaceColorCycling;
        _edgeColorCycling = settings.EdgeColorCycling;
        _edgeColorAngle = settings.EdgeColorAngle;
        _showGridLines = settings.ShowGridLines;
        _showGenerationLabels = settings.ShowGenerationLabels;
        _showWireframe = settings.ShowWireframe;

        _timeline.RangeChanged += OnRangeChanged;
        _timeline.PlayToggled += playing => _isPlaying = playing;
        _timeline.ResetRequested += OnReset;
    }

    public void TogglePlayPause()
    {
        _isPlaying = !_isPlaying;
        _timeline.SetPlaying(_isPlaying);
    }

    public void Tick(double currentTime)
    {
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

    private void OnRangeChanged(int start, int end)
    {
        _displayStart = start;
        _displayEnd = end;
    }

    private void OnReset()
    {
        _isPlaying = false;
        _timeline.SetPlaying(false);
        _engine.Clear();
        _engine.SetRule("conway");
        _engine.SetToroidal(false);
        _selectedRuleIdx = 0;
        _toroidal = false;
        _showCustomRule = false;

        var pattern = _patternLoader.GetBuiltInPattern("r-pentomino");
        if (pattern != null)
            _engine.InitializeFromPattern(pattern);

        SyncDisplayRange();
    }

    public void Render(int windowWidth, int windowHeight)
    {
        RenderControlPanel();
        _timeline.Render(windowWidth, windowHeight);
        _statusBar.ShowEditBadge = _editController?.IsActive ?? false;
        _statusBar.Render(_displayStart, _displayEnd, _engine.RuleString,
            _renderer.GetVisibleCellCount(), windowWidth, windowHeight);
    }

    private void RenderControlPanel()
    {
        float s = _dpiScale;
        ImGui.SetNextWindowPos(new Vector2(10 * s, 10 * s), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(310 * s, 620 * s), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(260 * s, 200 * s), new Vector2(450 * s, 2000 * s));

        if (ImGui.Begin("Game of Life 3D", ImGuiWindowFlags.NoCollapse))
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

    private void RenderSimulationSection()
    {
        if (UIHelpers.SectionHeader("\u2699", "Simulation"))
        {
            float s = _dpiScale;
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
            ImGui.SetNextItemWidth(fullWidth - 70 * s);
            ImGui.SliderFloat("##density", ref _randomDensity, 0.05f, 0.8f, "Density: %.0f%%");
            ImGui.SameLine();
            if (UIHelpers.AccentButton("Go"))
            {
                _engine.InitializeRandom(_randomDensity);
                SyncDisplayRange();
            }
        }
    }

    private void RenderStatsSection()
    {
        if (UIHelpers.SectionHeader("\u2261", "Statistics", defaultOpen: false))
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
                    min * 0.9f, max * 1.1f, new Vector2(fullWidth, 60 * _dpiScale));
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
        if (UIHelpers.SectionHeader("\u25A6", "Patterns"))
        {
            float fullWidth = ImGui.GetContentRegionAvail().X;
            float spacing = ImGui.GetStyle().ItemSpacing.X;

            // Render pattern buttons flowing across available width
            float currentX = 0;
            foreach (var kvp in _patternLoader.GetBuiltInPatternMap())
            {
                float btnWidth = ImGui.CalcTextSize(kvp.Value.Name).X + ImGui.GetStyle().FramePadding.X * 2;

                // Wrap to next line if this button wouldn't fit
                if (currentX > 0 && currentX + btnWidth > fullWidth)
                {
                    currentX = 0;
                }
                else if (currentX > 0)
                {
                    ImGui.SameLine();
                }

                if (ImGui.Button(kvp.Value.Name))
                {
                    _engine.InitializeFromPattern(kvp.Value.Pattern);
                    SyncDisplayRange();
                }
                UIHelpers.Tooltip(kvp.Value.Description);

                currentX += btnWidth + spacing;
            }
        }
    }

    private void RenderVisualSection()
    {
        if (UIHelpers.SectionHeader("\u25C9", "Appearance"))
        {
            var settings = _renderer.Settings;
            float fullWidth = ImGui.GetContentRegionAvail().X;

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

            if (ImGui.Checkbox("Grid Lines", ref _showGridLines))
                settings.ShowGridLines = _showGridLines;

            if (ImGui.Checkbox("Generation Labels", ref _showGenerationLabels))
                settings.ShowGenerationLabels = _showGenerationLabels;

            if (ImGui.Checkbox("Rounded Cubes", ref _useBeveledCubes))
                settings.UseBeveledCubes = _useBeveledCubes;

            UIHelpers.ThinSeparator();

            // ── Colors ──
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
            ImGui.Text("Colors");
            ImGui.PopStyleColor();

            if (ImGui.Checkbox("Face Color Cycling", ref _faceColorCycling))
                settings.FaceColorCycling = _faceColorCycling;
            UIHelpers.Tooltip("Animate face colors based on generation using a gradient");

            if (!_faceColorCycling)
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

    private void RenderEditingSection()
    {
        if (_editController == null) return;

        if (UIHelpers.SectionHeader("\u270E", "Editing", defaultOpen: false))
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
                    _editController.TryActivate(_isPlaying, _displayStart);
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
        if (UIHelpers.SectionHeader("\u25C6", "File", defaultOpen: false))
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
        }
    }

    private void RenderCameraSection()
    {
        if (UIHelpers.SectionHeader("\u25CE", "Camera", defaultOpen: false))
        {
            if (ImGui.Button("Reset Camera", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                _camera.Reset();

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
            UIHelpers.LabelValue("  RF", "Up / Down");
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
        _showGridLines = s.ShowGridLines;
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
        _useBeveledCubes = s.UseBeveledCubes;
    }
}
