using NativeFileDialogSharp;

namespace GameOfLife3D.NET.IO;

public static class FileDialogHelper
{
    public static string? OpenFile(string filter = "rle,json", string? defaultPath = null)
    {
        var result = Dialog.FileOpen(filter, defaultPath);
        return result.IsOk ? result.Path : null;
    }

    public static string? SaveFile(string filter = "json", string? defaultPath = null)
    {
        var result = Dialog.FileSave(filter, defaultPath);
        return result.IsOk ? result.Path : null;
    }
}
