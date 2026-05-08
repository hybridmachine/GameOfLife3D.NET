using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Renders the reflective water-surface floor and owns the off-screen
/// reflection FBO that the main scene is rendered into (mirrored across Y=0)
/// before being sampled as a texture by the floor fragment shader.
///
/// The floor is a single quad spanning roughly 1.5x the play-field's grid size
/// with a circular alpha edge fade so it has no visible boundary. When the
/// reflection FBO is bypassed (e.g. instance count is too high) the floor still
/// renders but as a flat tinted water surface — the shader just receives a 1x1
/// black texture and a zero reflection-strength uniform.
/// </summary>
public sealed class ReflectiveFloorRenderer : IDisposable
{
    private readonly GL _gl;

    // Quad geometry: two triangles in XZ ∈ [-1, 1] (scaled at draw time).
    private uint _quadVao;
    private uint _quadVbo;

    // Reflection FBO — the main scene gets rendered into this with a Y-mirrored
    // view matrix; the floor shader then samples it via projective UVs.
    private uint _reflectFbo;
    private uint _reflectColorTex;
    private uint _reflectDepthRbo;
    private int _reflectWidth;
    private int _reflectHeight;
    private int _mainWidth;
    private int _mainHeight;
    private float _lastResolutionScale = -1f;

    // 1x1 black fallback texture used when no reflection has been rendered this
    // frame (FloorMode == Off branch never gets here, but Reflective with the
    // huge-instance fallback does).
    private uint _blackTex;

    public uint ReflectionTexture => _reflectColorTex;

    public ReflectiveFloorRenderer(GL gl)
    {
        _gl = gl;
    }

    public unsafe void Initialize(int mainWidth, int mainHeight, float resolutionScale)
    {
        CreateQuad();
        CreateBlackTexture();
        _mainWidth = mainWidth;
        _mainHeight = mainHeight;
        CreateReflectionFbo(resolutionScale);
    }

    public void Resize(int mainWidth, int mainHeight, float resolutionScale)
    {
        bool sizeChanged = mainWidth != _mainWidth || mainHeight != _mainHeight;
        bool scaleChanged = Math.Abs(resolutionScale - _lastResolutionScale) > 0.001f;
        if (!sizeChanged && !scaleChanged) return;

        _mainWidth = mainWidth;
        _mainHeight = mainHeight;
        DeleteReflectionFbo();
        CreateReflectionFbo(resolutionScale);
    }

    public void EnsureScale(float resolutionScale)
    {
        if (Math.Abs(resolutionScale - _lastResolutionScale) <= 0.001f) return;
        DeleteReflectionFbo();
        CreateReflectionFbo(resolutionScale);
    }

