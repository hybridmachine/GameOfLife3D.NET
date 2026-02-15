using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;

namespace GameOfLife3D.NET.IO;

public sealed class SessionData
{
    public GameState? GameState { get; set; }
    public CameraSessionData? Camera { get; set; }
    public RenderSessionData? RenderSettings { get; set; }
    public int DisplayStart { get; set; }
    public int DisplayEnd { get; set; }
}

public sealed class CameraSessionData
{
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }
    public float Distance { get; set; }
    public float Phi { get; set; }
    public float Theta { get; set; }
}

public sealed class RenderSessionData
{
    public float CellPadding { get; set; }
    public bool FaceColorCycling { get; set; }
    public bool EdgeColorCycling { get; set; }
    public float EdgeColorAngle { get; set; }
    public bool ShowGridLines { get; set; }
    public bool ShowGenerationLabels { get; set; }
    public bool ShowWireframe { get; set; }
    public float CellColorR { get; set; }
    public float CellColorG { get; set; } = 1f;
    public float CellColorB { get; set; } = 0.533f;
    public float EdgeColorR { get; set; } = 1f;
    public float EdgeColorG { get; set; } = 1f;
    public float EdgeColorB { get; set; } = 1f;

    // Fog
    public bool FogEnabled { get; set; }
    public float FogStart { get; set; } = 20f;
    public float FogEnd { get; set; } = 100f;
    public float FogColorR { get; set; } = 0.05f;
    public float FogColorG { get; set; } = 0.05f;
    public float FogColorB { get; set; } = 0.08f;

    // Clip
    public bool ClipEnabled { get; set; }
    public float ClipY { get; set; } = 25f;

    // Background
    public int BackgroundMode { get; set; }
    public float BgTopR { get; set; } = 0.08f;
    public float BgTopG { get; set; } = 0.08f;
    public float BgTopB { get; set; } = 0.15f;
    public float BgBottomR { get; set; } = 0.02f;
    public float BgBottomG { get; set; } = 0.02f;
    public float BgBottomB { get; set; } = 0.04f;

    // Bloom
    public bool BloomEnabled { get; set; }
    public float BloomThreshold { get; set; } = 0.6f;
    public float BloomIntensity { get; set; } = 0.5f;

    // Beveled cubes
    public bool UseBeveledCubes { get; set; }
}

public static class SessionManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Save(string path, GameEngine engine, CameraController camera, RenderSettings settings,
        int displayStart, int displayEnd)
    {
        var session = new SessionData
        {
            GameState = engine.ExportState(),
            Camera = FromCameraState(camera.GetState()),
            RenderSettings = FromRenderSettings(settings),
            DisplayStart = displayStart,
            DisplayEnd = displayEnd,
        };

        string json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static SessionData? Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
    }

    private static CameraSessionData FromCameraState(CameraState state) => new()
    {
        TargetX = state.Target.X,
        TargetY = state.Target.Y,
        TargetZ = state.Target.Z,
        Distance = state.Distance,
        Phi = state.Phi,
        Theta = state.Theta,
    };

    public static CameraState ToCameraState(CameraSessionData data) => new()
    {
        Target = new Vector3(data.TargetX, data.TargetY, data.TargetZ),
        Distance = data.Distance,
        Phi = data.Phi,
        Theta = data.Theta,
    };

    private static RenderSessionData FromRenderSettings(RenderSettings s) => new()
    {
        CellPadding = s.CellPadding,
        FaceColorCycling = s.FaceColorCycling,
        EdgeColorCycling = s.EdgeColorCycling,
        EdgeColorAngle = s.EdgeColorAngle,
        ShowGridLines = s.ShowGridLines,
        ShowGenerationLabels = s.ShowGenerationLabels,
        ShowWireframe = s.ShowWireframe,
        CellColorR = s.CellColor.X,
        CellColorG = s.CellColor.Y,
        CellColorB = s.CellColor.Z,
        EdgeColorR = s.EdgeColor.X,
        EdgeColorG = s.EdgeColor.Y,
        EdgeColorB = s.EdgeColor.Z,
        // Fog
        FogEnabled = s.FogEnabled,
        FogStart = s.FogStart,
        FogEnd = s.FogEnd,
        FogColorR = s.FogColor.X,
        FogColorG = s.FogColor.Y,
        FogColorB = s.FogColor.Z,
        // Clip
        ClipEnabled = s.ClipEnabled,
        ClipY = s.ClipY,
        // Background
        BackgroundMode = (int)s.BackgroundMode,
        BgTopR = s.BackgroundTopColor.X,
        BgTopG = s.BackgroundTopColor.Y,
        BgTopB = s.BackgroundTopColor.Z,
        BgBottomR = s.BackgroundBottomColor.X,
        BgBottomG = s.BackgroundBottomColor.Y,
        BgBottomB = s.BackgroundBottomColor.Z,
        // Bloom
        BloomEnabled = s.BloomEnabled,
        BloomThreshold = s.BloomThreshold,
        BloomIntensity = s.BloomIntensity,
        // Beveled cubes
        UseBeveledCubes = s.UseBeveledCubes,
    };

    public static void ApplyRenderSettings(RenderSessionData data, RenderSettings target)
    {
        target.CellPadding = data.CellPadding;
        target.FaceColorCycling = data.FaceColorCycling;
        target.EdgeColorCycling = data.EdgeColorCycling;
        target.EdgeColorAngle = data.EdgeColorAngle;
        target.ShowGridLines = data.ShowGridLines;
        target.ShowGenerationLabels = data.ShowGenerationLabels;
        target.ShowWireframe = data.ShowWireframe;
        target.CellColor = new Vector3(data.CellColorR, data.CellColorG, data.CellColorB);
        target.EdgeColor = new Vector3(data.EdgeColorR, data.EdgeColorG, data.EdgeColorB);
        // Fog
        target.FogEnabled = data.FogEnabled;
        target.FogStart = data.FogStart;
        target.FogEnd = data.FogEnd;
        target.FogColor = new Vector3(data.FogColorR, data.FogColorG, data.FogColorB);
        // Clip
        target.ClipEnabled = data.ClipEnabled;
        target.ClipY = data.ClipY;
        // Background
        target.BackgroundMode = (BackgroundMode)data.BackgroundMode;
        target.BackgroundTopColor = new Vector3(data.BgTopR, data.BgTopG, data.BgTopB);
        target.BackgroundBottomColor = new Vector3(data.BgBottomR, data.BgBottomG, data.BgBottomB);
        // Bloom
        target.BloomEnabled = data.BloomEnabled;
        target.BloomThreshold = data.BloomThreshold;
        target.BloomIntensity = data.BloomIntensity;
        // Beveled cubes
        target.UseBeveledCubes = data.UseBeveledCubes;
    }
}
