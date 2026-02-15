#version 330 core

in vec2 vTexCoord;

uniform vec3 uTopColor;
uniform vec3 uBottomColor;
uniform bool uStarfield;
uniform float uTime;

out vec4 FragColor;

// Pseudo-random hash
float hash(vec2 p)
{
    p = fract(p * vec2(443.8975, 397.2973));
    p += dot(p, p.yx + 19.19);
    return fract(p.x * p.y);
}

void main()
{
    vec3 color = mix(uBottomColor, uTopColor, vTexCoord.y);

    if (uStarfield)
    {
        // Create a starfield using the hash function
        vec2 cell = floor(vTexCoord * 200.0);
        float h = hash(cell);
        if (h > 0.985)
        {
            float brightness = (h - 0.985) / 0.015;
            // Twinkle based on time
            float twinkle = 0.5 + 0.5 * sin(uTime * 2.0 + h * 100.0);
            color += vec3(brightness * twinkle * 0.8);
        }
    }

    FragColor = vec4(color, 1.0);
}
