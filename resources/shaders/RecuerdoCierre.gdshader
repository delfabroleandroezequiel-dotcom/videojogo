shader_type canvas_item;

// "The memory closes": the screen drains to a warm sepia and goes soft (a plain full-screen blur
// from the screen's mipmaps), dimming a little toward the end. amount 0 = untouched, 1 = sepia,
// blurred and dim.
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float amount : hint_range(0.0, 1.0) = 0.0;
uniform float max_lod = 4.0;
uniform vec3 sepia = vec3(1.0, 0.82, 0.6);

void fragment() {
	vec3 c = textureLod(screen_tex, SCREEN_UV, smoothstep(0.15, 1.0, amount) * max_lod).rgb;
	float luma = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 warm = mix(c, luma * sepia * 1.15, smoothstep(0.0, 0.6, amount));
	COLOR = vec4(warm * (1.0 - 0.3 * smoothstep(0.5, 1.0, amount)), 1.0);
}
