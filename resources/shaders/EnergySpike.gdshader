shader_type canvas_item;
render_mode blend_add;

void fragment() {
	float width = UV.y * 0.5;
	float dist = abs(UV.x - 0.5);
	float shape = smoothstep(width, width - 0.06, dist);
	float tip_fade = smoothstep(0.0, 0.18, UV.y);
	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = vec4(COLOR.rgb * tex_color.rgb, COLOR.a * tex_color.a * shape * tip_fade);
}
