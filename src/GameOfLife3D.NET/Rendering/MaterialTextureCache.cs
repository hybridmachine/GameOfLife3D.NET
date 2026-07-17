using Silk.NET.OpenGL;
using StbImageSharp;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// File-based texture loader + cache for <see cref="CellMaterial"/> texture
/// slots. Textures are decoded with StbImageSharp (RGBA8), mipmapped, and
/// sampled with repeat wrapping; anisotropic filtering is enabled up to the
/// implementation cap when <c>GL_EXT_texture_filter_anisotropic</c> is present.
///
/// Load results are cached by absolute path (failures included, as id 0) so a
/// missing file is logged to stderr exactly once and otherwise treated as
/// "no texture". Owned by <see cref="Renderer3D"/>; <see cref="Dispose"/>
/// deletes every cached texture.
/// </summary>
public sealed class MaterialTextureCache : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, uint> _textures = new(StringComparer.Ordinal);

    // Lazily probed anisotropy state: _anisoChecked guards the one-time
    // extension query so a GL hiccup can't spam per-texture retries.
    private bool _anisoChecked;
    private float _maxAnisotropy;

    public MaterialTextureCache(GL gl)
    {
        _gl = gl;
    }

    /// <summary>
    /// Returns the GL texture id for <paramref name="absolutePath"/>, loading
    /// and uploading it on first use. Returns 0 when the file cannot be read
    /// or decoded — the material then falls back to its constant value.
    /// </summary>
    public unsafe uint GetOrLoad(string absolutePath)
    {
        if (_textures.TryGetValue(absolutePath, out uint cached))
            return cached;

        uint texture = 0;
        try
        {
            ImageResult image;
            using (var stream = File.OpenRead(absolutePath))
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            texture = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, texture);

            fixed (byte* ptr = image.Data)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba8,
                    (uint)image.Width,
                    (uint)image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr);
            }

            _gl.GenerateMipmap(TextureTarget.Texture2D);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int)TextureWrapMode.Repeat);

            float aniso = GetMaxAnisotropy();
            if (aniso > 0f)
                _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxAnisotropy, aniso);

            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"MaterialTextureCache: failed to load '{absolutePath}': {ex.Message}");
            if (texture != 0)
            {
                _gl.DeleteTexture(texture);
                texture = 0;
            }
        }

        _textures[absolutePath] = texture;
        return texture;
    }

    /// <summary>
    /// One-time probe for anisotropic filtering support. Returns the cap to
    /// use (clamped to a sane 8×), or 0 when the extension is absent or the
    /// query itself fails — anisotropy must never block material loading.
    /// </summary>
    private float GetMaxAnisotropy()
    {
        if (_anisoChecked)
            return _maxAnisotropy;
        _anisoChecked = true;
        _maxAnisotropy = 0f;

        try
        {
            bool supported = false;
            int count = _gl.GetInteger(GetPName.NumExtensions);
            for (uint i = 0; i < count; i++)
            {
                string ext = _gl.GetStringS(StringName.Extensions, i);
                if (ext is "GL_EXT_texture_filter_anisotropic" or "GL_ARB_texture_filter_anisotropic")
                {
                    supported = true;
                    break;
                }
            }

            if (supported)
                _maxAnisotropy = Math.Min(8f, _gl.GetFloat(GLEnum.MaxTextureMaxAnisotropy));
        }
        catch
        {
            // Leave anisotropy disabled; linear-mipmap-linear still applies.
        }

        return _maxAnisotropy;
    }

    public void Dispose()
    {
        foreach (uint texture in _textures.Values)
        {
            if (texture != 0)
                _gl.DeleteTexture(texture);
        }
        _textures.Clear();
    }
}
