using System.Numerics;
using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;
using GameOfLife3D.NET.UI;

namespace GameOfLife3D.NET;

public sealed class CinematicController
{
    private const double RevealIntervalSeconds = 0.5;
    private const float MinDensity = 0.10f;
    private const float MaxDensity = 0.40f;
    private const int PrecomputeCount = 50;
    private const int MaxRetries = 5;
    private const double CycleDurationSeconds = PrecomputeCount * RevealIntervalSeconds;

    private readonly GameEngine _engine;
    private readonly CameraController _camera;
    private readonly ImGuiUI _ui;
    private readonly Renderer3D _renderer;

    private bool _isActive;
    private double _cycleStartTime;
    private double _lastRevealTime;
    private int _revealedEnd;

    public bool IsActive => _isActive;

    public CinematicController(GameEngine engine, CameraController camera, ImGuiUI ui, Renderer3D renderer)
    {
        _engine = engine;
        _camera = camera;
        _ui = ui;
        _renderer = renderer;
    }

    public void Start(double currentTime)
    {
        if (_isActive) return;

        _isActive = true;
        _ui.Pause();
        StartNewCycle(currentTime);
    }

    public void Stop()
    {
        if (!_isActive) return;

        _isActive = false;
        _camera.StopFlythrough();

        // Clear fade effect
        _renderer.Settings.FadeGeneration = -1f;
        _renderer.Settings.FadeOpacity = 1f;

        _ui.SyncDisplayRange();
    }

    public void Update(double currentTime)
    {
        if (!_isActive) return;

        // Check if it's time for a new cycle
        if (currentTime - _cycleStartTime >= CycleDurationSeconds)
        {
            StartNewCycle(currentTime);
            return;
        }

        double timeSinceReveal = currentTime - _lastRevealTime;

        // Reveal next generation when fade completes
        if (timeSinceReveal >= RevealIntervalSeconds &&
            _revealedEnd < _engine.GenerationCount - 1)
        {
            _revealedEnd++;
            _ui.SetDisplayEnd(_revealedEnd);
            _lastRevealTime = currentTime;
            timeSinceReveal = 0.0;
        }

        // Update fade opacity for the current generation being revealed
        float fadeProgress = (float)Math.Clamp(timeSinceReveal / RevealIntervalSeconds, 0.0, 1.0);
        _renderer.Settings.FadeGeneration = _revealedEnd;
        _renderer.Settings.FadeOpacity = fadeProgress;
    }

    private void StartNewCycle(double currentTime)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            float density = Random.Shared.NextSingle() * (MaxDensity - MinDensity) + MinDensity;
            _engine.InitializeRandom(density);
            _engine.ComputeGenerations(PrecomputeCount);
            _renderer.InvalidateState();

            _revealedEnd = 0;
            _ui.SetDisplayRange(0, 0);

            _cycleStartTime = currentTime;
            _lastRevealTime = currentTime;

            // Start with generation 0 fading in
            _renderer.Settings.FadeGeneration = 0f;
            _renderer.Settings.FadeOpacity = 0f;

            // Generate flythrough path using full precomputed range
            var path = FlythroughPathGenerator.Generate(
                _engine.Generations,
                0, PrecomputeCount - 1,
                _engine.GridSize,
                _camera.Position,
                _camera.Target);

            if (path != null)
            {
                _camera.StartFlythrough(path, (pos, lookAt) =>
                    FlythroughPathGenerator.Generate(
                        _engine.Generations,
                        0, PrecomputeCount - 1,
                        _engine.GridSize, pos, lookAt));
                return;
            }
        }

        // All retries exhausted — stop cinematic mode rather than getting stuck
        Stop();
    }
}
