// Triplanar texture projection helpers shared by pbr_cell.frag.
// Included via the existing #include "triplanar.glsl" mechanism in ShaderProgram.
//
// Instanced cell meshes carry position+normal only (no UVs/tangents), so
// material textures are projected from the object-space position (aPosition,
// pre-scale) along the three cardinal axes and blended by the surface
// normal's axis weights. This works identically on every cell shape and in
// the reflection pass.

/// Axis blend weights for triplanar mapping: pow(|N|, 4), normalized to sum 1.
vec3 triplanarWeights(vec3 N)
{
    vec3 w = pow(abs(N), vec3(4.0));
    return w / (w.x + w.y + w.z);
}

/// Blended triplanar sample of tex at the object-space position localPos.
/// scale is the per-material tiling factor (1 = one repeat per cell);
/// weights come from triplanarWeights(N).
vec4 triplanarSample(sampler2D tex, vec3 localPos, float scale, vec3 weights)
{
    vec3 p = localPos * scale;
    vec4 x = texture(tex, p.zy);
    vec4 y = texture(tex, p.xz);
    vec4 z = texture(tex, p.xy);
    return x * weights.x + y * weights.y + z * weights.z;
}

/// Tangent-less triplanar normal mapping. The tangent-space map is sampled on
/// each projection plane, each sample is transformed into object space with
/// an analytic per-plane tangent frame (the projection axis, oriented by the
/// sign of N on that axis), and the three results are blended by the
/// triplanar weights. A flat (0.5, 0.5, 1) map reproduces N exactly.
/// No mesh tangents or screen-space derivatives are required.
/// Returns the perturbed object-space normal; N need not be normalized but
/// the result is normalized.
vec3 triplanarNormal(sampler2D tex, vec3 localPos, float scale, vec3 N)
{
    vec3 weights = triplanarWeights(N);
    vec3 axisSign = sign(N);
    vec3 p = localPos * scale;

    // Tangent-space normals per plane, remapped to [-1, 1].
    vec3 tnX = texture(tex, p.zy).xyz * 2.0 - 1.0;
    vec3 tnY = texture(tex, p.xz).xyz * 2.0 - 1.0;
    vec3 tnZ = texture(tex, p.xy).xyz * 2.0 - 1.0;

    // Object-space frames: U/V span the projection plane, W is the projection
    // axis oriented by sign(N) so the normal keeps pointing outward on
    // negative-facing planes.
    //   X-projection (zy plane): U→Z, V→Y, W→X·sign(N.x)
    //   Y-projection (xz plane): U→X, V→Z, W→Y·sign(N.y)
    //   Z-projection (xy plane): U→X, V→Y, W→Z·sign(N.z)
    vec3 nX = vec3(tnX.z * axisSign.x, tnX.y, tnX.x);
    vec3 nY = vec3(tnY.x, tnY.z * axisSign.y, tnY.y);
    vec3 nZ = vec3(tnZ.x, tnZ.y, tnZ.z * axisSign.z);

    return normalize(nX * weights.x + nY * weights.y + nZ * weights.z);
}
