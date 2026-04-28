using System.Numerics;
using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.IO;
using GameOfLife3D.NET.Recording;
using ImGuiNET;

namespace GameOfLife3D.NET.UI;

// Editor window for video recording: keyframe list, codec/fps/output path, Start/Cancel.
// Also draws a small floating progress overlay while a recording is active.
// Requires ffmpeg on PATH; if missing, the panel shows install instructions and disables Start.
public sealed class RecordingPanel
{
    public bool Visible { get; set; }
    public Func<CameraState>? CurrentCameraStateProvider { get; set; }
    public Func<(int Width, int Height)>? FramebufferSizeProvider { get; set; }
    public RecordingController? Controller { get; set; }
    public Action<RecordingSettings, IReadOnlyList<CameraKeyframe>>? OnStartRecording { get; set; }
    public Action? OnCancelRecording { get; set; }

    public string? LastErrorMessage { get; set; }

    private readonly List<CameraKeyframe> _keyframes = new();

    private static readonly int[] FpsOptions = [24, 30, 60];

    private int _fpsIdx = 1;            // 30 default
    private int _codecIdx;              // 0 = WebM, 1 = MP4
    private bool? _h264Available;
    private bool? _ffmpegAvailable;
    private float _durationSeconds = 10f;
    private float _generationsPerSecond = 5f;
    private int _startGen = 0;
    private int _endGen = 50;
    private string _outputPath = "";
    private float _newKeyframeTime;

    public void Render(int windowWidth, int windowHeight)
    {
        // Always-on overlay while recording.
        if (Controller?.IsActive == true)
        {
            RenderProgressOverlay(windowWidth);
        }

        if (!Visible) return;

        ImGui.SetNextWindowSize(new Vector2(420, 560), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(windowWidth - 440, 60), ImGuiCond.FirstUseEver);
        bool open = true;
        if (!ImGui.Begin("Video Recording", ref open, ImGuiWindowFlags.None))
        {
            ImGui.End();
            Visible = open;
            return;
        }

        bool recording = Controller?.IsActive == true;
        if (recording)
        {
            ImGui.TextDisabled("Recording in progress…");
            ImGui.End();
            return;
        }

        ProbeFfmpeg();

        if (_ffmpegAvailable != true)
        {
            RenderFfmpegMissingBanner();
            ImGui.End();
            Visible = open;
            return;
        }

        if (!string.IsNullOrEmpty(LastErrorMessage))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
            ImGui.TextWrapped(LastErrorMessage);
            ImGui.PopStyleColor();
            if (ImGui.SmallButton("Clear")) LastErrorMessage = null;
            ImGui.Separator();
        }

        RenderOutputSection();
        ImGui.Separator();
        RenderTimingSection();
        ImGui.Separator();
        RenderKeyframesSection();
        ImGui.Separator();
        RenderActionsSection();

