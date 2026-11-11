using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

// The big chained hatch at the Cave 7 crossing, padlocked by the cave bandits: one padlock per key
// hangs from its front edge (a coloured rag tells them apart). Interacting with a key you carry
// takes its padlock off; with the last one gone the chain falls and the hatch swings down into the
// pit. Coming back shows at a glance how many keys are still missing.
//
// Each padlock is remembered on its own (path + index). LegacySealNames also counts as unlocked a
// padlock whose old separate seal (a sibling node with that name, used before 2026-11-10) was
// already opened in that save.
[GlobalClass]
public partial class CompuertaCandados : Node2D
{
	[Signal] public delegate void OpenedEventHandler();

	// One entry per padlock, same order in every array.
	[Export] public string[] KeyItemIds = System.Array.Empty<string>();
	[Export] public Texture2D[] PadlockTextures = System.Array.Empty<Texture2D>();
	[Export] public Vector2[] PadlockOffsets = System.Array.Empty<Vector2>();
	[Export] public string[] LegacySealNames = System.Array.Empty<string>();
	[Export] public bool ConsumesKeys = true;
	[Export] public float PadlockScale = 0.5f;
	[Export] public Texture2D LidTexture;
	[Export] public float Width = 160f;
	[Export] public float Thickness = 16f;
	[Export] public bool HingeLeft = true;
	[Export] public float OpenDegrees = 95f;
	[Export] public float OpenDuration = 0.8f;
	[Export] public float DelayAfterLastPadlock = 0.6f;

	public bool IsOpen { get; private set; }

	private Sprite2D[] _padlocks = System.Array.Empty<Sprite2D>();
	private bool[] _unlocked = System.Array.Empty<bool>();
	private CollisionShape2D _collision;
	private Node2D _pivot;
	private Label _prompt;
	private bool _playerInRange;
	private PlayerInventory _inventory;
	private bool _busy;

	private string PadlockKey(int i) => $"{GetPath()}:candado{i}";

