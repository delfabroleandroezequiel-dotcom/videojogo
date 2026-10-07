shader_type canvas_item;

// Exact-colour palette swap for pixel-art sheets: any texel matching source_colors[i] (within
// tolerance) is replaced by target_colors[i], alpha kept. Up to 24 pairs; unused slots ignored
// via color_count. Non-destructive — the original PNGs stay untouched.
// Colours are matched in sRGB, the space they're authored in: with HDR 2D on (this project), the
// sampled texture arrives linear, where the dark tones crowd together and a fixed tolerance mixes
// them up — so the texel AND the palette colours (source_color arrays are uploaded linearized)
// are converted back to sRGB for matching and gamma, and the result converted to linear for
// output. Keep the source_color hint: it makes Godot store these as colour arrays
// (PackedColorArray) — without it the editor re-types them and can wipe a saved palette to zeros.
uniform int color_count : hint_range(0, 24) = 0;
uniform vec4 source_colors[24] : source_color;
uniform vec4 target_colors[24] : source_color;
uniform float tolerance : hint_range(0.0, 0.1) = 0.01;
// Pairs from flicker_from onward shimmer over time (e.g. a burning blade); 24 = none.
uniform int flicker_from : hint_range(0, 24) = 24;
uniform float flicker_strength : hint_range(0.0, 1.0) = 0.0;
uniform float flicker_speed = 12.0;
// < 1 lifts the shadows more than the highlights (a dark sheet reads better in dim maps
// without washing out its light colours); 1 = untouched.
uniform float gamma : hint_range(0.4, 1.5) = 1.0;
// True when the canvas renders in linear (HDR 2D); false for a plain sRGB canvas.
uniform bool linear_canvas = true;

// The node's modulate/self-modulate (poison tint, fades). Captured in vertex() because in
// fragment() the built-in COLOR already has the texture multiplied in — multiplying it into the
// result again would square every colour (that was making the whole character too dark).
varying vec4 v_modulate;

void vertex() {
	v_modulate = COLOR;
}

vec3 to_srgb(vec3 c) {
	return mix(12.92 * c, 1.055 * pow(max(c, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055, step(vec3(0.0031308), c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow(max((c + 0.055) / 1.055, vec3(0.0)), vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	vec3 texel = linear_canvas ? to_srgb(tex.rgb) : tex.rgb;
	vec3 rgb = texel;
	for (int i = 0; i < color_count; i++) {
		vec3 source = linear_canvas ? to_srgb(source_colors[i].rgb) : source_colors[i].rgb;
		if (distance(texel, source) <= tolerance) {
			rgb = linear_canvas ? to_srgb(target_colors[i].rgb) : target_colors[i].rgb;
			if (i >= flicker_from) {
				float wave = sin(TIME * flicker_speed + UV.y * 90.0 + UV.x * 40.0)
					* sin(TIME * flicker_speed * 0.63 + UV.x * 70.0);
				rgb *= 1.0 + flicker_strength * wave;
			}
			break;
		}
	}
	rgb = pow(max(rgb, vec3(0.0)), vec3(gamma));
	if (linear_canvas) {
		rgb = to_linear(rgb);
	}
	COLOR = vec4(rgb, tex.a) * v_modulate;
}
