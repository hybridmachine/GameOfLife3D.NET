using GameOfLife3D.NET.IO;

namespace GameOfLife3D.NET.Recording;

public sealed record RecordingSettings
{
    public required VideoCodec Codec { get; init; }
    public required int Fps { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string OutputPath { get; init; }
    public required double DurationSeconds { get; init; }

    public int TotalFrames => Math.Max(1, (int)Math.Ceiling(DurationSeconds * Fps));
}
