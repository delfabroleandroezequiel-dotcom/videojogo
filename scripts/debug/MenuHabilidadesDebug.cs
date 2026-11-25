using Godot;
using Metroidvania.Player;

namespace Metroidvania.Debug;

// Dev-only abilities menu: F2 opens a list of checkboxes to give or take any ability on the spot
// (and in the save), so zones can be tested with the kit the player would really have.
// Built in code and added by LevelBootstrap only in debug builds — not an autoload, and it never
// exists in an exported release. Pauses the game while open.
public partial class MenuHabilidadesDebug : CanvasLayer
{
	private static readonly (string Id, string Nombre)[] Habilidades =
	{
		(PlayerAbilities.DoubleJump, "Doble salto"),
		(PlayerAbilities.Dash, "Dash"),
		(PlayerAbilities.ChargedAttack, "Ataque cargado"),
		(PlayerAbilities.PoundAttack, "Golpe en picada"),
		(PlayerAbilities.WallClimb, "Trepar paredes"),
		(PlayerAbilities.LedgeGrab, "Colgarse de cornisas"),
		(PlayerAbilities.RopeSwing, "Cuerdas (guantes)"),
		(PlayerAbilities.Swim, "Nadar"),
		(PlayerAbilities.Sprint, "Correr"),
		(PlayerAbilities.Roll, "Rodar"),
		(PlayerAbilities.Block, "Parry"),
		(PlayerAbilities.UpAttack, "Ataque hacia arriba"),
		(PlayerAbilities.RunThrust, "Estocada corriendo"),
		(PlayerAbilities.ComboFinisher, "Combo: 3er golpe"),
		(PlayerAbilities.ComboFourth, "Combo: 4to golpe"),
		(PlayerAbilities.FlyingSword, "Espada voladora"),
		(PlayerAbilities.Spell, "Sombra (hechizo 1)"),
		(PlayerAbilities.Spell2, "Hechizo 2"),
		(PlayerAbilities.Torch, "Antorcha"),
	};

	private Control _panel;
	private VBoxContainer _lista;
	private PlayerAbilities _abilities;
	private bool _wasPaused;

	public override void _Ready()
	{
		Layer = 110;
		ProcessMode = ProcessModeEnum.Always;
		_panel = new PanelContainer
		{
			Visible = false,
			Position = new Vector2(24f, 24f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		var box = new VBoxContainer();
		box.AddChild(new Label { Text = "Habilidades (F2 para cerrar)" });
		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(280f, 420f) };
		_lista = new VBoxContainer();
		scroll.AddChild(_lista);
		box.AddChild(scroll);
		_panel.AddChild(box);
		AddChild(_panel);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2 })
			return;
		GetViewport().SetInputAsHandled();
		if (_panel.Visible)
			Cerrar();
		else
			Abrir();
	}

	private void Abrir()
	{
		_abilities = (GetTree().GetFirstNodeInGroup("player") as Node)?.GetNodeOrNull<PlayerAbilities>("Abilities");
		if (_abilities is null)
			return;
		foreach (Node child in _lista.GetChildren())
			child.QueueFree();
		foreach ((string id, string nombre) in Habilidades)
		{
			var check = new CheckBox { Text = nombre, ButtonPressed = _abilities.Has(id) };
			string capturedId = id;
			check.Toggled += on =>
			{
				if (on)
					_abilities.Unlock(capturedId);
				else
					_abilities.Lock(capturedId);
			};
			_lista.AddChild(check);
		}
		_panel.Visible = true;
		_wasPaused = GetTree().Paused;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void Cerrar()
	{
		_panel.Visible = false;
		GetTree().Paused = _wasPaused;
	}
}
