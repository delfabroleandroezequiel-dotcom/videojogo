shader_type canvas_item;

// Re-pixelates a smooth / high-res sprite in place: averages each pixel_size block
// (snapped to each frame's own grid, so blocks never straddle two hframes and bleed the
// neighbouring pose in), hardens the alpha edge, and optionally posterizes to color_steps
// levels per channel for a limited-palette look. Tune live in the Inspector.
uniform float pixel_size : hint_range(1.0, 16.0, 1.0) = 3.0;
// Size of one frame in texels (texture width / hframes, height / vframes).
uniform vec2 frame_size = vec2(258.0, 174.0);
// 0 = keep the colours as they are.
uniform int color_steps : hint_range(0, 32) = 12;
uniform float alpha_cutoff : hint_range(0.0, 1.0) = 0.5;

// Volume shading: the frame is treated as a dome (a half-ellipsoid sitting on the frame's
// lower part) lit from light_dir (x right, y down, z toward the camera). The side away from
// the light darkens by up to shade_strength and the underside gets ambient occlusion — computed
// per block and before posterizing, so the shadows come out as stepped pixel-art bands.
// shade_strength 0 = off.
uniform float shade_strength : hint_range(0.0, 1.0) = 0.55;
uniform vec3 light_dir = vec3(-0.55, -0.75, 0.6);
// Where the dome's widest point sits, as a fraction of the frame height (below it = underside).
uniform float dome_base : hint_range(0.3, 1.0) = 0.8;
uniform float underside_occlusion : hint_range(0.0, 1.0) = 0.45;

// COLOR in fragment() already has the texture multiplied in, so the plain modulate
// (hit flash, fades) is taken from the vertex stage instead.
varying vec4 modulate;

void vertex() {
	modulate = COLOR;
}

void fragment() {
	vec2 tex_size = 1.0 / TEXTURE_PIXEL_SIZE;
	vec2 texel = UV * tex_size;
	vec2 local = mod(texel, frame_size);
	vec2 frame_origin = texel - local;
	vec2 block_start = floor(local / pixel_size) * pixel_size;

	// Average a 3x3 grid across the block (a proper downscale) instead of picking one texel —
	// single-texel sampling turned the fine painted detail into speckle noise at 3-4 px blocks.
	// Alpha-weighted so transparent texels don't darken the edge colour.
	vec4 sum = vec4(0.0);
	float alpha_sum = 0.0;
	for (int y = 0; y < 3; y++) {
		for (int x = 0; x < 3; x++) {
			vec2 p = block_start + (vec2(float(x), float(y)) + 0.5) * (pixel_size / 3.0);
			p = min(p, frame_size - 0.5);
			vec4 s = texture(TEXTURE, (frame_origin + p) / tex_size);
			sum.rgb += s.rgb * s.a;
			alpha_sum += s.a;
		}
	}
	vec4 c = vec4(alpha_sum > 0.0 ? sum.rgb / alpha_sum : vec3(0.0), alpha_sum / 9.0);

	if (shade_strength > 0.0) {
		vec2 q = (block_start + pixel_size * 0.5) / frame_size;
		float nx = (q.x - 0.5) * 2.0;
		float ny = (q.y - dome_base) / dome_base;
		vec3 normal = normalize(vec3(nx, ny, sqrt(max(0.0, 1.0 - nx * nx - ny * ny)) + 0.15));
		float lambert = max(dot(normal, normalize(light_dir)), 0.0);
		float light = mix(1.0 - shade_strength, 1.0 + shade_strength * 0.25, lambert);
		float occlusion = smoothstep(dome_base - 0.1, 1.0, q.y) * underside_occlusion;
		c.rgb *= light * (1.0 - occlusion);
	}

	if (color_steps > 0) {
		// Posterize in display (sRGB-ish) space: with HDR 2D the colour arrives linear, and
		// equal linear steps would crush the shadows into a couple of near-black bands.
		float steps = float(color_steps);
		vec3 display = pow(c.rgb, vec3(1.0 / 2.2));
		display = floor(display * steps + 0.5) / steps;
		c.rgb = pow(display, vec3(2.2));
	}
	c.a = c.a >= alpha_cutoff ? 1.0 : 0.0;
	COLOR = c * modulate;
}