	public override void _Ready()
	{
		var body = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
		_collision = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, Thickness) } };
		body.AddChild(_collision);
		AddChild(body);

		float lidWidth = LidTexture?.GetWidth() ?? Width;
		float hingeX = (HingeLeft ? -1f : 1f) * (lidWidth * 0.5f - 8f);
		_pivot = new Node2D { Position = new Vector2(hingeX, 0f) };
		AddChild(_pivot);
		if (LidTexture is not null)
			_pivot.AddChild(new Sprite2D { Texture = LidTexture, Position = new Vector2(-hingeX, 0f), TextureFilter = TextureFilterEnum.Nearest });

		int count = KeyItemIds.Length;
		_padlocks = new Sprite2D[count];
		_unlocked = new bool[count];
		string parentPath = GetParent()?.GetPath().ToString() ?? "";
		for (int i = 0; i < count; i++)
		{
			_unlocked[i] = SaveManager.Instance.IsPickupCollected(PadlockKey(i))
				|| (i < LegacySealNames.Length && !string.IsNullOrEmpty(LegacySealNames[i])
					&& SaveManager.Instance.IsPickupCollected($"{parentPath}/{LegacySealNames[i]}"));
			if (i < PadlockTextures.Length && PadlockTextures[i] is not null)
			{
				_padlocks[i] = new Sprite2D
				{
					Texture = PadlockTextures[i], Scale = Vector2.One * PadlockScale, ZIndex = 1,
					Position = i < PadlockOffsets.Length ? PadlockOffsets[i] : Vector2.Zero,
					TextureFilter = TextureFilterEnum.Nearest, Visible = !_unlocked[i],
				};
				AddChild(_padlocks[i]);
			}
		}

		if (AllUnlocked())
		{
			IsOpen = true;
			_collision.Disabled = true;
			_pivot.Rotation = SwingAngle;
			return;
		}

		_prompt = new Label
		{
			Text = "UI_INTERACT_PROMPT", HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-80f, -40f), Size = new Vector2(160f, 16f),
			MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false,
		};
		AddChild(_prompt);
		var detector = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		detector.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width + 30f, 110f) } });
		AddChild(detector);
		detector.BodyEntered += b =>
		{
			if (!b.IsInGroup("player"))
				return;
			_inventory = b.GetNodeOrNull<PlayerInventory>("Inventory");
			_playerInRange = true;
			UpdatePrompt();
		};
		detector.BodyExited += b =>
		{
			if (!b.IsInGroup("player"))
				return;
			_playerInRange = false;
			_inventory = null;
			UpdatePrompt();
		};
	}

	private float SwingAngle => (HingeLeft ? 1f : -1f) * Mathf.DegToRad(OpenDegrees);

	private bool AllUnlocked()
	{
		foreach (bool u in _unlocked)
			if (!u)
				return false;
		return _unlocked.Length > 0;
	}

	// The first still-locked padlock whose key the player carries, or -1.
	private int UnlockablePadlock()
	{
		if (_inventory is null)
			return -1;
		for (int i = 0; i < KeyItemIds.Length; i++)
			if (!_unlocked[i] && _inventory.HasItem(KeyItemIds[i]))
				return i;
		return -1;
	}

	// Only shown when there's something to open: without a key the hatch says nothing (the padlocks
	// rattle if you try).
	private void UpdatePrompt()
	{
		if (_prompt is not null)
			_prompt.Visible = _playerInRange && !IsOpen && !_busy && UnlockablePadlock() >= 0;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange || IsOpen || _busy || DialogueBox.Instance.IsOpen || !@event.IsActionPressed("interact"))
			return;
		GetViewport().SetInputAsHandled();
		int index = UnlockablePadlock();
		if (index >= 0)
			Unlock(index);
		else
			Rattle();
	}

	private void Rattle()
	{
		for (int i = 0; i < _padlocks.Length; i++)
		{
			if (_unlocked[i] || _padlocks[i] is not { } padlock)
				continue;
			Tween one = CreateTween();
			foreach (float angle in new[] { -0.25f, 0.2f, -0.12f, 0f })
				one.TweenProperty(padlock, "rotation", angle, 0.05f);
		}
		Sfx.PlayIfExistsAt(this, "World/Doors", "Chest Close");
	}

	private void Unlock(int index)
	{
		_busy = true;
		_unlocked[index] = true;
		SaveManager.Instance.MarkPickupCollected(PadlockKey(index));
		if (ConsumesKeys)
			SaveManager.Instance.ConsumeItem(KeyItemIds[index]);
		Sfx.PlayAt(this, "World/Doors", "Lock Unlock");
		UpdatePrompt();

		Tween tween = CreateTween();
		if (_padlocks[index] is { } padlock)
		{
			// Springs open, then drops into the pit.
			tween.TweenProperty(padlock, "position:y", padlock.Position.Y - 4f, 0.08f);
			tween.TweenProperty(padlock, "position:y", padlock.Position.Y + 70f, 0.45f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
			tween.Parallel().TweenProperty(padlock, "rotation", 1.4f, 0.45f);
			tween.Parallel().TweenProperty(padlock, "modulate:a", 0f, 0.45f);
			tween.TweenCallback(Callable.From(() => padlock.Visible = false));
		}
		tween.TweenCallback(Callable.From(() =>
		{
			_busy = false;
			if (AllUnlocked())
				Open();
			else
				UpdatePrompt();
		}));
	}

	public void Open()
	{
		if (IsOpen)
			return;
		IsOpen = true;
		UpdatePrompt();
		Tween tween = CreateTween();
		tween.TweenInterval(DelayAfterLastPadlock);
		tween.TweenCallback(Callable.From(() =>
		{
			_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			Sfx.PlayIfExistsAt(this, "World/Doors", "Portcullis Gate");
		}));
		tween.TweenProperty(_pivot, "rotation", SwingAngle, OpenDuration).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => EmitSignal(SignalName.Opened)));
	}
}
