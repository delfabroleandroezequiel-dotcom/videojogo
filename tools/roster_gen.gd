# Regenerates scenes/world/Mapas/Roster.tscn (the trailer roster on Roster.png): measures every
# character through a real RosterSlot and lays them out on the pyramid's ledges.
# Overwrites the scene — hand-moved figures are lost. Add new characters to ENTRIES.
# Run (Godot closed or from a copy): godot --headless --path . --script res://tools/roster_gen.gd -- write res://scenes/world/Mapas/Roster.tscn
extends SceneTree
# Builds scenes/world/Mapas/Roster.tscn: measures every character through a real RosterSlot,
# lays them out on the pyramid ledges of Roster.png and writes the scene.
# Run: godot --headless --path <proj> --script roster_gen.gd -- <measure|write> <out_path>

const S := "res://scenes/world/"
const F := "res://resources/sprites/"

# [group, path] or [group, path, fixed scale]. Scenes first so their SpriteFrames dedupe the plain-frames entries.
var ENTRIES := [
	["hero", "res://scenes/player/Player.tscn"],
	["ally", S + "Enemigos/Gransin.tscn"], ["ally", F + "AlatheaSpriteFrames.tres"],
	["boss", S + "SkeletonKingBoss.tscn"], ["boss", S + "Enemigos/MushroomBoss.tscn"], ["boss", S + "SpiderBoss.tscn"],
	["boss", S + "BossLobo.tscn"], ["boss", S + "Boss.tscn"], ["boss", S + "Enemys/LargeSkullBoss.tscn"],
	["boss", S + "Enemys/MountainEyeBoss.tscn"], ["boss", S + "Enemigos/Lugia.tscn"], ["boss", S + "Enemigos/LugiaVerde.tscn"],
	["giant", F + "GiantOgreRedSpriteFrames.tres"], ["giant", F + "GiantOgreBlueSpriteFrames.tres"],
	["bandit", S + "Enemys/Bandido1.tscn"], ["bandit", S + "Enemys/Bandido2.tscn"], ["bandit", S + "Enemys/Bandido3.tscn"],
	["bandit", S + "Enemigos/Bandido4.tscn"], ["bandit", S + "Enemigos/Bandido5.tscn"], ["bandit", S + "Enemigos/BandidoCorpulento.tscn"],
	["bandit", S + "Enemys/Perrito1.tscn"], ["bandit", F + "AssassinBandit3SpriteFrames.tres"], ["bandit", F + "HeavyBanditSpriteFrames.tres"],
	["bandit", F + "ThugSpriteFrames.tres"],
	["elf", S + "Enemigos/ElfArcher.tscn"], ["elf", S + "Enemigos/ElfMage.tscn"], ["elf", S + "Enemigos/ElfSpearman.tscn"],
	["elf", F + "ElfAssassinSpriteFrames.tres"], ["elf", F + "ElfSwordsmanSpriteFrames.tres"],
	["undead", F + "UndeadDeathKnightSpriteFrames.tres"], ["undead", F + "UndeadGhostSpriteFrames.tres"], ["undead", F + "UndeadNecromancerSpriteFrames.tres"],
	["undead", F + "UndeadSkeletonArcherSpriteFrames.tres"], ["undead", F + "UndeadSkeletonWarriorSpriteFrames.tres"],
	["undead", F + "SkeletonGuardSpriteFrames.tres"], ["undead", F + "SkeletonWarriorSpriteFrames.tres"],
	["undead", F + "NecromancerSpriteFrames.tres"], ["undead", F + "NecromancerCreativeKindSpriteFrames.tres"], ["undead", F + "NecropolisWarriorSpriteFrames.tres"],
	["undead", S + "Enemys/BurningDamned.tscn"], ["undead", S + "Enemys/BurningSkull.tscn"],
	["undead", S + "Enemigos/EsqueletoBallestero.tscn"], ["undead", S + "Enemigos/EsqueletoElite.tscn"],
	["undead", S + "Enemigos/EsqueletoEspadachin.tscn"], ["undead", S + "Enemigos/EsqueletoSepulturero.tscn"],
	["undead", S + "Enemigos/ZombiComun.tscn"], ["undead", S + "Enemigos/ZombiGordo.tscn"], ["undead", S + "Enemigos/ZombiBrazoLargo.tscn"],
	["undead", S + "Enemigos/ZombiEscupidor.tscn"], ["undead", S + "Enemigos/ZombiReptante.tscn"],
	["desert", F + "DesertCactusBedouinSpriteFrames.tres"], ["desert", F + "DesertCamelRiderBedouinSpriteFrames.tres"], ["desert", F + "DesertGlassBedouinSpriteFrames.tres"],
	["desert", F + "DesertSandBugSpriteFrames.tres"], ["desert", F + "DesertScorpionSpriteFrames.tres"], ["desert", F + "DesertVultureSpriteFrames.tres"],
	["dark", S + "Enemigos/GhostWarrior2.tscn"], ["dark", S + "Enemigos/Hechicero.tscn"], ["dark", S + "Enemigos/MagoCamino.tscn"],
	["dark", F + "NightborneSpriteFrames.tres"], ["dark", F + "BringerOfDeathSpriteFrames.tres"], ["dark", F + "ArchDemonSpriteFrames.tres"],
	["dark", F + "EvilWizardSpriteFrames.tres"], ["dark", F + "EvilWizard2SpriteFrames.tres"], ["dark", F + "EvilWizard3SpriteFrames.tres"],
	["dark", F + "ShadowUrchinSpriteFrames.tres"],
	["dark", S + "Enemigos/OscuroSegador.tscn"], ["dark", S + "Enemigos/OscuraHechicera.tscn"], ["dark", S + "Enemigos/OscuroCampeon.tscn"],
	["dark", S + "Enemigos/Diablilla.tscn"],
	["human", S + "Enemy.tscn"], ["human", S + "Enemigos/WarriorEnemy.tscn"], ["human", S + "Enemigos/WarriorEnemyBlond.tscn"],
	["human", S + "RangedEnemy.tscn"], ["human", S + "RangedEnemyBrown.tscn"],
	["human", F + "FantasyWarriorSpriteFrames.tres"], ["human", F + "HeroKnightSpriteFrames.tres"], ["human", F + "HeroKnight2SpriteFrames.tres"],
	["human", F + "HeroKnightSvenSpriteFrames.tres"], ["human", F + "HeroSpriteFrames.tres"], ["human", F + "HuntressSpriteFrames.tres"],
	["human", F + "Huntress2SpriteFrames.tres"], ["human", F + "MartialHeroSpriteFrames.tres"], ["human", F + "MartialHero3SpriteFrames.tres"],
	["human", F + "MedievalKingSpriteFrames.tres"], ["human", F + "MedievalWarriorSpriteFrames.tres"], ["human", F + "MedievalWarrior2SpriteFrames.tres"],
	["human", F + "MedievalWarrior3SpriteFrames.tres"], ["human", F + "WizardSpriteFrames.tres"], ["human", F + "PaladinSpriteFrames.tres"],
	["human", F + "SpearwomanSpriteFrames.tres"], ["human", F + "WomanWarriorSpriteFrames.tres"], ["human", F + "VikingSpriteFrames.tres"],
	["human", F + "NinjaSpriteFrames.tres"], ["human", F + "HoodedKnightSpriteFrames.tres"], ["human", F + "HoodedArcherSpriteFrames.tres"],
	["human", F + "ArmoredKnightSpriteFrames.tres"], ["human", F + "ShieldKnightSpriteFrames.tres"], ["human", F + "RangerArcherSpriteFrames.tres"],
	["human", F + "RiflemanSpriteFrames.tres"], ["human", F + "RedWarriorSpriteFrames.tres"], ["human", F + "ShamanSpriteFrames.tres"],
	["human", F + "AdventurerSpriteFrames.tres"], ["human", F + "FreeKnightSpriteFrames.tres"],
	["human", F + "FireWarriorSpriteFrames.tres"], ["human", F + "ProtagonistNuevoSpriteFrames.tres"],
	["human", F + "ProtagonistPreviewSpriteFrames.tres"],
	["monster", S + "Enemigos/OrcEnemy.tscn"], ["monster", S + "Enemigos/BatEnemy.tscn"], ["monster", S + "Enemigos/RatEnemy.tscn"],
	["monster", S + "Enemigos/SlimeEnemyBlue.tscn"], ["monster", S + "Enemigos/SlimeEnemyGreen.tscn"], ["monster", S + "Enemigos/SlimeEnemyRed.tscn"],
	["monster", S + "Enemigos/SpiderEnemy.tscn"], ["monster", S + "Enemigos/SpiderEnemyDark.tscn"], ["monster", S + "MosquitoEnemy.tscn"],
	["monster", S + "MushroomEnemy.tscn"], ["monster", S + "Enemigos/Hornet.tscn"], ["monster", S + "Enemigos/MurcielagoChico.tscn"],
	["monster", F + "MonsterMimicSpriteFrames.tres"],
	["monster", F + "MonsterRatSpriteFrames.tres"], ["monster", F + "MonsterSlimeSpriteFrames.tres"],
	["monster", F + "MonsterBatSpriteFrames.tres"],
	# Fantasy Monsters Pack (80x80 frames, same pixel density as the zombies): fixed 2x scale so the
	# small crawlers aren't normalized up to the protagonist's height. One colour variant per design
	# (B/C SpriteFrames exist too) - all 14 don't fit on the steps.
	["monster", F + "MonstruoFantasia1ASpriteFrames.tres", 2.0], ["monster", F + "MonstruoFantasia2ASpriteFrames.tres", 2.0],
	["monster", F + "MonstruoFantasia3ASpriteFrames.tres", 2.0], ["monster", F + "MonstruoFantasia4ASpriteFrames.tres", 2.0],
	["monster", F + "MonstruoFantasia5ASpriteFrames.tres", 2.0],
	["animal", F + "AkitaSpriteFrames.tres"], ["animal", F + "DalmatianSpriteFrames.tres"], ["animal", F + "DogSpriteFrames.tres"],
	["animal", F + "GoldenRetrieverSpriteFrames.tres"], ["animal", F + "GreatDaneSpriteFrames.tres"], ["animal", F + "SaintBernardSpriteFrames.tres"],
	["animal", F + "SchnauzerSpriteFrames.tres"], ["animal", F + "SiberianHuskySpriteFrames.tres"],
	["animal", F + "CatOrangeSpriteFrames.tres"], ["animal", F + "CatWhiteSpriteFrames.tres"],
	["town", F + "TavernBartenderSpriteFrames.tres"], ["town", F + "TavernGuest1SpriteFrames.tres"], ["town", F + "TavernGuest2SpriteFrames.tres"],
	["town", F + "TavernGuest3SpriteFrames.tres"], ["town", F + "MeatVendorSpriteFrames.tres"], ["town", F + "VendorSSWSpriteFrames.tres"],
	["town", F + "BlacksmithNpcSSWSpriteFrames.tres"],
]

