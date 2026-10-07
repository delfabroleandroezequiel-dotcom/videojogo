using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.UI;

public partial class MainMenu : Control
{
	private VBoxContainer _topMenu;
	private VBoxContainer _slotMenu;
	private VBoxContainer _namePrompt;
	private VBoxContainer _slotsContainer;
	private LineEdit _nameEdit;
	private CheckBox _randomizerCheck;
	private Button _continueButton;
	private SettingsPanel _settings;
	private int _pendingNewSlot = -1;

	private const float SlotRowWidth = 300f;
	private const float SlotRowHeight = 46f;
	private const float DeleteButtonWidth = 100f;

	public override void _Ready()
	{
		AddLogoHalo(GetNode<TextureRect>("Logo"));
		_topMenu = GetNode<VBoxContainer>("TopMenu");
		_slotMenu = GetNode<VBoxContainer>("SlotMenu");
		_namePrompt = GetNode<VBoxContainer>("NamePrompt");
		AddBackdrop(_namePrompt, new Vector2(90f, 50f));
		_slotsContainer = GetNode<VBoxContainer>("SlotMenu/SlotsContainer");
		_nameEdit = GetNode<LineEdit>("NamePrompt/NameEdit");
		_randomizerCheck = GetNode<CheckBox>("NamePrompt/RandomizerCheck");
		_continueButton = GetNode<Button>("TopMenu/ContinueButton");

		_continueButton.Pressed += OnContinuePressed;
		GetNode<Button>("TopMenu/NewGameButton").Pressed += () => ShowSlotMenu();
		GetNode<Button>("SlotMenu/BackButton").Pressed += ShowTopMenu;
		GetNode<Button>("NamePrompt/ConfirmButton").Pressed += OnNameConfirmed;
		GetNode<Button>("NamePrompt/CancelButton").Pressed += ShowTopMenu;

		_settings = GetNode<SettingsPanel>("Settings");
		// Same column and top edge as TopMenu, so every main-menu screen opens in the same spot.
		_settings.PlaceInColumn(_topMenu.AnchorLeft, _topMenu.AnchorTop, _topMenu.OffsetTop);
		GetNode<Button>("TopMenu/SettingsButton").Pressed += OnSettingsPressed;
		_settings.Closed += () => _topMenu.Visible = true;

		ShowTopMenu();
	}

	private void ShowTopMenu()
	{
		_continueButton.Disabled = SaveManager.Instance.FindMostRecentSlot() < 0;
		_topMenu.Visible = true;
		_slotMenu.Visible = false;
		_namePrompt.Visible = false;
	}

	private void ShowSlotMenu()
	{
		BuildSlotList();
		_topMenu.Visible = false;
		_slotMenu.Visible = true;
		_namePrompt.Visible = false;
	}

	private void ShowNamePrompt(int slot)
	{
		_pendingNewSlot = slot;
		_nameEdit.Text = "";
		_randomizerCheck.ButtonPressed = false;
		_topMenu.Visible = false;
		_slotMenu.Visible = false;
		_namePrompt.Visible = true;
	}

	private void OnSettingsPressed()
	{
		_topMenu.Visible = false;
		_settings.Open();
	}

