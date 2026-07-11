using System.Numerics;
using GameOfLife3D.NET.Engine;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class Renderer3D : IDisposable
{
    // Above this instance count we skip the reflection pass and render the
    // floor as flat tinted water. Matches InstancedCellRenderer's beveled-cube
    // cutoff so the visual fidelity drop happens at one consistent threshold.

    private readonly GL _gl;
    private ShaderProgram? _cubeShader;
    private ShaderProgram? _pbrShader;
    private ShaderProgram? _wireframeShader;
    private ShaderProgram? _gridShader;
    private ShaderProgram? _floorShader;
    private InstancedCellRenderer? _instancedRenderer;
    private GridRenderer? _gridRenderer;
    private ReflectiveFloorRenderer? _floorRenderer;
    private PostProcessPipeline? _postProcess;
    private BloomEffect? _bloom;
    private ShapeThumbnailRenderer? _shapeThumbnails;

    private readonly RenderSettings _settings = new();
    private int _gridSize = 50;

    // Dirty tracking
    private int _lastDisplayStart = -1;
    private int _lastDisplayEnd = -1;
    private int _lastGenerationCount = -1;
    private float _lastMinY;
    private float _lastMaxY;
    private int _currentInstanceCount;

    // Instance offset where each visible generation starts, indexed by
    // (genIndex - displayStart). Enables append/truncate fast paths when the
    // display range only grows or shrinks at the end (playback, scrubbing).
    private readonly List<int> _genStartOffsets = new();
    // Set when a rebuild/append hit the buffer capacity mid-write; incremental
    // paths are unsafe until the next full rebuild.
    private bool _bufferCapped;

    // Background full-rebuild job. Full rebuilds above the threshold fill a
    // lazily allocated staging buffer on a worker task; the render thread
    // keeps drawing the stale buffer, then swaps + uploads on completion.
    private const int BackgroundRebuildThreshold = 250_000;
    // Minimum clamped roughness for GGX to avoid specular singularities at r=0.
    private const float MinRoughness = 0.02f;
    private Task<RebuildJobResult>? _rebuildJob;
    private InstanceData[]? _stagingBuffer;
    // Bumped by InvalidateState so an in-flight job computed from content
    // that has since changed (edits, session loads) is discarded on arrival.
    private int _rebuildVersion;

    private sealed record RebuildJobResult(
        int Version, int DisplayStart, int DisplayEnd, int GenerationCount,
        int InstanceCount, bool Capped, List<int> GenStartOffsets, InstanceData[] Buffer);

    // Preview cells for edit mode
    private int _previewCount;

    // When true, UpdateGenerations is skipped so the falling-cells buffer
    // written by CinematicController isn't overwritten mid-transition.
    private bool _fallingActive;

    public RenderSettings Settings => _settings;
    public PostProcessPipeline? PostProcess => _postProcess;
    public ShapeThumbnailRenderer? ShapeThumbnails => _shapeThumbnails;

    /// <summary>
    /// When true (set during video recording), full instance rebuilds run
    /// synchronously so captured frames can't lag behind the fixed-step
    /// recording clock by a frame or two.
    /// </summary>
    public bool ForceSynchronousRebuilds { get; set; }

    public Renderer3D(GL gl)
    {
        _gl = gl;
    }

    public void Initialize()
    {
        _cubeShader = ShaderProgram.FromEmbeddedResources(_gl, "cube.vert", "cube.frag");
        _pbrShader = ShaderProgram.FromEmbeddedResources(_gl, "cube.vert", "pbr_cell.frag");
        _wireframeShader = ShaderProgram.FromEmbeddedResources(_gl, "wireframe.vert", "wireframe.frag");
        _gridShader = ShaderProgram.FromEmbeddedResources(_gl, "grid.vert", "grid.frag");
        _floorShader = ShaderProgram.FromEmbeddedResources(_gl, "floor.vert", "floor.frag");

        _instancedRenderer = new InstancedCellRenderer(_gl);
        _instancedRenderer.Initialize();

        _shapeThumbnails = new ShapeThumbnailRenderer(_gl);
        _shapeThumbnails.Render(_instancedRenderer.GetMeshes());

        _gridRenderer = new GridRenderer(_gl);
        _gridRenderer.UpdateGrid(_gridSize);

        _floorRenderer = new ReflectiveFloorRenderer(_gl);
    }

    public void InitializePostProcess(int width, int height)
    {
        _postProcess = new PostProcessPipeline(_gl);
        _postProcess.Initialize(width, height);

        _bloom = new BloomEffect(_gl);
        _bloom.Initialize(width, height);

        _floorRenderer?.Initialize(width, height, _settings.ReflectionResolutionScale);
    }

    public void ResizePostProcess(int width, int height)
    {
        _postProcess?.Resize(width, height);
        _bloom?.Resize(width, height);
        _floorRenderer?.Resize(width, height, _settings.ReflectionResolutionScale);
    }

    public void SetGridSize(int size)
    {
        _gridSize = size;
        _gridRenderer?.UpdateGrid(size);
        InvalidateState();
    }

    /// <summary>
    /// Applies a PBR material to the cell renderer. Pass <c>null</c> to
    /// revert to the legacy Lambertian shader.
    /// </summary>
    public void SetMaterial(CellMaterial? material)
    {
        _settings.ActiveMaterial = material;
    }

    /// <summary>
    /// Uploads default SH L1 coefficients to the PBR shader. sh[0] (the DC /
    /// L0 term) encodes the average ambient color; the remaining 8 coefficients
    /// are zeroed, giving a uniform, direction-independent ambient contribution.
    ///
    /// The scale factor 0.28 ≈ 1/(2√π) converts from an average radiance to the
    /// L0 SH coefficient in the Ramamoorthi &amp; Hanrahan irradiance basis used
    /// by <c>evalIrradianceSH</c> in ibl.glsl.
    ///
    /// A later revision can replace this with actual SH baked from the starfield
    /// cubemap via <c>SetPbrSHCoefficients</c> without any shader API changes.
    /// </summary>
    private void UploadDefaultIblSH(float envIntensity)
    {
        if (_pbrShader == null) return;
        _pbrShader.Use();

        // 1/(2√π) ≈ 0.28: converts average radiance to L0 SH coefficient.
        float ambient = envIntensity * 0.28f;
        _pbrShader.SetUniform("uIblSh[0]", new System.Numerics.Vector3(ambient, ambient, ambient));
        for (int i = 1; i < 9; i++)
            _pbrShader.SetUniform($"uIblSh[{i}]", System.Numerics.Vector3.Zero);
    }

    public void InvalidateState()
    {
        _lastDisplayStart = -1;
        _lastDisplayEnd = -1;
        _lastGenerationCount = -1;
        _rebuildVersion++;
    }

    public void SetPreviewCells(ReadOnlySpan<InstanceData> previewCells)
    {
        if (_instancedRenderer == null) return;
        var buffer = _instancedRenderer.GetInstanceBuffer();
        int baseCount = _currentInstanceCount;
        int maxInstances = _instancedRenderer.MaxInstances;
        int previewAdded = 0;

        for (int i = 0; i < previewCells.Length && baseCount + i < maxInstances; i++)
        {
            buffer[baseCount + i] = previewCells[i];
            previewAdded++;
        }

        _previewCount = previewAdded;
        _instancedRenderer.SetInstanceCount(baseCount + previewAdded, baseCount, baseCount + previewAdded);
    }

    public void ClearPreviewCells()
    {
        if (_instancedRenderer == null || _previewCount == 0) return;
        // Count-only change: the base cells are untouched, nothing to upload.
        _instancedRenderer.SetInstanceCount(_currentInstanceCount, 0, 0);
        _previewCount = 0;
    }

    /// <summary>
    /// Exposes the raw instance buffer so an external system (the cinematic
    /// falling-cells controller) can write positions directly without an
    /// intermediate copy.
    /// </summary>
    public InstanceData[] GetInstanceBuffer() =>
        _instancedRenderer?.GetInstanceBuffer() ?? [];

    /// <summary>Maximum number of instances the GPU buffer can hold.</summary>
    public int MaxInstances => _instancedRenderer?.MaxInstances ?? 0;

    /// <summary>
    /// Commits the current contents of the instance buffer for rendering,
    /// using the supplied instance count. Used by the falling-cells
    /// transition after <see cref="FallingCellsPhysics"/> has written
    /// positions into the buffer obtained via <see cref="GetInstanceBuffer"/>.
    /// </summary>
    public void SetFallingCells(int count)
    {
        if (_instancedRenderer == null) return;
        _currentInstanceCount = Math.Min(count, _instancedRenderer.MaxInstances);
        _instancedRenderer.SetInstanceCount(_currentInstanceCount);
        _previewCount = 0;
    }

    /// <summary>
    /// Toggles the falling-cells transition guard. When active,
    /// <see cref="UpdateGenerations"/> is skipped so the physics-driven
    /// instance buffer isn't overwritten. Disabling also calls
    /// <see cref="InvalidateState"/> so the next UpdateGenerations rebuilds.
    /// </summary>
    public void SetFallingActive(bool active)
    {
        _fallingActive = active;
        if (!active) InvalidateState();
    }

    public void UpdateGenerations(IReadOnlyList<Generation> generations, int displayStart, int displayEnd)
    {
        if (_instancedRenderer == null) return;
        if (_fallingActive) return;

        TryApplyCompletedRebuildJob(generations, displayStart);

        bool stateChanged = displayStart != _lastDisplayStart ||
                           displayEnd != _lastDisplayEnd ||
                           generations.Count != _lastGenerationCount;

        if (!stateChanged) return;

        long rebuildStart = System.Diagnostics.Stopwatch.GetTimestamp();

        int lastEffectiveEnd = Math.Min(_lastDisplayEnd, _lastGenerationCount - 1);
        int newEffectiveEnd = Math.Min(displayEnd, generations.Count - 1);

        // Incremental paths are valid only when the window start is unchanged,
        // previous state exists, the engine's generation list only grew
        // (generations are append-only; edits/clears shrink the count or go
        // through InvalidateState), and the buffer never hit capacity.
        bool incremental = displayStart == _lastDisplayStart
                        && _lastDisplayStart >= 0
                        && generations.Count >= _lastGenerationCount
                        && !_bufferCapped;

        if (incremental && newEffectiveEnd >= lastEffectiveEnd)
        {
            AppendGenerations(generations, displayStart, lastEffectiveEnd, newEffectiveEnd);
        }
        else if (incremental)
        {
            TruncateGenerations(displayStart, newEffectiveEnd);
        }
        else
        {
            if (_rebuildJob != null)
            {
                // A background job is already computing a buffer, but it's
                // chasing a request that's now stale (the display range moved
                // again, or the generation count changed outside the
                // incremental window). Bump the version so its result is
                // discarded if/when it lands, and let a fresh request
                // supersede it rather than stalling until the old job
                // finishes. The old task keeps running harmlessly in the
                // background against its own (now-unshared) staging buffer;
                // attach a fault-observing continuation so an exception in it
                // doesn't go unnoticed just because we stop awaiting its result.
                _rebuildJob.ContinueWith(
                    static t => _ = t.Exception,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                _rebuildVersion++;
                _rebuildJob = null;
            }

            if (!ForceSynchronousRebuilds &&
                EstimateInstanceCount(generations, displayStart, newEffectiveEnd) > BackgroundRebuildThreshold)
            {
                // Tracking state is updated when the job result is applied.
                StartBackgroundRebuild(generations, displayStart, displayEnd, newEffectiveEnd);
                return;
            }

            RebuildAllGenerations(generations, displayStart, newEffectiveEnd);
        }

        RenderPerfStats.LastRebuildMs = System.Diagnostics.Stopwatch.GetElapsedTime(rebuildStart).TotalMilliseconds;
        RenderPerfStats.LastRebuildInstances = _currentInstanceCount;

        _lastMinY = displayStart;
        _lastMaxY = Math.Max(displayEnd, displayStart + 1);
        _lastDisplayStart = displayStart;
        _lastDisplayEnd = displayEnd;
        _lastGenerationCount = generations.Count;
    }

    private void TryApplyCompletedRebuildJob(IReadOnlyList<Generation> generations, int displayStart)
    {
        if (_rebuildJob is not { IsCompleted: true } job) return;
        _rebuildJob = null;

        if (!job.IsCompletedSuccessfully)
        {
            _ = job.Exception; // observe so a faulted job can't surface later
            return;
        }

        var result = job.Result;
        // Discard stale results: content invalidated since launch (version
        // bumped by InvalidateState or by a superseding request), window
        // start moved, the engine's generation list shrank (reset/edit), or
        // an incremental append/truncate already advanced the live buffer
        // past what this job represents while it was computing — applying it
        // now would visibly regress the display. The normal state-changed
        // path will pick up from wherever the live buffer currently is.
        if (result.Version != _rebuildVersion ||
            result.DisplayStart != displayStart ||
            generations.Count < result.GenerationCount ||
            _lastGenerationCount >= result.GenerationCount ||
            _lastDisplayEnd >= result.DisplayEnd)
        {
            // The write itself already completed successfully; reclaim the
            // buffer for the next job instead of letting it go to waste.
            _stagingBuffer ??= result.Buffer;
            return;
        }

        // The staging buffer is fully written; swap it in and keep the old
        // live buffer as the next staging target.
        _stagingBuffer = _instancedRenderer!.SwapInstanceBuffer(result.Buffer);

        _genStartOffsets.Clear();
        _genStartOffsets.AddRange(result.GenStartOffsets);
        _bufferCapped = result.Capped;
        _currentInstanceCount = result.InstanceCount;
        _previewCount = 0;
        _instancedRenderer.SetInstanceCount(result.InstanceCount);

        _lastMinY = result.DisplayStart;
        _lastMaxY = Math.Max(result.DisplayEnd, result.DisplayStart + 1);
        _lastDisplayStart = result.DisplayStart;
        _lastDisplayEnd = result.DisplayEnd;
        _lastGenerationCount = result.GenerationCount;

        RenderPerfStats.LastRebuildInstances = result.InstanceCount;
        // If the display range grew while the job ran, the append fast path
        // catches up on this same call via the state-changed check.
    }

    private void StartBackgroundRebuild(IReadOnlyList<Generation> generations, int displayStart, int displayEnd, int effectiveEnd)
    {
        // Loan out the free staging buffer to this job and clear the field so
        // a job superseding this one (before it completes) allocates its own
        // buffer instead of racing on the same array. The loaned buffer comes
        // back via _stagingBuffer once this job's result is observed —
        // applied, discarded-but-reclaimed, or (rarely) abandoned outright.
        var staging = _stagingBuffer ?? new InstanceData[_instancedRenderer!.MaxInstances];
        _stagingBuffer = null;

        // Snapshot the generation references on the render thread: Generation
        // instances are immutable once created and the engine only appends or
        // replaces list entries, so the worker can read them safely while the
        // engine keeps computing.
        var gens = new Generation[Math.Max(effectiveEnd - displayStart + 1, 0)];
        for (int i = 0; i < gens.Length; i++)
            gens[i] = generations[displayStart + i];

        int version = _rebuildVersion;
        int genCount = generations.Count;
        int maxInstances = _instancedRenderer!.MaxInstances;
        float halfSize = _gridSize / 2f;

        _rebuildJob = Task.Run(() =>
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            var offsets = new List<int>(gens.Length);
            int instanceIndex = 0;
            bool capped = false;

            for (int i = 0; i < gens.Length; i++)
            {
                offsets.Add(instanceIndex);
                int genIndex = displayStart + i;
                foreach (var cell in gens[i].LiveCells)
                {
                    if (instanceIndex >= maxInstances)
                    {
                        capped = true;
                        break;
                    }

                    staging[instanceIndex++] = new InstanceData
                    {
                        Position = new Vector3(cell.X - halfSize, genIndex, cell.Y - halfSize),
                        GenerationT = genIndex,
                    };
                }
            }

            RenderPerfStats.LastRebuildMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            return new RebuildJobResult(version, displayStart, displayEnd, genCount, instanceIndex, capped, offsets, staging);
        });
    }

    private static int EstimateInstanceCount(IReadOnlyList<Generation> generations, int displayStart, int effectiveEnd)
    {
        long total = 0;
        for (int genIndex = displayStart; genIndex <= effectiveEnd; genIndex++)
            total += generations[genIndex].LiveCells.Count;
        return (int)Math.Min(total, int.MaxValue);
    }

    private void RebuildAllGenerations(IReadOnlyList<Generation> generations, int displayStart, int effectiveEnd)
    {
        var buffer = _instancedRenderer!.GetInstanceBuffer();
        int maxInstances = _instancedRenderer.MaxInstances;
        int instanceIndex = 0;
        float halfSize = _gridSize / 2f;

        _genStartOffsets.Clear();
        _bufferCapped = false;

        for (int genIndex = displayStart; genIndex <= effectiveEnd; genIndex++)
        {
            _genStartOffsets.Add(instanceIndex);
            instanceIndex = WriteGeneration(buffer, generations[genIndex], genIndex, instanceIndex, maxInstances, halfSize);
        }

        _currentInstanceCount = instanceIndex;
        _previewCount = 0;
        _instancedRenderer.SetInstanceCount(instanceIndex, 0, instanceIndex);
    }

    private void AppendGenerations(IReadOnlyList<Generation> generations, int displayStart, int lastEffectiveEnd, int newEffectiveEnd)
    {
        var buffer = _instancedRenderer!.GetInstanceBuffer();
        int maxInstances = _instancedRenderer.MaxInstances;
        int instanceIndex = _currentInstanceCount;
        int appendFrom = instanceIndex;
        float halfSize = _gridSize / 2f;

        for (int genIndex = Math.Max(lastEffectiveEnd + 1, displayStart); genIndex <= newEffectiveEnd; genIndex++)
        {
            _genStartOffsets.Add(instanceIndex);
            instanceIndex = WriteGeneration(buffer, generations[genIndex], genIndex, instanceIndex, maxInstances, halfSize);
        }

        _currentInstanceCount = instanceIndex;
        _previewCount = 0;
        _instancedRenderer.SetInstanceCount(instanceIndex, appendFrom, instanceIndex);
    }

    private void TruncateGenerations(int displayStart, int newEffectiveEnd)
    {
        int keepGens = Math.Max(newEffectiveEnd - displayStart + 1, 0);
        int newCount = keepGens < _genStartOffsets.Count ? _genStartOffsets[keepGens] : _currentInstanceCount;

        if (_genStartOffsets.Count > keepGens)
            _genStartOffsets.RemoveRange(keepGens, _genStartOffsets.Count - keepGens);

        _currentInstanceCount = newCount;
        _previewCount = 0;
        // Pure count change — no buffer contents were touched, so no upload.
        _instancedRenderer!.SetInstanceCount(newCount, 0, 0);
    }

    private int WriteGeneration(InstanceData[] buffer, Generation generation, int genIndex, int instanceIndex, int maxInstances, float halfSize)
    {
        foreach (var cell in generation.LiveCells)
        {
            if (instanceIndex >= maxInstances)
            {
                _bufferCapped = true;
                break;
            }

            buffer[instanceIndex++] = new InstanceData
            {
                Position = new Vector3(cell.X - halfSize, genIndex, cell.Y - halfSize),
                GenerationT = genIndex,
            };
        }
        return instanceIndex;
    }

    public void Render(Matrix4x4 view, Matrix4x4 proj, int screenWidth, int screenHeight, double currentTime, int logicalWidth = 0, int logicalHeight = 0)
    {
        if (_instancedRenderer == null || _cubeShader == null || _wireframeShader == null || _gridShader == null)
            return;

        bool useFBO = _postProcess != null;

        float cycleTime = 5.0f;
        float normalizedTime = (float)(currentTime % cycleTime) / cycleTime;
        float range = _lastMaxY - _lastMinY;
        float time = normalizedTime * range;

        // ── Reflection pass ────────────────────────────────────────────────
        // Render cubes into the reflection FBO with a Y-mirrored view; the
        // floor fragment shader will sample this texture later. Skipped when
        // reflective floor isn't selected, when too many cubes would make the
        // double-render too costly, or when the floor renderer hasn't been
        // initialized yet (pre-FBO setup).
        bool wantsReflection = _settings.FloorMode == FloorMode.Reflective
                            && _floorRenderer is { IsInitialized: true }
                            && _floorShader != null
                            && _currentInstanceCount <= CellMeshGeometryFactory.BeveledCubeRenderFallbackThreshold;

        if (wantsReflection)
        {
            // The reflection is ripple-distorted anyway, so render it with the
            // cheap cube mesh and step the reflection resolution down as the
            // instance count rises — the main scene keeps full quality.
            _floorRenderer!.EnsureScale(_settings.ReflectionResolutionScale * ReflectionAutoScale(_currentInstanceCount));
            Vector3 reflectClear = _settings.FogEnabled ? _settings.FogColor : new Vector3(0.02f, 0.03f, 0.05f);
            _floorRenderer.BeginReflectionPass(reflectClear);

            Matrix4x4 mirroredView = ReflectiveFloorRenderer.MirrorViewAcrossFloor(view);

            _cubeShader.Use();
            _cubeShader.SetUniform("uMinY", _lastMinY);
            _cubeShader.SetUniform("uMaxY", _lastMaxY);

            _gl.Enable(EnableCap.DepthTest);
            _instancedRenderer.RenderSolid(_cubeShader, mirroredView, proj, time, _settings, CellShape.Cube);

            _floorRenderer.EndReflectionPass();
        }

        // ── Main scene ─────────────────────────────────────────────────────
        if (useFBO)
        {
            _postProcess!.BeginScene(_settings, view, proj);
        }
        else
        {
            _gl.Viewport(0, 0, (uint)screenWidth, (uint)screenHeight);
            // Reset the clear color before clearing — the reflection pass
            // above may have left it as the fog/water clear, which would
            // otherwise leak into the main render when post-process is off.
            _gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        // Choose the solid-cell shader based on whether a PBR material is active.
        // The reflection pass always uses the legacy cube shader for performance.
        bool usePbr = _settings.ActiveMaterial != null && _pbrShader != null;
        ShaderProgram cellShader = usePbr ? _pbrShader! : _cubeShader;

        // Set Y range uniforms (after BeginScene to avoid being shadowed by any
        // shader binds the background pass might do).
        cellShader.Use();
        cellShader.SetUniform("uMinY", _lastMinY);
        cellShader.SetUniform("uMaxY", _lastMaxY);

        _wireframeShader.Use();
        _wireframeShader.SetUniform("uMinY", _lastMinY);
        _wireframeShader.SetUniform("uMaxY", _lastMaxY);

        // Upload PBR material uniforms and IBL state when the PBR shader is active.
        if (usePbr)
        {
            var mat = _settings.ActiveMaterial!;
            Vector3 camPos = ExtractCameraPosition(view);

            _pbrShader!.Use();
            _pbrShader.SetUniform("uCameraPos", camPos);
            _pbrShader.SetUniform("uBaseColor", mat.BaseColor);
            _pbrShader.SetUniform("uBaseMetalness", mat.BaseMetalness);
            _pbrShader.SetUniform("uBaseDiffuseRoughness", mat.BaseDiffuseRoughness);
            _pbrShader.SetUniform("uSpecularRoughness", Math.Max(mat.SpecularRoughness, MinRoughness));
            _pbrShader.SetUniform("uSpecularIor", mat.SpecularIor);
            _pbrShader.SetUniform("uEmissionColor", mat.EmissionColor);
            _pbrShader.SetUniform("uEmissionLuminance", mat.EmissionLuminance);
            _pbrShader.SetUniform("uCoatWeight", mat.CoatWeight);
            _pbrShader.SetUniform("uCoatRoughness", Math.Max(mat.CoatRoughness, MinRoughness));
            _pbrShader.SetUniform("uCoatIor", mat.CoatIor);
            _pbrShader.SetUniform("uEnvIntensity", _settings.EnvIntensity);

            // Upload SH IBL coefficients scaled to the current env intensity.
            UploadDefaultIblSH(_settings.EnvIntensity);
        }

        // Render solid cubes
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _instancedRenderer.RenderSolid(cellShader, view, proj, time, _settings);
        _gl.Disable(EnableCap.Blend);

        // Render wireframe
        if (_settings.ShowWireframe)
        {
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _instancedRenderer.RenderWireframe(_wireframeShader, view, proj, time, _settings);
            _gl.Disable(EnableCap.Blend);
        }

        // ── Floor (Grid / Reflective / Off) ────────────────────────────────
        switch (_settings.FloorMode)
        {
            case FloorMode.Grid:
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                _gridRenderer!.Render(_gridShader, view, proj);
                _gl.Disable(EnableCap.Blend);
                break;

            case FloorMode.Reflective when _floorRenderer is { IsInitialized: true } && _floorShader != null:
                Vector3 cameraPos = ExtractCameraPosition(view);
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                // Don't write depth so cubes drawn earlier still occlude the
                // floor naturally, and so a translucent water edge doesn't
                // punch a hole in the depth buffer used by later effects.
                _gl.DepthMask(false);
                _floorRenderer.Render(_floorShader, view, proj, cameraPos, _gridSize, (float)currentTime,
                    _settings, reflectionAvailable: wantsReflection);
                _gl.DepthMask(true);
                _gl.Disable(EnableCap.Blend);
                break;

            case FloorMode.Off:
            default:
                break;
        }

        // End FBO scene and composite
        if (useFBO)
        {
            _bloom?.SetReducedQuality(_currentInstanceCount > CellMeshGeometryFactory.BeveledCubeRenderFallbackThreshold);
            _postProcess!.EndSceneAndComposite(_bloom, _settings);
        }

        // Render generation labels via ImGui overlay (uses logical pixel coordinates)
        if (_settings.ShowGenerationLabels && _lastDisplayStart >= 0)
        {
            int labelW = logicalWidth > 0 ? logicalWidth : screenWidth;
            int labelH = logicalHeight > 0 ? logicalHeight : screenHeight;
            TextRenderer.RenderGenerationLabels(
                _lastDisplayStart, _lastDisplayEnd, _gridSize,
                view, proj, labelW, labelH);
        }
    }

    /// <summary>
    /// World-space camera position is the translation column of the inverse
    /// view matrix. Cheap and avoids threading the camera through every render
    /// call site.
    /// </summary>
    private static Vector3 ExtractCameraPosition(Matrix4x4 view)
    {
        if (!Matrix4x4.Invert(view, out var inv))
            return Vector3.Zero;
        return new Vector3(inv.M41, inv.M42, inv.M43);
    }

    /// <summary>
    /// Automatic quality tiers for the reflection render target. Applied as a
    /// factor on top of the user's ReflectionResolutionScale so the setting
    /// itself is never mutated.
    /// </summary>
    private static float ReflectionAutoScale(int instanceCount) => instanceCount switch
    {
        >= 250_000 => 0.25f,
        >= 100_000 => 0.5f,
        _ => 1.0f,
    };

    public int GetVisibleCellCount() => _currentInstanceCount;

    public void Dispose()
    {
        _instancedRenderer?.Dispose();
        _gridRenderer?.Dispose();
        _floorRenderer?.Dispose();
        _cubeShader?.Dispose();
        _pbrShader?.Dispose();
        _wireframeShader?.Dispose();
        _gridShader?.Dispose();
        _floorShader?.Dispose();
        _postProcess?.Dispose();
        _bloom?.Dispose();
        _shapeThumbnails?.Dispose();
    }
}
