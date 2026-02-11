using ImGuiNET;
using System.Numerics;

namespace GameOfLife3D.NET.UI;

public sealed class TimelineBar
{
    private static readonly string[] Speeds = ["0.25x", "0.5x", "1x", "2x", "4x", "8x"];
    private static readonly float[] SpeedValues = [0.25f, 0.5f, 1f, 2f, 4f, 8f];

    private readonly float _dpiScale;
    private int _startGeneration;
    private int _endGeneration;
    private int _totalGenerations;
    private bool _isPlaying;
    private float _speedMultiplier = 1f;

    public int StartGeneration => _startGeneration;
    public int EndGeneration => _endGeneration;
    public bool IsPlaying => _isPlaying;
    public float SpeedMultiplier => _speedMultiplier;

    public event Action<int, int>? RangeChanged;
    public event Action<bool>? PlayToggled;
    public event Action? ResetRequested;

    public TimelineBar(float dpiScale = 1.0f)
    {
        _dpiScale = dpiScale;
    }

    public void SetTotalGenerations(int total)
    {
        _totalGenerations = Math.Max(0, total);
        int max = Math.Max(0, _totalGenerations - 1);
        _startGeneration = Math.Min(_startGeneration, max);
        _endGeneration = Math.Min(_endGeneration, max);
    }

    public void SetRange(int start, int end)
    {
        int max = Math.Max(0, _totalGenerations - 1);
        _startGeneration = Math.Clamp(start, 0, max);
        _endGeneration = Math.Clamp(end, 0, max);
        if (_startGeneration > _endGeneration)
            (_startGeneration, _endGeneration) = (_endGeneration, _startGeneration);
    }

    public void SetPlaying(bool playing) => _isPlaying = playing;

    public void SetEndGeneration(int gen)
    {
        int max = Math.Max(0, _totalGenerations - 1);
        _endGeneration = Math.Clamp(gen, _startGeneration, max);
    }

    public void Render(int windowWidth, int windowHeight)
    {
        float s = _dpiScale;
        float barHeight = 60f * s;
        float barY = windowHeight - barHeight - 30f * s; // Above status bar

        ImGui.SetNextWindowPos(new Vector2(0, barY));
        ImGui.SetNextWindowSize(new Vector2(windowWidth, barHeight));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10 * s, 5 * s));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 0.9f));

        if (ImGui.Begin("Timeline", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings))
        {
            // Transport controls
            if (ImGui.Button("|<")) SeekEnd(0);
            ImGui.SameLine();
            if (ImGui.Button("<")) SeekEnd(_endGeneration - 1);
            ImGui.SameLine();

            string playLabel = _isPlaying ? "Pause" : "Play";
            if (ImGui.Button(playLabel))
            {
                _isPlaying = !_isPlaying;
                PlayToggled?.Invoke(_isPlaying);
            }
            ImGui.SameLine();
            if (ImGui.Button(">")) SeekEnd(_endGeneration + 1);
            ImGui.SameLine();
            if (ImGui.Button(">|")) SeekEnd(Math.Max(0, _totalGenerations - 1));
            ImGui.SameLine();
            if (ImGui.Button("Reset")) ResetRequested?.Invoke();
            ImGui.SameLine();

            // Speed selector
            ImGui.SetNextItemWidth(60 * s);
            int currentIdx = Array.IndexOf(SpeedValues, _speedMultiplier);
            if (currentIdx < 0) currentIdx = 2; // default 1x
            if (ImGui.Combo("##speed", ref currentIdx, Speeds, Speeds.Length))
            {
                _speedMultiplier = SpeedValues[currentIdx];
            }
            ImGui.SameLine();

            // Range slider
            int maxGen = Math.Max(0, _totalGenerations - 1);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 120 * s);

            int start = _startGeneration;
            int end = _endGeneration;
            // Use two separate sliders for start and end
            ImGui.Text($"Gen {_startGeneration}-{_endGeneration} / {maxGen}");

            // End generation slider (main scrubber)
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.SliderInt("##end", ref end, 0, maxGen, ""))
            {
                _endGeneration = end;
                if (_startGeneration > _endGeneration)
                    _startGeneration = _endGeneration;
                RangeChanged?.Invoke(_startGeneration, _endGeneration);
            }
        }
        ImGui.End();

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void SeekEnd(int gen)
    {
        int max = Math.Max(0, _totalGenerations - 1);
        _endGeneration = Math.Clamp(gen, 0, max);
        if (_startGeneration > _endGeneration)
            _startGeneration = _endGeneration;
        RangeChanged?.Invoke(_startGeneration, _endGeneration);
    }
}
