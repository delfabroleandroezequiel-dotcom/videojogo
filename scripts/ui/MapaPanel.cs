using System.Collections.Generic;
using System.Linq;
using Godot;
using Metroidvania.Mapa;
using Metroidvania.Save;

namespace Metroidvania.UI;

// World map view inside the pause menu (built by PauseMenu, not an autoload). Shows the region the
// player is in: every visited room of it drawn whole (Afterimage-style, no fog inside a room), the
// player's marker, hogueras and doors. Rooms come from the generated MapaMundo.tres; which ones show
// comes from SaveManager.GetVisitedScenes.
//
// Opens fitted to the whole visited region. Zoom: mouse wheel (towards the cursor), +/- keys, or the
// pad triggers (towards the screen centre). Pan: drag with the mouse, or move_* (WASD/arrows/stick).
public partial class MapaPanel : Control
{
	[Signal] public delegate void ClosedEventHandler();

	[Export] public string RutaMapa = MapaMundo.RutaPorDefecto;
	[Export] public float Margen = 70f;
	// Zoom range relative to "whole region fits the screen" (1).
	[Export] public float ZoomMinimo = 0.5f;
	[Export] public float ZoomMaximo = 60f;
	// Per wheel notch / per second held (keys, triggers).
	[Export] public float PasoZoomRueda = 1.15f;
	[Export] public float ZoomPorSegundo = 2.5f;
	// Screen pixels per second when panning with move_*.
	[Export] public float VelocidadPaneo = 600f;
	[Export] public Color ColorSala = new(0.23f, 0.27f, 0.32f, 0.95f);
	[Export] public Color ColorSalaActual = new(0.33f, 0.42f, 0.5f, 1f);
	[Export] public Color ColorBorde = new(0.75f, 0.82f, 0.88f, 0.9f);
	[Export] public Color ColorJugador = new(1f, 0.85f, 0.35f);
	[Export] public Color ColorHoguera = new(0.45f, 1f, 0.6f);
	[Export] public Color ColorPuerta = new(0.95f, 0.6f, 0.3f);

	private MapaMundo _mapa;
	private Label _titulo;
	private Label _aviso;
	private readonly List<SalaMapa> _visibles = new();
	private SalaMapa _actual;
	private Vector2 _jugador;
	private Rect2 _total;
	private Rect2 _regionActual;
	private readonly Dictionary<string, Rect2> _regiones = new();
	// View state, in region (world) coordinates: the point shown at the screen centre, and the zoom
	// on top of the fit-to-screen scale.
	private Vector2 _centroVista;
	private float _zoom = 1f;
	private bool _arrastrando;

	public override void _Ready()
	{
		// Offsets too, not just anchors: built hidden, a bare anchor preset left this at 0x0 and
		// everything drew around the top-left corner.
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		ClipContents = true;
		Visible = false;

		_titulo = new Label { Name = "Titulo", HorizontalAlignment = HorizontalAlignment.Center, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
		_titulo.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
		_titulo.OffsetTop = 18f;
		_titulo.OffsetBottom = 50f;
		_titulo.AddThemeFontSizeOverride("font_size", 22);
		_titulo.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_titulo);

		_aviso = new Label { Name = "Aviso", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Text = "UI_MAPA_SIN_DATOS" };
		_aviso.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_aviso.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_aviso);

