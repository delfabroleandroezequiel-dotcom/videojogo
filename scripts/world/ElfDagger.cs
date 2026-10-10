using System;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// ElfAssassin's thrown dagger: the Elves pack's own spinning Dagger frames, flying straight (only
// sees the player, like the elf arrows) with an elf-green neon streak. On a hit it poisons the
// player (same ApplyPoison contract PoisonSpit uses) on top of the small impact damage.
public partial class ElfDagger : Area2D
{
	public Vector2 Direction = Vector2.Right;
	public float Speed = 420f;
	public float Lifetime = 1.6f;
	public float DamageMultiplier = 0.6f;
	public int PoisonTickDamage = 3;
	public float PoisonTickInterval = 0.5f;
	public float PoisonDuration = 3f;
	public Stats ThrowerStats;
	public Color TrailColor = new(0.35f, 1f, 0.45f, 1f);

	public event Action<bool> Resolved;

	private const string DaggerDir = "res://assets/external/CharacterPacks/Elves_Pack/Elf_Assassin/Dagger/";

	private AnimatedSprite2D _sprite;
	private Line2D _trail;
	private readonly System.Collections.Generic.List<Vector2> _points = new();
	private float _age;
	private bool _done;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 8f } });

		var frames = new SpriteFrames();
		frames.SetAnimationSpeed("default", 16f);
		frames.SetAnimationLoop("default", true);
		for (int i = 1; i <= 4; i++)
			frames.AddFrame("default", GD.Load<Texture2D>($"{DaggerDir}Dagger{i}.png"));
		_sprite = new AnimatedSprite2D { SpriteFrames = frames, Scale = Vector2.One * 1.4f, FlipH = Direction.X < 0f };
		AddChild(_sprite);
		_sprite.Play("default");

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.1f));
		taper.AddPoint(new Vector2(1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 3.5f,
			WidthCurve = taper,
			Gradient = fade,
			Modulate = TrailColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			ZIndex = -1,
		};
		AddChild(_trail);
		Direction = Direction.Normalized();
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		_age += (float)delta;
		GlobalPosition += Direction * Speed * (float)delta;

		Vector2 p = GlobalPosition;
		if (_points.Count > 0 && _points[^1].DistanceSquaredTo(p) < 36f)
			_points[^1] = p;
		else
		{
			_points.Add(p);
			if (_points.Count > 12)
				_points.RemoveAt(0);
		}
		_trail.Points = _points.ToArray();

		if (_age >= Lifetime)
			Finish(false);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done || !body.IsInGroup("player"))
			return;
		Stats stats = body.GetNodeOrNull<Stats>("Stats");
		if (stats is null || stats.IsInvulnerable)
			return;

		int damage = Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier));
		int healthBefore = stats.CurrentHealth;
		stats.TakeDamage(damage, isProjectile: true, element: DamageElement.Poison);
		// Only poisons if the hit actually went through (a block stops the blade and its poison).
		if (stats.CurrentHealth < healthBefore && body.HasMethod("ApplyPoison"))
			body.Call("ApplyPoison", PoisonTickDamage, PoisonTickInterval, PoisonDuration);
		ImpactEffect.SpawnAt(this, GlobalPosition);
		Finish(true);
	}

	private void Finish(bool hit)
	{
		if (_done)
			return;
		_done = true;
		Resolved?.Invoke(hit);
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		Vector2[] points = _trail.Points;
		if (points.Length > 1 && GetTree().CurrentScene is { } scene)
		{
			RemoveChild(_trail);
			_trail.TopLevel = false;
			scene.AddChild(_trail);
			_trail.Points = points;
			Line2D trail = _trail;
			Tween fade = trail.CreateTween();
			fade.TweenProperty(trail, "modulate:a", 0f, 0.3f);
			fade.TweenCallback(Callable.From(trail.QueueFree));
		}
		QueueFree();
	}
}
