namespace GameOfLife3D.NET.IO;

public interface IVideoEncoder : IDisposable
{
    // Hands ownership of the buffer to the encoder. The encoder is responsible for returning
    // the buffer to System.Buffers.ArrayPool<byte>.Shared once it has been written. The caller
    // must not reuse the buffer after this call. byteCount is the number of leading bytes in
    // the buffer that contain pixel data; pooled buffers may have Length >= byteCount.
    void WriteFrame(byte[] buffer, int byteCount);
    void Finish();
    void Cancel();
    bool IsHealthy { get; }
    string? LastError { get; }
}
