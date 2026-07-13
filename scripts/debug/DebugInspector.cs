using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Debugging;

// Dev-only live balance panel: toggle with F3, lists every enemy currently in the scene with
// its stats editable on the spot, and a button to write the tweaked values back to the shared
// EnemyProfile .tres so every instance of that enemy type keeps the change after a restart.
// Self-removes outside debug builds (OS.HasFeature("debug") is false in an exported release
// build), so there's no manual "strip this before shipping" step to forget.
public partial class DebugInspector : CanvasLayer
{
	private Panel _panel;
	private VBoxContainer _list;

	public override void _Ready()
	{
		if (!OS.HasFeature("debug"))
		{
			QueueFree();
			return;
		}

		ProcessMode = ProcessModeEnum.Always;

		_panel = new Panel
		{
			Visible = false,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 0,
			OffsetTop = 0,
			OffsetRight = 0,
			OffsetBottom = 0,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		AddChild(_panel);

		var scroll = new ScrollContainer
		{
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 20,
			OffsetTop = 20,
			OffsetRight = -20,
			OffsetBottom = -20,
		};
		_panel.AddChild(scroll);

		_list = new VBoxContainer();
		_list.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_list);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("debug_inspector"))
			return;

		_panel.Visible = !_panel.Visible;
		if (_panel.Visible)
			Refresh();

		GetViewport().SetInputAsHandled();
	}

	private void Refresh()
	{
		foreach (Node child in _list.GetChildren())
			child.QueueFree();

		string mapName = GetTree().CurrentScene?.SceneFilePath ?? "?";

		var header = new Label { Text = $"Mapa actual: {mapName}" };
		header.AddThemeFontSizeOverride("font_size", 18);
		_list.AddChild(header);

		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is Enemy enemy && IsInstanceValid(enemy))
				_list.AddChild(BuildEnemyRow(enemy));
		}
	}

	private Control BuildEnemyRow(Enemy enemy)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);

		string sceneName = enemy.SceneFilePath.GetFile();
		var title = new Label { Text = $"{sceneName}  —  id: {enemy.PersistenceId}" };
		title.AddThemeFontSizeOverride("font_size", 16);
		box.AddChild(title);

		Stats stats = enemy.Stats;
		box.AddChild(BuildIntRow("Vida máxima", stats.MaxHealth, v => { stats.MaxHealth = v; stats.ResetToFull(); }));
		box.AddChild(BuildIntRow("Ataque", stats.AttackPower, v => stats.AttackPower = v));
		box.AddChild(BuildIntRow("Defensa", stats.Defense, v => stats.Defense = v));
		box.AddChild(BuildIntRow("Estamina máxima", stats.MaxStamina, v => { stats.MaxStamina = v; stats.ResetToFull(); }));
		box.AddChild(BuildFloatRow("Velocidad", enemy.MoveSpeed, v => enemy.MoveSpeed = v));
		box.AddChild(BuildFloatRow("Daño de contacto x", enemy.ContactDamageMultiplier, v => enemy.ContactDamageMultiplier = v));
		box.AddChild(BuildWeaknessRow(stats));
		box.AddChild(BuildFloatRow("Multiplicador debilidad", stats.WeaknessDamageMultiplier, v => stats.WeaknessDamageMultiplier = v));

		if (enemy.Profile is not null)
		{
			var saveButton = new Button { Text = "Guardar en perfil (" + enemy.Profile.ResourcePath.GetFile() + ")" };
			saveButton.Pressed += () => SaveProfile(enemy);
			box.AddChild(saveButton);
		}
		else
		{
			box.AddChild(new Label { Text = "(sin EnemyProfile — los cambios no se guardan)" });
		}

		box.AddChild(new HSeparator());
		return box;
	}

	private static void SaveProfile(Enemy enemy)
	{
		EnemyProfile profile = enemy.Profile;
		Stats stats = enemy.Stats;

		profile.MaxHealth = stats.MaxHealth;
		profile.AttackPower = stats.AttackPower;
		profile.Defense = stats.Defense;
		profile.MaxStamina = stats.MaxStamina;
		profile.Weakness = stats.Weakness;
		profile.WeaknessDamageMultiplier = stats.WeaknessDamageMultiplier;
		profile.MoveSpeed = enemy.MoveSpeed;
		profile.ContactDamageMultiplier = enemy.ContactDamageMultiplier;

		ResourceSaver.Save(profile, profile.ResourcePath);
	}

	private static Control BuildIntRow(string label, int value, System.Action<int> onChanged)
	{
		var spin = new SpinBox { MinValue = 0, MaxValue = 99999, Step = 1, Value = value, CustomMinimumSize = new Vector2(100, 0) };
		spin.ValueChanged += v => onChanged((int)v);
		return WrapRow(label, spin);
	}

	private static Control BuildFloatRow(string label, float value, System.Action<float> onChanged)
	{
		var spin = new SpinBox { MinValue = 0, MaxValue = 99999, Step = 0.05, Value = value, CustomMinimumSize = new Vector2(100, 0) };
		spin.ValueChanged += v => onChanged((float)v);
		return WrapRow(label, spin);
	}

	private static Control BuildWeaknessRow(Stats stats)
	{
		var option = new OptionButton();
		foreach (string name in System.Enum.GetNames<DamageElement>())
			option.AddItem(name);
		option.Selected = (int)stats.Weakness;
		option.ItemSelected += v => stats.Weakness = (DamageElement)v;
		return WrapRow("Debilidad", option);
	}

	private static Control WrapRow(string label, Control field)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(160, 0) });
		row.AddChild(field);
		return row;
	}
}
