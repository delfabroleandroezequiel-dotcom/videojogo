using System;
using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// ElfArcher's arrow. Unlike Projectile/ArrowProjectile it deliberately ignores every collision
// shape (walls, floors, platforms) — its mask only sees the player, so it flies through terrain and
// only ever stops on a hit or when its Lifetime runs out. Leaves a fading green streak of the path
// it travelled (a top-level Line2D fed with its recent global positions), which survives the arrow
// itself for a moment so a hit/expire doesn't make the trail pop out of existence.
public partial class ElfArrow : Area2D
{
	[Export] public float Speed = 420f;
	[Export] public float Lifetime = 2.2f;
	[Export] public float KnockbackForce = 160f;
	// Fraction of the shooter's AttackPower each arrow deals — multi-arrow volleys pass their own
	// (lower) value so a 6-arrow fan landing 2-3 arrows doesn't one-shot the player.
	[Export] public float DamageMultiplier = 1f;
	[Export] public int TrailMaxPoints = 18;
	[Export] public float TrailPointSpacing = 6f;
	[Export] public float TrailWidth = 3f;
	[Export] public Color TrailColor = new(0.35f, 1f, 0.45f, 1f);
	[Export] public float TrailFadeDuration = 0.35f;

	// Raised once, when this arrow hits the player (true) or expires without hitting (false) —
	// ElfArcher uses it to learn whether its shots are landing.
	public event Action<bool> Resolved;

	private Vector2 _direction = Vector2.Right;
	private Stats _shooterStats;
	private Line2D _trail;
	private readonly List<Vector2> _trailPoints = new();
	private bool _done;
	private float _age;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.15f));
		taper.AddPoint(new Vector2(1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = TrailWidth,
			WidthCurve = taper,
			Gradient = fade,
			Modulate = TrailColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			ZIndex = -1,
		};
		AddChild(_trail);
	}

	public void Launch(Vector2 direction, Stats shooterStats)
	{
		_direction = direction.Normalized();
		_shooterStats = shooterStats;
		Rotation = _direction.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;

		GlobalPosition += _direction * Speed * (float)delta;
		RecordTrailPoint();

		_age += (float)delta;
		if (_age >= Lifetime)
			Finish(hitPlayer: false);
	}

	private void RecordTrailPoint()
	{
		Vector2 tail = GlobalPosition;
		if (_trailPoints.Count > 0 && _trailPoints[^1].DistanceSquaredTo(tail) < TrailPointSpacing * TrailPointSpacing)
		{
			_trailPoints[^1] = tail;
		}
		else
		{
			_trailPoints.Add(tail);
			if (_trailPoints.Count > TrailMaxPoints)
				_trailPoints.RemoveAt(0);
		}
		_trail.Points = _trailPoints.ToArray();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done || !body.IsInGroup("player"))
			return;

		Stats targetStats = body.GetNodeOrNull<Stats>("Stats");
		if (targetStats is null || targetStats.IsInvulnerable)
			return;

		int damage = Mathf.Max(1, Mathf.RoundToInt((_shooterStats?.AttackPower ?? 10) * DamageMultiplier));
		targetStats.TakeDamage(damage, isProjectile: true);
		if (body.HasMethod("ApplyKnockback"))
			body.Call("ApplyKnockback", _direction, KnockbackForce);

		ImpactEffect.SpawnAt(this, GlobalPosition);
		Finish(hitPlayer: true);
	}

	private void Finish(bool hitPlayer)
	{
		if (_done)
			return;
		_done = true;
		Resolved?.Invoke(hitPlayer);
		SetDeferred(Area2D.PropertyName.Monitoring, false);

		// Hand the trail to the scene so it can finish fading after the arrow itself is gone.
		Vector2[] points = _trail.Points;
		_trail.QueueFree();
		if (points.Length > 1 && GetTree().CurrentScene is { } scene)
		{
			var ghost = (Line2D)_trail.Duplicate();
			ghost.TopLevel = false;
			ghost.Points = points;
			scene.AddChild(ghost);
			Tween fadeTween = ghost.CreateTween();
			fadeTween.TweenProperty(ghost, "modulate:a", 0f, TrailFadeDuration);
			fadeTween.TweenCallback(Callable.From(ghost.QueueFree));
		}

		QueueFree();
	}
}
