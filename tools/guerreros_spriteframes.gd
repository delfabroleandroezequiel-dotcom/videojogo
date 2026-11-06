extends SceneTree
# Builds resources/sprites/Guerrero<Name>SpriteFrames.tres from the PixelLab frames in
# assets/external/Chars/PixelLab/Guerreros/<Name>/<anim>_<i>.png and prints, per attack frame, how
# far the drawing reaches in front of the body centre (for hit frames / hitbox reach).
# Usage: godot --headless -s res://tools/guerreros_spriteframes.gd -- EnterbillEscudero RademHachero ...
const BASE = "res://assets/external/Chars/PixelLab/Guerreros/"
const ANIMS = {"idle": [6.0, true], "walk": [10.0, true], "attack": [14.0, false], "attack2": [14.0, false], "hurt": [12.0, false], "death": [10.0, false]}

func _init():
	for n in OS.get_cmdline_user_args():
		var sf = SpriteFrames.new()
		sf.remove_animation("default")
		for an in ANIMS:
			var frames = []
			var i = 0
			while ResourceLoader.exists(BASE + n + "/%s_%d.png" % [an, i]):
				frames.append(load(BASE + n + "/%s_%d.png" % [an, i])); i += 1
			if frames.is_empty(): continue
			sf.add_animation(an); sf.set_animation_speed(an, ANIMS[an][0]); sf.set_animation_loop(an, ANIMS[an][1])
			for t in frames: sf.add_frame(an, t)
			if an == "walk":
				sf.add_animation("run"); sf.set_animation_speed("run", 14.0); sf.set_animation_loop("run", true)
				for t in frames: sf.add_frame("run", t)
			if an.begins_with("attack") or an == "idle":
				var line = "%s %s:" % [n, an]
				for k in frames.size():
					var img = Image.load_from_file(ProjectSettings.globalize_path(BASE + n + "/%s_%d.png" % [an, k]))
					var r = img.get_used_rect()
					line += " %d:[%d..%d y%d..%d]" % [k, r.position.x - 48, r.end.x - 48, r.position.y - 80, r.end.y - 80]
				print(line)
		ResourceSaver.save(sf, "res://resources/sprites/Guerrero%sSpriteFrames.tres" % n)
	quit()
