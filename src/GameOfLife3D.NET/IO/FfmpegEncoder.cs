using System.Diagnostics;
using System.Text;

namespace GameOfLife3D.NET.IO;

// Spawns ffmpeg and pipes raw RGBA frames into its stdin.
// Discovery order: bundled binary next to exe → macOS Contents/Resources → PATH → common installs.
public sealed class FfmpegEncoder : IVideoEncoder
{
    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly StringBuilder _stderrTail = new();
    private readonly Task _stderrTask;
    private readonly string _outputPath;

    public bool IsHealthy { get; private set; } = true;
    public string? LastError { get; private set; }

    public FfmpegEncoder(string ffmpegPath, VideoCodec codec, int width, int height, int fps, string outputPath)
    {
        if (codec is not (VideoCodec.Vp9Webm or VideoCodec.H264Mp4))
            throw new ArgumentException($"FfmpegEncoder doesn't support {codec}.", nameof(codec));

        _outputPath = outputPath;
        string args = BuildArgs(codec, width, height, fps, outputPath);

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start ffmpeg: {ffmpegPath}");
        _stdin = _process.StandardInput.BaseStream;

        // Drain stderr on a background task so the pipe never blocks.
        _stderrTask = Task.Run(async () =>
        {
            var buf = new byte[4096];
            int n;
            try
            {
                while ((n = await _process.StandardError.BaseStream.ReadAsync(buf)) > 0)
                {
                    lock (_stderrTail)
                    {
                        _stderrTail.Append(Encoding.UTF8.GetString(buf, 0, n));
                        // Keep only the last ~4 KB for diagnostics.
                        if (_stderrTail.Length > 4096)
                            _stderrTail.Remove(0, _stderrTail.Length - 4096);
                    }
                }
            }
            catch { /* process exited */ }
        });
    }

    public void WriteFrame(ReadOnlySpan<byte> rgbaPixels, int width, int height)
    {
        if (!IsHealthy) throw new InvalidOperationException(LastError ?? "ffmpeg encoder not healthy.");
        try
        {
            _stdin.Write(rgbaPixels);
        }
        catch (Exception ex)
        {
            IsHealthy = false;
            LastError = $"ffmpeg pipe broken: {ex.Message}\n{StderrTail()}";
            throw new IOException(LastError);
        }
    }

    public void Finish()
    {
        try { _stdin.Close(); } catch { }
        if (!_process.WaitForExit(30_000))
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
            IsHealthy = false;
            LastError = "ffmpeg did not exit within 30s.";
            throw new IOException(LastError);
        }
        try { _stderrTask.Wait(2_000); } catch { }
        if (_process.ExitCode != 0)
        {
            IsHealthy = false;
            LastError = $"ffmpeg exited with code {_process.ExitCode}: {StderrTail()}";
            throw new IOException(LastError);
        }
    }

    public void Cancel()
    {
        IsHealthy = false;
        try { _process.Kill(entireProcessTree: true); } catch { }
        try { _stdin.Close(); } catch { }
        try { _process.WaitForExit(2_000); } catch { }
        try { File.Delete(_outputPath); } catch { }
    }

    public void Dispose()
    {
        try { _stdin.Dispose(); } catch { }
        try { _process.Dispose(); } catch { }
    }

    private string StderrTail()
    {
        lock (_stderrTail) return _stderrTail.ToString();
    }

    private static string BuildArgs(VideoCodec codec, int w, int h, int fps, string output)
    {
        // Common input: raw RGBA frames on stdin. Matches PostProcessPipeline.ReadFinalPixels output exactly.
        string input = $"-y -hide_banner -loglevel warning -f rawvideo -pix_fmt rgba -s {w}x{h} -r {fps} -i -";
        string videoArgs = codec switch
        {
            VideoCodec.Vp9Webm => "-c:v libvpx-vp9 -pix_fmt yuv420p -b:v 0 -crf 30 -row-mt 1 -threads 0",
            VideoCodec.H264Mp4 => "-c:v libx264 -pix_fmt yuv420p -preset medium -crf 18 -movflags +faststart",
            _ => throw new ArgumentOutOfRangeException(nameof(codec)),
        };
        return $"{input} {videoArgs} -an \"{output}\"";
    }

    // ─── Discovery / capability detection ──────────────────────────────────────

    private static bool? _libx264Cached;
    private static string? _probedBinaryPath;

    public static string? LocateBinary()
    {
        string name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        // 1. Bundled binary next to executable.
        string exeDir = AppContext.BaseDirectory;
        string bundled = Path.Combine(exeDir, name);
        if (File.Exists(bundled)) return bundled;

        // 2. macOS .app bundle: AppContext.BaseDirectory points at Contents/MacOS;
        //    the signing script relocates ffmpeg into Contents/Resources.
        if (OperatingSystem.IsMacOS())
        {
            string resources = Path.GetFullPath(Path.Combine(exeDir, "..", "Resources", "ffmpeg"));
            if (File.Exists(resources)) return resources;
        }

        // 3. PATH.
        string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            string candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }

        // 4. Common install locations.
        string[] extras = OperatingSystem.IsMacOS()
            ? new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" }
            : OperatingSystem.IsLinux()
                ? new[] { "/usr/bin/ffmpeg", "/usr/local/bin/ffmpeg" }
                : Array.Empty<string>();
        foreach (var p in extras) if (File.Exists(p)) return p;

        return null;
    }

    // Probes `ffmpeg -encoders` for libx264. Result is cached per binary path.
    public static bool SupportsLibx264(string ffmpegPath)
    {
        if (_libx264Cached is bool cached && _probedBinaryPath == ffmpegPath) return cached;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3_000);
            bool found = output.Contains("libx264", StringComparison.Ordinal);
            _libx264Cached = found;
            _probedBinaryPath = ffmpegPath;
            return found;
        }
        catch
        {
            _libx264Cached = false;
            _probedBinaryPath = ffmpegPath;
            return false;
        }
    }

    public static string CodecExtension(VideoCodec codec) => codec switch
    {
        VideoCodec.Vp9Webm => ".webm",
        VideoCodec.H264Mp4 => ".mp4",
        VideoCodec.PngSequence => "",
        _ => throw new ArgumentOutOfRangeException(nameof(codec)),
    };
}
