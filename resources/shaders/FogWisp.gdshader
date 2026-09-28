// One patch of drifting fog: two scrolling layers of seamless noise, thresholded into wisps and
// faded out toward the patch's edges by a soft elliptical mask. Used by FogWisps.cs.
shader_type canvas_item;

uniform sampler2D noise_tex : repeat_enable, filter_linear;
uniform vec4 fog_color : source_color = vec4(0.85, 0.9, 1.0, 1.0);
uniform float alpha = 0.25;
uniform vec2 scroll = vec2(0.01, 0.002);
uniform float noise_scale = 1.0;
// Where the noise starts/finishes turning into fog — higher low = thinner, more broken wisps.
uniform float density_low = 0.3;
uniform float density_high = 0.65;
uniform vec2 noise_offset = vec2(0.0);

void fragment() {
	vec2 centered = UV * 2.0 - 1.0;
	float mask = 1.0 - smoothstep(0.35, 1.0, length(centered));

	vec2 uv = UV * noise_scale + noise_offset;
	float a = texture(noise_tex, uv + TIME * scroll).r;
	float b = texture(noise_tex, uv * 1.7 - TIME * scroll * 0.6).r;
	float fog = smoothstep(density_low, density_high, a * 0.6 + b * 0.4);

	COLOR = vec4(fog_color.rgb, fog * mask * alpha);
}
