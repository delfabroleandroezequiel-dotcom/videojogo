using Godot;
using Godot.Collections;

namespace Metroidvania.Mapa;

// The generated world map (resources/mapa/MapaMundo.tres). Rebuilt by tools/generar_mapa_mundo.gd
// whenever maps change; the game only reads it. Each region is laid out on its own (a cave behind
// a door is a separate region, like Afterimage's per-area maps).
[GlobalClass]
public partial class MapaMundo : Resource
{
	public const string RutaPorDefecto = "res://resources/mapa/MapaMundo.tres";

	[Export] public Array<SalaMapa> Salas = new();

	public SalaMapa BuscarSala(string escena)
	{
		foreach (SalaMapa sala in Salas)
			if (sala.Escena == escena)
				return sala;
		return null;
	}
}
