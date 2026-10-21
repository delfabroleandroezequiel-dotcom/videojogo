// Full-screen veil pulsed when the player calls on the shadow: the world drains of colour and
// darkens for a moment. Driven by Player.PulseShadowVeil through `strength` (0 = off).
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint = vec3(0.82, 0.78, 1.0);

void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;
	float luma = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 drained = mix(c, vec3(luma) * tint, strength);
	// Edges sink darker than the centre, a quick vignette that comes with it.
	float edge = smoothstep(0.35, 0.95, distance(SCREEN_UV, vec2(0.5)));
	drained *= 1.0 - strength * (0.25 + 0.45 * edge);
	COLOR = vec4(drained, 1.0);
}
