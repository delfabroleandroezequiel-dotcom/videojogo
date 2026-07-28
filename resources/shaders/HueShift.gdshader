shader_type canvas_item;

// Rotates the hue of the texture so one piece of art can be recolored
// (e.g. reuse the red rose art as blue/yellow/purple) without redrawing it.
uniform float hue_shift : hint_range(0.0, 1.0) = 0.0;
uniform float saturation : hint_range(0.0, 1.0) = 1.0;

vec3 rgb2hsv(vec3 c) {
	vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = mix(vec4(c.bg, k.wz), vec4(c.gb, k.xy), step(c.b, c.g));
	vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c) {
	vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), c.y);
}

void fragment() {
	vec4 tex_color = texture(TEXTURE, UV);
	vec3 hsv = rgb2hsv(tex_color.rgb);
	// Only recolor pixels close to the source art's red hue (the petals/buds) so
	// green foliage and grey rocks keep their natural color regardless of hue_shift.
	float hue_dist = min(abs(hsv.x - 0.0), 1.0 - abs(hsv.x - 0.0));
	float mask = 1.0 - smoothstep(0.12, 0.3, hue_dist);
	hsv.x = fract(hsv.x + hue_shift * mask);
	hsv.y = mix(hsv.y, hsv.y * saturation, mask);
	COLOR = vec4(hsv2rgb(hsv), tex_color.a);
}
