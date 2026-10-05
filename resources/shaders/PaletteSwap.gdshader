shader_type canvas_item;

// Exact-colour palette swap for pixel-art sheets: any texel matching source_colors[i] (within
// tolerance) is replaced by target_colors[i], alpha kept. Up to 16 pairs; unused slots ignored
// via color_count. Non-destructive — the original PNGs stay untouched.
uniform int color_count : hint_range(0, 16) = 0;
uniform vec4 source_colors[16] : source_color;
uniform vec4 target_colors[16] : source_color;
uniform float tolerance : hint_range(0.0, 0.1) = 0.01;
// Pairs from flicker_from onward shimmer over time (e.g. a burning blade); 16 = none.
uniform int flicker_from : hint_range(0, 16) = 16;
uniform float flicker_strength : hint_range(0.0, 1.0) = 0.0;
uniform float flicker_speed = 12.0;
// < 1 lifts the shadows more than the highlights (a dark sheet reads better in dim maps
// without washing out its light colours); 1 = untouched.
uniform float gamma : hint_range(0.4, 1.5) = 1.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	vec3 rgb = tex.rgb;
	for (int i = 0; i < color_count; i++) {
		if (distance(tex.rgb, source_colors[i].rgb) <= tolerance) {
			rgb = target_colors[i].rgb;
			if (i >= flicker_from) {
				float wave = sin(TIME * flicker_speed + UV.y * 90.0 + UV.x * 40.0)
					* sin(TIME * flicker_speed * 0.63 + UV.x * 70.0);
				rgb *= 1.0 + flicker_strength * wave;
			}
			break;
		}
	}
	rgb = pow(max(rgb, vec3(0.0)), vec3(gamma));
	COLOR = vec4(rgb, tex.a) * COLOR;
}
