shader_type canvas_item;

// Sway for hanging plants growing on a thick branch that must stay still, like decoCueva6. Height
// can't tell them apart because the branch crosses the whole image, so colour does: green pixels
// bend and everything else stays put. Amounts are in texture pixels.
uniform float amplitude : hint_range(0.0, 30.0) = 5.0;
uniform float speed : hint_range(0.0, 5.0) = 0.7;
uniform float wavelength : hint_range(20.0, 800.0) = 140.0;
// How much greener than red a pixel has to be to count as plant.
uniform float green_threshold : hint_range(0.0, 0.3) = 0.04;

varying vec4 tint;
varying float instance_phase;

void vertex() {
	tint = COLOR;
	// Copies of the same branch placed apart don't move in lockstep.
	instance_phase = MODEL_MATRIX[3].x * 0.013 + MODEL_MATRIX[3].y * 0.007;
}

float plant_mask(vec4 c) {
	return step(0.5, c.a) * step(green_threshold, c.g - c.r);
}

void fragment() {
	float x = UV.x / TEXTURE_PIXEL_SIZE.x;
	float phase = x * TAU / wavelength + instance_phase;
	float t = TIME * speed;
	float sway = sin(t + phase) + 0.45 * sin(t * 1.73 + phase * 2.3 - UV.y * 5.0);
	vec4 still = texture(TEXTURE, UV);
	vec4 moved = texture(TEXTURE, UV + vec2(sway * amplitude * TEXTURE_PIXEL_SIZE.x, 0.0));
	// Plant moved over this pixel: show it. Otherwise keep the branch, or empty where plant left.
	vec4 result = plant_mask(moved) > 0.5 ? moved : (plant_mask(still) > 0.5 ? moved : still);
	COLOR = result * tint;
}
