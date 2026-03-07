using System.Numerics;
using GameOfLife3D.NET.Engine;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class Renderer3D : IDisposable
{
    private readonly GL _gl;
    private ShaderProgram? _cubeShader;
    private ShaderProgram? _wireframeShader;
    private ShaderProgram? _gridShader;
    private InstancedCubeRenderer? _instancedRenderer;
    private GridRenderer? _gridRenderer;
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

        _instancedRenderer = new InstancedCubeRenderer(_gl);
        _instancedRenderer.Initialize();

        _gridRenderer = new GridRenderer(_gl);
        _gridRenderer.UpdateGrid(_gridSize);
    }

    public void InitializePostProcess(int width, int height)
    {
        _postProcess = new PostProcessPipeline(_gl);
        _postProcess.Initialize(width, height);

        _bloom = new BloomEffect(_gl);
        _bloom.Initialize(width, height);
    }

    public void ResizePostProcess(int width, int height)
    {
        _postProcess?.Resize(width, height);
        _bloom?.Resize(width, height);
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

        // Begin FBO scene if available
        if (useFBO)
        {
            _postProcess!.BeginScene(_settings, view, proj);
        }
        else
        {
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        float cycleTime = 5.0f;
        float normalizedTime = (float)(currentTime % cycleTime) / cycleTime;
        float range = _lastMaxY - _lastMinY;
        float time = normalizedTime * range;

        // Set Y range uniforms
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

        // Render grid
        if (_settings.ShowGridLines)
        {
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gridRenderer!.Render(_gridShader, view, proj);
            _gl.Disable(EnableCap.Blend);
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

    public int GetVisibleCellCount() => _currentInstanceCount;

    public void Dispose()
    {
        _instancedRenderer?.Dispose();
        _gridRenderer?.Dispose();
        _cubeShader?.Dispose();
        _wireframeShader?.Dispose();
        _gridShader?.Dispose();
        _postProcess?.Dispose();
        _bloom?.Dispose();
    }
}
