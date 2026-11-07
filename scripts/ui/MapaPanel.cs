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
public partial class MapaPanel : Control
{
	[Signal] public delegate void ClosedEventHandler();

	[Export] public string RutaMapa = MapaMundo.RutaPorDefecto;
	[Export] public float Margen = 70f;
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
	private Transform2D _aPantalla = Transform2D.Identity;

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;

		_titulo = new Label { Name = "Titulo", HorizontalAlignment = HorizontalAlignment.Center, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
		_titulo.SetAnchorsPreset(LayoutPreset.TopWide);
		_titulo.OffsetTop = 18f;
		_titulo.AddThemeFontSizeOverride("font_size", 22);
		AddChild(_titulo);

		_aviso = new Label { Name = "Aviso", HorizontalAlignment = HorizontalAlignment.Center, Text = "UI_MAPA_SIN_DATOS" };
		_aviso.SetAnchorsPreset(LayoutPreset.Center);
		AddChild(_aviso);

		var pie = new Label { Name = "Pie", HorizontalAlignment = HorizontalAlignment.Center, Text = "UI_MAPA_VOLVER" };
		pie.SetAnchorsPreset(LayoutPreset.BottomWide);
		pie.OffsetTop = -36f;
		pie.Modulate = new Color(1f, 1f, 1f, 0.6f);
		AddChild(pie);
	}

	public void Open()
	{
		_mapa ??= ResourceLoader.Exists(RutaMapa) ? ResourceLoader.Load<MapaMundo>(RutaMapa) : null;
		Refrescar();
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

	public void HideNow() => Visible = false;

	private void Refrescar()
	{
		_visibles.Clear();
		string escena = GetTree().CurrentScene?.SceneFilePath ?? "";
		_actual = _mapa?.BuscarSala(escena);
		if (_actual == null)
		{
			_titulo.Text = TranslationServer.Translate("UI_MAPA_TITULO");
			_aviso.Visible = true;
			QueueRedraw();
			return;
		}
		_aviso.Visible = false;

		SaveManager save = SaveManager.Instance;
		foreach (SalaMapa sala in _mapa.Salas)
			if (sala.Region == _actual.Region && (sala == _actual || save == null || save.IsSceneVisited(sala.Escena)))
				_visibles.Add(sala);

		string claveRegion = "MAPA_REGION_" + _actual.Region.ToUpperInvariant();
		string nombre = TranslationServer.Translate(claveRegion);
		_titulo.Text = nombre == claveRegion ? _actual.Region : nombre;

		_jugador = GetTree().GetFirstNodeInGroup("player") is Node2D p ? _actual.Origen + p.GlobalPosition : _actual.Rect.GetCenter();
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			QueueRedraw();
	}

	public override void _Draw()
	{
		if (_visibles.Count == 0)
			return;

		Rect2 total = _visibles[0].Rect;
		foreach (SalaMapa s in _visibles)
			total = total.Merge(s.Rect);
		Vector2 area = Size - new Vector2(Margen * 2f, Margen * 2f + 40f);
		float escala = Mathf.Min(area.X / Mathf.Max(1f, total.Size.X), area.Y / Mathf.Max(1f, total.Size.Y));
		escala = Mathf.Min(escala, 0.12f); // a lone small room shouldn't fill the whole screen
		Vector2 centro = Size / 2f + new Vector2(0f, 10f);
		_aPantalla = new Transform2D(0f, Vector2.One * escala, 0f, centro - total.GetCenter() * escala);

		foreach (SalaMapa s in _visibles)
		{
			Rect2 r = APantalla(s.Rect);
			DrawRect(r, s == _actual ? ColorSalaActual : ColorSala);
			DrawRect(r, ColorBorde, false, 2f);
		}
		foreach (SalaMapa s in _visibles)
		{
			foreach (Vector2 h in s.Hogueras)
				DrawCircle(_aPantalla * h, 5f, ColorHoguera);
			foreach (Vector2 d in s.Puertas)
				DrawRect(new Rect2(_aPantalla * d - new Vector2(4f, 4f), new Vector2(8f, 8f)), ColorPuerta);
		}

		Vector2 j = _aPantalla * _jugador;
		float pulso = 1f + 0.25f * Mathf.Sin(Time.GetTicksMsec() / 180f);
		DrawCircle(j, 7f * pulso, new Color(ColorJugador, 0.35f));
		DrawCircle(j, 4f, ColorJugador);
	}

	public override void _Process(double delta)
	{
		if (Visible && _actual != null)
			QueueRedraw(); // pulsing player marker
	}

	private Rect2 APantalla(Rect2 r) => new(_aPantalla * r.Position, r.Size * _aPantalla.Scale);
}
