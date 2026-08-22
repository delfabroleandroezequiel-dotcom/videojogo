shader_type canvas_item;
render_mode blend_add;

// Radial glow: soft translucent fill in the middle, a brighter glowing ring right at the
// boundary — same "additive glow" convention as EnergySpike.gdshader, just a circle instead
// of a spike. Tint comes from the node's modulate/self_modulate, not a baked color, so any
// future user of this bubble can recolor it without touching the shader.
uniform float rim_width : hint_range(0.0, 0.5) = 0.12;
uniform float fill_strength : hint_range(0.0, 1.0) = 0.35;

void fragment() {
	vec2 centered = UV - vec2(0.5);
	float dist = length(centered) * 2.0;
	float rim = smoothstep(1.0 - rim_width, 1.0, dist) - smoothstep(1.0, 1.0 + rim_width * 0.5, dist);
	float fill = (1.0 - smoothstep(0.0, 1.0, dist)) * fill_strength;
	float shape = clamp(rim + fill, 0.0, 1.0);
	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = vec4(COLOR.rgb * tex_color.rgb, COLOR.a * tex_color.a * shape);
}
