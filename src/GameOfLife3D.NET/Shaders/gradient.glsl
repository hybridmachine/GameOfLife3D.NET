vec3 computeGradientColor(float worldY, float minY, float maxY, float time)
{
    float range = max(maxY - minY, 1.0);
    float offset = mod(time, range);
    float adjustedY = mod(worldY - minY - offset, range);
    float t = adjustedY / range;

    vec3 blue = vec3(0.0, 0.0, 1.0);
    vec3 green = vec3(0.0, 1.0, 0.0);
    vec3 yellow = vec3(1.0, 1.0, 0.0);
    vec3 black = vec3(0.0, 0.0, 0.0);
    vec3 purple = vec3(0.5, 0.0, 0.5);

    float segment = t * 5.0;
    if (segment < 1.0) return mix(blue, green, segment);
    if (segment < 2.0) return mix(green, yellow, segment - 1.0);
    if (segment < 3.0) return mix(yellow, black, segment - 2.0);
    if (segment < 4.0) return mix(black, purple, segment - 3.0);
    return mix(purple, blue, segment - 4.0);
}
