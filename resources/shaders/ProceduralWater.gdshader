shader_type canvas_item;

// Alternative to WaterFall's hand-painted sprite frames: everything here is generated from noise
// + TIME, no texture sampled at all — so there's no sheet to crop/seam-match, at the cost of a
// smoother/less "painted" look than the HEROES_FANTASY art. Meant to be compared side by side
// with WaterFall, not a guaranteed replacement.
uniform float wave_speed = 1.0;
uniform float wave_frequency = 8.0;
uniform vec4 shallow_color : source_color = vec4(0.55, 0.88, 0.95, 1.0);
uniform vec4 deep_color : source_color = vec4(0.05, 0.28, 0.48, 1.0);
uniform float foam_threshold : hint_range(0.0, 1.0) = 0.82;
uniform vec4 foam_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float opacity_multiplier : hint_range(0.0, 1.0) = 0.85;

// Pixelation: without these, the noise reads as a smooth photoreal-ish gradient, which clashes
// with the rest of the game's hand-painted pixel art. shape_size (the sprite's actual on-screen
// pixel dimensions — UV alone can't tell block size from shape size) lets pixel_block_size mean
// "this many real pixels per chunk" consistently regardless of how big a given instance is.
// color_steps posterizes the noise into that many discrete bands instead of a continuous mix,
// the other half of reading as pixel art instead of a shader gradient.
uniform vec2 shape_size = vec2(64.0, 200.0);
uniform float pixel_block_size : hint_range(1.0, 16.0) = 3.0;
uniform float color_steps : hint_range(2.0, 16.0) = 5.0;

// Optional wavy top edge (for a sea/pool surface instead of a hard rectangle) — a per-column
// sine offset, quantized to the same pixel-block grid as everything else so the wave steps
// instead of scrolling smoothly (which would look inconsistent against the blocky body), with a
// bright crest band riding right at the surface line.
uniform bool has_wavy_top = false;
uniform float wave_edge_amplitude = 6.0;
uniform float wave_edge_frequency = 2.0;
uniform float wave_edge_speed = 1.0;
uniform float crest_thickness = 3.0;
uniform vec4 crest_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// Splash/foam at the bottom edge — for a vertical fall's tip where it lands on a pool below,
// same idea as has_wavy_top's crest but jagged and jittering instead of a clean sine wave, so it
// reads as agitated impact spray rather than a calm surface line.
uniform bool has_base_foam = false;
uniform float base_foam_thickness = 5.0;
uniform float base_foam_turbulence = 10.0;
uniform vec4 base_foam_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// Same value-noise trick as everywhere else this project uses procedural noise (see
// FlowingLiquid.gdshader's hash) — bilinear-interpolated hash noise, cheap and good enough for a
// stylized flow, not aiming for photoreal water.
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
	// Snap to a coarse block grid (in real pixels, not UV fractions) before sampling noise, so
	// the flow reads as chunky pixel blocks instead of a smooth blur.
	vec2 pixelCoord = UV * shape_size;
	vec2 blockCoord = floor(pixelCoord / pixel_block_size) * pixel_block_size;
	vec2 uv = blockCoord / shape_size;

	float surfaceY = 0.0;
	if (has_wavy_top)
	{
		float phase = blockCoord.x / shape_size.x * wave_edge_frequency * TAU + TIME * wave_edge_speed;
		float offset = floor(sin(phase) * wave_edge_amplitude / pixel_block_size) * pixel_block_size;
		surfaceY = wave_edge_amplitude + offset;

		if (pixelCoord.y < surfaceY)
			discard;
	}

	// Two noise layers scrolling at different speeds/directions/scales so the flow doesn't look
	// like one repeating ripple — same idea as combining scroll+shimmer earlier, just both
	// layers are the same kind of thing here.
	float n1 = noise(uv * vec2(wave_frequency, wave_frequency * 2.0) + vec2(0.0, -TIME * wave_speed));
	float n2 = noise(uv * vec2(wave_frequency * 2.0, wave_frequency) + vec2(TIME * wave_speed * 0.5, -TIME * wave_speed * 1.3));
	float n = n1 * 0.6 + n2 * 0.4;

	// Posterize into discrete bands instead of a continuous mix — the other half of reading as
	// pixel art instead of a shader gradient.
	n = floor(n * color_steps) / color_steps;

	vec3 color = mix(deep_color.rgb, shallow_color.rgb, n);
	float alpha = mix(deep_color.a, shallow_color.a, n);

	if (n > foam_threshold)
	{
		color = foam_color.rgb;
	}

	if (has_wavy_top && pixelCoord.y - surfaceY < crest_thickness)
		color = crest_color.rgb;

	if (has_base_foam)
	{
		// Two jitter octaves at different block scales/speeds so the band's edge looks chaotic
		// (spray breaking apart) instead of a single smooth wobble.
		float jitterA = noise(vec2(blockCoord.x * 0.15, TIME * 3.0));
		float jitterB = noise(vec2(blockCoord.x * 0.4 + 50.0, TIME * 5.0 + 50.0));
		float jitter = (jitterA * 0.65 + jitterB * 0.35) * base_foam_turbulence;
		float bandStart = shape_size.y - base_foam_thickness - jitter;

		if (pixelCoord.y > bandStart)
			color = base_foam_color.rgb;
	}

	COLOR = vec4(color, alpha * opacity_multiplier);
}
