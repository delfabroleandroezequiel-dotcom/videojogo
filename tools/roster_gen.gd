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
	["ally", F + "AlatheaPixelLabSpriteFrames.tres"], ["ally", F + "ArthurSpriteFrames.tres"], ["ally", F + "TobiasSpriteFrames.tres"],
	["ally", F + "MatheoSpriteFrames.tres"], ["ally", F + "EsfianerSpriteFrames.tres"],
	["boss", S + "Enemigos/Artemisa.tscn"],
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
	["human", F + "FantasyWarriorSpriteFrames.tres"], ["human", F + "HeroKnightSpriteFrames.tres"], ["human", F + "HeroKnight2SpriteFrames.tres"],
	["human", F + "HeroKnightSvenSpriteFrames.tres"], ["human", F + "HeroSpriteFrames.tres"], ["human", F + "HuntressSpriteFrames.tres"],
	["human", F + "Huntress2SpriteFrames.tres"], ["human", F + "MartialHeroSpriteFrames.tres"], ["human", F + "MartialHero3SpriteFrames.tres"],
	["human", F + "MedievalKingSpriteFrames.tres"], ["human", F + "MedievalWarriorSpriteFrames.tres"], ["human", F + "MedievalWarrior2SpriteFrames.tres"],
	["human", F + "MedievalWarrior3SpriteFrames.tres"], ["human", F + "WizardSpriteFrames.tres"], ["human", F + "PaladinSpriteFrames.tres"],
	["human", F + "SpearwomanSpriteFrames.tres"], ["human", F + "WomanWarriorSpriteFrames.tres"], ["human", F + "VikingSpriteFrames.tres"],
	["human", F + "NinjaSpriteFrames.tres"], ["human", F + "HoodedKnightSpriteFrames.tres"], ["human", F + "HoodedArcherSpriteFrames.tres"],
	["human", F + "ArmoredKnightSpriteFrames.tres"], ["human", F + "ShieldKnightSpriteFrames.tres"], ["human", F + "RangerArcherSpriteFrames.tres"],
	["human", F + "RiflemanSpriteFrames.tres"], ["human", F + "RedWarriorSpriteFrames.tres"], ["human", F + "ShamanSpriteFrames.tres"],
	["human", F + "FreeKnightSpriteFrames.tres"],
	["human", F + "FireWarriorSpriteFrames.tres"], ["human", F + "ProtagonistNuevoSpriteFrames.tres"],
	["monster", S + "Enemigos/OrcEnemy.tscn"], ["monster", S + "Enemigos/BatEnemy.tscn"],
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
	# Fantasy Forest Enemies (96x96 canvas): fixed 1.4x so their pixels match the humans' density. Base palette only (2/3 variants exist).
	["monster", F + "BosqueEscarabajoSpriteFrames.tres", 1.4], ["monster", F + "BosqueLimoSpriteFrames.tres", 1.4],
	["monster", F + "BosqueGusanoSpriteFrames.tres", 1.4], ["monster", F + "BosqueAbejorroSpriteFrames.tres", 1.4],
	["monster", F + "BosqueHongoMoradoSpriteFrames.tres", 1.4], ["monster", F + "BosqueHongoEspinosoSpriteFrames.tres", 1.4],
	["monster", F + "BosqueOjoTalloSpriteFrames.tres", 1.4], ["monster", F + "BosqueHongoFantasmaSpriteFrames.tres", 1.4],
	["monster", F + "CthuluSpriteFrames.tres", 1.4],
	["animal", F + "AkitaSpriteFrames.tres"], ["animal", F + "DalmatianSpriteFrames.tres"], ["animal", F + "DogSpriteFrames.tres"],
	["animal", F + "GoldenRetrieverSpriteFrames.tres"], ["animal", F + "GreatDaneSpriteFrames.tres"], ["animal", F + "SaintBernardSpriteFrames.tres"],
	["animal", F + "SchnauzerSpriteFrames.tres"], ["animal", F + "SiberianHuskySpriteFrames.tres"],
	["animal", F + "CatOrangeSpriteFrames.tres"], ["animal", F + "CatWhiteSpriteFrames.tres"],
	["town", F + "TavernBartenderSpriteFrames.tres"], ["town", F + "TavernGuest1SpriteFrames.tres"], ["town", F + "TavernGuest2SpriteFrames.tres"],
	["town", F + "TavernGuest3SpriteFrames.tres"], ["town", F + "MeatVendorSpriteFrames.tres"], ["town", F + "VendorSSWSpriteFrames.tres"],
	["town", F + "BlacksmithNpcSSWSpriteFrames.tres"],
	# ── 2026-11-16: everything made since the last roster pass ──
	# Enemy scenes built on frames already listed above (they dedupe those frames entries).
	["elf", S + "Enemigos/ElfAssassin.tscn"], ["elf", S + "Enemigos/ElfSwordsman.tscn"],
	["dark", S + "Enemigos/Archidemonio.tscn"], ["dark", S + "Enemigos/ErizoSombrio.tscn"], ["dark", S + "Enemigos/HeraldoDeLaMuerte.tscn"],
	["dark", S + "Enemigos/NacidoDeLaNoche.tscn"], ["dark", S + "Enemigos/Piromante.tscn"],
	["undead", S + "Enemigos/CaballeroDeLaMuerte.tscn"], ["undead", S + "Enemigos/EsqueletoArquero.tscn"], ["undead", S + "Enemigos/EsqueletoGuerrero.tscn"],
	["undead", S + "Enemigos/Fantasma.tscn"], ["undead", S + "Enemigos/GuardiaEsqueleto.tscn"], ["undead", S + "Enemigos/GuerreroNecropolis.tscn"],
	["undead", S + "Enemigos/Nigromante.tscn"], ["undead", S + "Enemigos/NigromanteDeSangre.tscn"], ["undead", S + "Enemigos/HechiceroEspectral.tscn"],
	["human", S + "Enemigos/CaballeroDelEscudo.tscn"], ["human", S + "Enemigos/Chaman.tscn"], ["human", S + "Enemigos/EspadachinErrante.tscn"],
	["human", S + "Enemigos/GuerreroRojo.tscn"], ["human", S + "Enemigos/Paladin.tscn"], ["human", S + "Enemigos/RangerArcher.tscn"],
	["desert", S + "Enemigos/BeduinoCactus.tscn"], ["desert", S + "Enemigos/BeduinoVidrio.tscn"], ["desert", S + "Enemigos/BichoArena.tscn"],
	["desert", S + "Enemigos/Buitre.tscn"], ["desert", S + "Enemigos/Escorpion.tscn"], ["desert", S + "Enemigos/JineteCamello.tscn"],
	["monster", S + "Enemigos/LimoAcido.tscn"], ["monster", S + "Enemigos/Mimico.tscn"], ["monster", S + "Enemigos/RataPlaga.tscn"],
	["bandit", S + "Enemigos/Maton.tscn"], ["bandit", S + "Enemigos/Bandido6.tscn"], ["bandit", S + "Enemigos/Bandido7.tscn"],
	# PixelLab bandit variants.
	["bandit", S + "Enemigos/Bandido2Ballesta.tscn"], ["bandit", S + "Enemigos/Bandido2Escudo.tscn"], ["bandit", S + "Enemigos/Bandido2Lanza.tscn"],
	["bandit", S + "Enemigos/Bandido3Baston.tscn"], ["bandit", S + "Enemigos/Bandido3DosEspadas.tscn"], ["bandit", S + "Enemigos/Bandido3Honda.tscn"],
	["bandit", S + "Enemigos/Bandido4Bombas.tscn"], ["bandit", S + "Enemigos/Bandido4Hoz.tscn"], ["bandit", S + "Enemigos/Bandido4Latigo.tscn"],
	["bandit", S + "Enemigos/Bandido5Cadena.tscn"], ["bandit", S + "Enemigos/Bandido5Garrote.tscn"], ["bandit", S + "Enemigos/Bandido5Punos.tscn"],
	["bandit", S + "Enemigos/Bandido6Cerbatana.tscn"], ["bandit", S + "Enemigos/Bandido6Cimitarra.tscn"], ["bandit", S + "Enemigos/Bandido6Cuchillos.tscn"],
	["bandit", S + "Enemigos/Bandido7Alabarda.tscn"], ["bandit", S + "Enemigos/Bandido7Escudo.tscn"], ["bandit", S + "Enemigos/Bandido7Mandoble.tscn"],
	["bandit", S + "Enemigos/BandidoArquero.tscn"], ["bandit", S + "Enemigos/BandidoCuerda.tscn"], ["bandit", S + "Enemigos/BandidoDagas.tscn"],
	["bandit", S + "Enemigos/BandidoHacha.tscn"], ["bandit", S + "Enemigos/CorpulentoHachaDoble.tscn"], ["bandit", S + "Enemigos/CorpulentoMangual.tscn"],
	["bandit", S + "Enemigos/CorpulentoMartillo.tscn"],
	# Enterbill / Radem soldiers.
	["soldier", S + "Enemigos/GuerreroEnterbillAlabardero.tscn"], ["soldier", S + "Enemigos/GuerreroEnterbillBallestero.tscn"],
	["soldier", S + "Enemigos/GuerreroEnterbillCaballero.tscn"], ["soldier", S + "Enemigos/GuerreroEnterbillCapitan.tscn"],
	["soldier", S + "Enemigos/GuerreroEnterbillEscudero.tscn"], ["soldier", S + "Enemigos/GuerreroEnterbillLancero.tscn"],
	["soldier", S + "Enemigos/GuerreroEnterbillMaza.tscn"],
	["soldier", S + "Enemigos/GuerreroRademArquero.tscn"], ["soldier", S + "Enemigos/GuerreroRademCampeon.tscn"],
	["soldier", S + "Enemigos/GuerreroRademCimitarras.tscn"], ["soldier", S + "Enemigos/GuerreroRademHachero.tscn"],
	["soldier", S + "Enemigos/GuerreroRademJabalinero.tscn"], ["soldier", S + "Enemigos/GuerreroRademKhopesh.tscn"],
	["soldier", S + "Enemigos/GuerreroRademMangual.tscn"],
	# Wolves.
	["wolf", S + "Enemigos/Lobos/LoboViejoAlfa.tscn"], ["wolf", S + "Enemigos/Lobos/LoboTerrible.tscn"], ["wolf", S + "Enemigos/Lobos/LoboGris.tscn"],
	["wolf", S + "Enemigos/Lobos/LoboNegro.tscn"], ["wolf", S + "Enemigos/Lobos/LoboRojizo.tscn"], ["wolf", S + "Enemigos/Lobos/LoboMusgo.tscn"],
	["wolf", S + "Enemigos/Lobos/LoboNieve.tscn"], ["wolf", S + "Enemigos/Lobos/LoboDesierto.tscn"], ["wolf", S + "Enemigos/Lobos/LoboPodrido.tscn"],
	["wolf", S + "Enemigos/Lobos/LoboSombra.tscn"],
	# Story NPCs: Nix, Ruined Town, the rest.
	["npc", F + "ArthurViejoSpriteFrames.tres"], ["npc", F + "JonhaMacartySpriteFrames.tres"], ["giant", F + "LeonBarmanGiganteSpriteFrames.tres"],
	["giant", F + "MujerCaraGiganteSpriteFrames.tres"],
	["npc", F + "NixAldricSpriteFrames.tres"], ["npc", F + "NixBramSpriteFrames.tres"], ["npc", F + "NixBrunoSpriteFrames.tres"],
	["npc", F + "NixCapulinoSpriteFrames.tres"], ["npc", F + "NixEddaSpriteFrames.tres"], ["npc", F + "NixGarrikSpriteFrames.tres"],
	["npc", F + "NixHalvarSpriteFrames.tres"], ["npc", F + "NixLeomaxHeridoSpriteFrames.tres"], ["npc", F + "NixLioSpriteFrames.tres"],
	["npc", F + "NixMorlokSpriteFrames.tres"], ["npc", F + "NixOstigadaSpriteFrames.tres"], ["npc", F + "NixPipSpriteFrames.tres"],
	["npc", F + "NixSelaSpriteFrames.tres"], ["npc", F + "NixTomoSpriteFrames.tres"], ["npc", F + "NixUlfarSpriteFrames.tres"],
	["npc", F + "RuinasGobernadorSpriteFrames.tres"], ["npc", F + "RuinasDetectiveSpriteFrames.tres"], ["npc", F + "RuinasViajeroPerdidoSpriteFrames.tres"],
	["npc", F + "RuinasHerreroSpriteFrames.tres"], ["npc", F + "RuinasMercaderSpriteFrames.tres"], ["npc", F + "RuinasHerbolariaSpriteFrames.tres"],
	["npc", F + "RuinasCocineroSpriteFrames.tres"], ["npc", F + "RuinasAldeano1SpriteFrames.tres"], ["npc", F + "RuinasAldeano2SpriteFrames.tres"],
	["npc", F + "RuinasAldeano3SpriteFrames.tres"], ["npc", F + "RuinasAldeano4SpriteFrames.tres"],
	# Ullatorp townsfolk (PixelLab) and their animals.
	["npc", F + "Ullatorp/UllatorpGuiaSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpGuardiaSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpSacerdoteSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpBardoSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpMalabaristaSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpVendedorSalchichasSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpFloristaSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpLenadorSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpCampesinoSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpBorrachoSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpPanaderaSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpAprendizSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpNinoSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpNinaSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpAncianaSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpDamaSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpMineroSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpLavanderaSpriteFrames.tres", 1.158],
	["npc", F + "Ullatorp/UllatorpPastorSpriteFrames.tres", 1.158], ["npc", F + "Ullatorp/UllatorpCazadorSpriteFrames.tres", 1.158],
	["animal", F + "Ullatorp/UllatorpPerroSpriteFrames.tres", 0.75], ["animal", F + "Ullatorp/UllatorpCabraSpriteFrames.tres", 0.8],
	["animal", F + "Ullatorp/UllatorpCerdoSpriteFrames.tres", 0.95], ["animal", F + "Ullatorp/UllatorpGallinaSpriteFrames.tres", 0.5],
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
			if path.ends_with("Mimico.tscn"): slot.Animation = "idle_transformed"
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
	# Two scenes on the same frames (an old and a new enemy built on one sheet) keep the first.
	var used := {}
	var dup_scenes := {}
	for it in items:
		if it.path.ends_with(".tscn"):
			var mine := []
			for n in it.slot.get_node("_Preview/Figure").get_children():
				if n is AnimatedSprite2D and n.sprite_frames != null:
					mine.append(n.sprite_frames.resource_path)
			if not mine.is_empty() and mine.all(func(p): return used.has(p)):
				print("DEDUP ", it.path.get_file(), " (in ", used[mine[0]].get_file(), ")")
				dup_scenes[it.path] = true
				continue
			for p in mine:
				used[p] = it.path
	var kept := []
	for it in items:
		if dup_scenes.has(it.path):
			continue
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
		# Showcase size: everyone gets the same multiplier on their in-game size, picked in
		# _write as the biggest that fits the whole roster (≤ FIGURE_MULT). Bosses and giants are
		# already huge and stay as they are.
		if it.group != "boss" and it.group != "giant":
			it.base_scale = it.slot.FigureScale
			it.base_w = it.slot.ContentRect.size.x
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
const FIGURE_MULT := 1.5      # biggest showcase size (x in-game); shrunk until the whole roster fits
const BOSS_MAX_H := 205.0
# Two ranks per step, like a group photo: the back rank stands in the gaps of the front one, a
# little further up the ledge's grass, darker and drawn behind. A step holds up to RANK_FILL x
# its width of figures; a step that fits in one rank stays a single rank.
const RANK_FILL := 1.8
const BACK_RAISE := 12.0
const BACK_TINT := "Color(0.68, 0.68, 0.75, 1)"