    private unsafe void CreateQuad()
    {
        // Two triangles spanning XZ ∈ [-1, 1].
        float[] verts =
        [
            -1f, -1f,
             1f, -1f,
             1f,  1f,
            -1f, -1f,
             1f,  1f,
            -1f,  1f,
        ];

        _quadVao = _gl.GenVertexArray();
        _quadVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_quadVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _quadVbo);
        fixed (float* ptr = verts)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        _gl.BindVertexArray(0);
    }

    private unsafe void CreateBlackTexture()
    {
        _blackTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _blackTex);
        byte[] pixel = [0, 0, 0, 255];
        fixed (byte* ptr = pixel)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private unsafe void CreateReflectionFbo(float resolutionScale)
    {
        float clamped = Math.Clamp(resolutionScale, 0.25f, 1f);
        _lastResolutionScale = clamped;
        _reflectWidth = Math.Max(1, (int)(_mainWidth * clamped));
        _reflectHeight = Math.Max(1, (int)(_mainHeight * clamped));

        _reflectColorTex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _reflectColorTex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba16f,
            (uint)_reflectWidth, (uint)_reflectHeight, 0, PixelFormat.Rgba, PixelType.Float, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _reflectDepthRbo = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _reflectDepthRbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24,
            (uint)_reflectWidth, (uint)_reflectHeight);

        _reflectFbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectFbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, _reflectColorTex, 0);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer, _reflectDepthRbo);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            Console.Error.WriteLine($"Reflection FBO incomplete: {status}");

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>
    /// Bind the reflection FBO and prepare the GL state. Caller is responsible
    /// for issuing draw calls with a Y-mirrored view matrix (see
    /// <see cref="MirrorViewAcrossFloor"/>) and then calling
    /// <see cref="EndReflectionPass"/>.
    /// </summary>
    public void BeginReflectionPass(Vector3 clearColor)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _reflectFbo);
        _gl.Viewport(0, 0, (uint)_reflectWidth, (uint)_reflectHeight);
        _gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        // Mirroring across Y inverts triangle winding; flip the front-face
        // direction for the duration of the pass instead of disabling culling.
        _gl.FrontFace(FrontFaceDirection.CW);
    }

    public void EndReflectionPass()
    {
        _gl.FrontFace(FrontFaceDirection.Ccw);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>
    /// Build the view matrix that renders the scene as if seen from the
    /// reflected camera (mirrored across the Y=0 plane). System.Numerics uses
    /// a row-vector convention, so applying the mirror to world coordinates
    /// before the original view is "mirror * view".
    /// </summary>
    public static Matrix4x4 MirrorViewAcrossFloor(Matrix4x4 view)
    {
        return Matrix4x4.CreateScale(1f, -1f, 1f) * view;
    }

    /// <summary>
    /// Draw the floor quad, sampling the reflection texture (or a black
    /// fallback if reflection is disabled). Caller should bind whatever
    /// framebuffer the floor should land in (typically the main scene FBO)
    /// before calling, and is responsible for blend state.
    /// </summary>
    public void Render(
        ShaderProgram shader,
        Matrix4x4 view,
        Matrix4x4 proj,
        Vector3 cameraPos,
        float gridSize,
        float currentTime,
        RenderSettings settings,
        bool reflectionAvailable)
    {
        shader.Use();

        // Floor extends 1.5x the grid; alpha fades from 0.7 → 1.0 of that radius.
        float halfExtent = gridSize * 0.75f;
        shader.SetUniform("uView", view);
        shader.SetUniform("uProjection", proj);
        shader.SetUniform("uFloorSize", halfExtent);
        shader.SetUniform("uFloorY", 0f);
        shader.SetUniform("uCameraPos", cameraPos);
        shader.SetUniform("uWaterTint", settings.WaterTint);
        shader.SetUniform("uReflectivity", Math.Clamp(settings.Reflectivity, 0f, 1f));
        shader.SetUniform("uReflectionStrength", reflectionAvailable ? 1f : 0f);
        shader.SetUniform("uTime", currentTime);
        shader.SetUniform("uWaveStrength", Math.Max(0f, settings.WaveStrength));
        shader.SetUniform("uWaveSpeed", Math.Max(0f, settings.WaveSpeed));
        shader.SetUniform("uFloorRadius", halfExtent);
        shader.SetUniform("uFadeStart", 0.7f);
        shader.SetUniform("uFogEnabled", settings.FogEnabled);
        shader.SetUniform("uFogStart", settings.FogStart);
        shader.SetUniform("uFogEnd", settings.FogEnd);
        shader.SetUniform("uFogColor", settings.FogColor);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, reflectionAvailable ? _reflectColorTex : _blackTex);
        shader.SetUniform("uReflectionTex", 0);

        _gl.BindVertexArray(_quadVao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
        _gl.BindVertexArray(0);
    }

    private void DeleteReflectionFbo()
    {
        if (_reflectColorTex != 0) _gl.DeleteTexture(_reflectColorTex);
        if (_reflectDepthRbo != 0) _gl.DeleteRenderbuffer(_reflectDepthRbo);
        if (_reflectFbo != 0) _gl.DeleteFramebuffer(_reflectFbo);
        _reflectColorTex = 0;
        _reflectDepthRbo = 0;
        _reflectFbo = 0;
    }

    public void Dispose()
    {
        DeleteReflectionFbo();
        if (_quadVao != 0) _gl.DeleteVertexArray(_quadVao);
        if (_quadVbo != 0) _gl.DeleteBuffer(_quadVbo);
        if (_blackTex != 0) _gl.DeleteTexture(_blackTex);
    }
}
