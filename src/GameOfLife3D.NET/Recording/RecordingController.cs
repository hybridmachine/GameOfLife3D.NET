using GameOfLife3D.NET.IO;

namespace GameOfLife3D.NET.Recording;

// Drives a deterministic, frame-accurate recording session by piping the live post-bloom
// composite to an ffmpeg encoder. Captures whatever is currently rendered — whatever camera
// mode, playback state, and display range the user has active. No camera/UI takeover.
public sealed class RecordingController
{
    private RecordingSettings? _settings;
    private IVideoEncoder? _encoder;

    public RecordingClock Clock { get; } = new();

    public bool IsActive { get; private set; }
    public bool IsComplete => _settings != null && Clock.FrameIndex >= _settings.TotalFrames;
    public string? LastError { get; private set; }
    public RecordingSettings? Settings => _settings;

    public int CurrentFrame => Clock.FrameIndex;
    public int TotalFrames => _settings?.TotalFrames ?? 0;
    public double CurrentRecordingTime => Clock.CurrentTime;

    public void Begin(RecordingSettings settings)
    {
        if (IsActive) throw new InvalidOperationException("Recording already in progress.");

        LastError = null;

        // Construct the encoder before committing _settings so a launch failure leaves the
        // controller fully reset (Settings == null, IsActive == false) instead of partially-initialized.
        IVideoEncoder encoder;
        try
        {
            encoder = CreateEncoder(settings);
        }
        catch
        {
            _settings = null;
            throw;
        }

        _settings = settings;
        _encoder = encoder;
        Clock.Reset(settings.Fps);
        IsActive = true;
    }

    public void WriteFrame(byte[] rgbaPixels, int byteCount)
    {
        if (!IsActive || _encoder == null || _settings == null)
        {
            // Producer surrendered ownership; return to the pool so we don't leak it.
            System.Buffers.ArrayPool<byte>.Shared.Return(rgbaPixels);
            return;
        }
        try
        {
            _encoder.WriteFrame(rgbaPixels, byteCount);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            // FfmpegEncoder.WriteFrame returns the buffer to the pool on its own error paths.
            CancelInternal(deletePartial: true);
        }
    }

    public void AdvanceFrame()
    {
        if (!IsActive) return;
        Clock.Advance();
    }

    public void Finish()
    {
        if (!IsActive) return;
        try
        {
            _encoder?.Finish();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        TeardownInternal();
    }

    public void Cancel()
    {
        if (!IsActive) return;
        CancelInternal(deletePartial: true);
    }

    private void CancelInternal(bool deletePartial)
    {
        try
        {
            if (deletePartial) _encoder?.Cancel();
            else _encoder?.Finish();
        }
        catch { /* best-effort */ }
        TeardownInternal();
    }

    private void TeardownInternal()
    {
        try { _encoder?.Dispose(); } catch { }
        _encoder = null;
        _settings = null;
        IsActive = false;
    }

    private static IVideoEncoder CreateEncoder(RecordingSettings settings)
    {
        string? ffmpegPath = FfmpegEncoder.LocateBinary()
            ?? throw new InvalidOperationException(FfmpegEncoder.InstallInstructions());
        return new FfmpegEncoder(ffmpegPath, settings.Codec, settings.Width, settings.Height, settings.Fps, settings.OutputPath);
    }
}
