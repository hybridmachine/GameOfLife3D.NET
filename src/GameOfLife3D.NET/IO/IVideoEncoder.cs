namespace GameOfLife3D.NET.IO;

public interface IVideoEncoder : IDisposable
{
    // Hands ownership of the buffer to the encoder. The caller must not reuse it afterward.
    void WriteFrame(byte[] rgbaPixels, int width, int height);
    void Finish();
    void Cancel();
    bool IsHealthy { get; }
    string? LastError { get; }
}