	private void BuildSlotList()
	{
		foreach (Node child in _slotsContainer.GetChildren())
			child.QueueFree();

		for (int slot = 0; slot < SaveManager.SlotCount; slot++)
		{
			bool hasSave = SaveManager.Instance.HasSaveFile(slot);
			int capturedSlot = slot;

			HBoxContainer row = new() { Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", 6);
			_slotsContainer.AddChild(row);

			// Every row is SlotRowWidth wide: an occupied slot's name button shrinks to make room for
			// its (compact) delete button.
			Button slotButton = new()
			{
				Text = hasSave
					? string.Format(TranslationServer.Translate("UI_MAINMENU_SLOT_BUSY"), SaveManager.Instance.GetCharacterName(slot))
					: "UI_MAINMENU_SLOT_EMPTY",
				Disabled = hasSave,
				CustomMinimumSize = new Vector2(hasSave ? SlotRowWidth - DeleteButtonWidth - 6f : SlotRowWidth, SlotRowHeight),
			};
			slotButton.Pressed += () => ShowNamePrompt(capturedSlot);
			row.AddChild(slotButton);

			if (hasSave)
			{
				Button deleteButton = new()
				{
					Text = "UI_MAINMENU_DELETE",
					ThemeTypeVariation = "SmallButton",
					CustomMinimumSize = new Vector2(DeleteButtonWidth, SlotRowHeight),
				};
				deleteButton.Pressed += () => OnDeletePressed(capturedSlot);
				row.AddChild(deleteButton);
			}
		}
	}

	private void OnDeletePressed(int slot)
	{
		SaveManager.Instance.DeleteSave(slot);
		BuildSlotList();
	}

	private void OnContinuePressed()
	{
		int slot = SaveManager.Instance.FindMostRecentSlot();
		if (slot < 0)
			return;

		SaveManager.Instance.CurrentSlot = slot;
		SaveData data = SaveManager.Instance.LoadGame(slot);
		GetTree().ChangeSceneToFile(data.ScenePath);
	}

	// The logo's gold/green blends into the sunset, so it gets a soft dark halo: copies of itself tinted
	// black in a ring around it (a cheap blurred outline, no shader) plus a drop shadow, all drawn behind it.
	private static void AddLogoHalo(TextureRect logo)
	{
		const int Directions = 12;
		const float Radius = 4f;
		for (int i = 0; i <= Directions; i++)
		{
			bool dropShadow = i == Directions;
			Vector2 offset = dropShadow
				? new Vector2(3f, 5f)
				: Vector2.Right.Rotated(Mathf.Tau * i / Directions) * Radius;
			var copy = new TextureRect
			{
				Texture = logo.Texture,
				ExpandMode = logo.ExpandMode,
				StretchMode = logo.StretchMode,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ShowBehindParent = true,
				Modulate = new Color(0f, 0f, 0f, dropShadow ? 0.55f : 0.28f),
			};
			copy.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			copy.OffsetLeft = offset.X;
			copy.OffsetRight = offset.X;
			copy.OffsetTop = offset.Y;
			copy.OffsetBottom = offset.Y;
			logo.AddChild(copy);
		}
	}

	// Soft dark radial glow behind a menu column so its plain labels (no button frame of their own)
	// read against the bright painted background. Follows the menu's visibility.
	private static void AddBackdrop(Control menu, Vector2 padding)
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(0f, 0f, 0f, 0.72f));
		gradient.SetColor(1, new Color(0f, 0f, 0f, 0f));
		gradient.AddPoint(0.55f, new Color(0f, 0f, 0f, 0.55f));
		var backdrop = new TextureRect
		{
			Texture = new GradientTexture2D
			{
				Gradient = gradient,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f),
				FillTo = new Vector2(1f, 0.5f),
			},
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = menu.AnchorLeft,
			AnchorRight = menu.AnchorRight,
			AnchorTop = menu.AnchorTop,
			AnchorBottom = menu.AnchorBottom,
			OffsetLeft = menu.OffsetLeft - padding.X,
			OffsetRight = menu.OffsetRight + padding.X,
			OffsetTop = menu.OffsetTop - padding.Y,
			OffsetBottom = menu.OffsetBottom + padding.Y,
			Visible = menu.Visible,
		};
		Node parent = menu.GetParent();
		parent.AddChild(backdrop);
		parent.MoveChild(backdrop, menu.GetIndex());
		menu.VisibilityChanged += () => backdrop.Visible = menu.Visible;
	}

	private void OnNameConfirmed()
	{
		string name = _nameEdit.Text.Trim();
		if (string.IsNullOrEmpty(name))
			name = TranslationServer.Translate("UI_DEFAULT_HERO_NAME");

		SaveManager.Instance.CurrentSlot = _pendingNewSlot;
		SaveManager.Instance.CurrentCharacterName = name;
		SaveManager.Instance.ClearPendingLoad();
		SaveManager.Instance.ResetProgressState();
		SaveManager.Instance.EnemyRandomizerEnabled = _randomizerCheck.ButtonPressed;
		GetTree().ChangeSceneToFile(GameConfig.Instance.DefaultStartScenePath);
	}
}
