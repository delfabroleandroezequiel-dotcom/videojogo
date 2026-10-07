using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// The thrown sword (replaces the bow shot): spins out in a straight line, eases to a stop at
// MaxDistance (or as soon as it hits a wall), then flies back to the thrower like a boomerang,
// homing on them and speeding up. Hits every enemy it passes once on the way out and once more on
// the way back. Leaves neon afterimages in the thrower's element tint. Calls Returned when caught.
public partial class FlyingSword : Node2D
{
	private const string TexturePath = "res://assets/sprites/protagonist_main/flying_sword.png";

	public float MaxDistance = 240f;
	public float OutTime = 0.35f;
	public float ReturnSpeed = 380f;
	public float ReturnAcceleration = 900f;
	// Hard cap so a sword that can't reach the thrower (stuck behind geometry) still comes home.
	public float MaxFlightTime = 2.5f;
	public float SpinSpeed = 28f;
	public float GhostInterval = 0.025f;
	public float SpriteScale = 1.25f;
	public uint EnemyMask = 4;
	public uint WorldMask = 1;

	public event System.Action Returned;

	private Node2D _thrower;
	private Vector2 _catchOffset;
	private Vector2 _direction;
	private Vector2 _start;
	private int _damage;
	private DamageElement _element;
	private Color _tint;
	private Sprite2D _sprite;
	private Area2D _area;
	private float _time;
	private float _ghostTimer;
	private float _returnVelocity;
	private bool _returning;
	private bool _started;
	private readonly HashSet<ulong> _hitThisLeg = new();

	public void Setup(Node2D thrower, Vector2 catchOffset, float direction, int damage, DamageElement element, Color tint)
	{
		_thrower = thrower;
		_catchOffset = catchOffset;
		_direction = new Vector2(direction >= 0f ? 1f : -1f, 0f);
		_damage = damage;
		_element = element;
		_tint = tint;
	}

	public override void _Ready()
	{
		_sprite = new Sprite2D
		{
			Texture = GD.Load<Texture2D>(TexturePath),
			Scale = new Vector2(SpriteScale, SpriteScale),
			FlipH = _direction.X > 0f,
		};
		AddChild(_sprite);

		// Additive halo so the blade reads as charged with the element even mid-spin.
		var glow = new Sprite2D
		{
			Texture = _sprite.Texture,
			Scale = _sprite.Scale * 1.15f,
			FlipH = _sprite.FlipH,
			Material = NeonSilhouette.CreateMaterial(_tint, 1.2f),
			Modulate = new Color(1f, 1f, 1f, 0.45f),
			ShowBehindParent = true,
		};
		_sprite.AddChild(glow);

		_area = new Area2D { CollisionLayer = 0, CollisionMask = EnemyMask | WorldMask, Monitorable = false };
		_area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 18f } });
		_area.BodyEntered += OnBodyEntered;
		AddChild(_area);
	}

	public override void _PhysicsProcess(double delta)
	{
		// The thrower places the sword right after AddChild, i.e. after _Ready — so the launch
		// point is only known from the first physics tick on.
		if (!_started)
		{
			_started = true;
			_start = GlobalPosition;
		}

		float dt = (float)delta;
		_time += dt;
		_sprite.Rotation += SpinSpeed * dt * (_direction.X >= 0f ? 1f : -1f);

		if (!_returning)
		{
			// Ease-out to MaxDistance, then turn around.
			float t = Mathf.Clamp(_time / OutTime, 0f, 1f);
			float eased = 1f - (1f - t) * (1f - t);
			GlobalPosition = _start + _direction * MaxDistance * eased;
			if (t >= 1f)
				StartReturn();
		}
		else
		{
			if (!IsInstanceValid(_thrower))
			{
				QueueFree();
				return;
			}
			Vector2 target = _thrower.GlobalPosition + _catchOffset;
			_returnVelocity += ReturnAcceleration * dt;
			float step = (ReturnSpeed + _returnVelocity) * dt;
			Vector2 toTarget = target - GlobalPosition;
			if (toTarget.Length() <= Mathf.Max(step, 14f) || _time >= MaxFlightTime)
			{
				Returned?.Invoke();
				QueueFree();
				return;
			}
			GlobalPosition += toTarget.Normalized() * step;
		}

		_ghostTimer -= dt;
		if (_ghostTimer <= 0f)
		{
			_ghostTimer = GhostInterval;
			SpawnGhost();
		}
	}

	private void StartReturn()
	{
		if (_returning)
			return;
		_returning = true;
		// Fresh leg: enemies hit on the way out can be hit again on the way back.
		_hitThisLeg.Clear();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Enemy enemy)
		{
			if (enemy.Stats is null || !_hitThisLeg.Add(enemy.GetInstanceId()))
				return;
			enemy.Stats.TakeDamage(_damage, isProjectile: true, element: _element, ignoreInvulnerability: true, armInvulnerability: false);
			return;
		}
		// Anything else on the world mask is a wall/floor: bounce back early.
		if (!_returning)
			StartReturn();
	}

	private void SpawnGhost()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = _sprite.Texture,
			Scale = _sprite.Scale,
			FlipH = _sprite.FlipH,
			Rotation = _sprite.Rotation,
			Material = NeonSilhouette.CreateMaterial(_tint, 1.6f),
			Modulate = new Color(1f, 1f, 1f, 0.6f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalPosition = GlobalPosition;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.18f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}
}
