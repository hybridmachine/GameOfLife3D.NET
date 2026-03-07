using Silk.NET.OpenGL;
using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

public sealed class PostProcessPipeline : IDisposable
{
    private readonly GL _gl;

    // Scene FBO
    private uint _sceneFbo;
    private uint _sceneColorTexture;
    private uint _sceneDepthRbo;
    private int _width;
    private int _height;

    // Fullscreen quad
    private uint _quadVao;
    private uint _quadVbo;

    // Shaders
    private ShaderProgram? _compositeShader;
    private ShaderProgram? _backgroundShader;
    private uint _skyTexture;

    public uint SceneColorTexture => _sceneColorTexture;

    public PostProcessPipeline(GL gl)
    {
        _gl = gl;
    }

    public void Initialize(int width, int height)
    {
        _width = width;
        _height = height;

        CreateQuad();
        CreateSceneFBO(width, height);

        _compositeShader = ShaderProgram.FromEmbeddedResources(_gl, "postprocess.vert", "postprocess.frag");
        _backgroundShader = ShaderProgram.FromEmbeddedResources(_gl, "postprocess.vert", "background.frag");

        try
        {
            _skyTexture = EmbeddedTextureLoader.LoadTexture2D(
                _gl,
                "2k_stars_milky_way.jpg",
                TextureWrapMode.Repeat,
                TextureWrapMode.ClampToEdge);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load sky texture: {ex.Message}");
            _skyTexture = 0;
        }
    }

    private unsafe void CreateQuad()
    {
        float[] vertices =
        [
            // position   texcoord
            -1f, -1f,     0f, 0f,
             1f, -1f,     1f, 0f,
             1f,  1f,     1f, 1f,
            -1f, -1f,     0f, 0f,
             1f,  1f,     1f, 1f,
            -1f,  1f,     0f, 1f,
        ];

        _quadVao = _gl.GenVertexArray();
        _quadVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_quadVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);

        fixed (float* ptr = vertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);

        // Position: location 0
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);

        // TexCoord: location 1
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    private void CreateSceneFBO(int width, int height)
    {
        // Color texture
        _sceneColorTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _sceneColorTexture);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f,
                (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.Float, null);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        // Depth renderbuffer
        _sceneDepthRbo = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _sceneDepthRbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)width, (uint)height);

        // FBO
        _sceneFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _sceneColorTexture, 0);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, _sceneDepthRbo);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            Console.Error.WriteLine($"Scene FBO incomplete: {status}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Resize(int width, int height)
    {
        if (width == _width && height == _height) return;
        _width = width;
        _height = height;

        // Recreate textures at new size
        DeleteFBOResources();
        CreateSceneFBO(width, height);
    }

    public void BeginScene(
        RenderSettings settings,
        Matrix4x4 view,
        Matrix4x4 projection)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);

        Matrix4x4 viewProjection = view * projection;
        Matrix4x4 invViewProjection = Matrix4x4.Invert(viewProjection, out var inverted)
            ? inverted
            : Matrix4x4.Identity;

        if (settings.BackgroundMode != BackgroundMode.Solid)
        {
            // Draw background gradient (no depth write)
            _gl.Disable(EnableCap.DepthTest);
            RenderBackground(settings, invViewProjection);
            _gl.Enable(EnableCap.DepthTest);
            // Clear only depth, keep background color
            _gl.Clear(ClearBufferMask.DepthBufferBit);
        }
        else
        {
            _gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }
    }

    public void EndSceneAndComposite(BloomEffect? bloom, RenderSettings settings)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)_width, (uint)_height);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.Disable(EnableCap.DepthTest);

        // Run bloom if enabled
        uint bloomTexture = 0;
        if (bloom != null && settings.BloomEnabled)
        {
            bloom.Apply(_sceneColorTexture, _width, _height, settings.BloomThreshold);
            bloomTexture = bloom.OutputTexture;
        }

        // Final composite
        _compositeShader!.Use();

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _sceneColorTexture);
        _compositeShader.SetUniform("uSceneTexture", 0);
        _compositeShader.SetUniform("uBloomEnabled", settings.BloomEnabled && bloomTexture != 0);

        if (settings.BloomEnabled && bloomTexture != 0)
        {
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, bloomTexture);
            _compositeShader.SetUniform("uBloomTexture", 1);
            _compositeShader.SetUniform("uBloomIntensity", settings.BloomIntensity);
        }

        DrawQuad();

        _gl.Enable(EnableCap.DepthTest);
    }

    private void RenderBackground(
        RenderSettings settings,
        Matrix4x4 invViewProjection)
    {
        _backgroundShader!.Use();
        _backgroundShader.SetUniform("uTopColor", settings.BackgroundTopColor);
        _backgroundShader.SetUniform("uBottomColor", settings.BackgroundBottomColor);
        _backgroundShader.SetUniform("uStarfield", settings.BackgroundMode == BackgroundMode.Starfield && _skyTexture != 0);
        _backgroundShader.SetUniform("uInvViewProj", invViewProjection);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _skyTexture);
        _backgroundShader.SetUniform("uSkyTexture", 0);
        DrawQuad();
    }

    public void DrawQuad()
    {
        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    public unsafe byte[] ReadPixels()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
        var pixels = new byte[_width * _height * 4];
        fixed (byte* ptr = pixels)
        {
            _gl.ReadPixels(0, 0, (uint)_width, (uint)_height, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Flip vertically (OpenGL reads bottom-to-top)
        int rowSize = _width * 4;
        var flipped = new byte[pixels.Length];
        for (int y = 0; y < _height; y++)
        {
            Array.Copy(pixels, (_height - 1 - y) * rowSize, flipped, y * rowSize, rowSize);
        }
        return flipped;
    }

    public int Width => _width;
    public int Height => _height;

    private void DeleteFBOResources()
    {
        if (_sceneColorTexture != 0) _gl.DeleteTexture(_sceneColorTexture);
        if (_sceneDepthRbo != 0) _gl.DeleteRenderbuffer(_sceneDepthRbo);
        if (_sceneFbo != 0) _gl.DeleteFramebuffer(_sceneFbo);
    }

    public void Dispose()
    {
        DeleteFBOResources();
        if (_quadVao != 0) _gl.DeleteVertexArray(_quadVao);
        if (_quadVbo != 0) _gl.DeleteBuffer(_quadVbo);
        if (_skyTexture != 0) _gl.DeleteTexture(_skyTexture);
        _compositeShader?.Dispose();
        _backgroundShader?.Dispose();
    }
}
