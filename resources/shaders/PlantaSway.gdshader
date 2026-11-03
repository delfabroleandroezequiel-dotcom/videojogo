shader_type canvas_item;

// Sway for big hanging-plant images, where many separate vines share one texture. Bends the image
// in the fragment (a vertex shader could only move the quad's four corners, so everything would
// swing as one rigid sheet): the top stays pinned, the hanging ends move the most, and the phase
// changes across the width so neighbouring vines drift out of step. Amounts are in texture pixels.
// Leave a transparent margin around the art: samples past the texture's edge read as empty.
uniform float amplitude : hint_range(0.0, 30.0) = 7.0;
uniform float speed : hint_range(0.0, 5.0) = 0.9;
uniform float wavelength : hint_range(20.0, 800.0) = 180.0;
// How far down the image (0 top, 1 bottom) the plant is still fixed to the ceiling.
uniform float pin_height : hint_range(0.0, 0.9) = 0.3;

varying vec4 tint;
varying float instance_phase;

void vertex() {
	tint = COLOR;
	// Copies of the same plant placed apart don't move in lockstep.
	instance_phase = MODEL_MATRIX[3].x * 0.013 + MODEL_MATRIX[3].y * 0.007;
}

void fragment() {
	float x = UV.x / TEXTURE_PIXEL_SIZE.x;
	float bend = smoothstep(pin_height, 1.0, UV.y);
	float phase = x * TAU / wavelength + instance_phase;
	float t = TIME * speed;
	// Two out-of-tune waves so the motion never visibly repeats; the second also rolls downward.
	float sway = sin(t + phase) + 0.45 * sin(t * 1.73 + phase * 2.3 - UV.y * 5.0);
	vec2 uv = UV;
	uv.x += sway * amplitude * bend * TEXTURE_PIXEL_SIZE.x;
	COLOR = texture(TEXTURE, uv) * tint;
}
