shader_type canvas_item;
render_mode blend_add;

// Applied to the Line2D bolt geometry. UV.x runs along the bolt's length. Line2D doesn't reliably
// map UV.y across the line's width the way a plain quad would, so the "bright core vs soft glow"
// look comes from layering two separate Line2D nodes (thin bright one + wide dim one) instead of
// a per-pixel width falloff here — this shader only adds a flickering brightness crackle along
// the length, so a flat-colored Line2D reads as a living electric arc instead of a static line.
uniform float crackle_frequency : hint_range(1.0, 60.0) = 18.0;
uniform float crackle_speed : hint_range(0.0, 40.0) = 22.0;
uniform float crackle_strength : hint_range(0.0, 1.0) = 0.55;

float hash(float n)
{
	return fract(sin(n) * 43758.5453123);
}

void fragment()
{
	float phase = UV.x * crackle_frequency + TIME * crackle_speed;
	float crackle = mix(hash(floor(phase)), hash(floor(phase) + 1.0), fract(phase));
	float brightness = mix(1.0, crackle, crackle_strength);

	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = vec4(COLOR.rgb * tex_color.rgb, COLOR.a * tex_color.a * brightness);
}
