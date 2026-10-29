shader_type canvas_item;

// Out-of-focus title card: a golden-angle disc blur of the texture itself, `radius` in texels.
// 0 = sharp. Works on any texture without mipmaps.
uniform float radius : hint_range(0.0, 64.0) = 0.0;

const int TAPS = 32;

// fragment()'s COLOR already includes the texture, so keep the bare modulate from vertex().
varying vec4 modulate;

void vertex() {
	modulate = COLOR;
}

void fragment() {
	if (radius >= 0.05) {
		vec4 sum = vec4(0.0);
		for (int i = 0; i < TAPS; i++) {
			float r = sqrt((float(i) + 0.5) / float(TAPS)) * radius;
			float a = float(i) * 2.39996;
			sum += texture(TEXTURE, UV + vec2(cos(a), sin(a)) * r * TEXTURE_PIXEL_SIZE);
		}
		COLOR = sum / float(TAPS) * modulate;
	}
}
