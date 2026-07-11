using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Immutable record capturing the supported OpenPBR surface parameters for
/// cell rendering. A <c>null</c> value on <see cref="RenderSettings.ActiveMaterial"/>
/// means "use the legacy Lambertian shader".
///
/// Parameter names and default values follow the OpenPBR spec
/// (Academy Software Foundation). The following are explicitly deferred:
/// <list type="bullet">
///   <item><c>specular_anisotropy</c> — requires tangent vectors in the VBO.</item>
///   <item><c>transmission_weight</c> — requires order-independent transparency.</item>
///   <item>Any texture-mapped inputs — requires UV generation for instanced meshes.</item>
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

    // ── Specular layer ────────────────────────────────────────────────────────

    /// <summary>GGX microfacet roughness. α = roughness².</summary>
    public float SpecularRoughness { get; init; } = 0.3f;

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

    /// <summary>Clearcoat GGX roughness.</summary>
    public float CoatRoughness { get; init; } = 0.05f;

    /// <summary>Clearcoat IOR.</summary>
    public float CoatIor { get; init; } = 1.5f;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="CellMaterial"/> with physically sensible defaults
    /// (plastic-like dielectric).
    /// </summary>
    public static CellMaterial Default => new();
}
