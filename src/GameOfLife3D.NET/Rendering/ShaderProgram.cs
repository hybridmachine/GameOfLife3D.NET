using System.Numerics;
using System.Reflection;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly Dictionary<string, int> _uniformLocations = new();

    public uint Handle => _handle;

    private ShaderProgram(GL gl, uint handle)
    {
        _gl = gl;
        _handle = handle;
    }

    public static ShaderProgram FromEmbeddedResources(GL gl, string vertResourceName, string fragResourceName)
    {
        string vertSource = LoadEmbeddedResource(vertResourceName);
        string fragSource = LoadEmbeddedResource(fragResourceName);
        return FromSource(gl, vertSource, fragSource);
    }

    public static ShaderProgram FromSource(GL gl, string vertSource, string fragSource)
    {
        uint vert = CompileShader(gl, ShaderType.VertexShader, vertSource);
        uint frag = CompileShader(gl, ShaderType.FragmentShader, fragSource);

        uint program = gl.CreateProgram();
        gl.AttachShader(program, vert);
        gl.AttachShader(program, frag);
        gl.LinkProgram(program);

        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            string log = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            gl.DeleteShader(vert);
            gl.DeleteShader(frag);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        gl.DetachShader(program, vert);
        gl.DetachShader(program, frag);
        gl.DeleteShader(vert);
        gl.DeleteShader(frag);

        return new ShaderProgram(gl, program);
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);

        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Shader compile ({type}) failed: {log}");
        }

        return shader;
    }

    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string fullName = $"GameOfLife3D.NET.Shaders.{resourceName}";

        using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new FileNotFoundException($"Embedded resource not found: {fullName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Use() => _gl.UseProgram(_handle);

    public int GetUniformLocation(string name)
    {
        if (!_uniformLocations.TryGetValue(name, out int loc))
        {
            loc = _gl.GetUniformLocation(_handle, name);
            _uniformLocations[name] = loc;
        }
        return loc;
    }

    public void SetUniform(string name, float value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    public void SetUniform(string name, bool value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform1(loc, value ? 1 : 0);
    }

    public void SetUniform(string name, Vector3 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform3(loc, value.X, value.Y, value.Z);
    }

    public void SetUniform(string name, Vector4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.Uniform4(loc, value.X, value.Y, value.Z, value.W);
    }

    public unsafe void SetUniform(string name, Matrix4x4 value)
    {
        int loc = GetUniformLocation(name);
        if (loc >= 0) _gl.UniformMatrix4(loc, 1, false, (float*)&value);
    }

    public void Dispose()
    {
        _gl.DeleteProgram(_handle);
    }
}
