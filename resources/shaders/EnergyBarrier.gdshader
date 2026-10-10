shader_type canvas_item;
render_mode blend_add;

// EnergyBarrier's field: parallel neon beams running along the barrier's length (UV.y, 0 = top,
// 1 = bottom), each with a white-hot core, a soft glow falloff, a slow wobble, pulses of energy
// travelling up it and the odd flicker, over a faint drifting haze. `dissolve` (0 = solid,
// 1 = gone) eats the beams from the bottom up, each beam slightly staggered, with a burning
// bright edge at the cut. Same additive-glow convention as EnergySpike/ForceFieldBubble; sizes
// come in as pixels so the look stays the same however long or thick the barrier is.
uniform vec4 glow_color : source_color = vec4(0.1, 0.85, 1.0, 1.0);
uniform float intensity = 1.6;
uniform int beam_count = 3;
uniform float length_px = 200.0;
uniform float thickness_px = 36.0;
// Extra transparent margin each side of the field so the beams' soft glow fades out instead of
// being cut at the rect edge. The rect is thickness_px + 2 * pad_px wide.
uniform float pad_px = 14.0;
uniform float dissolve : hint_range(0.0, 1.0) = 0.0;
uniform float surge = 0.0;
uniform float seed = 0.0;

float hash(float n) { return fract(sin(n * 127.1 + seed * 311.7) * 43758.5453); }

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash(i.x + i.y * 57.0);
	float b = hash(i.x + 1.0 + i.y * 57.0);
	float c = hash(i.x + (i.y + 1.0) * 57.0);
	float d = hash(i.x + 1.0 + (i.y + 1.0) * 57.0);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
	float px = UV.x * (thickness_px + 2.0 * pad_px) - pad_px;
	float py = UV.y * length_px;
	float t = TIME + seed * 10.0;

	vec3 col = vec3(0.0);
	float n = float(beam_count);

	for (int i = 0; i < beam_count; i++) {
		float fi = float(i);
		float h = hash(fi + 1.0);

		// Each beam's cut point: the bottom of the visible part, raised as dissolve grows.
		// Staggered per beam and roughened with noise so it burns away rather than wiping.
		float stagger = h * 0.18;
		float cut = 1.06 - dissolve * (1.48 - stagger);
		cut += (noise(vec2(py * 0.08, fi * 7.0 + t * 2.0)) - 0.5) * 0.05 * step(0.001, dissolve);
		float keep = smoothstep(cut + 0.004, cut - 0.004, UV.y);
		float edge = exp(-abs(UV.y - cut) * length_px / 3.0) * step(0.001, dissolve) * step(UV.y, cut + 0.02);

		float wobble = sin(py * 0.045 + t * (2.0 + h * 1.5) + fi * 1.7) * 1.2
			+ sin(py * 0.13 - t * 5.0 + fi * 3.1) * 0.5;
		float center = (fi + 0.5) / n * thickness_px + wobble;
		float d = abs(px - center);

		float core = exp(-d * d / 1.1);
		float glow = exp(-d / 4.5) * 0.7 + exp(-d / 11.0) * 0.2;

		// Energy pulses climbing the beam + a slower brightness ripple.
		float pulse = pow(fract(py / 90.0 + t * (0.55 + h * 0.3) + h), 10.0) * 1.4;
		float ripple = 0.75 + 0.25 * sin(py * 0.07 + t * 4.0 + fi * 2.3);

		// Occasional per-beam flicker (a few frames dimmed), harder while surging.
		float flick = hash(floor(t * 14.0) + fi * 13.0);
		float flicker = flick > (0.94 - surge * 0.4) ? 0.35 : 1.0;

		float energy = (core * (1.0 + pulse) + glow * ripple) * flicker * keep;
		col += mix(glow_color.rgb, vec3(1.0), clamp(core * 0.5, 0.0, 1.0)) * energy;
		col += mix(glow_color.rgb, vec3(1.0), 0.6) * edge * exp(-d / 5.0) * 2.5;
	}

	// Faint haze between the beams, drifting upward, brighter near the emitters at both ends.
	float haze = noise(vec2(px * 0.12, py * 0.04 + t * 0.8)) * noise(vec2(px * 0.3 + 5.0, py * 0.1 + t * 1.6));
	float ends = exp(-min(py, length_px - py) / 14.0);
	float u = px / thickness_px;
	float across = smoothstep(0.0, 0.25, u) * smoothstep(1.0, 0.75, u);
	float field_keep = clamp(1.0 - dissolve * 1.6, 0.0, 1.0);
	col += glow_color.rgb * (haze * 0.32 + ends * 0.35) * across * field_keep;

	float side = min(UV.x, 1.0 - UV.x) * (thickness_px + 2.0 * pad_px);
	col *= smoothstep(0.0, pad_px, side);
	col *= intensity * (1.0 + surge * 0.8);
	COLOR = vec4(col, 1.0);
}