var slot_script
var holder: Node2D
var frame := 0
var items := []          # dictionaries: group, path, slot, scale
var scale_map := {}      # frames path -> scale used in some scene
var ref_height := 0.0

func _initialize():
	slot_script = load("res://scripts/world/RosterSlot.cs")
	holder = Node2D.new()
	get_root().add_child(holder)
	_build_scale_map("res://scenes")

func _process(_d):
	frame += 1
	if frame == 1:
		_spawn_all()
	elif frame == 3:
		_finish()
		return true
	return false

func _spawn_all():
	for e in ENTRIES:
		var slot = slot_script.new()
		var path: String = e[1]
		var res = load(path)
		if res == null:
			print("MISSING ", path)
			continue
		if path.ends_with(".tscn"):
			slot.Source = res
		else:
			slot.Frames = res
			if path.ends_with("MonsterMimicSpriteFrames.tres"): slot.Animation = "idle_transformed"
			slot.FigureScale = scale_map.get(path, 0.0) if scale_map.has(path) else 1.0
			if e.size() > 2: slot.FigureScale = e[2]
		holder.add_child(slot)
		items.append({"group": e[0], "path": path, "slot": slot, "known": path.ends_with(".tscn") or scale_map.has(path) or e.size() > 2})

func _finish():
	var args := OS.get_cmdline_user_args()
	var mode := args[0] if args.size() > 0 else "measure"
	# Reference height: the protagonist at its in-game scale.
	for it in items:
		if it.group == "hero":
			ref_height = it.slot.ContentRect.size.y
	# Frames used by scene entries → drop duplicate frames entries.
	var used := {}
	for it in items:
		if it.path.ends_with(".tscn"):
			for n in it.slot.get_node("_Preview/Figure").get_children():
				if n is AnimatedSprite2D and n.sprite_frames != null:
					used[n.sprite_frames.resource_path] = it.path
	var kept := []
	for it in items:
		if not it.path.ends_with(".tscn") and used.has(it.path):
			print("DEDUP ", it.path.get_file(), " (in ", used[it.path].get_file(), ")")
			continue
		# Unknown scale: normalize to the protagonist's height (animals smaller, giants bigger).
		if not it.known:
			var h: float = it.slot.ContentRect.size.y / max(0.001, it.slot.FigureScale)
			var target := ref_height
			if it.group == "animal": target = ref_height * 0.5
			if it.group == "giant": target = ref_height * 3.2
			it.slot.FigureScale = target / max(1.0, h)
		# Showcase size: everyone 1.5x their in-game size so they fill the big steps (bosses
		# and giants are already huge and stay as they are).
		if it.group != "boss" and it.group != "giant":
			it.slot.FigureScale = it.slot.FigureScale * FIGURE_MULT
		# Bosses and giants: capped so the front row doesn't bury two steps of figures behind it.
		elif it.slot.ContentRect.size.y > BOSS_MAX_H:
			it.slot.FigureScale = it.slot.FigureScale * BOSS_MAX_H / it.slot.ContentRect.size.y
		kept.append(it)
	items = kept
	for it in items:
		var r: Rect2 = it.slot.ContentRect
		print("%-8s %-46s scale=%5.2f w=%4d h=%4d %s" % [it.group, it.path.get_file(), it.slot.FigureScale, r.size.x, r.size.y, "" if it.known else "(norm)"])
	print("ref_height=", ref_height, " count=", items.size())
	if mode == "write":
		_write(args[1])

