namespace GameOfLife3D.NET.UI;

/// <summary>
/// Font Awesome 6 Free Solid icon codepoints.
/// Requires fa-solid-900.ttf loaded with MergeMode in the ImGui font atlas.
/// </summary>
static class Icons
{
    // Transport controls
    public const string SkipBack    = "\uF049";  // fa-backward-fast
    public const string StepBack    = "\uF048";  // fa-backward-step
    public const string Play        = "\uF04B";  // fa-play
    public const string Pause       = "\uF04C";  // fa-pause
    public const string StepForward = "\uF051";  // fa-forward-step
    public const string SkipForward = "\uF050";  // fa-forward-fast
    public const string Reset       = "\uF01E";  // fa-arrow-rotate-right

    // Section headers
    public const string Gear        = "\uF013";  // fa-gear
    public const string ChartBar    = "\uF080";  // fa-chart-simple
    public const string Grid        = "\uF00A";  // fa-table-cells
    public const string Palette     = "\uF53F";  // fa-palette
    public const string Pencil      = "\uF303";  // fa-pencil
    public const string FloppyDisk  = "\uF0C7";  // fa-floppy-disk
    public const string Camera      = "\uF030";  // fa-camera
}
