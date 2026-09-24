shader_type canvas_item;

// Death dissolve: the sprite burns away in blocky, pixel-sized chunks with a glowing edge eating
// into it. Noise is computed from the sprite's own local pixel position (not UV), so it works the
// same on AnimatedSprite2D frames cut out of a big atlas, and the chunks line up with the pixel art.
// Drive `progress` 0 → 1 (see Enemy.DissolveAway).
uniform float progress : hint_range(0.0, 1.0) = 0.0;
// Size (in the sprite's own pixels) of each dissolving chunk.
uniform float chunk_size = 1.0;
// Width of the glowing band, in noise units (0-1).
uniform float edge_width = 0.12;
uniform vec4 edge_color : source_color = vec4(0.35, 1.0, 0.5, 1.0);
uniform float edge_intensity = 2.2;
// > 0 makes it burn from the top down (in local pixels, over roughly this height).
uniform float top_down_bias = 0.35;
uniform float sprite_height = 48.0;

varying vec2 local_pos;

void vertex()
{
	local_pos = VERTEX;
}

float hash(vec2 p)
{
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float value_noise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

void fragment()
{
	vec4 base = texture(TEXTURE, UV) * COLOR;

	// Pixel-snapped coordinates → chunky, pixel-art-friendly dissolve.
	vec2 cell = floor(local_pos / chunk_size);
	float n = value_noise(cell * 0.35) * 0.7 + hash(cell) * 0.3;
	// Top of the sprite (negative local y) goes first when biased.
	float height01 = clamp(local_pos.y / sprite_height + 0.5, 0.0, 1.0);
	n = mix(n, n * 0.5 + height01 * 0.5, top_down_bias);

	float threshold = progress * (1.0 + edge_width);
	if (n < threshold - edge_width)
	{
		COLOR = vec4(0.0);
	}
	else if (n < threshold)
	{
		COLOR = vec4(edge_color.rgb * edge_intensity, base.a * edge_color.a);
	}
	else
	{
		COLOR = base;
	}
}