		var pie = new Label { Name = "Pie", HorizontalAlignment = HorizontalAlignment.Center, Text = "UI_MAPA_VOLVER" };
		pie.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
		pie.OffsetTop = -40f;
		pie.OffsetBottom = -12f;
		pie.Modulate = new Color(1f, 1f, 1f, 0.6f);
		pie.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(pie);
	}

	public void Open()
	{
		_mapa ??= ResourceLoader.Exists(RutaMapa) ? ResourceLoader.Load<MapaMundo>(RutaMapa) : null;
		Refrescar();
		// Opens framed on the region you're in; zoom out (down to the whole visited world at 1x
		// and below) to see where it sits among the others.
		_zoom = Mathf.Clamp(EscalaPara(_regionActual) / Mathf.Max(0.000001f, EscalaPara(_total)), ZoomMinimo, ZoomMaximo);
		_centroVista = _regionActual.GetCenter();
		Visible = true;
	}

	// Escape/B: closes the map if it's open (true = handled).
	public bool Back()
	{
		if (!Visible)
			return false;
		HideNow();
		EmitSignal(SignalName.Closed);
		return true;
	}

	public void HideNow()
	{
		Visible = false;
		_arrastrando = false;
	}

	private void Refrescar()
	{
		_visibles.Clear();
		string escena = GetTree().CurrentScene?.SceneFilePath ?? "";
		_actual = _mapa?.BuscarSala(escena);
		if (_actual == null)
		{
			_titulo.Text = TranslationServer.Translate("UI_MAPA_TITULO");
			_aviso.Visible = true;
			_total = new Rect2();
			QueueRedraw();
			return;
		}
		_aviso.Visible = false;

		// The whole world you've walked: every visited room of every region (interiors aside).
		SaveManager save = SaveManager.Instance;
		foreach (SalaMapa sala in _mapa.Salas)
			if (!sala.Interior && (sala == _actual || save == null || save.IsSceneVisited(sala.Escena)))
				_visibles.Add(sala);

		_jugador = GetTree().GetFirstNodeInGroup("player") is Node2D p ? _actual.Origen + p.GlobalPosition : _actual.Rect.GetCenter();
		if (_actual.Interior)
			_jugador = _actual.PuertaMundo;

		_total = new Rect2(_jugador, Vector2.Zero);
		_regionActual = new Rect2(_jugador, Vector2.Zero);
		_regiones.Clear();
		foreach (SalaMapa s in _visibles)
		{
			_total = _total.Merge(s.Rect);
			_regiones[s.Region] = _regiones.TryGetValue(s.Region, out Rect2 r) ? r.Merge(s.Rect) : s.Rect;
		}
		// Inside a house, "your region" is the one around its door.
		SalaMapa fuera = _actual.Interior ? _visibles.FirstOrDefault(s => s.Rect.HasPoint(_jugador)) : _actual;
		if (fuera != null && _regiones.TryGetValue(fuera.Region, out Rect2 propia))
			_regionActual = propia;

		_titulo.Text = NombreRegion(fuera?.Region ?? _actual.Region);
		QueueRedraw();
	}

	// ---- View transform ----

	// Scale at zoom 1: the whole visited region fits the area left between title and footer. Capped
	// so a lone small room doesn't fill the screen.
	private float EscalaPara(Rect2 r)
	{
		Vector2 area = Size - new Vector2(Margen * 2f, Margen * 2f + 40f);
		float s = Mathf.Min(area.X / Mathf.Max(1f, r.Size.X), area.Y / Mathf.Max(1f, r.Size.Y));
		return Mathf.Min(s, 0.12f);
	}

	private float Escala => EscalaPara(_total) * _zoom;

	private static string NombreRegion(string region)
	{
		string clave = "MAPA_REGION_" + region.ToUpperInvariant();
		string nombre = TranslationServer.Translate(clave);
		return nombre == clave ? region : nombre;
	}

	// A steady, distinct tint per region (hue from its name) so zones read apart on one map.
	private Color TonoRegion(string region, Color baseColor)
	{
		float hue = (Mathf.PosMod(region.GetHashCode(), 997) / 997f);
		return baseColor.Lerp(Color.FromHsv(hue, 0.45f, baseColor.V + 0.15f, baseColor.A), 0.35f);
	}
	private Vector2 CentroPantalla => Size / 2f + new Vector2(0f, 10f);
	private Vector2 APantalla(Vector2 mundo) => CentroPantalla + (mundo - _centroVista) * Escala;
	private Vector2 AMundo(Vector2 pantalla) => _centroVista + (pantalla - CentroPantalla) / Escala;
	private Rect2 APantalla(Rect2 r) => new(APantalla(r.Position), r.Size * Escala);

	// Zooms keeping the world point under `pantalla` fixed on screen.
	private void Zoom(float factor, Vector2 pantalla)
	{
		Vector2 antes = AMundo(pantalla);
		_zoom = Mathf.Clamp(_zoom * factor, ZoomMinimo, ZoomMaximo);
		_centroVista += antes - AMundo(pantalla);
		LimitarCentro();
		QueueRedraw();
	}

	// The view centre can't leave the region, so you can't pan off into nothing.
	private void LimitarCentro()
	{
		_centroVista = new Vector2(
			Mathf.Clamp(_centroVista.X, _total.Position.X, _total.End.X),
			Mathf.Clamp(_centroVista.Y, _total.Position.Y, _total.End.Y));
	}

	// ---- Input ----

	public override void _GuiInput(InputEvent @event)
	{
		if (_actual == null)
			return;
		if (@event is InputEventMouseButton mb)
		{
			if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp)
				Zoom(PasoZoomRueda, mb.Position);
			else if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown)
				Zoom(1f / PasoZoomRueda, mb.Position);
			else if (mb.ButtonIndex == MouseButton.Left)
				_arrastrando = mb.Pressed;
			else
				return;
			AcceptEvent();
		}
		else if (@event is InputEventMouseMotion mm && _arrastrando)
		{
			_centroVista -= mm.Relative / Escala;
			LimitarCentro();
			QueueRedraw();
			AcceptEvent();
		}
	}

	public override void _Process(double delta)
	{
		if (!Visible || _actual == null)
			return;
		float dt = (float)delta;

		// Held zoom: +/- (main row or keypad) and the pad triggers, towards the screen centre.
		float zoomEje = 0f;
		if (Input.IsKeyPressed(Key.Equal) || Input.IsKeyPressed(Key.Plus) || Input.IsKeyPressed(Key.KpAdd))
			zoomEje += 1f;
		if (Input.IsKeyPressed(Key.Minus) || Input.IsKeyPressed(Key.KpSubtract))
			zoomEje -= 1f;
		foreach (int pad in Input.GetConnectedJoypads())
			zoomEje += Input.GetJoyAxis(pad, JoyAxis.TriggerRight) - Input.GetJoyAxis(pad, JoyAxis.TriggerLeft);
		if (Mathf.Abs(zoomEje) > 0.15f)
			Zoom(Mathf.Pow(ZoomPorSegundo, Mathf.Clamp(zoomEje, -1f, 1f) * dt), CentroPantalla);

		Vector2 paneo = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		if (paneo != Vector2.Zero)
		{
			_centroVista += paneo * VelocidadPaneo * dt / Escala;
			LimitarCentro();
		}

		QueueRedraw(); // pulsing player marker
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	// ---- Drawing ----

	public override void _Draw()
	{
		if (_visibles.Count == 0)
			return;

		float escala = Escala;
		foreach (SalaMapa s in _visibles)
		{
			Rect2 r = APantalla(s.Rect);
			DrawRect(r, s == _actual ? ColorSalaActual : TonoRegion(s.Region, ColorSala));
			DrawRect(r, ColorBorde, false, Mathf.Clamp(escala * 15f, 1f, 2f));
		}

		// Region names over their rooms (only once the region is big enough on screen to label).
		Font fuente = ThemeDB.FallbackFont;
		foreach ((string region, Rect2 rr) in _regiones)
		{
			Rect2 r = APantalla(rr);
			if (r.Size.X < 60f)
				continue;
			string nombre = NombreRegion(region);
			Vector2 medida = fuente.GetStringSize(nombre, HorizontalAlignment.Left, -1, 14);
			Vector2 pos = new(r.GetCenter().X - medida.X / 2f, r.Position.Y - 8f);
			DrawString(fuente, pos, nombre, HorizontalAlignment.Left, -1, 14, new Color(1f, 1f, 1f, 0.75f));
		}

		// Icons grow a little with zoom, but stay readable when zoomed out.
		float icono = Mathf.Clamp(escala * 40f, 4f, 9f);
		foreach (SalaMapa s in _visibles)
		{
			foreach (Vector2 h in s.Hogueras)
				DrawCircle(APantalla(h), icono, ColorHoguera);
			foreach (Vector2 d in s.Puertas)
				DrawRect(new Rect2(APantalla(d) - Vector2.One * icono * 0.8f, Vector2.One * icono * 1.6f), ColorPuerta);
		}

		Vector2 j = APantalla(_jugador);
		float pulso = 1f + 0.25f * Mathf.Sin(Time.GetTicksMsec() / 180f);
		DrawCircle(j, icono * 1.6f * pulso, new Color(ColorJugador, 0.35f));
		DrawCircle(j, icono, ColorJugador);
	}
}
