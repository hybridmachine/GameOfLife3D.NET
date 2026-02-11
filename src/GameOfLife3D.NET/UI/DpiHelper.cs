using System.Runtime.InteropServices;

namespace GameOfLife3D.NET.UI;

internal static partial class DpiHelper
{
    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();

    /// <summary>
    /// Returns the system DPI scale factor (e.g. 1.5 for 150% scaling).
    /// Falls back to 1.0 if the P/Invoke call fails.
    /// </summary>
    public static float GetSystemDpiScale()
    {
        try
        {
            uint dpi = GetDpiForSystem();
            if (dpi > 0)
                return dpi / 96.0f;
        }
        catch
        {
            // Not available (e.g. non-Windows or older OS)
        }
        return 1.0f;
    }
}
