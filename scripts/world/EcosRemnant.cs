using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The Ecos a death left behind (SaveManager.DropEcosOnDeath): a bright cyan wisp hovering where the
// player last stood on the floor. Touching it gives them all back. Spawned by LevelBootstrap when the
// map loads, if this is the map the drop is in; dying again before reaching it replaces the drop, so
// this one is gone for good. Built in code.
public partial class EcosRemnant : Area2D
{
	[Export] public Color WispColor = new(0.45f, 0.95f, 1f, 1f);
	[Export] public float PickupRadius = 22f;

	private Node2D _visual;
	private float _age;
	private bool _taken;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		Monitorable = false;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = PickupRadius } });

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var soft = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		_visual = new Node2D();
		AddChild(_visual);
		_visual.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = WispColor with { A = 0.85f }, Scale = Vector2.One * 1.3f });
		_visual.AddChild(new Sprite2D { Texture = soft, Material = additive, Modulate = WispColor.Lightened(0.7f), Scale = Vector2.One * 0.4f });
		_visual.AddChild(new CpuParticles2D
		{
			Amount = 16,
			Lifetime = 0.9f,
			Texture = soft,
			Material = additive,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 9f,
			Direction = Vector2.Up,
			Gravity = new Vector2(0f, -50f),
			InitialVelocityMin = 6f,
			InitialVelocityMax = 18f,
			ScaleAmountMin = 0.06f,
			ScaleAmountMax = 0.12f,
			Color = WispColor,
			LocalCoords = false,
		});
		AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = WispColor,
			Energy = 1f,
			TextureScale = 0.45f,
		});

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		_age += (float)delta;
		_visual.Position = new Vector2(0f, Mathf.Sin(_age * 2.5f) * 4f);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_taken || body is not Metroidvania.Player.Player)
			return;
		_taken = true;
		SaveManager.Instance.RecoverDroppedEcos();
		Sfx.PlayAt(this, "Magic", "Spell Impact");
		Tween vanish = CreateTween();
		vanish.TweenProperty(_visual, "scale", Vector2.One * 1.8f, 0.25f);
		vanish.Parallel().TweenProperty(this, "modulate:a", 0f, 0.25f);
		vanish.TweenCallback(Callable.From(QueueFree));
	}
}
