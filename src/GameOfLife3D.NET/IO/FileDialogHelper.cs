using System.Diagnostics;
using System.Runtime.InteropServices;
using NativeFileDialogSharp;

namespace GameOfLife3D.NET.IO;

public static class FileDialogHelper
{
    public static string? OpenFile(string filter = "rle,json", string? defaultPath = null)
    {
        try
        {
            var result = Dialog.FileOpen(filter, defaultPath);
            return result.IsOk ? result.Path : null;
        }
        catch (DllNotFoundException)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacOSOpenFile(filter);
            return null;
        }
    }

    public static string? SaveFile(string filter = "json", string? defaultPath = null)
    {
        try
        {
            var result = Dialog.FileSave(filter, defaultPath);
            return result.IsOk ? result.Path : null;
        }
        catch (DllNotFoundException)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacOSSaveFile(filter);
            return null;
        }
    }

    private static string? MacOSOpenFile(string filter)
    {
        var extensions = filter.Split(',').Select(e => e.Trim()).ToArray();
        var typeList = string.Join(", ", extensions.Select(e => $"\"{e}\""));
        var script = $@"set chosenFile to choose file with prompt ""Open File"" of type {{{typeList}}}
return POSIX path of chosenFile";
        return RunOsascript(script);
    }

    private static string? MacOSSaveFile(string filter)
    {
        var ext = filter.Split(',').First().Trim();
        var script = $@"set chosenFile to choose file name with prompt ""Save File"" default name ""export.{ext}""
return POSIX path of chosenFile";
        return RunOsascript(script);
    }

    private static string? RunOsascript(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            proc.StandardInput.Write(script);
            proc.StandardInput.Close();

            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();

            return proc.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
