using System.Numerics;
using ImGuiNET;

namespace GameOfLife3D.NET.UI;

/// <summary>
/// Centralized theme for the application UI.
/// Defines a cohesive dark color palette with teal/cyan accent
/// and applies consistent ImGui styling.
/// </summary>
public static class Theme
{
    // ── Background layers ──────────────────────────────────────────
    public static readonly Vector4 BgDeep      = new(0.06f, 0.06f, 0.09f, 1.00f);
    public static readonly Vector4 BgPanel      = new(0.09f, 0.09f, 0.13f, 0.97f);
    public static readonly Vector4 BgSurface    = new(0.12f, 0.12f, 0.17f, 1.00f);
    public static readonly Vector4 BgSurfaceAlt = new(0.14f, 0.14f, 0.20f, 1.00f);
    public static readonly Vector4 BgPopup      = new(0.10f, 0.10f, 0.15f, 0.98f);

    // ── Borders & separators ───────────────────────────────────────
    public static readonly Vector4 Border      = new(0.20f, 0.20f, 0.28f, 0.60f);
    public static readonly Vector4 BorderLight = new(0.25f, 0.25f, 0.35f, 0.40f);
    public static readonly Vector4 Separator   = new(0.20f, 0.20f, 0.28f, 0.50f);

    // ── Text ───────────────────────────────────────────────────────
    public static readonly Vector4 TextPrimary   = new(0.93f, 0.93f, 0.96f, 1.00f);
    public static readonly Vector4 TextSecondary = new(0.62f, 0.62f, 0.70f, 1.00f);
    public static readonly Vector4 TextMuted     = new(0.45f, 0.45f, 0.52f, 1.00f);
    public static readonly Vector4 TextDisabled  = new(0.35f, 0.35f, 0.40f, 1.00f);

    // ── Accent (teal/cyan — matches 3D visualization) ──────────────
    public static readonly Vector4 Accent       = new(0.00f, 0.78f, 0.55f, 1.00f); // #00C78C
    public static readonly Vector4 AccentHover  = new(0.00f, 0.88f, 0.62f, 1.00f);
    public static readonly Vector4 AccentActive = new(0.00f, 0.65f, 0.46f, 1.00f);
    public static readonly Vector4 AccentMuted  = new(0.00f, 0.78f, 0.55f, 0.15f);
    public static readonly Vector4 AccentDim    = new(0.00f, 0.78f, 0.55f, 0.35f);

    // ── Interactive elements ───────────────────────────────────────
    public static readonly Vector4 FrameBg       = new(0.14f, 0.14f, 0.20f, 1.00f);
    public static readonly Vector4 FrameHover    = new(0.18f, 0.18f, 0.25f, 1.00f);
    public static readonly Vector4 FrameActive   = new(0.20f, 0.20f, 0.28f, 1.00f);

    public static readonly Vector4 Button        = new(0.16f, 0.16f, 0.22f, 1.00f);
    public static readonly Vector4 ButtonHover   = new(0.20f, 0.20f, 0.28f, 1.00f);
    public static readonly Vector4 ButtonActive  = new(0.14f, 0.14f, 0.20f, 1.00f);

    public static readonly Vector4 Header        = new(0.14f, 0.14f, 0.20f, 1.00f);
    public static readonly Vector4 HeaderHover   = new(0.18f, 0.18f, 0.26f, 1.00f);
    public static readonly Vector4 HeaderActive  = new(0.12f, 0.12f, 0.18f, 1.00f);

    // ── Slider / Scrollbar ─────────────────────────────────────────
    public static readonly Vector4 SliderGrab       = Accent;
    public static readonly Vector4 SliderGrabActive = AccentHover;
    public static readonly Vector4 ScrollBg         = new(0.08f, 0.08f, 0.12f, 0.50f);
    public static readonly Vector4 ScrollGrab       = new(0.22f, 0.22f, 0.30f, 1.00f);
    public static readonly Vector4 ScrollGrabHover  = new(0.28f, 0.28f, 0.38f, 1.00f);
    public static readonly Vector4 ScrollGrabActive = new(0.34f, 0.34f, 0.44f, 1.00f);

    // ── Tabs ───────────────────────────────────────────────────────
    public static readonly Vector4 Tab          = new(0.10f, 0.10f, 0.15f, 1.00f);
    public static readonly Vector4 TabHovered   = new(0.18f, 0.18f, 0.26f, 1.00f);
    public static readonly Vector4 TabSelected  = new(0.14f, 0.14f, 0.20f, 1.00f);

    // ── Check / Radio ──────────────────────────────────────────────
    public static readonly Vector4 CheckMark = Accent;

    // ── Status colors ──────────────────────────────────────────────
    public static readonly Vector4 StatusGreen  = new(0.30f, 0.85f, 0.45f, 1.00f);
    public static readonly Vector4 StatusYellow = new(0.95f, 0.80f, 0.25f, 1.00f);

