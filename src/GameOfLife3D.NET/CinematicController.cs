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
    private const double TransitionDurationSeconds = 20.0;
    private const double FadeOutDuration = 2.0;
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
    private static readonly CellShape[] AllCellShapes = Enum.GetValues<CellShape>();

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
    private CellShape? _savedShape;
    private int _lastShapeIndex = -1;

    // Falling-cells transition state
    private enum CinematicPhase { Revealing, Falling }
    private CinematicPhase _phase = CinematicPhase.Revealing;
    private double _fallStartTime;
    private double _lastUpdateTime;
    private readonly FallingCellsPhysics _physics = new();
    private Vector3[] _fallingPositions = [];
    private float[] _fallingGenT = [];

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
        _phase = CinematicPhase.Revealing;
        _playlistIndex = 0;
        _savedGradientStops = new List<Vector3>(_renderer.Settings.GradientStops);
        _lastPaletteIndex = ResolveCurrentPaletteIndex(_renderer.Settings.GradientStops);
        _savedShape = _renderer.Settings.Shape;
        _lastShapeIndex = Array.IndexOf(AllCellShapes, _renderer.Settings.Shape);
        _ui.Pause();
        StartNewCycle(currentTime);
    }

    public void Stop()
    {
        if (!_isActive) return;

        _isActive = false;
        _camera.StopFlythrough();

        // Clean up falling-cells transition state
        _physics.Clear();
        _phase = CinematicPhase.Revealing;
        _renderer.SetFallingActive(false);
        _renderer.Settings.GlobalAlpha = 1f;

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

        if (_savedShape.HasValue)
        {
            _renderer.Settings.Shape = _savedShape.Value;
            _savedShape = null;
            _lastShapeIndex = -1;
        }

        _ui.SyncDisplayRange();
    }

    public void Update(double currentTime)
    {
        if (!_isActive) return;

        if (_phase == CinematicPhase.Falling)
        {
            UpdateFalling(currentTime);
            return;
        }

        // Check if it's time for a new cycle
        if (currentTime - _cycleStartTime >= CycleDurationSeconds)
        {
            BeginFallingPhase(currentTime);
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

    private void BeginFallingPhase(double currentTime)
    {
        _phase = CinematicPhase.Falling;
        _fallStartTime = currentTime;
        _lastUpdateTime = currentTime;

        SnapshotVisibleCells();
        _renderer.SetFallingActive(true);

        // Disable per-generation fade; use GlobalAlpha for the fade-out instead.
        _renderer.Settings.FadeGeneration = -1f;
        _renderer.Settings.FadeOpacity = 1f;
        _renderer.Settings.GlobalAlpha = 1f;
    }

    private void UpdateFalling(double currentTime)
    {
        float delta = (float)Math.Clamp(currentTime - _lastUpdateTime, 0.0, 0.1);
        _lastUpdateTime = currentTime;

        _physics.Step(delta);

        var buffer = _renderer.GetInstanceBuffer();
        int count = _physics.WriteInstanceData(buffer, _renderer.MaxInstances);
        _renderer.SetFallingCells(count);

        double elapsed = currentTime - _fallStartTime;

        // Fade out in the final FadeOutDuration seconds.
        if (elapsed >= TransitionDurationSeconds - FadeOutDuration)
        {
            float fadeRemaining = (float)(TransitionDurationSeconds - elapsed);
            _renderer.Settings.GlobalAlpha = Math.Clamp(fadeRemaining / (float)FadeOutDuration, 0f, 1f);
        }

        if (elapsed >= TransitionDurationSeconds)
        {
            EndFallingPhase(currentTime);
        }
    }

    private void EndFallingPhase(double currentTime)
    {
        _physics.Clear();
        _renderer.SetFallingActive(false);
        _renderer.Settings.GlobalAlpha = 1f;
        _renderer.Settings.FadeGeneration = -1f;
        _renderer.Settings.FadeOpacity = 1f;
        _phase = CinematicPhase.Revealing;
        StartNewCycle(currentTime);
    }

    private void SnapshotVisibleCells()
    {
        float halfSize = _engine.GridSize / 2f;
        int maxCells = FallingCellsPhysics.MaxCells;

        if (_fallingPositions.Length < maxCells)
        {
            _fallingPositions = new Vector3[maxCells];
            _fallingGenT = new float[maxCells];
        }

        int count = 0;
        for (int g = 0; g <= _revealedEnd && g < _engine.GenerationCount; g++)
        {
            var gen = _engine.Generations[g];
            foreach (var cell in gen.LiveCells)
            {
                if (count >= maxCells) break;
                _fallingPositions[count] = new Vector3(cell.X - halfSize, g, cell.Y - halfSize);
                _fallingGenT[count] = g;
                count++;
            }
            if (count >= maxCells) break;
        }

        _physics.Initialize(_fallingPositions.AsSpan(0, count), _fallingGenT.AsSpan(0, count));
    }

    private void StartNewCycle(double currentTime)
    {
        ApplyNextPalette();
        ApplyNextCellShape();

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
        _renderer.Settings.GlobalAlpha = 1f;

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

    private void ApplyNextCellShape()
    {
        int next = PickNextCellShapeIndex();
        _renderer.Settings.Shape = AllCellShapes[next];
        _lastShapeIndex = next;
    }

    private int PickNextCellShapeIndex()
    {
        int n = AllCellShapes.Length;
        if (n <= 1) return 0;
        if (_lastShapeIndex < 0) return Random.Shared.Next(n);
        int next = Random.Shared.Next(n - 1);
        if (next >= _lastShapeIndex) next++;
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
