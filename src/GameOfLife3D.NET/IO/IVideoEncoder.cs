namespace GameOfLife3D.NET.IO;

public interface IVideoEncoder : IDisposable
{
    void WriteFrame(ReadOnlySpan<byte> rgbaPixels, int width, int height);
    void Finish();
    void Cancel();
    bool IsHealthy { get; }
    string? LastError { get; }
}
