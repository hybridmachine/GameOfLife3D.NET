using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.IO;
using GameOfLife3D.NET.Rendering;
using GameOfLife3D.NET.UI;

namespace GameOfLife3D.NET.Recording;

// Drives a deterministic, frame-accurate recording session:
//  * Owns the RecordingClock (1/fps per frame).
//  * Owns the IVideoEncoder (PNG sequence or ffmpeg pipe).
//  * Drives camera (via FlythroughPath built from keyframes) and generation scrubbing.
//  * Restores prior camera/UI state on Finish/Cancel.
public sealed class RecordingController
{
    private RecordingSettings? _settings;
    private IVideoEncoder? _encoder;
    private CameraController? _camera;
    private GameEngine? _engine;
    private ImGuiUI? _ui;
    private CameraState? _preRecordingCameraState;
    private bool _preRecordingPlaying;
    private int _preRecordingDisplayStart;
    private int _preRecordingDisplayEnd;
    private bool _preRecordingHudHidden;

    public RecordingClock Clock { get; } = new();

    public bool IsActive { get; private set; }
    public bool IsComplete => _settings != null && Clock.FrameIndex >= _settings.TotalFrames;
    public string? LastError { get; private set; }
    public RecordingSettings? Settings => _settings;

    public int CurrentFrame => Clock.FrameIndex;
    public int TotalFrames => _settings?.TotalFrames ?? 0;
    public double CurrentRecordingTime => Clock.CurrentTime;

    // Begins a recording. Throws on misconfiguration; sets IsActive on success.
    public void Begin(
        RecordingSettings settings,
        IReadOnlyList<CameraKeyframe> keyframes,
        CameraController camera,
        GameEngine engine,
        ImGuiUI ui)
    {
        if (IsActive) throw new InvalidOperationException("Recording already in progress.");
        if (keyframes.Count < 2) throw new ArgumentException("Need at least 2 keyframes.", nameof(keyframes));

        _settings = settings;
        _camera = camera;
        _engine = engine;
        _ui = ui;
        LastError = null;

        // Ensure required generations are precomputed.
        int requiredGens = Math.Max(settings.EndGeneration + 1, settings.StartGeneration + 1);
        if (engine.GenerationCount < requiredGens)
            engine.ComputeGenerations(requiredGens);

        // Snapshot UI/camera state for restoration.
        _preRecordingCameraState = camera.GetState();
        _preRecordingPlaying = ui.IsPlaying;
        _preRecordingDisplayStart = ui.DisplayStart;
        _preRecordingDisplayEnd = ui.DisplayEnd;
        _preRecordingHudHidden = ui.IsCinematicModeActive;

        ui.Pause();
        ui.SetDisplayRange(settings.StartGeneration, Math.Min(settings.EndGeneration, engine.GenerationCount - 1));

        // Build keyframe-driven camera path. Stretch the keyframes to the recording duration:
        // editor produces times in [0, last], recording wants playback to span [0, DurationSeconds].
        var sortedKeys = keyframes.OrderBy(k => k.TimeSeconds).ToList();
        double keyframeSpan = sortedKeys[^1].TimeSeconds - sortedKeys[0].TimeSeconds;
        if (keyframeSpan <= 0) throw new ArgumentException("Keyframes must span a positive duration.");

        double scale = settings.DurationSeconds / keyframeSpan;
        var scaledKeys = sortedKeys
            .Select(k => new CameraKeyframe((k.TimeSeconds - sortedKeys[0].TimeSeconds) * scale, k.State))
            .ToList();

        var path = FlythroughPath.FromKeyframes(scaledKeys);
        camera.StartFlythrough(path);

        // Open the encoder.
        _encoder = CreateEncoder(settings);

        Clock.Reset(settings.Fps);
        IsActive = true;
    }

    // Called per-frame after rendering. Reads the post-bloom composite into the encoder.
    public void WriteFrame(byte[] rgbaPixels)
    {
        if (!IsActive || _encoder == null || _settings == null) return;
        try
        {
            _encoder.WriteFrame(rgbaPixels, _settings.Width, _settings.Height);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            CancelInternal(deletePartial: false);
        }
    }

    // Advance generation scrubbing + frame counter. Call once per recorded frame, before rendering.
    public void Tick()
    {
        if (!IsActive || _settings == null || _ui == null) return;

        // Generation scrub: linear interpolation across [StartGen, EndGen] over DurationSeconds.
        double t = Math.Clamp(Clock.CurrentTime / _settings.DurationSeconds, 0.0, 1.0);
        int genRangeSize = Math.Max(0, _settings.EndGeneration - _settings.StartGeneration);
        int currentGen = _settings.StartGeneration + (int)Math.Round(t * genRangeSize);
        currentGen = Math.Clamp(currentGen, _settings.StartGeneration, _settings.EndGeneration);
        _ui.SetDisplayEnd(currentGen);
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

        // Restore camera and UI state.
        if (_camera != null)
        {
            if (_camera.IsFlythroughActive) _camera.StopFlythrough();
            if (_preRecordingCameraState != null) _camera.SetState(_preRecordingCameraState);
        }
        if (_ui != null)
        {
            _ui.SetDisplayRange(_preRecordingDisplayStart, _preRecordingDisplayEnd);
            _ui.IsCinematicModeActive = _preRecordingHudHidden;
            if (_preRecordingPlaying) _ui.TogglePlayPause();
        }

        IsActive = false;
    }

    private static IVideoEncoder CreateEncoder(RecordingSettings settings)
    {
        string? ffmpegPath = FfmpegEncoder.LocateBinary()
            ?? throw new InvalidOperationException(FfmpegEncoder.InstallInstructions());
        return new FfmpegEncoder(ffmpegPath, settings.Codec, settings.Width, settings.Height, settings.Fps, settings.OutputPath);
    }
}
