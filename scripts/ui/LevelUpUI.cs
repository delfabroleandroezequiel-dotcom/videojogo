using System;
using System.Collections.Generic;
using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

// The lit save point's menu: spend Ecos on a level of Vida, Estamina or Daño (one row each, with
// the stat's level and what the next level would take it to), then rest or leave. The price of the
// next level is shared by all three and grows with the total level (LevelingConfig). Not an
// autoload (see CLAUDE.md): SavePoint creates one through Open() and it frees itself on close.
// Pauses the tree while open, like ShopUI.
public partial class LevelUpUI : CanvasLayer
{
	private static readonly Color GoodText = new(0.5f, 1f, 0.55f);
	private static readonly Color BadText = new(1f, 0.35f, 0.3f);
	private static readonly Color EcosText = new(0.6f, 0.97f, 1f);

	private static readonly Dictionary<string, string> StatNameKeys = new()
	{
		{ StatIds.Vitality, "UI_STAT_VITALITY" },
		{ StatIds.Endurance, "UI_STAT_ENDURANCE" },
		{ StatIds.Strength, "UI_STAT_STRENGTH" },
	};

	private Action _onRest;
	private Metroidvania.Player.Player _player;
	private Label _ecosLabel;
	private Label _costLabel;
	private Label _statusLabel;
	private readonly List<(string StatId, Label Level, Label Value, Button Buy)> _rows = new();
	private ulong _openedAtMs;

	public static void Open(Node context, Action onRest)
	{
		var ui = new LevelUpUI
		{
			_onRest = onRest,
			_player = context.GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player,
		};
		context.GetTree().Root.AddChild(ui);
	}

	public override void _Ready()
	{
		Layer = 50;
		ProcessMode = ProcessModeEnum.Always;
		_openedAtMs = Time.GetTicksMsec();
		GetTree().Paused = true;
		Build();
		Refresh();
		_rows[0].Buy.GrabFocus();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// The interact press that opened it must not close it too.
		if (Time.GetTicksMsec() - _openedAtMs < 150)
			return;
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	private void Close()
	{
		GetTree().Paused = false;
		QueueFree();
	}

	private void Build()
	{
		var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 12);
		root.AddChild(box);

		var header = new HBoxContainer();
		var title = new Label { Text = "UI_LEVELUP_TITLE", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddChild(title);
		_ecosLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
		JekyriaFrame.StyleHeader(_ecosLabel, 22);
		_ecosLabel.AddThemeColorOverride("font_color", EcosText);
		header.AddChild(_ecosLabel);
		box.AddChild(header);

		_costLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_costLabel.AddThemeFontSizeOverride("font_size", 18);
		box.AddChild(_costLabel);
		box.AddChild(new HSeparator());

		foreach (string statId in StatIds.All)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 16);
			var name = new Label { Text = StatNameKeys[statId], CustomMinimumSize = new Vector2(150f, 0f), VerticalAlignment = VerticalAlignment.Center };
			JekyriaFrame.StyleHeader(name, 20);
			var level = new Label { CustomMinimumSize = new Vector2(80f, 0f), VerticalAlignment = VerticalAlignment.Center };
			level.AddThemeFontSizeOverride("font_size", 18);
			var value = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
			value.AddThemeFontSizeOverride("font_size", 18);
			var buy = new Button { Text = "+", CustomMinimumSize = new Vector2(48f, 40f), FocusMode = Control.FocusModeEnum.All };
			string id = statId;
			buy.Pressed += () => TryBuy(id);
			buy.MouseEntered += () => buy.GrabFocus();
			row.AddChild(name);
			row.AddChild(level);
			row.AddChild(value);
			row.AddChild(buy);
			box.AddChild(row);
			_rows.Add((statId, level, value, buy));
		}

		_statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_statusLabel.AddThemeFontSizeOverride("font_size", 16);
		box.AddChild(_statusLabel);
		box.AddChild(new HSeparator());

		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		actions.AddThemeConstantOverride("separation", 24);
		var rest = new Button { Text = "UI_LEVELUP_REST", CustomMinimumSize = new Vector2(160f, 40f) };
		rest.Pressed += () =>
		{
			Close();
			_onRest?.Invoke();
		};
		var leave = new Button { Text = "UI_LEVELUP_LEAVE", CustomMinimumSize = new Vector2(160f, 40f) };
		leave.Pressed += Close;
		rest.MouseEntered += () => rest.GrabFocus();
		leave.MouseEntered += () => leave.GrabFocus();
		actions.AddChild(rest);
		actions.AddChild(leave);
		box.AddChild(actions);

		JekyriaFrame.Dress(root, box, title, new Vector2(600f, 420f));
	}

	private int NextCost() => LevelingConfig.Get().CostForTotalLevel(SaveManager.Instance.TotalStatLevel);

	private void TryBuy(string statId)
	{
		if (!SaveManager.Instance.BuyStatLevel(statId))
		{
			_statusLabel.Text = TranslationServer.Translate("UI_LEVELUP_NOT_ENOUGH");
			_statusLabel.Modulate = BadText;
			return;
		}
		_player?.ApplyStatLevels();
		_statusLabel.Text = "";
		Refresh();
	}

	private void Refresh()
	{
		SaveManager save = SaveManager.Instance;
		LevelingConfig config = LevelingConfig.Get();
		int cost = NextCost();
		_ecosLabel.Text = string.Format(TranslationServer.Translate("UI_LEVELUP_ECOS"), save.Ecos);
		_costLabel.Text = string.Format(TranslationServer.Translate("UI_LEVELUP_COST"), cost);
		_costLabel.Modulate = save.Ecos >= cost ? GoodText : BadText;

		Stats stats = _player?.GetNodeOrNull<Stats>("Stats");
		foreach ((string statId, Label level, Label value, Button buy) in _rows)
		{
			int current = save.GetStatLevel(statId);
			level.Text = string.Format(TranslationServer.Translate("UI_LEVELUP_LEVEL"), current);
			int gain = config.BonusFor(current + 1, config.PerLevel(statId)) - config.BonusFor(current, config.PerLevel(statId));
			int now = stats is null ? 0 : statId switch
			{
				StatIds.Vitality => stats.MaxHealth,
				StatIds.Endurance => stats.MaxStamina,
				_ => stats.AttackPower,
			};
			value.Text = $"{now}  →  {now + gain}";
			buy.Disabled = save.Ecos < cost;
		}
	}
}
