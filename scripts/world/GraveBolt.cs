using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// GraveArbalist's crossbow bolt, built in code from the pack's own arrow sheets
// (GraveBoltSpriteFrames: flying / hit_wall / hit_ground / hit_shield). Fast and straight, stopped
// by solid terrain (one-way platforms let it through): it sticks in walls and floors for a moment,
// and a blocked bolt glances off the shield instead.
public partial class GraveBolt : Area2D
{
	private const string FramesPath = "res://resources/sprites/GraveBoltSpriteFrames.tres";

	public float Speed = 460f;
	public float Lifetime = 1.6f;
	public float DamageMultiplier = 1f;
	public float KnockbackForce = 180f;
	public float VisualScale = 1.6f;
	public Color TrailColor = new(0.45f, 1f, 0.55f, 1f);
	public Stats ShooterStats;
	public Vector2 Direction = Vector2.Right;

	private AnimatedSprite2D _sprite;
	private Line2D _trail;
	private Vector2 _lastTrailPoint;
	private float _age;
	private bool _done;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 1u | 2u;
		Direction = Direction.Normalized();
		Rotation = Direction.Angle();
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(16f, 4f) } });
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Scale = new Vector2(VisualScale, VisualScale),
			// Arrow drawn at x17-46 of a 64 cell: centre it on the collision box.
			Position = new Vector2(-0.5f * VisualScale, 0f),
		};
		AddChild(_sprite);
		_sprite.Play("flying");
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 2f,
			Modulate = TrailColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		};
		AddChild(_trail);
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_age += dt;
		GlobalPosition += Direction * Speed * dt;
		if (_trail.GetPointCount() == 0 || _lastTrailPoint.DistanceTo(GlobalPosition) > 6f)
		{
			_lastTrailPoint = GlobalPosition;
			_trail.AddPoint(GlobalPosition);
			if (_trail.GetPointCount() > 8)
				_trail.RemovePoint(0);
		}
		if (_age >= Lifetime)
			End("", 0f);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done)
			return;
		if (body is Metroidvania.Player.Player player)
		{
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				return;
			int before = stats.CurrentHealth;
			int attack = ShooterStats is not null ? ShooterStats.AttackPower : 10;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(attack * DamageMultiplier)), isProjectile: true);
			if (stats.CurrentHealth < before)
			{
				player.ApplyKnockback(new Vector2(Mathf.Sign(Direction.X), -0.2f).Normalized(), KnockbackForce);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
				End("", 0f);
			}
			else
			{
				End("hit_shield", 0f);
			}
			return;
		}
		if (body is CollisionObject2D solid && (solid.CollisionLayer & PhysicsLayers.OneWayPlatforms) != 0)
			return;
		// Stuck in the terrain: in a floor if it was heading mostly down, else in a wall.
		End(Direction.Y > 0.6f ? "hit_ground" : "hit_wall", 1.2f);
	}

	private void End(string anim, float linger)
	{
		_done = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		Tween fade = CreateTween();
		fade.TweenProperty(_trail, "modulate:a", 0f, 0.2f);
		if (string.IsNullOrEmpty(anim))
		{
			_sprite.Visible = false;
			fade.TweenCallback(Callable.From(QueueFree));
			return;
		}
		if (anim == "hit_ground")
		{
			Rotation = 0f;
			_sprite.FlipH = Direction.X < 0f;
		}
		_sprite.Play(anim);
		fade.TweenInterval(linger + 0.3f);
		fade.TweenProperty(_sprite, "modulate:a", 0f, 0.3f);
		fade.TweenCallback(Callable.From(QueueFree));
	}
}
