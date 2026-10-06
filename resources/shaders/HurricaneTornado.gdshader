shader_type canvas_item;

// Pixel-art hurricane column (Joe Higashi "Hurricane Upper" style). A cone that starts as a point
// on the ground and widens upward, made of stacked U-shaped ribbons that keep rising. Each ribbon
// is built row by row out of horizontal brush streaks of random length that overshoot its ends
// (ragged edges), fat in the middle and thinning to the sides; tone is streaky — bright crest on
// top and in the middle, darker toward the sides and underside. Four flat tones (c0 brightest ..
// c3 darkest), quantized to pix-sized pixels. Drawn on a ColorRect whose bottom (minus
// bottom_margin) is the ground.

uniform vec2 rect_size = vec2(230.0, 176.0);
uniform float bottom_margin = 8.0;
uniform float column_height = 160.0;
uniform float tip_radius = 3.0;
uniform float top_radius = 62.0;
// How many ribbons fit in the column, and how fast they climb (ribbons per second).
uniform float band_count = 10.0;
uniform float rise_speed = 1.4;
// < 1 widens quickly off the point (cone), 1 = straight funnel.
uniform float cone_power = 0.65;
uniform float thickness_bottom = 5.0;
uniform float thickness_top = 20.0;
// Ellipse squash: how strongly each ribbon curves up at its ends.
uniform float flatten = 0.5;
uniform float spin_speed = 70.0;
uniform float sway = 5.0;
uniform float pix = 2.0;
// 0..1: how much of the column is grown in (bottom-up).
uniform float presence = 1.0;
// Driven from script so the column starts from the same state every cast.
uniform float t_time = 0.0;
uniform vec4 c0 : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 c1 : source_color = vec4(0.84, 0.886, 0.949, 1.0);
uniform vec4 c2 : source_color = vec4(0.576, 0.655, 0.769, 1.0);
uniform vec4 c3 : source_color = vec4(0.337, 0.396, 0.541, 1.0);

float hash(vec2 p) {
	return fract(sin(p.x * 127.1 + p.y * 311.7) * 43758.5453);
}

void fragment() {
	vec2 local = UV * rect_size;
	float x = floor((local.x - rect_size.x * 0.5) / pix) * pix;
	float y = floor((rect_size.y - bottom_margin - local.y) / pix) * pix;
	if (y < 0.0 || y > column_height * presence) {
		discard;
	}

	float H = column_height;
	float T = t_time;
	float row = floor(y / pix);
	// Warped band coordinate: ribbons pack tighter near the point.
	float b = band_count * pow(y / H, 0.8) - T * rise_speed;
	int ib = int(floor(b));
	float best_pri = -1.0;
	int best_idx = -1;

	for (int k = -1; k <= 2; k++) {
		float fi = float(ib + k);
		float uc = pow(max(0.0, (fi + 0.5 + T * rise_speed) / band_count), 1.25);
		if (uc > 1.08) {
			continue;
		}
		float cy = uc * H;
		float t = clamp(uc, 0.0, 1.0);
		float rx = mix(tip_radius, top_radius, pow(t, cone_power));
		float ry = max(1.5, rx * flatten);
		float thick = mix(thickness_bottom, thickness_top, t);
		float even = mod(fi, 2.0) < 0.5 ? 1.0 : -1.0;
		float lx = x - (sway * t * sin(T * 2.3 + t * 4.0) + even * rx * 0.07 * sin(T * 5.0 + fi * 1.3));

		// Per-row ragged reach: streaks overshoot the ribbon's ends by a random amount.
		float h_row = hash(vec2(row * 0.73 + fi * 17.0, 4.2));
		if (abs(lx) > rx * (1.0 + 0.7 * h_row * h_row)) {
			continue;
		}
		float xn = min(1.0, abs(lx) / rx);
		float front_y = cy - ry * sqrt(max(0.0, 1.0 - xn * xn));
		float prof = pow(1.0 - xn * xn, 0.7);
		float th = thick * (0.25 + 0.75 * prof) * (0.85 + 0.3 * hash(vec2(fi, floor(lx / 6.0))));
		float top = front_y + th * 0.55;
		float bot = front_y - th * 0.45;
		bool in_front = y <= top && y >= bot;
		if (xn >= 1.0 && abs(y - front_y) > 3.5) {
			continue;
		}
		if (!in_front && xn < 1.0) {
			continue;
		}

		// Horizontal brush segmentation drifting along x (the spin); gappier toward the ends.
		float seg_len = 5.0 + 9.0 * hash(vec2(row, fi * 3.0 + 1.0));
		float sx = lx + (in_front ? 1.0 : -1.0) * T * spin_speed + hash(vec2(row, fi)) * 40.0;
		float seg_id = floor(sx / seg_len);
		if (hash(vec2(seg_id + row * 0.31, fi * 7.0)) < 0.10 + 0.55 * xn * xn) {
			continue;
		}

		int idx = 3;
		if (in_front) {
			float across = (top - y) / max(1.0, top - bot);
			float light = 1.0 - across;
			light *= 0.55 + 0.45 * (1.0 - xn);
			light += (hash(vec2(row * 1.3, seg_id)) - 0.5) * 0.16;
			light *= 0.75 + 0.25 * smoothstep(0.0, 0.5, t);
			idx = light > 0.72 ? 0 : (light > 0.5 ? 1 : (light > 0.28 ? 2 : 3));
			if (xn >= 1.0) {
				idx = max(idx, 2);
			}
		}
		// Ribbons dissolve as they leave the top of the column.
		if (hash(vec2(row * 2.1, seg_id * 1.7)) > 1.0 - smoothstep(0.92, 1.06, uc)) {
			continue;
		}

		// The lower ribbon sits in front of the one above it.
		float pri = (in_front ? 2.0 : 1.0) - fi * 0.001;
		if (pri > best_pri) {
			best_pri = pri;
			best_idx = idx;
		}
	}

	if (best_idx < 0) {
		discard;
	}
	vec4 col = best_idx == 0 ? c0 : (best_idx == 1 ? c1 : (best_idx == 2 ? c2 : c3));
	COLOR = vec4(col.rgb, col.a * COLOR.a);
}
