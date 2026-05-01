using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameOfLife3D.NET.IO;

/// <summary>
/// Persists global UI preferences that should apply across sessions.
/// </summary>
public sealed class UiSettingsState
{
    private const string FileName = "ui.state.json";

    public const float BaseFontSize = 14f;
    public const float FontSizeStep = 1f;
    public const float MinFontSize = 12f;
    public const float MaxFontSize = 24f;
    public const float FontAtlasLogicalSize = MaxFontSize;

    public float? FontSize { get; set; }

    [JsonIgnore]
    public bool IsFontSizeAutomatic => FontSize == null;

    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, FileName);

    public static UiSettingsState Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new UiSettingsState();

            string json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<UiSettingsState>(json);
            return loaded?.Validated() ?? new UiSettingsState();
        }
        catch
        {
            return new UiSettingsState();
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UiSettingsState: save failed: {ex.Message}");
        }
    }

    public float GetEffectiveFontSize(int resolutionWidth, int resolutionHeight) =>
        FontSize.HasValue ? ClampFontSize(FontSize.Value) : GetAutomaticFontSize(resolutionWidth, resolutionHeight);

    public static float GetAutomaticFontSize(int resolutionWidth, int resolutionHeight)
    {
        int shortEdge = Math.Min(Math.Max(resolutionWidth, 1), Math.Max(resolutionHeight, 1));

        float size = shortEdge switch
        {
            >= 2000 => 20f,
            >= 1440 => 18f,
            >= 1080 => 16f,
            >= 900 => 15f,
            _ => BaseFontSize,
        };

        return ClampFontSize(size);
    }

    public static float ClampFontSize(float fontSize) =>
        Math.Clamp(fontSize, MinFontSize, MaxFontSize);

    private UiSettingsState Validated()
    {
        if (FontSize.HasValue)
            FontSize = ClampFontSize(FontSize.Value);
        return this;
    }
}