# ── scale lookup from existing scenes ───────────────────────────────────────────────────────────
func _build_scale_map(dir_path: String):
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return
	for f in dir.get_files():
		if f.ends_with(".tscn") and not f.begins_with("Roster"):
			_scan_scene(dir_path + "/" + f)
	for d in dir.get_directories():
		_build_scale_map(dir_path + "/" + d)

var _re_ext := RegEx.create_from_string('\\[ext_resource[^\\]]*path="([^"]+SpriteFrames\\.tres)"[^\\]]*id="([^"]+)"')
var _re_scale := RegEx.create_from_string('^scale = Vector2\\(([-0-9.e]+), ([-0-9.e]+)\\)')
var _re_frames := RegEx.create_from_string('^sprite_frames = ExtResource\\("([^"]+)"\\)')

func _scan_scene(path: String):
	var text := FileAccess.get_file_as_string(path)
	var ids := {}
	for m in _re_ext.search_all(text):
		ids[m.get_string(2)] = m.get_string(1)
	if ids.is_empty():
		return
	var low_priority := path.find("EnemyLookAll") >= 0 or path.find("MainMenu") >= 0
	var cur_scale := 0.0
	var cur_frames := ""
	for line in text.split("\n"):
		if line.begins_with("[node"):
			_record(cur_frames, cur_scale, low_priority)
			cur_scale = 0.0
			cur_frames = ""
			continue
		var ms := _re_scale.search(line)
		if ms: cur_scale = abs(float(ms.get_string(2)))
		var mf := _re_frames.search(line)
		if mf and ids.has(mf.get_string(1)): cur_frames = ids[mf.get_string(1)]
	_record(cur_frames, cur_scale, low_priority)

