using GameOfLife3D.NET.IO;

namespace GameOfLife3D.NET.Recording;

public sealed record RecordingSettings
{
    public required VideoCodec Codec { get; init; }
    public required int Fps { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string OutputPath { get; init; }   // file path for video; directory for PNG sequence

    // Total recorded duration in seconds. TotalFrames = ceil(DurationSeconds * Fps).
    public required double DurationSeconds { get; init; }

    // Generation playback during the recording.
    public required int StartGeneration { get; init; }
    public required int EndGeneration { get; init; }
    // Generations advanced per recording-second of output. e.g. 2.0 → 60s recording covers 120 generations.
    public required double GenerationsPerSecond { get; init; }

    public bool HideHud { get; init; } = true;

    public int TotalFrames => Math.Max(1, (int)Math.Ceiling(DurationSeconds * Fps));
}
