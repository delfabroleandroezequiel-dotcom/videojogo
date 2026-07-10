extends SceneTree

# Splits a sheet containing several poses (transparent background, any layout —
# single strip or a multi-row grid) into individual, aligned PNG frames, all
# sharing one canvas size and anchor so a character doesn't visibly grow/shrink
# or pop when switching animations.
#
# Detects poses via connected-component blob labeling (flood fill over pixels
# with alpha above threshold), so it doesn't care whether poses sit on one row
# or several, and naturally discards small unrelated blobs (e.g. a baked-in
# pose number printed under the character) via a minimum blob size.
#
# Usage:
#   godot --headless --script tools/align_sprite_frames.gd -- <input_png> <output_dir> <prefix> [--scale F] [--canvas W H]
#
# --scale F        resize each cropped pose by this factor before placing it on the
#                   canvas (use when a sheet was generated at a different zoom level
#                   than the others; compare average content width across sheets to
#                   pick F, since width holds steadier across poses than height).
# --canvas W H     force a specific shared output canvas size instead of auto-sizing
#                   to this batch alone (use once you know the size that fits every
#                   animation, so they all share the same anchor point).

func _initialize():
	var raw_args = OS.get_cmdline_user_args()
	if raw_args.size() < 3:
		print("Usage: align_sprite_frames.gd <input_png> <output_dir> <prefix> [--scale F] [--canvas W H]")
		quit(1)
		return

	var input_path: String = raw_args[0]
	var output_dir: String = raw_args[1]
	var prefix: String = raw_args[2]

	var scale := 1.0
	var forced_canvas_w := -1
	var forced_canvas_h := -1

	var i := 3
	while i < raw_args.size():
		if raw_args[i] == "--scale" and i + 1 < raw_args.size():
			scale = float(raw_args[i + 1])
			i += 2
		elif raw_args[i] == "--canvas" and i + 2 < raw_args.size():
			forced_canvas_w = int(raw_args[i + 1])
			forced_canvas_h = int(raw_args[i + 2])
			i += 3
		else:
			i += 1

	var img := Image.load_from_file(ProjectSettings.globalize_path(input_path))
	if img == null:
		print("Failed to load: ", input_path)
		quit(1)
		return

	var w := img.get_width()
	var h := img.get_height()
	var alpha_threshold := 0.05

	var occupied := PackedByteArray()
	occupied.resize(w * h)
	for y in range(h):
		for x in range(w):
			occupied[y * w + x] = 1 if img.get_pixel(x, y).a > alpha_threshold else 0

	var visited := PackedByteArray()
	visited.resize(w * h)

	var blobs := []
	var min_blob_pixels := 200

	for y0 in range(h):
		for x0 in range(w):
			var idx0 = y0 * w + x0
			if occupied[idx0] == 0 or visited[idx0] == 1:
				continue

			var min_x = x0
			var max_x = x0
			var min_y = y0
			var max_y = y0
			var count = 0

			var stack := [Vector2i(x0, y0)]
			visited[idx0] = 1
			while stack.size() > 0:
				var p: Vector2i = stack.pop_back()
				count += 1
				if p.x < min_x: min_x = p.x
				if p.x > max_x: max_x = p.x
				if p.y < min_y: min_y = p.y
				if p.y > max_y: max_y = p.y

				var neighbors = [Vector2i(p.x + 1, p.y), Vector2i(p.x - 1, p.y), Vector2i(p.x, p.y + 1), Vector2i(p.x, p.y - 1)]
				for n in neighbors:
					if n.x < 0 or n.x >= w or n.y < 0 or n.y >= h:
						continue
					var nidx = n.y * w + n.x
					if occupied[nidx] == 1 and visited[nidx] == 0:
						visited[nidx] = 1
						stack.append(n)

			if count >= min_blob_pixels:
				blobs.append([min_x, max_x, min_y, max_y])

	print("Detected ", blobs.size(), " blob(s) before merging: ", blobs)

	# Merge blobs whose bounding boxes are close/overlapping (a pose can be split into
	# several disconnected pixel islands, e.g. a sword tip separated from the hand by
	# anti-aliasing, or a diagonal motion-streak whose axis-aligned bbox pokes into the
	# next pose). Only merge when at least one of the two blobs is small — a real
	# fragment, not another full pose — so two adjacent complete poses never get fused
	# just because an elongated streak's bbox overlaps its neighbor's.
	var merge_gap := 6
	var small_blob_area := 1200
	var merged := true
	while merged:
		merged = false
		for a in range(blobs.size()):
			if blobs[a] == null:
				continue
			for b in range(a + 1, blobs.size()):
				if blobs[b] == null:
					continue
				var ba = blobs[a]
				var bb = blobs[b]
				var area_a = (ba[1] - ba[0] + 1) * (ba[3] - ba[2] + 1)
				var area_b = (bb[1] - bb[0] + 1) * (bb[3] - bb[2] + 1)
				if area_a >= small_blob_area and area_b >= small_blob_area:
					continue
				var overlap_x = ba[0] - merge_gap <= bb[1] and bb[0] - merge_gap <= ba[1]
				var overlap_y = ba[2] - merge_gap <= bb[3] and bb[2] - merge_gap <= ba[3]
				if overlap_x and overlap_y:
					blobs[a] = [min(ba[0], bb[0]), max(ba[1], bb[1]), min(ba[2], bb[2]), max(ba[3], bb[3])]
					blobs[b] = null
					merged = true
		var compact := []
		for b in blobs:
			if b != null:
				compact.append(b)
		blobs = compact

	print("Blobs after merge: ", blobs.size(), " -> ", blobs)

	# Reading order: cluster into rows by vertical center proximity, then sort each
	# row left to right, so a multi-row grid comes out in the expected animation order.
	var avg_h := 0.0
	for b in blobs:
		avg_h += (b[3] - b[2] + 1)
	avg_h /= blobs.size()

	var by_center_y := blobs.duplicate()
	by_center_y.sort_custom(func(a, b): return (a[2] + a[3]) < (b[2] + b[3]))

	var rows := []
	var current_row := []
	var last_center_y := -1000.0
	for b in by_center_y:
		var center_y = (b[2] + b[3]) / 2.0
		if current_row.size() > 0 and abs(center_y - last_center_y) > avg_h * 0.6:
			rows.append(current_row)
			current_row = []
		current_row.append(b)
		last_center_y = center_y
	if current_row.size() > 0:
		rows.append(current_row)

	var frames := []
	for row in rows:
		row.sort_custom(func(a, b): return a[0] < b[0])
		for b in row:
			frames.append(b)

	print("Frame order (x0,x1,y0,y1): ", frames)

	var widths := []
	var heights := []
	for f in frames:
		widths.append(f[1] - f[0] + 1)
		heights.append(f[3] - f[2] + 1)

	var avg_w := 0.0
	var avg_h2 := 0.0
	for x in widths:
		avg_w += x
	for x in heights:
		avg_h2 += x
	avg_w /= widths.size()
	avg_h2 /= heights.size()
	print("Average content size before scaling: ", avg_w, " x ", avg_h2, " (use this to compute --scale for other sheets)")

	var scaled_max_w := 0
	var scaled_max_h := 0
	for j in range(frames.size()):
		var fw: int = int(round(widths[j] * scale))
		var fh: int = int(round(heights[j] * scale))
		if fw > scaled_max_w:
			scaled_max_w = fw
		if fh > scaled_max_h:
			scaled_max_h = fh

	var pad := 4
	var canvas_w := forced_canvas_w if forced_canvas_w > 0 else scaled_max_w + pad * 2
	var canvas_h := forced_canvas_h if forced_canvas_h > 0 else scaled_max_h + pad * 2

	var out_dir_global = ProjectSettings.globalize_path(output_dir)
	DirAccess.make_dir_recursive_absolute(out_dir_global)

	var idx := 0
	for f in frames:
		var x0 = f[0]
		var x1 = f[1]
		var y0 = f[2]
		var y1 = f[3]
		var fw = x1 - x0 + 1
		var fh = y1 - y0 + 1

		var cropped := Image.create(fw, fh, false, Image.FORMAT_RGBA8)
		cropped.blit_rect(img, Rect2i(x0, y0, fw, fh), Vector2i(0, 0))

		if scale != 1.0:
			var sw: int = int(round(fw * scale))
			var sh: int = int(round(fh * scale))
			cropped.resize(sw, sh, Image.INTERPOLATE_NEAREST)
			fw = sw
			fh = sh

		var canvas := Image.create(canvas_w, canvas_h, false, Image.FORMAT_RGBA8)
		var dest_x := int((canvas_w - fw) / 2.0)
		var dest_y := canvas_h - pad - int(fh)
		canvas.blit_rect(cropped, Rect2i(0, 0, fw, fh), Vector2i(dest_x, dest_y))

		var out_path := "%s/%s_%02d.png" % [output_dir, prefix, idx]
		canvas.save_png(ProjectSettings.globalize_path(out_path))
		print("Saved ", out_path, " (", fw, "x", fh, " -> canvas ", canvas_w, "x", canvas_h, ")")
		idx += 1

	print("Canvas size used: ", canvas_w, "x", canvas_h)
	quit()
