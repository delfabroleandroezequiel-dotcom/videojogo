using System.Collections.Generic;
using Godot;

namespace Metroidvania.UI;

// Lightweight "Has obtenido" card — unlike RestScreen/DeathScreen this doesn't pause the game or
// blur the screen, since it's meant to fire mid-exploration (chest opened, item picked up) rather
// than at a hard beat like resting or dying. Same Instance.Show(...)-from-anywhere autoload
// pattern as those, and ZoneTitle, so any future reward source (loose pickups, quest turn-ins)
// can call this without needing a node reference threaded through.
public partial class ItemRewardPopup : CanvasLayer
{
	[Export] public float FadeInDuration = 0.35f;
	[Export] public float HoldDuration = 2.2f;
	[Export] public float FadeOutDuration = 0.6f;

	public static ItemRewardPopup Instance { get; private set; }

	private Control _root;
	private Label _titleLabel;
	private VBoxContainer _itemsList;
	private Tween _activeTween;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Instance = this;

		_root = GetNode<Control>("Root");
		_titleLabel = GetNode<Label>("Root/VBox/TitleLabel");
		_itemsList = GetNode<VBoxContainer>("Root/VBox/ItemsList");
		_root.Modulate = new Color(1, 1, 1, 0);
	}

	// rewardLines are already-formatted display strings (see Chest.BuildRewardLines) — this
	// widget just lays them out, it doesn't know about Item/gold itself.
	public void Show(IReadOnlyList<string> rewardLines)
	{
		if (rewardLines is null || rewardLines.Count == 0)
			return;

		_activeTween?.Kill();

		_titleLabel.Text = TranslationServer.Translate("UI_ITEM_OBTAINED_TITLE");

		foreach (Node child in _itemsList.GetChildren())
			child.QueueFree();

		foreach (string line in rewardLines)
		{
			_itemsList.AddChild(new Label
			{
				Text = line,
				HorizontalAlignment = HorizontalAlignment.Center,
			});
		}

		_root.Modulate = new Color(1, 1, 1, 0);
		_activeTween = CreateTween();
		_activeTween.TweenProperty(_root, "modulate:a", 1f, FadeInDuration);
		_activeTween.TweenInterval(HoldDuration);
		_activeTween.TweenProperty(_root, "modulate:a", 0f, FadeOutDuration);
	}
}
