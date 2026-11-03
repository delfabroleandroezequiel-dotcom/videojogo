shader_type canvas_item;

// Living rock for the decoRoca images: the rock stays put while only the foliage sways, and every
// so often a gust sweeps across the image, pushing the leaves harder and lighting them as it passes.
// Foliage is told apart by colour (these images have no separate layers), and its black outline by
// touching green. Amounts are in texture pixels. Colours are linear here (hdr_2d is on).
uniform float amplitude : hint_range(0.0, 30.0) = 5.0;
uniform float speed : hint_range(0.0, 5.0) = 1.1;
uniform float wavelength : hint_range(20.0, 800.0) = 170.0;
uniform float outline_reach : hint_range(1.0, 20.0) = 6.0;
// Seconds between gusts, width of the gust front (0-1 of the image) and how much it adds.
uniform float gust_period : hint_range(2.0, 30.0) = 8.0;
uniform float gust_width : hint_range(0.05, 1.0) = 0.3;
uniform float gust_strength : hint_range(0.0, 6.0) = 2.2;
uniform float gust_sheen : hint_range(0.0, 2.0) = 0.55;

varying vec4 tint;
varying float instance_phase;

void vertex() {
	tint = COLOR;
	// Copies of the same rock placed apart don't move in lockstep.
	instance_phase = MODEL_MATRIX[3].x * 0.013 + MODEL_MATRIX[3].y * 0.007;
}

bool is_green(vec4 c) {
	return c.a > 0.5 && c.g > c.r * 1.3 && c.g > c.b * 1.6;
}

bool is_foliage(sampler2D tex, vec2 uv, vec2 reach) {
	vec4 c = texture(tex, uv);
	if (is_green(c))
		return true;
	if (c.a <= 0.5 || max(c.r, max(c.g, c.b)) > 0.008)
		return false;
	return is_green(texture(tex, uv + vec2(reach.x, 0.0))) || is_green(texture(tex, uv - vec2(reach.x, 0.0)))
		|| is_green(texture(tex, uv + vec2(0.0, reach.y))) || is_green(texture(tex, uv - vec2(0.0, reach.y)));
}

void fragment() {
	vec2 px = TEXTURE_PIXEL_SIZE;
	float t = TIME * speed;
	float phase = UV.x / px.x * TAU / wavelength + instance_phase;
	// The front crosses the image once per period, starting and leaving past its edges.
	float front = fract(TIME / gust_period + instance_phase * 0.1) * (1.0 + gust_width * 2.0) - gust_width;
	float gust = 1.0 - smoothstep(0.0, gust_width, abs(UV.x - front));
	float sway = sin(t + phase - UV.y * 4.0) + 0.45 * sin(t * 1.73 + phase * 2.3);
	vec2 offset = vec2(sway + gust * 0.8, sway * 0.3) * amplitude * (1.0 + gust * gust_strength) * px;

	vec2 reach = px * outline_reach;
	vec2 moved_uv = UV + offset;
	bool here = is_foliage(TEXTURE, UV, reach);
	bool there = is_foliage(TEXTURE, moved_uv, reach);
	// Rock is never resampled: a pixel only changes if a leaf moves onto it. Where a leaf moves away
	// there's no rock painted behind it, so it only clears against empty air and otherwise stays.
	vec4 c = texture(TEXTURE, UV);
	vec4 moved = texture(TEXTURE, moved_uv);
	if (there || (here && moved.a <= 0.5))
		c = moved;
	if (there)
		c.rgb *= 1.0 + gust * gust_sheen;
	COLOR = c * tint;
}
