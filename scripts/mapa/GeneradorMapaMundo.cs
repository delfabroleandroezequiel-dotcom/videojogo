using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Metroidvania.Mapa;

// Builds MapaMundo.tres from the map scenes themselves, so the map follows the level design instead
// of being drawn by hand. Run it headless (tools/generar_mapa_mundo.gd) whenever maps change.
//
// It reads each scene's SceneState (no instancing, so no C# _Ready runs and no scene-count crash):
// the room's size comes from LevelBootstrap's CameraLimit* (falling back to the geometry), and its
// LevelTransitions say where it leads. Walking those links from MapaMundoConfig.Raices, each room
// is placed against the edge of the room it was reached from, lined up so the exit meets the
// target's spawn point. "Interact" exits (doors, cave mouths) start a separate region instead.
// The report lists what didn't fit: rooms reached at two different spots (a loop that doesn't
// close), overlapping rooms, broken targets, and rooms nothing reaches.
public partial class GeneradorMapaMundo : RefCounted
{
	private const float Infinito = 1_000_000f;

	private sealed class Salida
	{
		public Vector2 Pos;
		public string Destino;
		public Vector2 Spawn;
		public bool Interactuar;
	}

	private sealed class Sala
	{
		public string Escena;
		public Rect2 Local;
		public readonly List<Salida> Salidas = new();
		public readonly List<Vector2> Hogueras = new();
	}

	private readonly Dictionary<string, Sala> _salas = new();
	private readonly StringBuilder _reporte = new();

	public string Generar(string rutaConfig, string rutaSalida)
	{
		var config = ResourceLoader.Load<MapaMundoConfig>(rutaConfig) ?? new MapaMundoConfig();
		var mapa = new MapaMundo();
		var colocadas = new Dictionary<string, SalaMapa>();
		var pendientesRegion = new Queue<string>(config.Raices);

		while (pendientesRegion.Count > 0)
		{
			string raiz = pendientesRegion.Dequeue();
			if (colocadas.ContainsKey(raiz) || Excluida(config, raiz) || Leer(raiz) is not Sala salaRaiz)
				continue;
			string region = RegionDe(raiz);
			var cola = new Queue<string>();
			Colocar(mapa, colocadas, config, salaRaiz, region, Ajuste(config, raiz));
			cola.Enqueue(raiz);

			while (cola.Count > 0)
			{
				Sala a = _salas[cola.Dequeue()];
				SalaMapa am = colocadas[a.Escena];
				foreach (Salida salida in a.Salidas)
				{
					string destino = salida.Destino;
					if (string.IsNullOrEmpty(destino) || destino == a.Escena || Excluida(config, destino))
						continue;
					if (Leer(destino) is not Sala b)
					{
						_reporte.AppendLine($"ROTA      {Corto(a.Escena)} -> {destino} (no existe)");
						continue;
					}
					// A door, or a walk into another folder (each folder under Mapas/ is its own zone,
					// designed on its own grid): mark it and lay that side out as a separate region.
					if (salida.Interactuar || Carpeta(destino) != Carpeta(a.Escena))
					{
						am.Puertas = am.Puertas.Append(am.Origen + salida.Pos).ToArray();
						if (!colocadas.ContainsKey(destino))
							pendientesRegion.Enqueue(destino);
						continue;
					}

					Vector2 origenB = PosicionContigua(am, salida, b) + Ajuste(config, destino);
					if (colocadas.TryGetValue(destino, out SalaMapa existente))
					{
						if (existente.Region == region && existente.Origen.DistanceTo(origenB) > config.ToleranciaConflicto)
							_reporte.AppendLine($"CONFLICTO {Corto(a.Escena)} -> {Corto(destino)}: queda a "
								+ $"{(origenB - existente.Origen).Round()} px de donde la puso otra conexión");
						continue;
					}
					Colocar(mapa, colocadas, config, b, region, origenB);
					cola.Enqueue(destino);
				}
			}
		}

		ReportarSolapes(mapa);
		ReportarSinAlcanzar(config, colocadas);

		DirAccess.MakeDirRecursiveAbsolute(rutaSalida.GetBaseDir());
		Error err = ResourceSaver.Save(mapa, rutaSalida);
		var regiones = mapa.Salas.Select(s => s.Region).Distinct().Count();
		string resumen = $"MapaMundo: {mapa.Salas.Count} salas en {regiones} regiones -> {rutaSalida} ({err})\n"
			+ "Regiones: " + string.Join(", ", mapa.Salas.GroupBy(s => s.Region).Select(g => $"{g.Key} ({g.Count()})")) + "\n";
		return resumen + (_reporte.Length == 0 ? "Sin problemas." : _reporte.ToString());
	}

