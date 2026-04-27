namespace GameOfLife3D.NET.Recording;

// Deterministic frame-counter clock used in place of wall-clock during recording.
// CurrentTime advances by exactly 1/fps per frame, regardless of vsync/jitter.
public sealed class RecordingClock
{
    private int _frameIndex;
    private int _fps;

    public int FrameIndex => _frameIndex;
    public int Fps => _fps;
    public double FrameDelta => _fps > 0 ? 1.0 / _fps : 0.0;
    public double CurrentTime => _fps > 0 ? (double)_frameIndex / _fps : 0.0;

    public void Reset(int fps)
    {
        if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
        _fps = fps;
        _frameIndex = 0;
    }

    public void Advance() => _frameIndex++;
}
