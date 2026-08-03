shader_type canvas_item;

// Fades a TileMapLayer's own pixels toward black as they approach a world-space ground line,
// so a tall foreground prop built from several stacked tile cells (e.g. a big pine tree) blends
// its base into the floor instead of reading as a sprite pasted on top with a visible seam. Uses
// world Y rather than each tile's own UV so the fade is one smooth gradient across the whole
// layer regardless of tile-cell boundaries — a per-tile UV fade would instead darken the bottom
// edge of every individual cell (branches included), banding at each tile seam.
uniform float ground_y = 400.0;
uniform float fade_height : hint_range(1.0, 400.0) = 60.0;
uniform float fade_darkness : hint_range(0.0, 1.0) = 0.85;

varying float world_y;

void vertex()
{
	world_y = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).y;
}

void fragment()
{
	vec4 tex_color = texture(TEXTURE, UV);
	float fade = clamp((world_y - (ground_y - fade_height)) / fade_height, 0.0, 1.0);
	tex_color.rgb *= mix(1.0, 1.0 - fade_darkness, fade);
	COLOR = tex_color;
}
