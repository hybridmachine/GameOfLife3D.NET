using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class BloomEffect : IDisposable
{
    private readonly GL _gl;

    private ShaderProgram? _brightPassShader;
    private ShaderProgram? _blurShader;

    // Ping-pong FBOs at quarter resolution
    private uint _brightFbo;
    private uint _brightTexture;
    private readonly uint[] _pingPongFbos = new uint[2];
    private readonly uint[] _pingPongTextures = new uint[2];

    // Fullscreen quad (shared via pipeline)
    private uint _quadVao;
    private uint _quadVbo;

    private int _bloomWidth;
    private int _bloomHeight;

    public uint OutputTexture => _pingPongTextures[0];

    public BloomEffect(GL gl)
    {
        _gl = gl;
    }

    public void Initialize(int fullWidth, int fullHeight)
    {
        _bloomWidth = fullWidth / 4;
        _bloomHeight = fullHeight / 4;

        CreateQuad();
        CreateFBOs();

        _brightPassShader = ShaderProgram.FromEmbeddedResources(_gl, "postprocess.vert", "bloom_bright.frag");
        _blurShader = ShaderProgram.FromEmbeddedResources(_gl, "postprocess.vert", "bloom_blur.frag");
    }

    public void Resize(int fullWidth, int fullHeight)
    {
        int newW = fullWidth / 4;
        int newH = fullHeight / 4;
        if (newW == _bloomWidth && newH == _bloomHeight) return;

        _bloomWidth = newW;
        _bloomHeight = newH;
        DeleteFBOs();
        CreateFBOs();
    }

    public void Apply(uint sceneTexture, int fullWidth, int fullHeight, float threshold)
    {
        if (_brightPassShader == null || _blurShader == null) return;

        _gl.Viewport(0, 0, (uint)_bloomWidth, (uint)_bloomHeight);
        _gl.Disable(EnableCap.DepthTest);

        // Step 1: Bright-pass extraction
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _brightFbo);
        _brightPassShader.Use();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, sceneTexture);
        SetSamplerUniform(_brightPassShader, "uSceneTexture", 0);
        _brightPassShader.SetUniform("uThreshold", threshold);
        DrawQuad();

        // Step 2: Two-pass Gaussian blur (ping-pong)
        bool horizontal = true;
        uint inputTexture = _brightTexture;

        for (int i = 0; i < 6; i++) // 3 horizontal + 3 vertical passes
        {
            int targetIdx = horizontal ? 1 : 0;
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _pingPongFbos[targetIdx]);

            _blurShader.Use();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, inputTexture);
            SetSamplerUniform(_blurShader, "uImage", 0);
            _blurShader.SetUniform("uHorizontal", horizontal);
            DrawQuad();

            inputTexture = _pingPongTextures[targetIdx];
            horizontal = !horizontal;
        }

        // Result is in pingPongTextures[0]
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)fullWidth, (uint)fullHeight);
    }

    private void SetSamplerUniform(ShaderProgram shader, string name, int unit)
    {
        int loc = shader.GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, unit);
    }

    private unsafe void CreateQuad()
    {
        float[] vertices =
        [
            -1f, -1f,  0f, 0f,
             1f, -1f,  1f, 0f,
             1f,  1f,  1f, 1f,
            -1f, -1f,  0f, 0f,
             1f,  1f,  1f, 1f,
            -1f,  1f,  0f, 1f,
        ];

        _quadVao = _gl.GenVertexArray();
        _quadVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_quadVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);

        fixed (float* ptr = vertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    private void CreateFBOs()
    {
        _brightTexture = CreateColorTexture(_bloomWidth, _bloomHeight);
        _brightFbo = CreateFBO(_brightTexture);

        for (int i = 0; i < 2; i++)
        {
            _pingPongTextures[i] = CreateColorTexture(_bloomWidth, _bloomHeight);
            _pingPongFbos[i] = CreateFBO(_pingPongTextures[i]);
        }
    }

    private uint CreateColorTexture(int w, int h)
    {
        uint tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        unsafe
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f,
                (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.Float, null);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        return tex;
    }

    private uint CreateFBO(uint colorTexture)
    {
        uint fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, colorTexture, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return fbo;
    }

    private void DrawQuad()
    {
        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    private void DeleteFBOs()
    {
        if (_brightTexture != 0) _gl.DeleteTexture(_brightTexture);
        if (_brightFbo != 0) _gl.DeleteFramebuffer(_brightFbo);

        for (int i = 0; i < 2; i++)
        {
            if (_pingPongTextures[i] != 0) _gl.DeleteTexture(_pingPongTextures[i]);
            if (_pingPongFbos[i] != 0) _gl.DeleteFramebuffer(_pingPongFbos[i]);
        }
    }

    public void Dispose()
    {
        DeleteFBOs();
        if (_quadVao != 0) _gl.DeleteVertexArray(_quadVao);
        if (_quadVbo != 0) _gl.DeleteBuffer(_quadVbo);
        _brightPassShader?.Dispose();
        _blurShader?.Dispose();
    }
}
