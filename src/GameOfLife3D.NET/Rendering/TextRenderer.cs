using System.Numerics;
using ImGuiNET;

namespace GameOfLife3D.NET.Rendering;

public static class TextRenderer
{
    public static void RenderGenerationLabels(
        int displayStart, int displayEnd, int gridSize,
        Matrix4x4 view, Matrix4x4 proj,
        int screenWidth, int screenHeight)
    {
        var drawList = ImGui.GetForegroundDrawList();
        int step = Math.Max(1, (displayEnd - displayStart) / 10);

        for (int gen = displayStart; gen <= displayEnd; gen += step)
        {
            // World position: to the right of the grid, at this generation's Y level
            var worldPos = new Vector3(gridSize / 2f + 3f, gen, 0f);
            if (WorldToScreen(worldPos, view, proj, screenWidth, screenHeight, out var screenPos))
            {
                string label = $"Gen {gen}";
                drawList.AddText(screenPos, 0xFFFFFFFF, label);
            }
        }
    }

    private static bool WorldToScreen(Vector3 worldPos, Matrix4x4 view, Matrix4x4 proj,
        int screenWidth, int screenHeight, out Vector2 screenPos)
    {
        var viewPos = Vector4.Transform(new Vector4(worldPos, 1f), view);
        var clipPos = Vector4.Transform(viewPos, proj);

        if (clipPos.W <= 0.001f)
        {
            screenPos = default;
            return false;
        }

        var ndc = new Vector3(clipPos.X / clipPos.W, clipPos.Y / clipPos.W, clipPos.Z / clipPos.W);

        // NDC to screen: X goes right, Y goes down in screen space
        screenPos = new Vector2(
            (ndc.X * 0.5f + 0.5f) * screenWidth,
            (1f - (ndc.Y * 0.5f + 0.5f)) * screenHeight
        );

        return ndc.Z >= -1f && ndc.Z <= 1f;
    }
}
