extends SceneTree

# Regenerates resources/mapa/MapaMundo.tres from the map scenes (see GeneradorMapaMundo.cs).
# Run from the project root:
#   godot --headless --path . --script res://tools/generar_mapa_mundo.gd
# The report it prints lists layout conflicts, overlaps, broken targets and unreachable rooms.

const CONFIG := "res://resources/mapa/MapaMundoConfig.tres"
const SALIDA := "res://resources/mapa/MapaMundo.tres"

func _initialize() -> void:
	var generador = load("res://scripts/mapa/GeneradorMapaMundo.cs").new()
	print(generador.Generar(CONFIG, SALIDA))
	quit()