func _record(frames: String, s: float, low: bool):
	if frames == "" or s <= 0.0:
		return
	if scale_map.has(frames) and low:
		return
	scale_map[frames] = s

# ── layout + write ──────────────────────────────────────────────────────────────────────────────
const BG_SCALE := 2.0
# Ledges of Roster.png in image pixels: [surface y, x from, x to].
const ROWS := [
	[338, 700, 1075],
	[408, 455, 1325],
	[475, 405, 1340],
	[546, 345, 1400],
	[619, 290, 1495],
	[697, 255, 1525],
	[782, 30, 1745],
]
const GAP := 10.0
const FIGURE_MULT := 1.5
const BOSS_MAX_H := 205.0

func _write(out_path: String):
	# Which groups go on which rows (top → bottom); anything that doesn't fit spills downward.
	# Top row: the hero and allies. Rows 1-5: factions in this order, filled in sequence (a
	# faction may wrap onto the next step). Ground: bosses + giants + whatever's left (beasts).
	var order := ["elf", "dark", "bandit", "undead", "human", "desert", "town", "monster", "animal"]
	var by_group := {}
	for it in items:
		if not by_group.has(it.group): by_group[it.group] = []
		by_group[it.group].append(it)
	var rows := []
	for i in ROWS.size(): rows.append([])
	rows[0].append_array(by_group.get("ally", []))
	rows[0].append_array(by_group.get("hero", []))
	var queue := []
	for g in order:
		queue.append_array(by_group.get(g, []))
	var row_i := 1
	var used := 0.0
	for it in queue:
		var w: float = it.slot.ContentRect.size.x + GAP
		while row_i < ROWS.size() - 1 and used + w > (ROWS[row_i][2] - ROWS[row_i][1]) * BG_SCALE:
			row_i += 1
			used = 0.0
		rows[row_i].append(it)
		used += w
	rows[ROWS.size() - 1].append_array(by_group.get("giant", []))
	rows[ROWS.size() - 1].append_array(by_group.get("boss", []))
	# Hero in the middle of the top row: allies either side.
	var top: Array = rows[0]
	for k in top.size():
		if top[k].group == "hero":
			var hero = top[k]
			top.remove_at(k)
			top.insert(top.size() / 2, hero)
			break
	# Ground row: giants at the far edges, bosses in the centre, the rest around them.
	var ground: Array = rows[ROWS.size() - 1]
	var giants := ground.filter(func(x): return x.group == "giant")
	var bosses := ground.filter(func(x): return x.group == "boss")
	var others := ground.filter(func(x): return x.group != "giant" and x.group != "boss")
	var half := others.size() / 2
	var arranged := []
	if giants.size() > 0: arranged.append(giants[0])
	arranged.append_array(others.slice(0, half))
	arranged.append_array(bosses)
	arranged.append_array(others.slice(half))
	if giants.size() > 1: arranged.append(giants[1])
	rows[ROWS.size() - 1] = arranged

	for i in ROWS.size():
		var tw := 0.0
		for it in rows[i]: tw += it.slot.ContentRect.size.x + GAP
		print("row %d: %d figures, %d / %d px" % [i, rows[i].size(), tw, (ROWS[i][2] - ROWS[i][1]) * BG_SCALE])
	var lines := []
	var ext := []
	var ext_ids := {}
	ext.append('[ext_resource type="Script" path="res://scripts/world/RosterSlot.cs" id="slot"]')
	ext.append('[ext_resource type="Texture2D" uid="uid://p5pqv51madx3" path="res://assets/sprites/deco/Roster.png" id="bg"]')
	ext.append('[ext_resource type="PackedScene" path="res://scenes/player/Player.tscn" id="player"]')
	var center_x := 887.0 * BG_SCALE
	var names := {}
	for i in ROWS.size():
		var row: Array = rows[i]
		if row.is_empty(): continue
		lines.append('\n[node name="Row%d" type="Node2D" parent="Figures"]' % i)
		var total := 0.0
		for it in row: total += it.slot.ContentRect.size.x + GAP
		total -= GAP
		var span: float = (ROWS[i][2] - ROWS[i][1]) * BG_SCALE
		var x: float = ROWS[i][1] * BG_SCALE + max(0.0, (span - total) * 0.5)
		var spacing_extra: float = 0.0 if total >= span or row.size() < 2 else (span - total) / (row.size() - 1) if i == ROWS.size() - 1 else min(40.0, (span - total) / (row.size() - 1))
		x = ROWS[i][1] * BG_SCALE + max(0.0, (span - total - spacing_extra * (row.size() - 1)) * 0.5)
		var y: float = ROWS[i][0] * BG_SCALE
		for it in row:
			var r: Rect2 = it.slot.ContentRect
			var w: float = r.size.x
			var px: float = x - r.position.x   # content's left edge at x
			x += w + GAP + spacing_extra
			if not ext_ids.has(it.path):
				var id := "r%d" % ext_ids.size()
				ext_ids[it.path] = id
				var t := "PackedScene" if it.path.ends_with(".tscn") else "SpriteFrames"
				ext.append('[ext_resource type="%s" path="%s" id="%s"]' % [t, it.path, id])
			var base: String = it.path.get_file().get_basename().replace("SpriteFrames", "")
			if it.group == "hero": base = "Protagonista"
			var nm := base
			var n := 2
			while names.has(nm):
				nm = "%s%d" % [base, n]; n += 1
			names[nm] = true
			var mirror: bool = (px + r.position.x + w * 0.5) > center_x
			print("FIG %s %d %.0f %.0f %.0f %.0f" % [nm, i, px + r.position.x, y + r.position.y, w, r.size.y])
			lines.append('\n[node name="%s" type="Node2D" parent="Figures/Row%d"]' % [nm, i])
			lines.append('position = Vector2(%.1f, %.1f)' % [px, y])
			lines.append('script = ExtResource("slot")')
			if it.path.ends_with(".tscn"):
				lines.append('Source = ExtResource("%s")' % ext_ids[it.path])
			else:
				lines.append('Frames = ExtResource("%s")' % ext_ids[it.path])
			if abs(it.slot.FigureScale - 1.0) > 0.0001:
				lines.append('FigureScale = %.4f' % it.slot.FigureScale)
			if mirror:
				lines.append('Mirror = true')
			if it.path.ends_with("MonsterMimicSpriteFrames.tres"):
				lines.append('Animation = "idle_transformed"')

	var out := []
	out.append('[gd_scene format=3]\n')
	out.append_array(ext)
	out.append('\n[sub_resource type="RectangleShape2D" id="ground_shape"]\nsize = Vector2(%.1f, 80)' % (1774.0 * BG_SCALE))
	out.append('\n[node name="Roster" type="Node2D"]')
	out.append('\n[node name="Background" type="Sprite2D" parent="."]\nz_index = -10\ntexture = ExtResource("bg")\ncentered = false\nscale = Vector2(%.1f, %.1f)' % [BG_SCALE, BG_SCALE])
	out.append('\n[node name="Ground" type="StaticBody2D" parent="."]\nposition = Vector2(%.1f, %.1f)' % [887.0 * BG_SCALE, 782.0 * BG_SCALE + 40.0])
	out.append('\n[node name="CollisionShape2D" type="CollisionShape2D" parent="Ground"]\nshape = SubResource("ground_shape")')
	for i in range(ROWS.size() - 1):
		var w: float = (ROWS[i][2] - ROWS[i][1]) * BG_SCALE
		out.append('\n[node name="Ledge%d" type="StaticBody2D" parent="."]\ncollision_layer = 33\nposition = Vector2(%.1f, %.1f)' % [i, (ROWS[i][1] + ROWS[i][2]) * 0.5 * BG_SCALE, ROWS[i][0] * BG_SCALE])
		out.append('\n[node name="CollisionShape2D" type="CollisionShape2D" parent="Ledge%d"]\nposition = Vector2(0, 4)\none_way_collision = true\nshape = SubResource("ledge_shape_%d")' % [i, i])
	out.append('\n[node name="Figures" type="Node2D" parent="."]')
	out.append_array(lines)
	out.append('\n[node name="Player" parent="." instance=ExtResource("player")]\nposition = Vector2(%.1f, %.1f)\nFallDeathY = 2200.0' % [120.0, 782.0 * BG_SCALE - 40.0])
	var text := "\n".join(out) + "\n"
	# Ledge shapes (sub_resources must come before nodes).
	var subs := ""
	for i in range(ROWS.size() - 1):
		subs += '\n[sub_resource type="RectangleShape2D" id="ledge_shape_%d"]\nsize = Vector2(%.1f, 8)\n' % [i, (ROWS[i][2] - ROWS[i][1]) * BG_SCALE]
	text = text.replace('\n[node name="Roster"', subs + '\n[node name="Roster"')
	var f := FileAccess.open(out_path, FileAccess.WRITE)
	f.store_string(text)
	f.close()
	print("WROTE ", out_path)
