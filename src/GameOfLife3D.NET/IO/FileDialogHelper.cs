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
            // macOS disables files whose custom extension has no registered UTI.
            // Leave MaterialX dialogs unfiltered and let the importer validate the file.
            string? nativeFilter = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && HasExtension(filter, "mtlx")
                    ? null
                    : filter;
            var result = Dialog.FileOpen(nativeFilter, defaultPath);
            return result.IsOk ? result.Path : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
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
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacOSSaveFile(filter);
            return null;
        }
    }

    private static string? MacOSOpenFile(string filter)
    {
        string typeClause = HasExtension(filter, "mtlx")
            ? ""
            : $" of type {{{string.Join(", ", ParseExtensions(filter).Select(e => $"\"{e}\""))}}}";
        var script = $@"set chosenFile to choose file with prompt ""Open File""{typeClause}
return POSIX path of chosenFile";
        return RunOsascript(script);
    }

    private static string? MacOSSaveFile(string filter)
    {
        var ext = ParseExtensions(filter).First();
        var script = $@"set chosenFile to choose file name with prompt ""Save File"" default name ""export.{ext}""
return POSIX path of chosenFile";
        return RunOsascript(script);
    }

    private static string[] ParseExtensions(string filter) =>
        filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool HasExtension(string filter, string extension) =>
        ParseExtensions(filter).Contains(extension, StringComparer.OrdinalIgnoreCase);

    private static string? RunOsascript(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            proc.StandardInput.Write(script);
            proc.StandardInput.Close();

            string output = proc.StandardOutput.ReadToEnd().Trim();
            if (!proc.WaitForExit(30_000))
            {
                proc.Kill();
                return null;
            }

            return proc.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
