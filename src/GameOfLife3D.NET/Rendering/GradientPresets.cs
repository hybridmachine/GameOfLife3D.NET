using System.Collections.Immutable;
using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Built-in face-cycling gradient palettes. Backed by an
/// <see cref="ImmutableArray{T}"/>-of-tuples so the iteration order shown in
/// the UI combo is contractual (not "happens to work" via Dictionary insertion
/// order) and the per-preset color arrays cannot be mutated through the
/// exposed handle. "Custom" is a sentinel rendered by the UI itself when a
/// user-edited palette no longer matches any preset, and is therefore not
/// stored in this table.
/// </summary>
public static class GradientPresets
{
    public const string CustomLabel = "Custom";

    public static readonly ImmutableArray<(string Name, ImmutableArray<Vector3> Stops)> Presets =
        ImmutableArray.Create(
            ("Classic",   RenderSettings.DefaultGradientStops),
            ("Sunset",    ImmutableArray.Create(
                new Vector3(0.15f, 0.00f, 0.30f),
                new Vector3(0.60f, 0.05f, 0.40f),
                new Vector3(1.00f, 0.35f, 0.10f),
                new Vector3(1.00f, 0.80f, 0.20f),
                new Vector3(0.30f, 0.00f, 0.05f))),
            ("Ocean",     ImmutableArray.Create(
                new Vector3(0.02f, 0.05f, 0.20f),
                new Vector3(0.00f, 0.30f, 0.55f),
                new Vector3(0.00f, 0.70f, 0.85f),
                new Vector3(0.60f, 0.95f, 1.00f),
                new Vector3(0.95f, 0.98f, 1.00f))),
            ("Aurora",    ImmutableArray.Create(
                new Vector3(0.02f, 0.05f, 0.25f),
                new Vector3(0.00f, 0.50f, 0.55f),
                new Vector3(0.10f, 0.95f, 0.50f),
                new Vector3(0.85f, 0.20f, 0.85f),
                new Vector3(0.35f, 0.05f, 0.55f))),
            ("Mono-glow", ImmutableArray.Create(
                new Vector3(0.00f, 0.10f, 0.15f),
                new Vector3(0.00f, 0.85f, 1.00f),
                new Vector3(0.95f, 1.00f, 1.00f))),
            ("Heat",      ImmutableArray.Create(
                new Vector3(0.00f, 0.00f, 0.00f),
                new Vector3(0.40f, 0.00f, 0.00f),
                new Vector3(1.00f, 0.15f, 0.00f),
                new Vector3(1.00f, 0.55f, 0.00f),
                new Vector3(1.00f, 0.95f, 0.40f),
                new Vector3(1.00f, 1.00f, 1.00f))));

    /// <summary>Returns the preset name whose stops match <paramref name="stops"/> exactly, or null.</summary>
    public static string? Match(IReadOnlyList<Vector3> stops)
    {
        foreach (var (name, preset) in Presets)
        {
            if (preset.Length != stops.Count) continue;
            bool same = true;
            for (int i = 0; i < preset.Length; i++)
            {
                if (preset[i] != stops[i]) { same = false; break; }
            }
            if (same) return name;
        }
        return null;
    }
}
