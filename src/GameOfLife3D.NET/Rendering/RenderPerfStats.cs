namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Lightweight instrumentation for the instance-buffer hot path. Written by
/// <see cref="Renderer3D"/> (CPU rebuild) and <see cref="InstancedCellRenderer"/>
/// (GPU upload), read by the status bar when perf stats are enabled. Values
/// hold the most recent occurrence of each event rather than per-frame
/// averages, since rebuilds/uploads only happen when state changes.
/// </summary>
public static class RenderPerfStats
{
    /// <summary>Milliseconds spent in the last CPU-side instance rebuild.</summary>
    public static double LastRebuildMs { get; set; }

    /// <summary>Number of instances written by the last rebuild.</summary>
    public static int LastRebuildInstances { get; set; }

    /// <summary>Milliseconds spent in the last BufferSubData upload.</summary>
    public static double LastUploadMs { get; set; }

    /// <summary>Bytes transferred by the last BufferSubData upload.</summary>
    public static long LastUploadBytes { get; set; }
}
