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
        ImGui.SetNextWindowSize(new Vector2(300 * s, 600 * s), ImGuiCond.FirstUseEver);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.08f, 0.08f, 0.12f, 0.95f));

        if (ImGui.Begin("Controls"))
        {
            RenderSimulationSection();
            ImGui.Separator();
            RenderPatternSection();
            ImGui.Separator();
            RenderVisualSection();
            ImGui.Separator();
            RenderFileSection();
            ImGui.Separator();
            RenderCameraSection();
        }
        ImGui.End();
        ImGui.PopStyleColor();
    }

    private void RenderSimulationSection()
    {
        if (ImGui.CollapsingHeader("Simulation", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Grid size
            float s = _dpiScale;
            ImGui.SetNextItemWidth(120 * s);
            if (ImGui.Combo("Grid Size", ref _selectedGridSizeIdx, GridSizes, GridSizes.Length))
            {
                int newSize = GridSizeValues[_selectedGridSizeIdx];
                _engine.SetGridSize(newSize);
                _renderer.SetGridSize(newSize);
                SyncDisplayRange();
            }

            // Rule preset
            ImGui.SetNextItemWidth(180 * s);
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
                ImGui.SetNextItemWidth(80 * s);
                ImGui.InputText("Birth", ref _customBirth, 9);
                ImGui.SetNextItemWidth(80 * s);
                ImGui.InputText("Survival", ref _customSurvival, 9);
                if (ImGui.Button("Apply Custom Rule"))
                {
                    var birth = _customBirth.Where(c => c >= '0' && c <= '8')
                        .Select(c => c - '0').Distinct().ToArray();
                    var survival = _customSurvival.Where(c => c >= '0' && c <= '8')
                        .Select(c => c - '0').Distinct().ToArray();
                    _engine.SetCustomRule(birth, survival);
                    RecomputeGenerations();
                }
            }

            // Toroidal
            if (ImGui.Checkbox("Toroidal", ref _toroidal))
            {
                _engine.SetToroidal(_toroidal);
                RecomputeGenerations();
            }

            // Generations
            ImGui.Spacing();
            ImGui.Text($"Generations: {_engine.GenerationCount}");

            if (ImGui.Button("Compute 10"))
            {
                _engine.ComputeGenerations(_engine.GenerationCount + 10);
                SyncDisplayRange();
            }
            ImGui.SameLine();
            if (ImGui.Button("Compute 50"))
            {
                _engine.ComputeGenerations(_engine.GenerationCount + 50);
                SyncDisplayRange();
            }
            ImGui.SameLine();
            if (ImGui.Button("Compute 100"))
            {
                _engine.ComputeGenerations(_engine.GenerationCount + 100);
                SyncDisplayRange();
            }

            // Random init
            ImGui.SetNextItemWidth(120 * s);
            ImGui.SliderFloat("Density", ref _randomDensity, 0.05f, 0.8f, "%.2f");
            ImGui.SameLine();
            if (ImGui.Button("Random"))
            {
                _engine.InitializeRandom(_randomDensity);
                SyncDisplayRange();
            }
        }
    }

    private void RenderPatternSection()
    {
        if (ImGui.CollapsingHeader("Patterns", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var kvp in _patternLoader.GetBuiltInPatternMap())
            {
                if (ImGui.Button(kvp.Value.Name))
                {
                    _engine.InitializeFromPattern(kvp.Value.Pattern);
                    SyncDisplayRange();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(kvp.Value.Description);
                ImGui.SameLine();
            }
            ImGui.NewLine();
        }
    }

    private void RenderVisualSection()
    {
        if (ImGui.CollapsingHeader("Visual Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var settings = _renderer.Settings;

            // Cell padding
            if (ImGui.SliderFloat("Padding", ref _cellPadding, 0f, 50f, "%.0f%%"))
            {
                settings.CellPadding = _cellPadding / 100f;
                _renderer.InvalidateState();
            }

            // Face color cycling
            if (ImGui.Checkbox("Face Color Cycling", ref _faceColorCycling))
                settings.FaceColorCycling = _faceColorCycling;

            // Solid color (when cycling off)
            if (!_faceColorCycling)
            {
                if (ImGui.ColorEdit3("Cell Color", ref _cellColor))
                    settings.CellColor = _cellColor;
            }

            // Edge/wireframe
            if (ImGui.Checkbox("Wireframe", ref _showWireframe))
                settings.ShowWireframe = _showWireframe;

            if (_showWireframe)
            {
                if (ImGui.Checkbox("Edge Color Cycling", ref _edgeColorCycling))
                    settings.EdgeColorCycling = _edgeColorCycling;

                if (_edgeColorCycling)
                {
                    if (ImGui.SliderFloat("Hue Angle", ref _edgeColorAngle, 0f, 360f, "%.0f"))
                        settings.EdgeColorAngle = _edgeColorAngle;
                }
                else
                {
                    if (ImGui.ColorEdit3("Edge Color", ref _edgeColor))
                        settings.EdgeColor = _edgeColor;
                }
            }

            // Grid lines
            if (ImGui.Checkbox("Grid Lines", ref _showGridLines))
                settings.ShowGridLines = _showGridLines;

            // Generation labels
            if (ImGui.Checkbox("Generation Labels", ref _showGenerationLabels))
                settings.ShowGenerationLabels = _showGenerationLabels;
        }
    }

    private void RenderFileSection()
    {
        if (ImGui.CollapsingHeader("File"))
        {
            if (ImGui.Button("Load Pattern (RLE)"))
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

            if (ImGui.Button("Save Session"))
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

            if (ImGui.Button("Load Session"))
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
        }
    }

    private void RenderCameraSection()
    {
        if (ImGui.CollapsingHeader("Camera"))
        {
            if (ImGui.Button("Reset Camera"))
                _camera.Reset();

            ImGui.TextWrapped("LMB: Orbit | RMB: Pan | Scroll: Zoom");
            ImGui.TextWrapped("WASD: Move | QE: Rotate | RF: Up/Down");
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
