using Godot;

namespace Metroidvania.World;

// Bastón pickup: the wisp of shadow a defeated enemy leaves behind. Standing next to it shows the
// interact prompt ("E", same as levers/chests); pressing interact stores one "use" of that enemy type
// (EnemyKey = its scene path) via Player.CollectShadowEssence, to summon later as a one-attack
// shadow. Bobs in place and fades out if ignored.
public partial class ShadowEssence : Area2D
{
	[Export] public string EnemyKey = "";
	[Export] public float Lifetime = 12f;
	[Export] public float InteractRadius = 26f;
	[Export] public Color WispColor = new(0.55f, 0.35f, 1f, 1f);

	private float _age;
	private bool _taken;
	private Node2D _visual;
	private Label _prompt;
	private Metroidvania.Player.Player _playerInside;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		Monitorable = false;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = InteractRadius } });
		_prompt = new Label
		{
			Text = "UI_INTERACT_PROMPT",
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-8f, -36f),
			Size = new Vector2(16f, 16f),
			Visible = false,
		};
		AddChild(_prompt);
		BodyExited += body =>
		{
			if (body == _playerInside)
			{
				_playerInside = null;
				_prompt.Visible = false;
			}
		};

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		_visual = new Node2D();
		AddChild(_visual);
		_visual.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = WispColor with { A = 0.8f }, Scale = Vector2.One * 0.9f });
		_visual.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = new Color(0.9f, 0.85f, 1f, 1f), Scale = Vector2.One * 0.3f });
		_visual.AddChild(new CpuParticles2D
		{
			Amount = 10,
			Lifetime = 0.6f,
			Texture = soft,
			Material = additive,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 6f,
			Direction = Vector2.Up,
			Gravity = new Vector2(0f, -40f),
			InitialVelocityMin = 5f,
			InitialVelocityMax = 15f,
			ScaleAmountMin = 0.05f,
			ScaleAmountMax = 0.1f,
			Color = WispColor,
			LocalCoords = false,
		});
		AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = WispColor,
			Energy = 0.7f,
			TextureScale = 0.3f,
		});

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_taken)
			return;
		float dt = (float)delta;
		_age += dt;
		_visual.Position = new Vector2(0f, Mathf.Sin(_age * 3f) * 3f);
		if (_age > Lifetime - 1f)
			Modulate = Modulate with { A = Mathf.Max(0f, Lifetime - _age) };
		if (_age >= Lifetime)
		{
			QueueFree();
			return;
		}

	}

	private void OnBodyEntered(Node2D body)
	{
		if (_taken || body is not Metroidvania.Player.Player player)
			return;
		_playerInside = player;
		_prompt.Visible = true;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_taken || _playerInside is null || !@event.IsActionPressed("interact"))
			return;
		GetViewport().SetInputAsHandled();
		TryAbsorb(_playerInside);
	}

	// Stored → absorbed. All 5 slots full with other types → the player gets to pick a slot to
	// discard for it (Player.OfferShadowSlotSwap); declining leaves the essence where it is (press
	// interact again to be asked again).
	private void TryAbsorb(Metroidvania.Player.Player player)
	{
		if (player.CollectShadowEssence(EnemyKey))
		{
			Absorb();
			return;
		}
		ShowFull();
		player.OfferShadowSlotSwap(EnemyKey, accepted =>
		{
			if (accepted && IsInstanceValid(this))
				Absorb();
		});
	}

	private void Absorb()
	{
		_taken = true;
		_prompt.Visible = false;
		Tween fade = CreateTween();
		fade.TweenProperty(this, "scale", Vector2.One * 0.2f, 0.2f);
		fade.Parallel().TweenProperty(this, "modulate:a", 0f, 0.2f);
		fade.TweenCallback(Callable.From(QueueFree));
	}

	private void ShowFull()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var label = new Label { Text = TranslationServer.Translate("UI_SHADOW_ESSENCE_FULL"), Modulate = WispColor };
		scene.AddChild(label);
		label.GlobalPosition = GlobalPosition + new Vector2(-14f, -30f);
		Tween rise = label.CreateTween();
		rise.TweenProperty(label, "global_position:y", label.GlobalPosition.Y - 20f, 0.7f);
		rise.Parallel().TweenProperty(label, "modulate:a", 0f, 0.7f);
		rise.TweenCallback(Callable.From(label.QueueFree));
	}
}
