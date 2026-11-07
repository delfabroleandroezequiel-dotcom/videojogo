using Godot;
using Godot.Collections;

namespace Metroidvania.Mapa;

// Hand-edited input for GeneradorMapaMundo (resources/mapa/MapaMundoConfig.tres). Everything here
// survives a regeneration; the generated MapaMundo.tres does not.
[GlobalClass]
public partial class MapaMundoConfig : Resource
{
	// Where the crawl starts (the first one is the overworld). Scenes not reachable from any of
	// these through LevelTransitions stay off the map (test rooms, arenas, cinematics).
	[Export] public Array<string> Raices = new() { "res://scenes/world/Mapas/Nix.tscn" };
	// Scene paths containing any of these never enter the map, even when something links to them.
	[Export] public Array<string> Excluir = new() { "/Prueba", "/Llamas/", "Test", "MapaVideo" };
	// Manual nudge per scene path, in pixels, applied after the automatic layout (and carried to
	// everything placed from that room onwards).
	[Export] public Dictionary<string, Vector2> AjustesPosicion = new();
	// Two placements of the same room further apart than this are reported as a layout conflict.
	[Export] public float ToleranciaConflicto = 96f;
}
