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
        float barHeight = 64f * s;
        float statusBarHeight = 30f * s;
        float barY = windowHeight - barHeight - statusBarHeight;

        ImGui.SetNextWindowPos(new Vector2(0, barY));
        ImGui.SetNextWindowSize(new Vector2(windowWidth, barHeight));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14 * s, 8 * s));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6 * s, 6 * s));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.07f, 0.07f, 0.10f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.Border);

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.Begin("Timeline", flags))
        {
            // Draw top accent line
            var drawList = ImGui.GetWindowDrawList();
            var winPos = ImGui.GetWindowPos();
            drawList.AddLine(
                winPos,
                new Vector2(winPos.X + windowWidth, winPos.Y),
                Theme.AccentDimU32, 2f * s);

            RenderTransportRow(s);
            RenderScrubberRow(s);
        }
        ImGui.End();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void RenderTransportRow(float s)
    {
        float btnSize = 28 * s;
        var btnSizeVec = new Vector2(btnSize, btnSize);

        // Transport: Skip to start
        if (TransportButton("\u23EE", btnSizeVec, "First generation"))
            SeekEnd(0);
        ImGui.SameLine();

        // Step back
        if (TransportButton("\u23F4", btnSizeVec, "Previous generation"))
            SeekEnd(_endGeneration - 1);
        ImGui.SameLine();

        // Play / Pause — accent colored when playing
        if (_isPlaying)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Theme.AccentMuted);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentDim);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Accent);
        }
        string playIcon = _isPlaying ? "\u23F8" : "\u25B6";
        string playTip = _isPlaying ? "Pause" : "Play";
        if (TransportButton(playIcon, btnSizeVec, playTip))
        {
            _isPlaying = !_isPlaying;
            PlayToggled?.Invoke(_isPlaying);
        }
        if (_isPlaying)
            ImGui.PopStyleColor(3);
        ImGui.SameLine();

        // Step forward
        if (TransportButton("\u23F5", btnSizeVec, "Next generation"))
            SeekEnd(_endGeneration + 1);
        ImGui.SameLine();

        // Skip to end
        if (TransportButton("\u23ED", btnSizeVec, "Last generation"))
            SeekEnd(Math.Max(0, _totalGenerations - 1));
        ImGui.SameLine();

        ImGui.Dummy(new Vector2(6 * s, 0));
        ImGui.SameLine();

        // Reset
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        if (TransportButton("\u27F3", btnSizeVec, "Reset simulation"))
            ResetRequested?.Invoke();
        ImGui.PopStyleColor();
        ImGui.SameLine();

        ImGui.Dummy(new Vector2(8 * s, 0));
        ImGui.SameLine();

        // Speed selector
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4 * s);
        ImGui.Text("Speed");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(58 * s);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 4 * s);
        int currentIdx = Array.IndexOf(SpeedValues, _speedMultiplier);
        if (currentIdx < 0) currentIdx = 2;
        if (ImGui.Combo("##speed", ref currentIdx, Speeds, Speeds.Length))
        {
            _speedMultiplier = SpeedValues[currentIdx];
        }
        ImGui.SameLine();

        ImGui.Dummy(new Vector2(8 * s, 0));
        ImGui.SameLine();

        // Generation range badge
        int maxGen = Math.Max(0, _totalGenerations - 1);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4 * s);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        ImGui.Text("Gen");
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.Text($"{_startGeneration}\u2013{_endGeneration}");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextMuted);
        ImGui.Text($"/ {maxGen}");
        ImGui.PopStyleColor();
    }

    private void RenderScrubberRow(float s)
    {
        int maxGen = Math.Max(0, _totalGenerations - 1);
        int end = _endGeneration;

        // Custom-drawn scrubber track
        float trackHeight = 6 * s;
        float availWidth = ImGui.GetContentRegionAvail().X;
        var cursor = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        // Background track
        var trackMin = new Vector2(cursor.X, cursor.Y + 2 * s);
        var trackMax = new Vector2(cursor.X + availWidth, trackMin.Y + trackHeight);
        drawList.AddRectFilled(trackMin, trackMax, Theme.BgSurfaceAltU32, trackHeight * 0.5f);

        // Filled portion
        if (maxGen > 0)
        {
            float fillFraction = (float)_endGeneration / maxGen;
            var fillMax = new Vector2(trackMin.X + availWidth * fillFraction, trackMax.Y);
            drawList.AddRectFilled(trackMin, fillMax, Theme.AccentDimU32, trackHeight * 0.5f);
        }

        // Invisible slider overlaid on top of custom track
        ImGui.SetNextItemWidth(availWidth);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Theme.AccentHover);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, 14 * s);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, trackHeight * 0.5f);

        if (ImGui.SliderInt("##scrubber", ref end, 0, maxGen, ""))
        {
            _endGeneration = end;
            if (_startGeneration > _endGeneration)
                _startGeneration = _endGeneration;
            RangeChanged?.Invoke(_startGeneration, _endGeneration);
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
    }

    private static bool TransportButton(string icon, Vector2 size, string tooltip)
    {
        bool clicked = ImGui.Button(icon, size);
        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(tooltip))
            ImGui.SetTooltip(tooltip);
        return clicked;
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