    // ── Packed uint32 colors for draw-list calls ───────────────────
    public static uint AccentU32       => ImGui.ColorConvertFloat4ToU32(Accent);
    public static uint AccentDimU32    => ImGui.ColorConvertFloat4ToU32(AccentDim);
    public static uint AccentMutedU32  => ImGui.ColorConvertFloat4ToU32(AccentMuted);
    public static uint TextPrimaryU32  => ImGui.ColorConvertFloat4ToU32(TextPrimary);
    public static uint TextSecondaryU32 => ImGui.ColorConvertFloat4ToU32(TextSecondary);
    public static uint TextMutedU32    => ImGui.ColorConvertFloat4ToU32(TextMuted);
    public static uint BgSurfaceU32    => ImGui.ColorConvertFloat4ToU32(BgSurface);
    public static uint BgSurfaceAltU32 => ImGui.ColorConvertFloat4ToU32(BgSurfaceAlt);
    public static uint BorderU32       => ImGui.ColorConvertFloat4ToU32(Border);
    public static uint SeparatorU32    => ImGui.ColorConvertFloat4ToU32(Separator);

    /// <summary>
    /// Applies the full theme to ImGui. Call once after ImGui initialization.
    /// DPI scaling should already have been applied via style.ScaleAllSizes().
    /// </summary>
    public static void Apply(float dpiScale)
    {
        var style = ImGui.GetStyle();

        // ── Geometry ───────────────────────────────────────────────
        style.WindowRounding    = 8f;
        style.ChildRounding     = 6f;
        style.FrameRounding     = 5f;
        style.PopupRounding     = 6f;
        style.GrabRounding      = 4f;
        style.TabRounding       = 5f;
        style.ScrollbarRounding = 6f;

        style.WindowBorderSize  = 1f;
        style.ChildBorderSize   = 1f;
        style.FrameBorderSize   = 0f;
        style.PopupBorderSize   = 1f;
        style.TabBorderSize     = 0f;

        style.WindowPadding     = new Vector2(12f, 10f);
        style.FramePadding      = new Vector2(8f, 5f);
        style.ItemSpacing       = new Vector2(8f, 6f);
        style.ItemInnerSpacing  = new Vector2(6f, 4f);
        style.IndentSpacing     = 16f;
        style.ScrollbarSize     = 12f;
        style.GrabMinSize       = 10f;

        style.WindowTitleAlign  = new Vector2(0.5f, 0.5f);

        // Scale all sizes for DPI
        style.ScaleAllSizes(dpiScale);

        // ── Colors ─────────────────────────────────────────────────
        var c = style.Colors;

        c[(int)ImGuiCol.Text]                  = TextPrimary;
        c[(int)ImGuiCol.TextDisabled]          = TextDisabled;
        c[(int)ImGuiCol.WindowBg]              = BgPanel;
        c[(int)ImGuiCol.ChildBg]               = BgSurface;
        c[(int)ImGuiCol.PopupBg]               = BgPopup;
        c[(int)ImGuiCol.Border]                = Border;
        c[(int)ImGuiCol.BorderShadow]          = Vector4.Zero;

        c[(int)ImGuiCol.FrameBg]               = FrameBg;
        c[(int)ImGuiCol.FrameBgHovered]        = FrameHover;
        c[(int)ImGuiCol.FrameBgActive]         = FrameActive;

        c[(int)ImGuiCol.TitleBg]               = BgDeep;
        c[(int)ImGuiCol.TitleBgActive]         = BgSurface;
        c[(int)ImGuiCol.TitleBgCollapsed]      = BgDeep;
        c[(int)ImGuiCol.MenuBarBg]             = BgSurface;

        c[(int)ImGuiCol.ScrollbarBg]           = ScrollBg;
        c[(int)ImGuiCol.ScrollbarGrab]         = ScrollGrab;
        c[(int)ImGuiCol.ScrollbarGrabHovered]  = ScrollGrabHover;
        c[(int)ImGuiCol.ScrollbarGrabActive]   = ScrollGrabActive;

        c[(int)ImGuiCol.CheckMark]             = CheckMark;
        c[(int)ImGuiCol.SliderGrab]            = SliderGrab;
        c[(int)ImGuiCol.SliderGrabActive]      = SliderGrabActive;

        c[(int)ImGuiCol.Button]                = Button;
        c[(int)ImGuiCol.ButtonHovered]         = ButtonHover;
        c[(int)ImGuiCol.ButtonActive]          = ButtonActive;

        c[(int)ImGuiCol.Header]                = Header;
        c[(int)ImGuiCol.HeaderHovered]         = HeaderHover;
        c[(int)ImGuiCol.HeaderActive]          = HeaderActive;

        c[(int)ImGuiCol.Separator]             = Separator;
        c[(int)ImGuiCol.SeparatorHovered]      = AccentDim;
        c[(int)ImGuiCol.SeparatorActive]       = Accent;

        c[(int)ImGuiCol.ResizeGrip]            = AccentMuted;
        c[(int)ImGuiCol.ResizeGripHovered]     = AccentDim;
        c[(int)ImGuiCol.ResizeGripActive]      = Accent;

        c[(int)ImGuiCol.Tab]                   = Tab;
        c[(int)ImGuiCol.TabHovered]            = TabHovered;
        c[(int)ImGuiCol.TabSelected]           = TabSelected;

        c[(int)ImGuiCol.PlotLines]             = Accent;
        c[(int)ImGuiCol.PlotLinesHovered]      = AccentHover;
        c[(int)ImGuiCol.PlotHistogram]         = Accent;
        c[(int)ImGuiCol.PlotHistogramHovered]  = AccentHover;

        c[(int)ImGuiCol.TextSelectedBg]        = AccentMuted;
        c[(int)ImGuiCol.DragDropTarget]        = AccentHover;
        c[(int)ImGuiCol.NavHighlight]          = Accent;
    }
}
