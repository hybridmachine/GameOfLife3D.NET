using System.Numerics;
using ImGuiNET;

namespace GameOfLife3D.NET.UI;

/// <summary>
/// Renders a 2D mini-grid preview of a pattern into the current ImGui window
/// using the window draw list. Scales the pattern to fit the requested size
/// while preserving aspect ratio.
/// </summary>
public static class PatternPreview
{
    private static readonly Vector4 GridBg = new(0.08f, 0.08f, 0.12f, 1.0f);
    private static readonly Vector4 GridBorder = new(0.25f, 0.25f, 0.35f, 0.60f);

    /// <summary>
    /// Draws a pattern preview of the given size. If <paramref name="pattern"/> is
    /// null (e.g. still loading), draws an empty framed area instead.
    /// </summary>
    public static void Draw(bool[,]? pattern, Vector2 size)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var max = new Vector2(origin.X + size.X, origin.Y + size.Y);

        uint bgU32 = ImGui.ColorConvertFloat4ToU32(GridBg);
        uint borderU32 = ImGui.ColorConvertFloat4ToU32(GridBorder);
        drawList.AddRectFilled(origin, max, bgU32, 4f);
        drawList.AddRect(origin, max, borderU32, 4f);

        if (pattern != null)
        {
            int rows = pattern.GetLength(0);
            int cols = pattern.GetLength(1);

            if (rows > 0 && cols > 0)
            {
                float padding = 4f;
                float availW = size.X - padding * 2;
                float availH = size.Y - padding * 2;

                float cellSize = MathF.Max(1f, MathF.Min(availW / cols, availH / rows));
                float gridW = cellSize * cols;
                float gridH = cellSize * rows;

                float offsetX = origin.X + padding + (availW - gridW) * 0.5f;
                float offsetY = origin.Y + padding + (availH - gridH) * 0.5f;

                uint cellU32 = Theme.AccentU32;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if (!pattern[r, c]) continue;
                        float x = offsetX + c * cellSize;
                        float y = offsetY + r * cellSize;
                        drawList.AddRectFilled(
                            new Vector2(x, y),
                            new Vector2(x + cellSize, y + cellSize),
                            cellU32);
                    }
                }
            }
        }

        // Advance the cursor past the reserved area so subsequent widgets stack below.
        ImGui.Dummy(size);
    }
}
