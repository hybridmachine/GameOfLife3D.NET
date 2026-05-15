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
    private static readonly string[] CuratedPatternIds =
    [
        "r-pentomino",
        "acorn",
        "diehard",
        "thunderbird",
        "gosper-glider-gun",
        "koks-galaxy",
        "pulsar",
        "pentadecathlon",
        "lwss",
        "mwss",
        "hwss",
    ];

    private readonly GameEngine _engine;
    private readonly CameraController _camera;
    private readonly ImGuiUI _ui;
    private readonly Renderer3D _renderer;
    private readonly PatternLoader _patternLoader;
    private readonly PatternLibrary _patternLibrary;

    private bool _isActive;
    private double _cycleStartTime;
    private double _lastRevealTime;
    private int _revealedEnd;
    private int _playlistIndex;
    private List<Vector3>? _savedGradientStops;
    private int _lastPaletteIndex = -1;

    public bool IsActive => _isActive;

    public CinematicController(
        GameEngine engine,
        CameraController camera,
        ImGuiUI ui,
        Renderer3D renderer,
        PatternLoader patternLoader,
        PatternLibrary patternLibrary)
    {
        _engine = engine;
        _camera = camera;
        _ui = ui;
        _renderer = renderer;
        _patternLoader = patternLoader;
        _patternLibrary = patternLibrary;
    }

    public void Start(double currentTime)
    {
        if (_isActive) return;

        _isActive = true;
        _playlistIndex = 0;
        _savedGradientStops = new List<Vector3>(_renderer.Settings.GradientStops);
        _lastPaletteIndex = ResolveCurrentPaletteIndex(_renderer.Settings.GradientStops);
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

        if (_savedGradientStops is not null)
        {
            _renderer.Settings.GradientStops = new List<Vector3>(_savedGradientStops);
            _ui.SyncGradientPresetLabel();
            _savedGradientStops = null;
            _lastPaletteIndex = -1;
        }

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
        ApplyNextPalette();

        int attemptsRemaining = CuratedPatternIds.Length + 1;
        while (attemptsRemaining-- > 0)
        {
            if (_playlistIndex < CuratedPatternIds.Length)
            {
                string patternId = CuratedPatternIds[_playlistIndex++];
                if (TryStartPatternCycle(patternId, currentTime))
                    return;

                continue;
            }

            _playlistIndex = 0;
            if (TryStartRandomCycle(currentTime))
                return;
        }

        // All curated patterns and the random fallback failed — stop rather than getting stuck.
        Stop();
    }

    private bool TryStartPatternCycle(string patternId, double currentTime)
    {
        string? patternName = _patternLibrary.Get(patternId)?.Name
            ?? _patternLoader.GetBuiltInPatternInfo(patternId)?.Name;
        var pattern = _patternLibrary.GetPattern(patternId)
            ?? _patternLoader.GetBuiltInPattern(patternId);
        if (pattern == null)
            return false;

        _engine.InitializeFromPattern(pattern);
        _engine.ComputeGenerations(PrecomputeCount);

        if (!TryStartPreparedCycle(currentTime))
            return false;

        _ui.StartCinematicPatternLabel(patternName ?? patternId, currentTime);
        return true;
    }

    private bool TryStartRandomCycle(double currentTime)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            float density = Random.Shared.NextSingle() * (MaxDensity - MinDensity) + MinDensity;
            _engine.InitializeRandom(density);
            _engine.ComputeGenerations(PrecomputeCount);

            if (TryStartPreparedCycle(currentTime))
            {
                _ui.StartCinematicPatternLabel("Random Seed", currentTime);
                return true;
            }
        }

        return false;
    }

    private bool TryStartPreparedCycle(double currentTime)
    {
        int endGeneration = Math.Min(PrecomputeCount - 1, _engine.GenerationCount - 1);
        if (endGeneration < 0)
            return false;

        _renderer.InvalidateState();

        _revealedEnd = 0;
        _ui.SetDisplayRange(0, 0);

        _cycleStartTime = currentTime;
        _lastRevealTime = currentTime;

        // Start with generation 0 fading in.
        _renderer.Settings.FadeGeneration = 0f;
        _renderer.Settings.FadeOpacity = 0f;

        var path = FlythroughPathGenerator.Generate(
            _engine.Generations,
            0, endGeneration,
            _engine.GridSize,
            _camera.Position,
            _camera.Target);

        if (path == null)
            return false;

        _camera.StartFlythrough(path, (pos, lookAt) =>
            FlythroughPathGenerator.Generate(
                _engine.Generations,
                0, endGeneration,
                _engine.GridSize, pos, lookAt));

        return true;
    }

    private void ApplyNextPalette()
    {
        int next = PickNextPaletteIndex();
        _renderer.Settings.GradientStops = new List<Vector3>(GradientPresets.Presets[next].Stops);
        _lastPaletteIndex = next;
        _ui.SyncGradientPresetLabel();
    }

    private int PickNextPaletteIndex()
    {
        int n = GradientPresets.Presets.Length;
        if (n <= 1) return 0;
        if (_lastPaletteIndex < 0) return Random.Shared.Next(n);
        int next = Random.Shared.Next(n - 1);
        if (next >= _lastPaletteIndex) next++;
        return next;
    }

    private static int ResolveCurrentPaletteIndex(IReadOnlyList<Vector3> stops)
    {
        string? name = GradientPresets.Match(stops);
        if (name is null) return -1;
        var presets = GradientPresets.Presets;
        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i].Name == name) return i;
        }
        return -1;
    }
}
