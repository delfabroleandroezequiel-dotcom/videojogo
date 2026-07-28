shader_type canvas_item;

// Waves a single static banner/flag texture instead of swapping between drawn frames.
// Displacement grows from top (anchored to the pole) to bottom (flapping freely).
uniform float wave_speed : hint_range(0.0, 10.0) = 2.5;
uniform float wave_amplitude : hint_range(0.0, 0.2) = 0.035;
uniform float wave_frequency : hint_range(0.0, 20.0) = 5.0;

void fragment() {
	float displacement = sin(UV.y * wave_frequency + TIME * wave_speed) * wave_amplitude * UV.y;
	vec2 uv = vec2(UV.x + displacement, UV.y);
	COLOR = texture(TEXTURE, uv);
}
