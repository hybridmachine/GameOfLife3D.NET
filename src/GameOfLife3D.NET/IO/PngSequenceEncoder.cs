namespace GameOfLife3D.NET.IO;

// Writes one PNG per frame into an output directory using the existing ScreenshotCapture encoder.
public sealed class PngSequenceEncoder : IVideoEncoder
{
    private readonly string _outputDir;
    private readonly string _baseName;
    private int _frameIndex;

    public bool IsHealthy { get; private set; } = true;
    public string? LastError { get; private set; }

    public PngSequenceEncoder(string outputDir, string baseName = "frame")
    {
        _outputDir = outputDir;
        _baseName = baseName;
        Directory.CreateDirectory(_outputDir);
    }

    public void WriteFrame(ReadOnlySpan<byte> rgbaPixels, int width, int height)
    {
        if (!IsHealthy) throw new InvalidOperationException(LastError ?? "Encoder not healthy.");

        try
        {
            string path = Path.Combine(_outputDir, $"{_baseName}_{_frameIndex:D6}.png");
            // ScreenshotCapture takes a byte[]; rent a copy from the span. Allocation is fine here:
            // the calling thread has already paid for a glReadPixels on every frame.
            ScreenshotCapture.SavePng(path, rgbaPixels.ToArray(), width, height);
            _frameIndex++;
        }
        catch (Exception ex)
        {
            IsHealthy = false;
            LastError = ex.Message;
            throw;
        }
    }

    public void Finish() { /* no-op; PNGs flush on each WriteFrame */ }

    public void Cancel()
    {
        IsHealthy = false;
        // Intentionally do not delete partial files: PNG sequences are useful even when truncated.
    }

    public void Dispose() { }
}