        ImGui.End();
        Visible = open;
    }

    private void ProbeFfmpeg()
    {
        if (_ffmpegAvailable != null) return;
        string? path = FfmpegEncoder.LocateBinary();
        _ffmpegAvailable = path != null;
        _h264Available = path != null && FfmpegEncoder.SupportsLibx264(path);
    }

    private void RenderFfmpegMissingBanner()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.3f, 1f));
        ImGui.TextWrapped("Video recording requires ffmpeg.");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.TextWrapped(FfmpegEncoder.InstallInstructions());
    }

    private void RenderOutputSection()
    {
        ImGui.TextUnformatted("Output");

        bool h264Ok = _h264Available == true;

        // Codec dropdown — MP4 only enabled when libx264 is available.
        ImGui.SetNextItemWidth(220);
        string mp4Label = h264Ok ? "MP4 (H.264)" : "MP4 (H.264) — needs libx264";
        string[] codecLabels = ["WebM (VP9)", mp4Label];
        ImGui.Combo("Codec", ref _codecIdx, codecLabels, codecLabels.Length);
        if (_codecIdx == 1 && !h264Ok) _codecIdx = 0;

        if (FramebufferSizeProvider != null)
        {
            var (fbW, fbH) = FramebufferSizeProvider();
            ImGui.TextDisabled($"Capture size: {fbW} × {fbH} (current window framebuffer)");
        }

        ImGui.SetNextItemWidth(80);
        string[] fpsLabels = FpsOptions.Select(f => f.ToString()).ToArray();
        ImGui.Combo("FPS", ref _fpsIdx, fpsLabels, fpsLabels.Length);

        ImGui.InputText("##outpath", ref _outputPath, 1024);
        ImGui.SameLine();
        if (ImGui.Button("Browse..."))
        {
            var codec = SelectedCodec;
            string ext = FfmpegEncoder.CodecExtension(codec).TrimStart('.');
            string? picked = FileDialogHelper.SaveFile(ext, _outputPath);
            if (!string.IsNullOrEmpty(picked))
            {
                string dotExt = FfmpegEncoder.CodecExtension(codec);
                if (!picked.EndsWith(dotExt, StringComparison.OrdinalIgnoreCase)) picked += dotExt;
                _outputPath = picked;
            }
        }
    }

    private void RenderTimingSection()
    {
        ImGui.TextUnformatted("Timing");
        ImGui.SetNextItemWidth(120);
        ImGui.InputFloat("Duration (s)", ref _durationSeconds, 1f, 5f, "%.1f");
        if (_durationSeconds < 1f) _durationSeconds = 1f;

        ImGui.SetNextItemWidth(120);
        ImGui.InputFloat("Gens / sec", ref _generationsPerSecond, 0.5f, 1f, "%.1f");
        if (_generationsPerSecond < 0.1f) _generationsPerSecond = 0.1f;

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Start gen", ref _startGen);
        if (_startGen < 0) _startGen = 0;
        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("End gen", ref _endGen);
        if (_endGen < _startGen) _endGen = _startGen;
    }

    private void RenderKeyframesSection()
    {
        ImGui.TextUnformatted($"Camera Keyframes ({_keyframes.Count})");

        ImGui.SetNextItemWidth(80);
        ImGui.InputFloat("Time##kf", ref _newKeyframeTime, 0.5f, 1f, "%.2f");
        ImGui.SameLine();
        if (ImGui.Button("Add at Current Camera"))
        {
            if (CurrentCameraStateProvider != null)
            {
                _keyframes.Add(new CameraKeyframe(_newKeyframeTime, CurrentCameraStateProvider()));
                _keyframes.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
                _newKeyframeTime += 1f;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            _keyframes.Clear();
            _newKeyframeTime = 0;
        }

        if (ImGui.BeginTable("kf_table", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 24);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 64);
            ImGui.TableSetupColumn("Target");
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 32);
            ImGui.TableHeadersRow();

            int? toRemove = null;
            for (int i = 0; i < _keyframes.Count; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.Text(i.ToString());
                ImGui.TableSetColumnIndex(1); ImGui.Text($"{_keyframes[i].TimeSeconds:F2}s");
                var t = _keyframes[i].State.Target;
                ImGui.TableSetColumnIndex(2); ImGui.Text($"({t.X:F1}, {t.Y:F1}, {t.Z:F1})");
                ImGui.TableSetColumnIndex(3);
                ImGui.PushID(i);
                if (ImGui.SmallButton("X")) toRemove = i;
                ImGui.PopID();
            }
            if (toRemove.HasValue) _keyframes.RemoveAt(toRemove.Value);
            ImGui.EndTable();
        }
    }

    private void RenderActionsSection()
    {
        bool canRecord = _keyframes.Count >= 2 && !string.IsNullOrEmpty(_outputPath);
        if (!canRecord) ImGui.BeginDisabled();
        if (ImGui.Button("Start Recording", new Vector2(160, 0)))
        {
            try
            {
                LastErrorMessage = null;
                // Width/Height are placeholders here — App overrides them with the live FBO size.
                var settings = new RecordingSettings
                {
                    Codec = SelectedCodec,
                    Fps = FpsOptions[_fpsIdx],
                    Width = 0,
                    Height = 0,
                    OutputPath = _outputPath,
                    DurationSeconds = _durationSeconds,
                    StartGeneration = _startGen,
                    EndGeneration = _endGen,
                    GenerationsPerSecond = _generationsPerSecond,
                    HideHud = true,
                };
                OnStartRecording?.Invoke(settings, _keyframes.ToList());
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
            }
        }
        if (!canRecord) ImGui.EndDisabled();

        if (_keyframes.Count < 2)
            ImGui.TextDisabled("Add at least 2 keyframes.");
        if (string.IsNullOrEmpty(_outputPath))
            ImGui.TextDisabled("Pick an output path.");
    }

    private void RenderProgressOverlay(int windowWidth)
    {
        if (Controller == null) return;

        ImGui.SetNextWindowPos(new Vector2(windowWidth - 270, 10), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.85f);
        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse
                    | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
        if (ImGui.Begin("##recording_overlay", flags))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.3f, 0.3f, 1f));
            ImGui.TextUnformatted("● REC");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.Text($"{Controller.CurrentFrame}/{Controller.TotalFrames}");

            float progress = Controller.TotalFrames > 0
                ? (float)Controller.CurrentFrame / Controller.TotalFrames
                : 0f;
            ImGui.ProgressBar(progress, new Vector2(240, 0));

            if (ImGui.Button("Cancel", new Vector2(240, 0)))
            {
                OnCancelRecording?.Invoke();
            }
        }
        ImGui.End();
    }

    private VideoCodec SelectedCodec => _codecIdx switch
    {
        0 => VideoCodec.Vp9Webm,
        1 => VideoCodec.H264Mp4,
        _ => VideoCodec.Vp9Webm,
    };
}
