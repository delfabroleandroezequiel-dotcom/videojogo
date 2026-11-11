using Godot;
using Metroidvania.Items;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

// A bandit-padlocked wooden trapdoor lying over a pit (the coloured seals at the Cave 7 crossing):
// PixelLab art instead of a neon field, because Act 1 has almost no magic. Interact with the right
// key: the padlock drops off, the lid swings down into the pit on its hinge and the way opens.
// Same contract as a key-locked EnergyBarrier — IsOpen, Open(), Opened, and the unlock remembered
// by node path — so a save made with the old barrier keeps its opened seals.
[GlobalClass]
public partial class TrampillaCandado : Node2D
{
	[Signal] public delegate void OpenedEventHandler();

	[Export] public string RequiredKeyItemId = "";
	[Export] public bool ConsumesKey = true;
	// Span that blocks the pit (the art can be wider: the lid rests on the floor at both ends).
	[Export] public float Width = 80f;
	[Export] public float Thickness = 14f;
	[Export] public Texture2D LidTexture;
	[Export] public Texture2D PadlockTexture;
	[Export] public float PadlockScale = 0.5f;
	[Export] public Vector2 PadlockOffset = new(0f, 6f);
	// The lid swings down from its left end (false: from the right).
	[Export] public bool HingeLeft = true;
	[Export] public float OpenDegrees = 95f;
	[Export] public float OpenDuration = 0.55f;

	public bool IsOpen { get; private set; }

	private StaticBody2D _body;
	private CollisionShape2D _collision;
	private Node2D _pivot;
	private Sprite2D _padlock;
	private Label _prompt;
	private bool _playerInRange;
	private PlayerInventory _inventory;
	private string _persistenceId;
	private bool _busy;

	private bool IsKeyLocked => !string.IsNullOrEmpty(RequiredKeyItemId);
	private float Swing => (HingeLeft ? 1f : -1f) * Mathf.DegToRad(OpenDegrees);

	public override void _Ready()
	{
		_persistenceId = GetPath().ToString();
		IsOpen = IsKeyLocked && SaveManager.Instance.IsPickupCollected(_persistenceId);
		Build();
		if (IsOpen)
			ApplyOpen();
	}

	private void Build()
	{
		_body = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
		_collision = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, Thickness) } };
		_body.AddChild(_collision);
		AddChild(_body);

		float lidWidth = LidTexture?.GetWidth() ?? Width;
		float hingeX = (HingeLeft ? -1f : 1f) * (lidWidth * 0.5f - 6f);
		_pivot = new Node2D { Position = new Vector2(hingeX, 0f) };
		AddChild(_pivot);
		if (LidTexture is not null)
			_pivot.AddChild(new Sprite2D { Texture = LidTexture, Position = new Vector2(-hingeX, 0f), TextureFilter = TextureFilterEnum.Nearest });

		if (PadlockTexture is not null)
		{
			_padlock = new Sprite2D
			{
				Texture = PadlockTexture, Position = PadlockOffset, Scale = Vector2.One * PadlockScale,
				Centered = true, TextureFilter = TextureFilterEnum.Nearest, ZIndex = 1,
			};
			AddChild(_padlock);
		}

		if (!IsKeyLocked)
			return;
		_prompt = new Label
		{
			Text = "UI_INTERACT_PROMPT", HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-80f, -38f), Size = new Vector2(160f, 16f),
			MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false,
		};
		AddChild(_prompt);
		var detector = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		detector.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width + 30f, 110f) } });
		AddChild(detector);
		detector.BodyEntered += body =>
		{
			if (!body.IsInGroup("player"))
				return;
			_inventory = body.GetNodeOrNull<PlayerInventory>("Inventory");
			_playerInRange = true;
			UpdatePrompt();
		};
		detector.BodyExited += body =>
		{
			if (!body.IsInGroup("player"))
				return;
			_playerInRange = false;
			_inventory = null;
			UpdatePrompt();
		};
	}

	private bool PlayerHasKey => _inventory is not null && _inventory.HasItem(RequiredKeyItemId);

	private void UpdatePrompt()
	{
		if (_prompt is null)
			return;
		// Only when the key is in hand: without it there's no text, the padlock just rattles if you try.
		_prompt.Visible = _playerInRange && !IsOpen && !_busy && PlayerHasKey;
		_prompt.Text = "UI_INTERACT_PROMPT";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_playerInRange || IsOpen || _busy || !IsKeyLocked || DialogueBox.Instance.IsOpen)
			return;
		if (!@event.IsActionPressed("interact"))
			return;
		GetViewport().SetInputAsHandled();
		if (PlayerHasKey)
			Unlock();
		else
			Rattle();
	}

	// No key: the padlock rattles against the hasp.
	private void Rattle()
	{
		if (_padlock is null)
			return;
		Tween tween = CreateTween();
		foreach (float angle in new[] { -0.25f, 0.2f, -0.12f, 0f })
			tween.TweenProperty(_padlock, "rotation", angle, 0.05f);
		Sfx.PlayIfExistsAt(this, "World/Doors", "Chest Close");
	}

	private void Unlock()
	{
		_busy = true;
		SaveManager.Instance.MarkPickupCollected(_persistenceId);
		if (ConsumesKey)
			SaveManager.Instance.ConsumeItem(RequiredKeyItemId);
		Sfx.PlayAt(this, "World/Doors", "Lock Unlock");
		UpdatePrompt();

		Tween tween = CreateTween();
		if (_padlock is not null)
		{
			// The padlock springs open and drops into the dark.
			tween.TweenProperty(_padlock, "position:y", _padlock.Position.Y - 4f, 0.08f);
			tween.TweenProperty(_padlock, "position:y", _padlock.Position.Y + 70f, 0.45f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
			tween.Parallel().TweenProperty(_padlock, "rotation", 1.4f, 0.45f);
			tween.Parallel().TweenProperty(_padlock, "modulate:a", 0f, 0.45f);
		}
		tween.TweenCallback(Callable.From(Open));
	}

	public void Open()
	{
		if (IsOpen && !_busy)
			return;
		IsOpen = true;
		_busy = false;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		UpdatePrompt();
		if (_padlock is not null && _padlock.Modulate.A > 0f)
			CreateTween().TweenProperty(_padlock, "modulate:a", 0f, 0.2f);
		Sfx.PlayIfExistsAt(this, "World/Doors", "Chest Open");
		Tween tween = CreateTween();
		tween.TweenProperty(_pivot, "rotation", Swing, OpenDuration).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => EmitSignal(SignalName.Opened)));
	}

	private void ApplyOpen()
	{
		_collision.Disabled = true;
		_pivot.Rotation = Swing;
		if (_padlock is not null)
			_padlock.Visible = false;
	}
}
