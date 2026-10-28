// Full-screen finishing pass ("laqueado") drawn by LacquerLayer under the HUD: exposure,
// saturation, contrast, split toning (shadow/highlight tints), vignette, film grain and an
// optional chromatic fringe. HDR 2D makes the screen texture linear, so grading happens in an
// approximate sRGB space and is converted back.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform float exposure = 0.0;
uniform float contrast = 1.0;
uniform float saturation = 1.0;
uniform vec3 shadow_tint = vec3(0.0);
uniform vec3 highlight_tint = vec3(0.0);
uniform float vignette = 0.0;
uniform float grain = 0.0;
// Pixels of red/blue split at the screen edges (0 = off).
uniform float aberration = 0.0;

const vec3 LUMA = vec3(0.2126, 0.7152, 0.0722);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec3 col;
	if (aberration > 0.0) {
		vec2 offset = (uv - 0.5) * 2.0 * aberration * SCREEN_PIXEL_SIZE;
		col = vec3(texture(screen_tex, uv + offset).r, texture(screen_tex, uv).g, texture(screen_tex, uv - offset).b);
	} else {
		col = texture(screen_tex, uv).rgb;
	}

	col *= exp2(exposure);
	vec3 c = pow(max(col, vec3(0.0)), vec3(1.0 / 2.2));
	c = mix(vec3(dot(c, LUMA)), c, saturation);
	c = (c - 0.5) * contrast + 0.5;
	float l = clamp(dot(c, LUMA), 0.0, 1.0);
	c += shadow_tint * (1.0 - l) + highlight_tint * l;

	// Aspect-corrected so the vignette is round, not stretched to the screen.
	vec2 d = uv - 0.5;
	d.x *= SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	c *= 1.0 - vignette * smoothstep(0.3, 0.95, length(d));

	// Re-rolled 24 times a second, like real film, instead of crawling every frame.
	c += (hash(FRAGCOORD.xy + floor(TIME * 24.0) * 17.0) - 0.5) * grain;

	COLOR = vec4(pow(max(c, vec3(0.0)), vec3(2.2)), 1.0);
}
