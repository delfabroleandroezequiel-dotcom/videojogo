using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Crawler released by a Gigante hole (AgujeroGigante.Larvas). It creeps over the body toward the
// player — only onto points that are on one of the Gigante's climbable zones, so it stays on the
// skin — and bites when it reaches them (damage + a grip jolt that costs stamina). Parented to the
// body part it hatched on, so the body's motion carries it. An enemy-layer body with its own Stats:
// one or two hits kill it. Gives up and dissolves after Vida seconds.
public partial class LarvaAgujero : StaticBody2D
{
	[Export] public SpriteFrames Frames;
	[Export] public float Velocidad = 85f;
	[Export] public int Danio = 6;
	[Export] public int JoltStamina = 15;
	[Export] public float Duracion = 14f;
	[Export] public float Escala = 1.6f;

	public Node2D Objetivo;
	public ZonaTrepable[] Zonas = System.Array.Empty<ZonaTrepable>();

	private AnimatedSprite2D _sprite;
	private float _biteCooldown;
	private float _age;
	private bool _dying;

	public override void _Ready()
	{
		CollisionLayer = ProjectileReflect.EnemyLayer;
		CollisionMask = 0;
		AddToGroup("enemy");
		// Undo the parent part's scale so the larva keeps one size whatever it hatched on.
		Scale = Vector2.One / GetParent<Node2D>().GlobalScale.X;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 16f * Escala } });
		var stats = new Stats { Name = "Stats", MaxHealth = 15, AttackPower = Danio };
		AddChild(stats);
		stats.Died += Morir;
		stats.HitTaken += _ => Modulate = new Color(2f, 2f, 2f);

		_sprite = new AnimatedSprite2D { SpriteFrames = Frames, Scale = Vector2.One * Escala, Modulate = new Color(0.85f, 1f, 0.75f) };
		AddChild(_sprite);
		if (Frames is not null && Frames.HasAnimation("idle"))
			_sprite.Play("idle");
		_sprite.Scale = Vector2.Zero;
		CreateTween().TweenProperty(_sprite, "scale", Vector2.One * Escala, 0.25f).SetTrans(Tween.TransitionType.Back);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dying)
			return;
		float dt = (float)delta;
		_age += dt;
		_biteCooldown -= dt;
		Modulate = Modulate.Lerp(Colors.White, 10f * dt);
		if (_age > Duracion || !IsInstanceValid(Objetivo))
		{
			Morir();
			return;
		}

		Vector2 target = Objetivo.GlobalPosition + new Vector2(0f, -20f);
		Vector2 toTarget = target - GlobalPosition;
		if (toTarget.Length() < 34f)
		{
			if (_biteCooldown <= 0f && Objetivo is Metroidvania.Player.Player player)
			{
				_biteCooldown = 1f;
				player.GetNode<Stats>("Stats").TakeDamage(Danio);
				player.ApplyGripJolt(JoltStamina);
			}
			return;
		}
		Vector2 step = toTarget.Normalized() * Velocidad * dt;
		// Stay on the skin: try straight, then sliding along each axis.
		foreach (Vector2 candidate in new[] { step, new Vector2(step.X, 0f), new Vector2(0f, step.Y) })
		{
			if (candidate == Vector2.Zero || !OnSkin(GlobalPosition + candidate))
				continue;
			GlobalPosition += candidate;
			_sprite.FlipH = candidate.X < 0f;
			break;
		}
	}

	private bool OnSkin(Vector2 point)
	{
		foreach (ZonaTrepable zone in Zonas)
		{
			if (IsInstanceValid(zone) && zone.ContainsGlobalPoint(point))
				return true;
		}
		return false;
	}

	private void Morir()
	{
		if (_dying)
			return;
		_dying = true;
		CollisionLayer = 0;
		RemoveFromGroup("enemy");
		Tween t = CreateTween().SetParallel();
		t.TweenProperty(this, "modulate:a", 0f, 0.3f);
		t.TweenProperty(_sprite, "scale", _sprite.Scale * 1.4f, 0.3f);
		t.Chain().TweenCallback(Callable.From(QueueFree));
	}
}
