using System.Numerics;
using ImGuiNET;

namespace GameOfLife3D.NET.UI;

/// <summary>
/// Reusable custom-drawn UI components for a polished look.
/// </summary>
public static class UIHelpers
{
    /// <summary>
    /// Draws a section header with a teal accent bar on the left and an optional icon prefix.
    /// Returns true when the section is expanded (like CollapsingHeader).
    /// </summary>
    public static bool SectionHeader(string icon, string label, bool defaultOpen = true)
    {
        var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        string display = string.IsNullOrEmpty(icon) ? label : $"{icon}  {label}";

        // Push accent-tinted header colors
        ImGui.PushStyleColor(ImGuiCol.Header, Theme.AccentMuted);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Theme.AccentDim);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, Theme.AccentMuted);

        bool open = ImGui.CollapsingHeader(display, flags);

        ImGui.PopStyleColor(3);

        // Draw accent bar on left edge of the header
        if (open)
        {
            var drawList = ImGui.GetWindowDrawList();
            var cursorScreen = ImGui.GetCursorScreenPos();
            float barWidth = 3f;
            float regionHeight = 0; // will be drawn after content
            var barStart = new Vector2(cursorScreen.X - ImGui.GetStyle().WindowPadding.X + 2f, cursorScreen.Y);

            // Store bar start for EndSectionContent
            drawList.AddRectFilled(
                barStart,
                new Vector2(barStart.X + barWidth, barStart.Y),
                Theme.AccentU32,
                1f);
        }

        return open;
    }

    /// <summary>
    /// Renders a button with the accent color scheme. Use for primary actions.
    /// </summary>
    public static bool AccentButton(string label, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.AccentActive);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.02f, 0.02f, 0.04f, 1f));

        bool clicked = ImGui.Button(label, size);

        ImGui.PopStyleColor(4);
        return clicked;
    }

    /// <summary>
    /// Renders a small icon-style button (square, minimal padding).
    /// </summary>
    public static bool IconButton(string icon, string tooltipText, float size = 0)
    {
        if (size > 0)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(size * 0.15f, size * 0.08f));
        }

        bool clicked = ImGui.Button(icon, size > 0 ? new Vector2(size, size) : default);

        if (size > 0)
            ImGui.PopStyleVar();

        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(tooltipText))
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, Theme.BgPopup);
            ImGui.SetTooltip(tooltipText);
            ImGui.PopStyleColor();
        }

        return clicked;
    }

    /// <summary>
    /// Renders a row of equally-sized buttons. Returns the index of the clicked button, or -1.
    /// </summary>
    public static int ButtonRow(string[] labels, float totalWidth = 0)
    {
        int clicked = -1;
        float available = totalWidth > 0 ? totalWidth : ImGui.GetContentRegionAvail().X;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float buttonWidth = (available - spacing * (labels.Length - 1)) / labels.Length;

        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (ImGui.Button(labels[i], new Vector2(buttonWidth, 0)))
                clicked = i;
        }

        return clicked;
    }

    /// <summary>
    /// Draws a labeled separator: ── Label ──────────
    /// </summary>
    public static void LabeledSeparator(string label)
    {
        ImGui.Spacing();
        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        float textWidth = ImGui.CalcTextSize(label).X;
        float lineY = cursor.Y + ImGui.GetTextLineHeight() * 0.5f;
        float pad = 6f;

        // Left line
        drawList.AddLine(
            new Vector2(cursor.X, lineY),
            new Vector2(cursor.X + pad, lineY),
            Theme.SeparatorU32, 1f);

        // Label
        drawList.AddText(
            new Vector2(cursor.X + pad * 2, cursor.Y),
            Theme.TextSecondaryU32, label);

        // Right line
        float afterText = cursor.X + pad * 3 + textWidth;
        drawList.AddLine(
            new Vector2(afterText, lineY),
            new Vector2(cursor.X + width, lineY),
            Theme.SeparatorU32, 1f);

        ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight() + 2));
    }

    /// <summary>
    /// Draws a thin horizontal rule with spacing.
    /// </summary>
    public static void ThinSeparator()
    {
        ImGui.Spacing();
        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        drawList.AddLine(
            cursor,
            new Vector2(cursor.X + width, cursor.Y),
            Theme.SeparatorU32, 1f);
        ImGui.Dummy(new Vector2(0, 4));
    }

    /// <summary>
    /// Renders a label in muted text followed by a value in primary text.
    /// </summary>
    public static void LabelValue(string label, string value)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.TextSecondary);
        ImGui.Text(label);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.Text(value);
    }

    /// <summary>
    /// Begins a grouped visual container with subtle background and border.
    /// Must be paired with EndGroup().
    /// </summary>
    public static void BeginGroup(string id, float height = 0)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.BgSurface);
        ImGui.PushStyleColor(ImGuiCol.Border, Theme.BorderLight);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.BeginChild(id, new Vector2(0, height), ImGuiChildFlags.Border | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.None);
    }

    /// <summary>
    /// Ends a grouped visual container.
    /// </summary>
    public static void EndGroup()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(2);
    }

    /// <summary>
    /// Renders a styled tooltip for the previous item.
    /// </summary>
    public static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.PushStyleColor(ImGuiCol.PopupBg, Theme.BgPopup);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 6));
            ImGui.SetTooltip(text);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
    }

    /// <summary>
    /// Renders a small colored badge/pill with text.
    /// </summary>
    public static void Badge(string text, Vector4 bgColor, Vector4 textColor)
    {
        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        float padX = 6f;
        float padY = 2f;
        float rounding = (textSize.Y + padY * 2) * 0.5f;

        var min = cursor;
        var max = new Vector2(cursor.X + textSize.X + padX * 2, cursor.Y + textSize.Y + padY * 2);

        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(bgColor), rounding);
        drawList.AddText(new Vector2(cursor.X + padX, cursor.Y + padY),
            ImGui.ColorConvertFloat4ToU32(textColor), text);

        ImGui.Dummy(new Vector2(textSize.X + padX * 2, textSize.Y + padY * 2));
    }
}
