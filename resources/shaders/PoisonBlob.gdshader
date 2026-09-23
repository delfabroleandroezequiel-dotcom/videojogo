shader_type canvas_item;

// Toxic poison material shared by the flying glob and the ground puddle it leaves behind (like
// ProceduralWater's boolean feature flags). is_puddle=false is a roughly circular blob that
// wobbles as it flies; is_puddle=true squashes it into a flattened spill with the same noisy,
// bubbling surface.
uniform bool is_puddle = false;
uniform vec4 base_color : source_color = vec4(0.14, 0.32, 0.08, 0.85);
uniform vec4 highlight_color : source_color = vec4(0.6, 0.95, 0.2, 0.9);
uniform float wobble_speed : hint_range(0.0, 5.0) = 1.4;
uniform float wobble_strength : hint_range(0.0, 0.5) = 0.18;
uniform float edge_softness : hint_range(0.0, 0.3) = 0.08;
uniform float bubble_frequency : hint_range(1.0, 20.0) = 7.0;
uniform float bubble_speed : hint_range(0.0, 5.0) = 1.0;
uniform float bubble_threshold : hint_range(0.0, 1.0) = 0.72;
uniform float squash : hint_range(0.3, 1.0) = 0.6;

float hash(vec2 p)
{
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void fragment()
{
	vec2 centered = (UV - vec2(0.5)) * 2.0;
	if (is_puddle)
		centered.y /= squash;

	float angle = atan(centered.y, centered.x);
	float radius = length(centered);

	// Two noise octaves keyed by angle so the silhouette bulges/pinches unevenly around its rim
	// instead of pulsing as a uniform circle.
	float edgeNoise = noise(vec2(angle * 2.5, TIME * wobble_speed)) * 0.6
		+ noise(vec2(angle * 5.0 + 30.0, TIME * wobble_speed * 1.7 + 30.0)) * 0.4;
	float edge = 1.0 + (edgeNoise - 0.5) * wobble_strength;

	float shape = 1.0 - smoothstep(edge - edge_softness, edge, radius);
	if (shape <= 0.0)
		discard;

	// Bubbling highlights: a scrolling noise field thresholded over TIME so bright blotches
	// appear to surface and pop rather than just drifting.
	float bubbleNoise = noise(UV * bubble_frequency + vec2(TIME * bubble_speed, -TIME * bubble_speed * 0.6));
	float bubbles = smoothstep(bubble_threshold, bubble_threshold + 0.15, bubbleNoise);

	vec3 color = mix(base_color.rgb, highlight_color.rgb, bubbles);
	float alpha = mix(base_color.a, highlight_color.a, bubbles * 0.6) * shape;

	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = vec4(color * COLOR.rgb, alpha * tex_color.a * COLOR.a);
}
