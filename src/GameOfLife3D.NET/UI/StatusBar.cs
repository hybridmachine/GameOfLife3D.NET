using ImGuiNET;
using System.Numerics;

namespace GameOfLife3D.NET.UI;

public sealed class StatusBar
{
    private readonly float _dpiScale;
    private int _fps;
    private int _frameCount;
    private double _lastFpsTime;

    public int Fps => _fps;

    public StatusBar(float dpiScale = 1.0f)
    {
        _dpiScale = dpiScale;
    }

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
        float s = _dpiScale;
        var drawList = ImGui.GetForegroundDrawList();

        float padding = 10f * s;
        float barHeight = ImGui.GetTextLineHeight() + padding * 2;

        // Background
        var barMin = new Vector2(0, windowHeight - barHeight);
        var barMax = new Vector2(windowWidth, windowHeight);
        drawList.AddRectFilled(barMin, barMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.05f, 0.05f, 0.08f, 0.95f)));

        // Top border accent
        drawList.AddLine(barMin, new Vector2(barMax.X, barMin.Y),
            Theme.SeparatorU32, 1f);

        float x = padding;
        float textY = windowHeight - barHeight + padding;

        // Segment: Generation
        x = DrawSegment(drawList, x, textY, "GEN", $"{displayStart}\u2013{displayEnd}", s);

        x = DrawDivider(drawList, x, textY, s);

        // Segment: Rule
        x = DrawSegment(drawList, x, textY, "RULE", ruleString, s);

        x = DrawDivider(drawList, x, textY, s);

        // Segment: Cells
        x = DrawSegment(drawList, x, textY, "CELLS", FormatNumber(cellCount), s);

        x = DrawDivider(drawList, x, textY, s);

        // Segment: FPS — color coded
        uint fpsColor = _fps >= 55 ? Theme.TextPrimaryU32
            : _fps >= 30 ? ImGui.ColorConvertFloat4ToU32(Theme.StatusYellow)
            : ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.3f, 0.3f, 1f));
        DrawSegment(drawList, x, textY, "FPS", _fps.ToString(), s, fpsColor);
    }

    private static float DrawSegment(ImDrawListPtr drawList, float x, float y, string label, string value, float s, uint valueColor = 0)
    {
        if (valueColor == 0)
            valueColor = Theme.TextPrimaryU32;

        // Label
        drawList.AddText(new Vector2(x, y), Theme.TextMutedU32, label);
        x += ImGui.CalcTextSize(label).X + 5 * s;

        // Value
        drawList.AddText(new Vector2(x, y), valueColor, value);
        x += ImGui.CalcTextSize(value).X;

        return x;
    }

    private static float DrawDivider(ImDrawListPtr drawList, float x, float y, float s)
    {
        float gap = 10 * s;
        float divX = x + gap;
        float lineHeight = ImGui.GetTextLineHeight();
        drawList.AddLine(
            new Vector2(divX, y + 2),
            new Vector2(divX, y + lineHeight - 2),
            Theme.SeparatorU32, 1f);
        return divX + gap;
    }

    private static string FormatNumber(int n)
    {
        return n >= 1_000_000 ? $"{n / 1_000_000.0:F1}M"
             : n >= 1_000 ? $"{n / 1_000.0:F1}K"
             : n.ToString();
    }
}