	private static void Colocar(MapaMundo mapa, Dictionary<string, SalaMapa> colocadas, MapaMundoConfig config,
		Sala sala, string region, Vector2 origen)
	{
		var sm = new SalaMapa
		{
			Escena = sala.Escena,
			Region = region,
			Origen = origen,
			Rect = new Rect2(sala.Local.Position + origen, sala.Local.Size),
			Hogueras = sala.Hogueras.Select(h => h + origen).ToArray(),
		};
		mapa.Salas.Add(sm);
		colocadas[sala.Escena] = sm;
	}

	// Puts B against whichever edge of A the exit is closest to, sliding it along that edge so the
	// exit point lines up with B's spawn point.
	private static Vector2 PosicionContigua(SalaMapa a, Salida salida, Sala b)
	{
		Rect2 ra = a.Rect;
		Vector2 p = a.Origen + salida.Pos;
		float izq = Mathf.Abs(p.X - ra.Position.X), der = Mathf.Abs(ra.End.X - p.X);
		float arr = Mathf.Abs(p.Y - ra.Position.Y), aba = Mathf.Abs(ra.End.Y - p.Y);
		float min = Mathf.Min(Mathf.Min(izq, der), Mathf.Min(arr, aba));
		Rect2 rb = b.Local;
		if (min == der)
			return new Vector2(ra.End.X - rb.Position.X, p.Y - salida.Spawn.Y);
		if (min == izq)
			return new Vector2(ra.Position.X - rb.End.X, p.Y - salida.Spawn.Y);
		if (min == aba)
			return new Vector2(p.X - salida.Spawn.X, ra.End.Y - rb.Position.Y);
		return new Vector2(p.X - salida.Spawn.X, ra.Position.Y - rb.End.Y);
	}

	private void ReportarSolapes(MapaMundo mapa)
	{
		var salas = mapa.Salas.ToList();
		for (int i = 0; i < salas.Count; i++)
			for (int j = i + 1; j < salas.Count; j++)
			{
				if (salas[i].Region != salas[j].Region)
					continue;
				Rect2 inter = salas[i].Rect.Intersection(salas[j].Rect);
				if (inter.Size.X > 32f && inter.Size.Y > 32f)
					_reporte.AppendLine($"SOLAPE    {Corto(salas[i].Escena)} y {Corto(salas[j].Escena)} "
						+ $"se pisan {inter.Size.Round()} px");
			}
	}

	// Grouped per folder: a whole unconnected zone (an unlinked greybox region) is one line.
	private void ReportarSinAlcanzar(MapaMundoConfig config, Dictionary<string, SalaMapa> colocadas)
	{
		var porCarpeta = new SortedDictionary<string, List<string>>();
		foreach (string ruta in TodasLasEscenas("res://scenes/world/Mapas"))
		{
			if (colocadas.ContainsKey(ruta) || Excluida(config, ruta))
				continue;
			if (Leer(ruta) is Sala s && s.Salidas.Count > 0)
			{
				string carpeta = Carpeta(ruta) == "" ? "Mapas/" : Carpeta(ruta);
				if (!porCarpeta.TryGetValue(carpeta, out List<string> lista))
					porCarpeta[carpeta] = lista = new List<string>();
				lista.Add(System.IO.Path.GetFileNameWithoutExtension(ruta));
			}
		}
		foreach ((string carpeta, List<string> lista) in porCarpeta)
			_reporte.AppendLine($"SUELTA    {carpeta}: {lista.Count} sala(s) sin conexión desde las raíces"
				+ (lista.Count <= 20 ? $" ({string.Join(", ", lista)})" : ""));
	}

	// "Cueva1/" for Mapas/Cueva1/X.tscn, "" for scenes directly under Mapas/.
	private static string Carpeta(string ruta)
	{
		if (!ruta.StartsWith("res://scenes/world/Mapas/"))
			return "";
		string rel = Corto(ruta);
		int barra = rel.LastIndexOf('/');
		return barra < 0 ? "" : rel[..(barra + 1)];
	}

	// Region id: the zone's folder (Cueva1, Quebrada...), or the root scene's name for scenes
	// directly under Mapas/ (Nix, a house interior...).
	private static string RegionDe(string ruta)
	{
		string carpeta = Carpeta(ruta).TrimEnd('/');
		return carpeta != "" ? carpeta : System.IO.Path.GetFileNameWithoutExtension(ruta);
	}

	private static IEnumerable<string> TodasLasEscenas(string dir)
	{
		using var d = DirAccess.Open(dir);
		if (d is null)
			yield break;
		foreach (string f in d.GetFiles())
			if (f.EndsWith(".tscn"))
				yield return dir + "/" + f;
		foreach (string sub in d.GetDirectories())
			foreach (string f in TodasLasEscenas(dir + "/" + sub))
				yield return f;
	}

