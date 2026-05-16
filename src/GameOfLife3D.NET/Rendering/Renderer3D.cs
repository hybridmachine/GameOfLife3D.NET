using System.Numerics;
using GameOfLife3D.NET.Engine;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class Renderer3D : IDisposable
{
    // Above this instance count we skip the reflection pass and render the
    // floor as flat tinted water. Matches InstancedCellRenderer's beveled-cube
    // cutoff so the visual fidelity drop happens at one consistent threshold.
    private const int ReflectionMaxInstances = 500_000;

    private readonly GL _gl;
    private ShaderProgram? _cubeShader;
    private ShaderProgram? _wireframeShader;
    private ShaderProgram? _gridShader;
    private ShaderProgram? _floorShader;
    private InstancedCellRenderer? _instancedRenderer;
    private GridRenderer? _gridRenderer;
    private ReflectiveFloorRenderer? _floorRenderer;
    private PostProcessPipeline? _postProcess;
    private BloomEffect? _bloom;

    private readonly RenderSettings _settings = new();
    private int _gridSize = 50;

    // Dirty tracking
    private int _lastDisplayStart = -1;
    private int _lastDisplayEnd = -1;
    private int _lastGenerationCount = -1;
    private float _lastMinY;
    private float _lastMaxY;
    private int _currentInstanceCount;

    // Preview cells for edit mode
    private int _previewCount;

    public RenderSettings Settings => _settings;
    public PostProcessPipeline? PostProcess => _postProcess;

    public Renderer3D(GL gl)
    {
        _gl = gl;
    }

    public void Initialize()
    {
        _cubeShader = ShaderProgram.FromEmbeddedResources(_gl, "cube.vert", "cube.frag");
        _wireframeShader = ShaderProgram.FromEmbeddedResources(_gl, "wireframe.vert", "wireframe.frag");
        _gridShader = ShaderProgram.FromEmbeddedResources(_gl, "grid.vert", "grid.frag");
        _floorShader = ShaderProgram.FromEmbeddedResources(_gl, "floor.vert", "floor.frag");

        _instancedRenderer = new InstancedCellRenderer(_gl);
        _instancedRenderer.Initialize();

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

    public void InvalidateState()
    {
        _lastDisplayStart = -1;
        _lastDisplayEnd = -1;
        _lastGenerationCount = -1;
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
        _instancedRenderer.SetInstanceCount(baseCount + previewAdded);
    }

    public void ClearPreviewCells()
    {
        if (_instancedRenderer == null || _previewCount == 0) return;
        _instancedRenderer.SetInstanceCount(_currentInstanceCount);
        _previewCount = 0;
    }

    public void UpdateGenerations(IReadOnlyList<Generation> generations, int displayStart, int displayEnd)
    {
        if (_instancedRenderer == null) return;

        bool stateChanged = displayStart != _lastDisplayStart ||
                           displayEnd != _lastDisplayEnd ||
                           generations.Count != _lastGenerationCount;

        if (!stateChanged) return;

        var buffer = _instancedRenderer.GetInstanceBuffer();
        int maxInstances = _instancedRenderer.MaxInstances;
        int instanceIndex = 0;
        float halfSize = _gridSize / 2f;

        for (int genIndex = displayStart; genIndex <= displayEnd && genIndex < generations.Count; genIndex++)
        {
            var generation = generations[genIndex];
            foreach (var cell in generation.LiveCells)
            {
                if (instanceIndex >= maxInstances) break;

                buffer[instanceIndex++] = new InstanceData
                {
                    Position = new Vector3(cell.X - halfSize, genIndex, cell.Y - halfSize),
                    GenerationT = genIndex,
                };
            }
        }

        _currentInstanceCount = instanceIndex;
        _instancedRenderer.SetInstanceCount(instanceIndex);

        _lastMinY = displayStart;
        _lastMaxY = Math.Max(displayEnd, displayStart + 1);
        _lastDisplayStart = displayStart;
        _lastDisplayEnd = displayEnd;
        _lastGenerationCount = generations.Count;
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
                            && _currentInstanceCount <= ReflectionMaxInstances;

        if (wantsReflection)
        {
            _floorRenderer!.EnsureScale(_settings.ReflectionResolutionScale);
            Vector3 reflectClear = _settings.FogEnabled ? _settings.FogColor : new Vector3(0.02f, 0.03f, 0.05f);
            _floorRenderer.BeginReflectionPass(reflectClear);

            Matrix4x4 mirroredView = ReflectiveFloorRenderer.MirrorViewAcrossFloor(view);

            _cubeShader.Use();
            _cubeShader.SetUniform("uMinY", _lastMinY);
            _cubeShader.SetUniform("uMaxY", _lastMaxY);

            _gl.Enable(EnableCap.DepthTest);
            _instancedRenderer.RenderSolid(_cubeShader, mirroredView, proj, time, _settings);

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

        // Set Y range uniforms (after BeginScene to avoid being shadowed by any
        // shader binds the background pass might do).
        _cubeShader.Use();
        _cubeShader.SetUniform("uMinY", _lastMinY);
        _cubeShader.SetUniform("uMaxY", _lastMaxY);

        _wireframeShader.Use();
        _wireframeShader.SetUniform("uMinY", _lastMinY);
        _wireframeShader.SetUniform("uMaxY", _lastMaxY);

        // Render solid cubes
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _instancedRenderer.RenderSolid(_cubeShader, view, proj, time, _settings);
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

    public int GetVisibleCellCount() => _currentInstanceCount;

    public void Dispose()
    {
        _instancedRenderer?.Dispose();
        _gridRenderer?.Dispose();
        _floorRenderer?.Dispose();
        _cubeShader?.Dispose();
        _wireframeShader?.Dispose();
        _gridShader?.Dispose();
        _floorShader?.Dispose();
        _postProcess?.Dispose();
        _bloom?.Dispose();
    }
}
