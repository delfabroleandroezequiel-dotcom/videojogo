using System.Collections.Generic;
using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

public enum ChestRarity
{
	None,
	Common,
	Uncommon,
	Rare,
	Epic,
	Legendary,
	Relic
}

// Interactive loot container built on GandalfHardcore's "Chests" pack: a base skin (20 color
// variants) layered with an optional Lock overlay and an optional rarity-tinted burst effect.
// All three share the exact same 32x32 / 13-row frame grid, so playing the same "open" animation
// on all three at once keeps them frame-locked with zero manual sync code — the Lock art visibly
// falls off and the burst fades on its own, they're not driven frame-by-frame from here.
// Same interact-while-in-range pattern as Lever/SavePoint, and the same GetPath()-keyed
// persistence SaveManager already uses for one-time pickups (see IsPickupCollected), so a
// duplicated chest node doesn't need a hand-assigned ID.
// [Tool] so SkinIndex/Rarity/lock state preview live in the editor while placing chests.
[Tool]
public partial class Chest : Area2D
{
	private const string ChestFolder = "res://assets/external/gandalfhardcore/GandalfHardcore Chests/";
	private const string LockTexturePath = ChestFolder + "chest Lock.png";
	private const int FrameSize = 32;
	private const int SheetRows = 13;

	// The lid finishes its opening swing and settles by frame 6 of the 13-frame sheet (frames
	// 7-12 are the same loop's mirrored close-back-down half — this pack's animation is a full
	// closed-open-closed cycle, not a one-way open). Stopping here instead of playing through is
	// what keeps the chest open instead of swinging shut again.
	private const int OpenSettleFrame = 6;
	private const float FramesPerSecond = 10f;

	private int _skinIndex = 1;

	[Export(PropertyHint.Range, "1,20,1")]
	public int SkinIndex
	{
		get => _skinIndex;
		set { _skinIndex = Mathf.Clamp(value, 1, 20); Rebuild(); }
	}

	private string _requiredKeyItemId = "";

	// Empty: opens freely. Set to a Key-type Item's Id (see ItemDatabase/ItemType.Key) to require
	// the player already have that item — checked via PlayerInventory.HasItem, not consumed, same
	// as how Rings stay owned after being equipped. How keys actually reach the player's
	// inventory is a separate, not-yet-built system; this only gates the open.
	[Export]
	public string RequiredKeyItemId
	{
		get => _requiredKeyItemId;
		set { _requiredKeyItemId = value; Rebuild(); }
	}

	private ChestRarity _rarity = ChestRarity.Common;

	// None skips the burst effect entirely (e.g. a tutorial chest with nothing dramatic inside).
	[Export]
	public ChestRarity Rarity
	{
		get => _rarity;
		set { _rarity = value; Rebuild(); }
	}

	[Export] public int GoldReward;
	[Export] public Item ItemReward;

	private float _visualScale = 2f;

	// The pack's native 32x32 cell reads as a tiny prop next to this game's character art (a much
	// higher-detail custom sprite scaled up 1.4x on its own). Scales Base/Lock/Effect together —
	// and the collision box + all three sprites' ground-anchor offset with them, see Rebuild —
	// rather than scaling the whole Area2D node in the editor, which would also scale the
	// InteractPrompt's font and offsets in a way that doesn't track visual size sensibly.
	[Export(PropertyHint.Range, "0.5,4,0.05")]
	public float VisualScale
	{
		get => _visualScale;
		set { _visualScale = Mathf.Max(0.1f, value); Rebuild(); }
	}

	private bool _flipHorizontal;

	// Mirrors the whole chest — useful when the same skin needs to sit facing the other way to
	// match a wall/alcove without needing a second set of frames.
	[Export]
	public bool FlipHorizontal
	{
		get => _flipHorizontal;
		set { _flipHorizontal = value; Rebuild(); }
	}

	private const float BaseVisualOffsetY = -16f;
	private const float BaseCollisionSizeX = 26f;
	private const float BaseCollisionSizeY = 22f;
	private const float BaseCollisionOffsetY = -13f;

	private string _persistenceId;
	private bool _playerInRange;
	private bool _opened;
	private PlayerInventory _playerInventory;
	private AnimatedSprite2D _base;
	private AnimatedSprite2D _lock;
	private AnimatedSprite2D _effect;
	private CollisionShape2D _collisionShape;
	private Label _interactPrompt;
	private bool _initialized;

	public override void _Ready()
	{
		_base = GetNode<AnimatedSprite2D>("Base");
		_lock = GetNode<AnimatedSprite2D>("Lock");
		_effect = GetNode<AnimatedSprite2D>("Effect");
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		_interactPrompt = GetNode<Label>("InteractPrompt");
		_interactPrompt.Visible = false;

		if (!Engine.IsEditorHint())
		{
			_persistenceId = GetPath().ToString();
			_opened = SaveManager.Instance.IsPickupCollected(_persistenceId);
		}

		_initialized = true;
		Rebuild();

		if (Engine.IsEditorHint())
			return;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Engine.IsEditorHint() || !_playerInRange || _opened)
			return;

