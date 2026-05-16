using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Renders each registered CellShape mesh once at startup into a small RGBA
/// texture and exposes the GL texture handles for ImGui Image() calls. The
/// thumbnails are flat-shaded with a neutral mid-gray so the shape itself is
/// readable regardless of the user's current cell color.
/// </summary>
public sealed class ShapeThumbnailRenderer : IDisposable
{
    private const int Size = 32;
    private static readonly Vector3 ThumbColor = new(0.65f, 0.78f, 0.90f);

    private readonly GL _gl;
    private readonly Dictionary<CellShape, uint> _textures = new();
    private ShaderProgram? _shader;

    public ShapeThumbnailRenderer(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Returns the GL texture handle for the given shape's thumbnail, or
    /// null if the shape hasn't been rendered yet (e.g. Render not called).
    /// </summary>
    public uint? GetTexture(CellShape shape)
        => _textures.TryGetValue(shape, out var id) ? id : null;

    /// <summary>
    /// Renders one 32×32 RGBA thumbnail per supplied mesh into a fresh GL
    /// texture. Saves and restores the previous viewport, framebuffer binding,
    /// and depth-test state.
    ///
    /// Intended to be called exactly once at startup, before any scene shader
    /// or texture is bound. The method does not save/restore the active shader
    /// program or the GL_TEXTURE_BINDING_2D unit; if a future caller invokes
    /// it mid-frame those bindings will be clobbered.
    /// </summary>
    public unsafe void Render(IReadOnlyDictionary<CellShape, IInstancedMesh> meshes)
    {
        _shader = ShaderProgram.FromEmbeddedResources(_gl, "thumb.vert", "thumb.frag");

        // Save GL state we're about to perturb.
        int[] prevViewport = new int[4];
        _gl.GetInteger(GLEnum.Viewport, prevViewport);
        _gl.GetInteger(GLEnum.FramebufferBinding, out int prevFbo);
        bool prevDepthTest = _gl.IsEnabled(EnableCap.DepthTest);

        // One FBO + depth renderbuffer, reused across shapes.
        uint fbo = _gl.GenFramebuffer();
        uint depthRbo = _gl.GenRenderbuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
            InternalFormat.DepthComponent24, Size, Size);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRbo);

        Matrix4x4 view = Matrix4x4.CreateLookAt(
            new Vector3(0.9f, 0.8f, 1.4f), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4f, 1f, 0.1f, 5f);
        Matrix4x4 mvp = view * proj;

        // Light direction supplied in object space — see thumb.vert.
        Vector3 lightObjectSpace = Vector3.Normalize(new Vector3(0.6f, 0.9f, 0.4f));

        _gl.Viewport(0, 0, Size, Size);
        _gl.Enable(EnableCap.DepthTest);

        _shader.Use();
        _shader.SetUniform("uMvp", mvp);
        _shader.SetUniform("uColor", ThumbColor);
        _shader.SetUniform("uLightDir", lightObjectSpace);

        foreach (var kv in meshes)
        {
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
                Size, Size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.ClampToEdge);

            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);

            _gl.ClearColor(0.10f, 0.12f, 0.16f, 1f); // dark slate
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            _gl.BindVertexArray(kv.Value.Vao);
            _gl.DrawElements(PrimitiveType.Triangles, kv.Value.IndexCount,
                DrawElementsType.UnsignedInt, null);

            _textures[kv.Key] = tex;
        }

        _gl.BindVertexArray(0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)prevFbo);
        _gl.Viewport(prevViewport[0], prevViewport[1],
            (uint)prevViewport[2], (uint)prevViewport[3]);
        if (!prevDepthTest) _gl.Disable(EnableCap.DepthTest);

        _gl.DeleteFramebuffer(fbo);
        _gl.DeleteRenderbuffer(depthRbo);
    }

    public void Dispose()
    {
        foreach (var tex in _textures.Values)
            _gl.DeleteTexture(tex);
        _textures.Clear();
        _shader?.Dispose();
    }
}
