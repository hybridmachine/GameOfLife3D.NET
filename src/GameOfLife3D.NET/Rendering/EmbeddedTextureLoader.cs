using System.Reflection;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace GameOfLife3D.NET.Rendering;

public static class EmbeddedTextureLoader
{
    public static unsafe uint LoadTexture2D(GL gl, string resourceSuffix, TextureWrapMode wrapS, TextureWrapMode wrapT)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new FileNotFoundException($"Embedded texture not found: {resourceSuffix}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Unable to open embedded texture stream: {resourceName}");
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        uint texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);

        fixed (byte* ptr = image.Data)
        {
            gl.TexImage2D(
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

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapS);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapT);

        gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }
}
