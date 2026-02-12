using System.Numerics;
using GameOfLife3D.NET.Camera;
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

    public ImGuiUI(GameEngine engine, Renderer3D renderer, CameraController camera, PatternLoader patternLoader, float dpiScale = 1.0f)
    {
        _engine = engine;
        _renderer = renderer;
        _camera = camera;
        _patternLoader = patternLoader;
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
            RenderPatternSection();
            ImGui.Spacing();
            RenderVisualSection();
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
        }
    }

    private void RenderFileSection()
    {
        if (UIHelpers.SectionHeader("\u2B29", "File", defaultOpen: false))
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
        }
    }

    private void RenderCameraSection()
    {
        if (UIHelpers.SectionHeader("\u29BE", "Camera", defaultOpen: false))
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
    }
}
