shader_type canvas_item;
render_mode blend_add;

// Continuous flamethrower stream, drawn on a Sprite2D quad stretched along local +X (rotate the
// parent to aim). UV.x runs from the nozzle (0) to the stream's far tip (1); UV.y crosses its
// width. Same hash/noise trick as ProceduralWater.gdshader, driving a ragged flickering cone
// silhouette and a hot-core color ramp instead of a water surface.
uniform float cone_spread : hint_range(0.0, 1.0) = 0.55;
uniform float base_width : hint_range(0.0, 1.0) = 0.14;
uniform float turbulence_frequency : hint_range(1.0, 20.0) = 6.0;
uniform float turbulence_speed : hint_range(0.0, 12.0) = 4.5;
uniform float turbulence_strength : hint_range(0.0, 1.0) = 0.4;
uniform float flicker_speed : hint_range(0.0, 20.0) = 9.0;
uniform float flicker_strength : hint_range(0.0, 1.0) = 0.18;
uniform vec4 core_color : source_color = vec4(1.0, 0.98, 0.85, 1.0);
uniform vec4 mid_color : source_color = vec4(1.0, 0.55, 0.1, 1.0);
uniform vec4 tip_color : source_color = vec4(0.6, 0.1, 0.04, 0.85);

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
	vec2 flow = vec2(-TIME * turbulence_speed, 0.0);
	float n1 = noise(UV * vec2(turbulence_frequency, turbulence_frequency * 1.5) + flow);
	float n2 = noise(UV * vec2(turbulence_frequency * 2.2, turbulence_frequency * 2.0) + flow * 1.6 + 50.0);
	float turbulence = n1 * 0.6 + n2 * 0.4;

	// Cone widens away from the nozzle, its edge perturbed by turbulence so it licks/frays
	// instead of a clean wedge.
	float width = base_width + cone_spread * UV.x + (turbulence - 0.5) * turbulence_strength * (0.3 + UV.x);
	float dist = abs(UV.y - 0.5) * 2.0;
	float shape = smoothstep(width, width - 0.06, dist);

	// The tail dissolves into flickering wisps instead of cutting off with a hard edge, and the
	// nozzle ramps in over a few pixels so it doesn't pop into existence.
	float tip_fray = smoothstep(0.7, 1.0, UV.x) * step(turbulence, 0.5 + UV.x * 0.35);
	shape *= (1.0 - tip_fray) * smoothstep(0.0, 0.04, UV.x);

	float flicker = 1.0 - flicker_strength
		+ flicker_strength * noise(vec2(TIME * flicker_speed, UV.x * 4.0));

	// Red should actually be reached and stay opaque for a stretch of the tail instead of fading
	// out together with the color shift — otherwise the reddest pixels are also the most
	// transparent ones and the stream reads as white/orange only. The tip_fray/shape falloff
	// above already handles the "dissolve at the very end" look, so this ramp doesn't need to.
	vec3 color = mix(core_color.rgb, mid_color.rgb, smoothstep(0.0, 0.22, UV.x));
	color = mix(color, tip_color.rgb, smoothstep(0.22, 0.8, UV.x));
	color += turbulence * 0.06;

	float alpha_ramp = mix(core_color.a, mix(mid_color.a, tip_color.a, smoothstep(0.22, 0.8, UV.x)), smoothstep(0.0, 0.22, UV.x));

	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = vec4(COLOR.rgb * color, COLOR.a * tex_color.a * shape * flicker * alpha_ramp);
}
