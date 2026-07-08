shader_type canvas_item;

uniform float intensity : hint_range(0.0, 2.0) = 0.55;
uniform float radius : hint_range(0.0, 1.5) = 0.75;
uniform float softness : hint_range(0.01, 1.5) = 0.6;

void fragment()
{
	vec2 uv = SCREEN_UV - vec2(0.5);
	float dist = length(uv) * 1.414213562;
	float vignette = smoothstep(radius, radius + softness, dist);
	COLOR = vec4(0.0, 0.0, 0.0, vignette * intensity);
}