	private static bool Excluida(MapaMundoConfig config, string ruta) => config.Excluir.Any(ruta.Contains);

	private static Vector2 Ajuste(MapaMundoConfig config, string ruta) =>
		config.AjustesPosicion.TryGetValue(ruta, out Vector2 v) ? v : Vector2.Zero;

	private static string Corto(string ruta) => ruta.Replace("res://scenes/world/Mapas/", "");

	// ---- Reading one scene ----

	private Sala Leer(string ruta)
	{
		if (ruta.StartsWith("uid://"))
			ruta = ResourceUid.GetIdPath(ResourceUid.TextToId(ruta));
		if (_salas.TryGetValue(ruta, out Sala cache))
			return cache;
		if (!ResourceLoader.Exists(ruta) || ResourceLoader.Load<PackedScene>(ruta) is not PackedScene escena)
			return null;

		SceneState st = escena.GetState();
		var sala = new Sala { Escena = ruta };
		var globales = new Dictionary<string, Transform2D>();
		var puntos = new List<Vector2>();
		var limites = new Dictionary<string, float>();

		for (int i = 0; i < st.GetNodeCount(); i++)
		{
			var props = new Dictionary<string, Variant>();
			for (int j = 0; j < st.GetNodePropertyCount(i); j++)
				props[st.GetNodePropertyName(i, j)] = st.GetNodePropertyValue(i, j);

			string camino = st.GetNodePath(i).ToString();
			string padre = st.GetNodePath(i, true).ToString();
			Transform2D local = new(
				props.TryGetValue("rotation", out Variant r) ? r.AsSingle() : 0f,
				props.TryGetValue("scale", out Variant sc) ? sc.AsVector2() : Vector2.One,
				0f,
				props.TryGetValue("position", out Variant p) ? p.AsVector2() : Vector2.Zero);
			Transform2D global = i > 0 && globales.TryGetValue(padre, out Transform2D tp) ? tp * local : local;
			globales[camino] = global;

			if (i == 0)
			{
				foreach (string lado in new[] { "Left", "Right", "Top", "Bottom" })
					if (props.TryGetValue("CameraLimit" + lado, out Variant v) && Mathf.Abs(v.AsSingle()) < Infinito)
						limites[lado] = v.AsSingle();
				continue;
			}

			string instancia = st.GetNodeInstance(i)?.ResourcePath ?? "";
			if (props.TryGetValue("TargetScenePath", out Variant destino))
			{
				var salida = new Salida
				{
					Pos = global.Origin,
					Destino = destino.AsString(),
					Spawn = props.TryGetValue("TargetSpawnPosition", out Variant sp) ? sp.AsVector2() : Vector2.Zero,
					Interactuar = props.TryGetValue("RequireInteract", out Variant ri) && ri.AsBool(),
				};
				if (salida.Destino.StartsWith("uid://"))
					salida.Destino = ResourceUid.GetIdPath(ResourceUid.TextToId(salida.Destino));
				sala.Salidas.Add(salida);
				puntos.Add(global.Origin);
			}
			else if (instancia.EndsWith("/SavePoint.tscn"))
			{
				sala.Hogueras.Add(global.Origin);
				puntos.Add(global.Origin);
			}
			else if (props.TryGetValue("shape", out Variant forma) && forma.As<Shape2D>() is RectangleShape2D rect)
			{
				Vector2 h = rect.Size / 2f;
				foreach (Vector2 esquina in new[] { -h, h, new Vector2(h.X, -h.Y), new Vector2(-h.X, h.Y) })
					puntos.Add(global * esquina);
			}
			else if (props.TryGetValue("polygon", out Variant poli) && poli.VariantType == Variant.Type.PackedVector2Array)
			{
				foreach (Vector2 v in poli.AsVector2Array())
					puntos.Add(global * v);
			}
			else if (!string.IsNullOrEmpty(instancia))
			{
				puntos.Add(global.Origin);
			}
		}

		Rect2 geo = puntos.Count == 0 ? new Rect2(0, 0, 1280, 720) : new Rect2(puntos[0], Vector2.Zero);
		foreach (Vector2 pt in puntos)
			geo = geo.Expand(pt);
		float izq = limites.GetValueOrDefault("Left", geo.Position.X);
		float der = limites.GetValueOrDefault("Right", geo.End.X);
		float arr = limites.GetValueOrDefault("Top", geo.Position.Y);
		float aba = limites.GetValueOrDefault("Bottom", geo.End.Y);
		sala.Local = new Rect2(izq, arr, Mathf.Max(64f, der - izq), Mathf.Max(64f, aba - arr));

		_salas[ruta] = sala;
		return sala;
	}
}
