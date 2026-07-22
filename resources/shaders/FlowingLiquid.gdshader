shader_type canvas_item;

// Recolors water art into lava by luminance onto a 3-stop shadow/mid/highlight gradient instead
// of a straight channel multiply. A simple multiply can't turn this blue-dominant art red
// (there's barely any red channel to work with) and an earlier 2-stop luminance attempt on
// GandalfHardcore's lava art came out muddy/olive — a wider 3-stop gradient with
// contrast-stretched luminance is what actually reads as molten rock. See WaterFall.cs for how
// the actual flow animation works (discrete hand-painted frame variants, not this shader).
uniform bool is_lava = false;
uniform vec4 lava_shadow : source_color = vec4(0.04, 0.016, 0.016, 1.0);
uniform vec4 lava_mid : source_color = vec4(0.78, 0.16, 0.04, 1.0);
uniform vec4 lava_highlight : source_color = vec4(1.0, 0.86, 0.31, 1.0);

// Explicit instead of relying on the CanvasItem's automatic modulate — a custom fragment shader
// takes over COLOR entirely, so nothing multiplies it by the node's Modulate for free.
uniform float opacity_multiplier : hint_range(0.0, 1.0) = 1.0;

void fragment()
{
	vec4 tex = texture(TEXTURE, UV);

	if (is_lava)
	{
		float luminance = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
		float centered = luminance - 0.5;
		float stretched = sign(centered) * pow(abs(centered) * 2.0, 0.6) * 0.5;
		float adjusted = clamp(0.5 + stretched, 0.0, 1.0);

		vec3 color = adjusted < 0.55
			? mix(lava_shadow.rgb, lava_mid.rgb, adjusted / 0.55)
			: mix(lava_mid.rgb, lava_highlight.rgb, (adjusted - 0.55) / 0.45);

		COLOR = vec4(color, tex.a * opacity_multiplier);
	}
	else
	{
		COLOR = vec4(tex.rgb, tex.a * opacity_multiplier);
	}
}
