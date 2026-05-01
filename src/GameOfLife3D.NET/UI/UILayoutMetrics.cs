using ImGuiNET;

namespace GameOfLife3D.NET.UI;

public static class UILayoutMetrics
{
    public static float TimelineBarHeight =>
        Math.Max(64f, ImGui.GetTextLineHeightWithSpacing() * 2f + 26f);

    public static float StatusBarHeight =>
        Math.Max(30f, ImGui.GetTextLineHeight() + 20f);
}