var lines := []
var ext := []
var ext_ids := {}
var names := {}

func _span(i: int) -> float:
	return (ROWS[i][2] - ROWS[i][1]) * BG_SCALE

func _width(it, mult: float) -> float:
	return (it.base_w * mult if it.has("base_w") else it.slot.ContentRect.size.x) + GAP

# Fills steps 1..5 in faction order at this size, spilling onto the ground's free width;
# returns the rows, or [] if the roster doesn't fit.
func _fill(queue: Array, mult: float, ground_free: float) -> Array:
	var rows := []
	for i in ROWS.size(): rows.append([])
	var last := ROWS.size() - 1
	var row_i := 1
	var used := 0.0
	for it in queue:
		var w := _width(it, mult)
		while used + w > (_span(row_i) * RANK_FILL if row_i < last else ground_free):
			if row_i == last:
				return []
			row_i += 1
			used = 0.0
		rows[row_i].append(it)
		used += w
	return rows

func _write(out_path: String):
	# Top step: the hero and allies. Steps 1-5: factions in this order, filled in sequence (a
	# faction may wrap onto the next step). Ground: bosses + giants, with whatever's left between.
	var order := ["elf", "dark", "soldier", "bandit", "undead", "human", "desert", "npc", "town", "monster", "wolf", "animal"]
	var by_group := {}
	for it in items:
		if not by_group.has(it.group): by_group[it.group] = []
		by_group[it.group].append(it)
	var queue := []
	for g in order:
		queue.append_array(by_group.get(g, []))
	var last := ROWS.size() - 1
	var big: Array = by_group.get("giant", []) + by_group.get("boss", [])
	var ground_free := _span(last)
	for it in big: ground_free -= it.slot.ContentRect.size.x + GAP
	# Biggest size at which everyone fits.
	var mult := FIGURE_MULT
	var rows := []
	while mult > 0.5:
		rows = _fill(queue, mult, ground_free)
		if not rows.is_empty(): break
		mult -= 0.02
	print("figure size: %.2fx in-game" % mult)
	for it in items:
		if it.has("base_scale"):
			it.slot.FigureScale = it.base_scale * mult
	rows[0].append_array(by_group.get("ally", []))
	var hero: Array = by_group.get("hero", [])
	# Ground row: giants at the far edges, bosses in the centre, the rest around them.
	var giants: Array = by_group.get("giant", [])
	var others: Array = rows[last]
	var half := others.size() / 2
	var arranged := []
	var ghalf := (giants.size() + 1) / 2
	arranged.append_array(giants.slice(0, ghalf))
	arranged.append_array(others.slice(0, half))
	arranged.append_array(by_group.get("boss", []))
	arranged.append_array(others.slice(half))
	arranged.append_array(giants.slice(ghalf))
	rows[last] = arranged

	ext.append('[ext_resource type="Script" path="res://scripts/world/RosterSlot.cs" id="slot"]')
	ext.append('[ext_resource type="Texture2D" uid="uid://p5pqv51madx3" path="res://assets/sprites/deco/Roster.png" id="bg"]')
	ext.append('[ext_resource type="PackedScene" path="res://scenes/player/Player.tscn" id="player"]')
	for i in ROWS.size():
		var row: Array = rows[i]
		var span := _span(i)
		var x0: float = ROWS[i][1] * BG_SCALE
		var y: float = ROWS[i][0] * BG_SCALE
		var total := 0.0
		for it in row: total += it.slot.ContentRect.size.x + GAP
		if i == 0: total += hero[0].slot.ContentRect.size.x + GAP
		var front := []
		var back := []
		if i == last or total - GAP <= span:
			front = row.duplicate()
		else:
			for k in row.size():
				if k % 2 == 0: front.append(row[k])
				else: back.append(row[k])
			# Taller figure of each pair goes to the back so nobody's hidden.
			for k in back.size():
				if front[k].slot.ContentRect.size.y > back[k].slot.ContentRect.size.y + 6:
					var t = front[k]
					front[k] = back[k]
					back[k] = t
		if i == 0:
			front.insert(front.size() / 2, hero[0])
		print("row %d: %d front + %d back, %d px of figures on a %d px step" % [i, front.size(), back.size(), total, span])
		if front.is_empty(): continue
		var front_name := "Row%d" % i
		lines.append('\n[node name="%s" type="Node2D" parent="Figures"]\nz_index = %d' % [front_name, 2 * i - 15])
		if back.is_empty():
			# Single rank: packed and centred (the ground spreads out over its whole width).
			var tw := 0.0
			for it in front: tw += it.slot.ContentRect.size.x + GAP
			tw -= GAP
			var extra: float = 0.0
			if tw < span and front.size() > 1:
				extra = (span - tw) / (front.size() - 1)
				if i != last: extra = min(40.0, extra)
			var x: float = x0 + max(0.0, (span - tw - extra * (front.size() - 1)) * 0.5)
			for it in front:
				var r: Rect2 = it.slot.ContentRect
				_emit(it, front_name, x + r.size.x * 0.5, y)
				x += r.size.x + GAP + extra
		else:
			# Two ranks: front evenly over the step, back centred in the front's gaps.
			var step := span / front.size()
			for k in front.size():
				_emit(front[k], front_name, x0 + (k + 0.5) * step, y)
			var back_name := "Row%dAtras" % i
			lines.append('\n[node name="%s" type="Node2D" parent="Figures"]\nz_index = %d\nmodulate = %s' % [back_name, 2 * i - 16, BACK_TINT])
			var bstep: float = span / (back.size() + 1) if back.size() >= front.size() else step
			for k in back.size():
				_emit(back[k], back_name, x0 + (k + 1) * bstep, y - BACK_RAISE)

	var out := []
	out.append('[gd_scene format=3]\n')
	out.append_array(ext)
	out.append('\n[sub_resource type="RectangleShape2D" id="ground_shape"]\nsize = Vector2(%.1f, 80)' % (1774.0 * BG_SCALE))
	out.append('\n[node name="Roster" type="Node2D"]')
	out.append('\n[node name="Background" type="Sprite2D" parent="."]\nz_index = -20\ntexture = ExtResource("bg")\ncentered = false\nscale = Vector2(%.1f, %.1f)' % [BG_SCALE, BG_SCALE])
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

# One figure node with its visible centre at cx and feet at y, facing the middle of the steps.
func _emit(it, parent: String, cx: float, y: float):
	var r: Rect2 = it.slot.ContentRect
	var px: float = cx - (r.position.x + r.size.x * 0.5)
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
		nm = "%s%d" % [base, n]
		n += 1
	names[nm] = true
	var mirror: bool = cx > 887.0 * BG_SCALE
	print("FIG %s %s %.0f %.0f %.0f %.0f" % [nm, parent, cx - r.size.x * 0.5, y + r.position.y, r.size.x, r.size.y])
	lines.append('\n[node name="%s" type="Node2D" parent="Figures/%s"]' % [nm, parent])
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
	if it.path.ends_with("MonsterMimicSpriteFrames.tres") or it.path.ends_with("Mimico.tscn"):
		lines.append('Animation = "idle_transformed"')
