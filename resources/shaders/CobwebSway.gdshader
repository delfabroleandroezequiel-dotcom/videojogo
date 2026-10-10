shader_type canvas_item;

// Slow air-current sway for cobwebs. Moves vertices (not UVs), so it also works on a TileMapLayer
// whose tiles come from a shared atlas. The phase comes from the WORLD position, so neighbouring
// tiles share their edge vertices' offset (no seams opening between them) while different spots
// of a web drift out of sync, which reads less mechanical than one uniform swing.
uniform float amplitude : hint_range(0.0, 8.0) = 2.0;
uniform float speed : hint_range(0.0, 5.0) = 1.2;
uniform float wave_scale : hint_range(0.0, 0.1) = 0.015;
// 1 = pinned along the texture's top edge and swinging below it (a single hanging Sprite2D).
// Leave at 0 on a TileMapLayer: its UVs are atlas coordinates, not per-tile ones.
uniform float anchor_top : hint_range(0.0, 1.0) = 0.0;

void vertex() {
	vec2 world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	float phase = world.x * wave_scale + world.y * wave_scale * 1.7;
	float t = TIME * speed;
	vec2 offset = vec2(sin(t + phase), 0.5 * cos(t * 0.8 + phase * 1.3)) * amplitude;
	VERTEX += offset * mix(1.0, UV.y, anchor_top);
}