		if (@event.IsActionPressed("interact"))
		{
			TryOpen();
			GetViewport().SetInputAsHandled();
		}
	}

	// Property setters (SkinIndex etc.) call this directly, and Godot also invokes those same
	// setters to restore this node's saved state while it's still being instantiated as part of a
	// parent scene, before _Ready runs — _base/_lock/_effect are still null at that point.
	// _initialized gates this to only actually run once, from _Ready, after node refs exist and
	// (at runtime) the persisted _opened state has already landed.
	private void Rebuild()
	{
		if (!_initialized || !IsInsideTree())
			return;

		Texture2D baseTexture = GD.Load<Texture2D>($"{ChestFolder}chest sheet {_skinIndex}.png");
		_base.SpriteFrames = BuildSpriteFrames(baseTexture, OpenSettleFrame + 1);

		bool isLocked = !string.IsNullOrEmpty(_requiredKeyItemId);
		_lock.SpriteFrames = BuildSpriteFrames(GD.Load<Texture2D>(LockTexturePath), OpenSettleFrame + 1);

		string effectFile = EffectFileName(_rarity);
		if (effectFile is not null)
			_effect.SpriteFrames = BuildSpriteFrames(GD.Load<Texture2D>(ChestFolder + effectFile), SheetRows);

		// Position scales along with Scale (rather than staying at the unscaled -16) so the
		// sprite's bottom edge — it's centered by default — stays pinned to the node's own origin
		// (y=0, the ground line) at any VisualScale instead of sinking through the floor as it
		// grows.
		var scale = new Vector2(_flipHorizontal ? -_visualScale : _visualScale, _visualScale);
		var visualPosition = new Vector2(0f, BaseVisualOffsetY * _visualScale);
		foreach (AnimatedSprite2D layer in new[] { _base, _lock, _effect })
		{
			layer.Scale = scale;
			layer.Position = visualPosition;
		}

		_collisionShape.Shape = new RectangleShape2D { Size = new Vector2(BaseCollisionSizeX, BaseCollisionSizeY) * _visualScale };
		_collisionShape.Position = new Vector2(0f, BaseCollisionOffsetY * _visualScale);

		// Font size stays fixed on purpose (a prompt that grows with the chest reads as a UI bug,
		// not a bigger chest) — only its vertical offset tracks VisualScale, so it keeps clearing
		// the top of the lid instead of overlapping it.
		_interactPrompt.Position = new Vector2(-8f, -22f * _visualScale - 16f);

		if (_opened)
		{
			_base.Animation = "open";
			_base.Frame = OpenSettleFrame;
			_lock.Visible = false;
			_effect.Visible = false;
		}
		else
		{
			_base.Animation = "closed";
			_base.Frame = 0;
			_lock.Visible = isLocked;
			_lock.Animation = "closed";
			_lock.Frame = 0;
			_effect.Visible = false;
		}

		UpdatePromptText();
	}

	private void TryOpen()
	{
		bool isLocked = !string.IsNullOrEmpty(_requiredKeyItemId);
		bool hasKey = _playerInventory is not null && _playerInventory.HasItem(_requiredKeyItemId);
		if (isLocked && !hasKey)
			return;

		_opened = true;
		SaveManager.Instance.MarkPickupCollected(_persistenceId);

		_base.Play("open");
		if (isLocked)
			_lock.Play("open");

		if (EffectFileName(_rarity) is not null)
		{
			_effect.Visible = true;
			_effect.Play("open");
		}

		var rewardLines = new List<string>();

		if (GoldReward > 0)
		{
			SaveManager.Instance.AddGold(GoldReward);
			rewardLines.Add(string.Format(TranslationServer.Translate("UI_ITEM_OBTAINED_GOLD_LINE"), GoldReward));
		}

		if (ItemReward is not null && _playerInventory is not null)
		{
			_playerInventory.CollectItem(ItemReward);
			rewardLines.Add(TranslationServer.Translate(ItemReward.ItemName));
		}

		ItemRewardPopup.Instance.Show(rewardLines);

		_interactPrompt.Visible = false;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInventory = body.GetNodeOrNull<PlayerInventory>("Inventory");
		_playerInRange = true;
		UpdatePromptText();
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		_playerInventory = null;
		_interactPrompt.Visible = false;
	}

	private void UpdatePromptText()
	{
		if (_interactPrompt is null || _opened || !_playerInRange)
		{
			if (_interactPrompt is not null)
				_interactPrompt.Visible = false;
			return;
		}

		bool isLocked = !string.IsNullOrEmpty(_requiredKeyItemId);
		bool hasKey = _playerInventory is not null && _playerInventory.HasItem(_requiredKeyItemId);
		_interactPrompt.Text = isLocked && !hasKey ? "UI_CHEST_LOCKED_PROMPT" : "UI_INTERACT_PROMPT";
		_interactPrompt.Visible = true;
	}

	private static string EffectFileName(ChestRarity rarity) => rarity switch
	{
		ChestRarity.Common => "Effect color common.png",
		ChestRarity.Uncommon => "Effect color uncommon.png",
		ChestRarity.Rare => "Effect color rare.png",
		ChestRarity.Epic => "Effect color epic.png",
		ChestRarity.Legendary => "Effect color legendary.png",
		ChestRarity.Relic => "Effect color relic.png",
		_ => null,
	};

	private static SpriteFrames BuildSpriteFrames(Texture2D sheet, int openFrameCount)
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		frames.AddAnimation("closed");
		frames.SetAnimationLoopMode("closed", SpriteFrames.LoopMode.None);
		frames.SetAnimationSpeed("closed", FramesPerSecond);
		frames.AddFrame("closed", MakeFrameTexture(sheet, 0));

		frames.AddAnimation("open");
		frames.SetAnimationLoopMode("open", SpriteFrames.LoopMode.None);
		frames.SetAnimationSpeed("open", FramesPerSecond);
		for (int i = 0; i < openFrameCount; i++)
			frames.AddFrame("open", MakeFrameTexture(sheet, i));

		return frames;
	}

	private static AtlasTexture MakeFrameTexture(Texture2D sheet, int frameIndex) => new()
	{
		Atlas = sheet,
		Region = new Rect2(0, frameIndex * FrameSize, FrameSize, FrameSize),
	};
}
