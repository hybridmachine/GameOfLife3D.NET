using ImGuiNET;
using System.Numerics;

namespace GameOfLife3D.NET.UI;

public sealed class StatusBar
{
    private int _fps;
    private int _frameCount;
    private double _lastFpsTime;

    public int Fps => _fps;

    public void UpdateFPS(double currentTime)
    {
        _frameCount++;
        if (currentTime - _lastFpsTime >= 1.0)
        {
            _fps = (int)(_frameCount / (currentTime - _lastFpsTime));
            _frameCount = 0;
            _lastFpsTime = currentTime;
        }
    }

    public void Render(int displayStart, int displayEnd, string ruleString, int cellCount, int windowWidth, int windowHeight)
    {
        var drawList = ImGui.GetForegroundDrawList();

        string statusText = $"Gen: {displayStart}-{displayEnd} | Rule: {ruleString} | Cells: {cellCount} | FPS: {_fps}";

        var textSize = ImGui.CalcTextSize(statusText);
        float padding = 8f;
        float barHeight = textSize.Y + padding * 2;

        // Background bar at bottom
        var barMin = new Vector2(0, windowHeight - barHeight);
        var barMax = new Vector2(windowWidth, windowHeight);
        drawList.AddRectFilled(barMin, barMax, 0xCC000000);

        // Text
        var textPos = new Vector2(padding, windowHeight - barHeight + padding);
        drawList.AddText(textPos, 0xFFFFFFFF, statusText);
    }
}
