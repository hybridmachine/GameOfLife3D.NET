using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Immutable record capturing the supported OpenPBR surface parameters for
/// cell rendering. A <c>null</c> value on <see cref="RenderSettings.ActiveMaterial"/>
/// means "use the legacy Lambertian shader".
///
/// Parameter names and default values follow the OpenPBR spec
/// (Academy Software Foundation). Texture slots hold absolute file paths;
/// <c>null</c> means "use the constant only" (the constant acts as a
/// multiplicative tint/scale on the texture sample). Simplifications:
/// <list type="bullet">
///   <item><c>transmission_weight</c> and subsurface — require order-independent
///   transparency / SSS infrastructure, a poor fit for millions of instanced cells.</item>
///   <item>Anisotropy tangent frame — derived from the object-space X axis
///   instead of per-shape authored tangents.</item>
///   <item>Texture mapping uses shader-side triplanar projection of the
///   object-space position (no per-shape UVs).</item>
/// </list>
/// </summary>
public sealed record CellMaterial
{
    // ── Base layer ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tint applied on top of the gradient/solid face color. Default white = no tint.
    /// </summary>
    public Vector3 BaseColor { get; init; } = Vector3.One;

    /// <summary>0 = dielectric, 1 = metallic.</summary>
    public float BaseMetalness { get; init; } = 0f;

    /// <summary>Oren-Nayar diffuse roughness (0 = Lambertian).</summary>
    public float BaseDiffuseRoughness { get; init; } = 0f;

    /// <summary>Base layer blend weight (base_weight, 1 = full strength).</summary>
    public float BaseWeight { get; init; } = 1f;

    // ── Specular layer ────────────────────────────────────────────────────────

    /// <summary>Specular lobe blend weight (specular_weight, 1 = full strength).</summary>
    public float SpecularWeight { get; init; } = 1f;

    /// <summary>Tint applied to dielectric specular reflection (specular_color).</summary>
    public Vector3 SpecularColor { get; init; } = Vector3.One;

    /// <summary>GGX microfacet roughness. α = roughness².</summary>
    public float SpecularRoughness { get; init; } = 0.3f;

    /// <summary>
    /// Specular roughness anisotropy (0 = isotropic, ±1 = fully anisotropic).
    /// The tangent frame is derived from the object-space X axis — see class remarks.
    /// </summary>
    public float SpecularAnisotropy { get; init; } = 0f;

    /// <summary>Index of refraction for Fresnel. F0 = ((ior-1)/(ior+1))².</summary>
    public float SpecularIor { get; init; } = 1.5f;

    // ── Emission ──────────────────────────────────────────────────────────────

    /// <summary>Emission tint color.</summary>
    public Vector3 EmissionColor { get; init; } = Vector3.Zero;

    /// <summary>
    /// Emission luminance scale (0 = no emission). Feeds the bloom bright-pass
    /// when bloom is enabled.
    /// </summary>
    public float EmissionLuminance { get; init; } = 0f;

    // ── Coat (clearcoat) layer ────────────────────────────────────────────────

    /// <summary>Clearcoat blend weight (0 = disabled).</summary>
    public float CoatWeight { get; init; } = 0f;

    /// <summary>Tint applied to coat reflection and absorption (coat_color).</summary>
    public Vector3 CoatColor { get; init; } = Vector3.One;

    /// <summary>Clearcoat GGX roughness.</summary>
    public float CoatRoughness { get; init; } = 0.05f;

    /// <summary>Coat roughness anisotropy (0 = isotropic).</summary>
    public float CoatAnisotropy { get; init; } = 0f;

    /// <summary>Clearcoat IOR.</summary>
    public float CoatIor { get; init; } = 1.5f;

    /// <summary>
    /// Darkening of the base layer under the coat at grazing angles
    /// (coat_darkening, 1 = full artistic darkening, 0 = none).
    /// </summary>
    public float CoatDarkening { get; init; } = 1f;

    // ── Fuzz (sheen) layer ────────────────────────────────────────────────────

    /// <summary>Fuzz (sheen) blend weight (0 = disabled).</summary>
    public float FuzzWeight { get; init; } = 0f;

    /// <summary>Fuzz tint color.</summary>
    public Vector3 FuzzColor { get; init; } = Vector3.One;

    /// <summary>Fuzz roughness for the Charlie sheen lobe.</summary>
    public float FuzzRoughness { get; init; } = 0.5f;

    // ── Thin-film iridescence ────────────────────────────────────────────────

    /// <summary>Thin-film interference weight (0 = disabled).</summary>
    public float ThinFilmWeight { get; init; } = 0f;

    /// <summary>Thin-film thickness in nanometers (0 when the film is absent).</summary>
    public float ThinFilmThickness { get; init; } = 0f;

    /// <summary>Thin-film index of refraction.</summary>
    public float ThinFilmIor { get; init; } = 1.33f;

    // ── Geometry ─────────────────────────────────────────────────────────────

    /// <summary>Surface opacity multiplier (geometry_opacity, 1 = opaque).</summary>
    public float GeometryOpacity { get; init; } = 1f;

    // ── Textures ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Triplanar tiling scale applied to all texture projections
    /// (app-specific; 1 = one texture repeat per cell).
    /// </summary>
    public float TextureScale { get; init; } = 1f;

    /// <summary>Absolute path of the base_color texture, or null.</summary>
    public string? BaseColorTexture { get; init; }

    /// <summary>Absolute path of the base_metalness texture, or null.</summary>
    public string? MetalnessTexture { get; init; }

    /// <summary>Absolute path of the specular_roughness texture, or null.</summary>
    public string? RoughnessTexture { get; init; }

    /// <summary>Absolute path of the geometry_normal (tangent-space) map, or null.</summary>
    public string? NormalTexture { get; init; }

    /// <summary>Absolute path of the emission_color texture, or null.</summary>
    public string? EmissionTexture { get; init; }

    /// <summary>Absolute path of the geometry_opacity texture, or null.</summary>
    public string? OpacityTexture { get; init; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="CellMaterial"/> with physically sensible defaults
    /// (plastic-like dielectric).
    /// </summary>
    public static CellMaterial Default => new();
}
