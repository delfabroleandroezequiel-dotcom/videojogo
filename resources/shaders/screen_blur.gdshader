shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform float blur_amount : hint_range(0.0, 16.0) = 0.0;

void fragment() {
	vec2 offset = SCREEN_PIXEL_SIZE * blur_amount;

	vec4 sum = texture(screen_texture, SCREEN_UV) * 4.0;
	sum += texture(screen_texture, SCREEN_UV + vec2(offset.x, 0.0));
	sum += texture(screen_texture, SCREEN_UV - vec2(offset.x, 0.0));
	sum += texture(screen_texture, SCREEN_UV + vec2(0.0, offset.y));
	sum += texture(screen_texture, SCREEN_UV - vec2(0.0, offset.y));
	sum += texture(screen_texture, SCREEN_UV + offset);
	sum += texture(screen_texture, SCREEN_UV - offset);
	sum += texture(screen_texture, SCREEN_UV + vec2(offset.x, -offset.y));
	sum += texture(screen_texture, SCREEN_UV + vec2(-offset.x, offset.y));

	COLOR = sum / 12.0;
}
