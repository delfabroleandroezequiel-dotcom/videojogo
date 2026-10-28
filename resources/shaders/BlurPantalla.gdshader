shader_type canvas_item;

// Full-screen blur for cinematics (a dazed/fading look): samples the screen's mipmaps at a level
// set by `strength`, with a slight darkening toward the edges. 0 = untouched.
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform float max_lod = 4.5;

void fragment() {
	vec3 c = textureLod(screen_tex, SCREEN_UV, strength * max_lod).rgb;
	float edge = smoothstep(0.3, 0.9, distance(SCREEN_UV, vec2(0.5)));
	c *= 1.0 - strength * 0.5 * edge;
	COLOR = vec4(c, 1.0);
}
