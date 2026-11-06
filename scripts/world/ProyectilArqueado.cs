using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Procedural thrown/shot projectile on a gravity arc (Radem's javelins and fire arrows): a drawn
// shaft that points along its velocity, a neon trail, and a hit on the player. A parry knocks it
// out of the air. Where it lands it sticks in the ground for a moment and, with GroundFireScene
// set, leaves a fire patch (fire arrow). No sprite sheet: shaft + head are Line2D/Polygon2D.
public partial class ProyectilArqueado : Area2D
{
	[Export] public float ArcGravity = 520f;
	[Export] public float Lifetime = 3f;
	[Export] public float DamageMultiplier = 1f;
	[Export] public float ShaftLength = 26f;
	[Export] public Color ShaftColor = new(0.45f, 0.3f, 0.18f, 1f);
	[Export] public Color TipColor = new(0.85f, 0.85f, 0.9f, 1f);
	[Export] public Color TrailColor = new(1f, 0.15f, 0.08f, 1f);
	[Export] public float StuckSeconds = 1.2f;
	[Export] public PackedScene GroundFireScene;
	[Export] public DamageElement Element = DamageElement.Normal;

	private Vector2 _velocity;
	private Stats _shooterStats;
	private bool _done;
	private Line2D _trail;
	private Node2D _body;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 3;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 4f }, Position = new Vector2(ShaftLength * 0.5f, 0f) });
		BodyEntered += OnBodyEntered;
		GetTree().CreateTimer(Lifetime).Timeout += () => { if (!_done && IsInstanceValid(this)) QueueFree(); };

		_body = new Node2D();
		AddChild(_body);
		_body.AddChild(new Line2D
		{
			Points = new[] { new Vector2(-ShaftLength * 0.5f, 0f), new Vector2(ShaftLength * 0.5f, 0f) },
			Width = 2f,
			DefaultColor = ShaftColor,
		});
		_body.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(ShaftLength * 0.5f + 5f, 0f), new Vector2(ShaftLength * 0.5f - 1f, -2.5f), new Vector2(ShaftLength * 0.5f - 1f, 2.5f) },
			Color = TipColor,
		});

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 4f,
			Gradient = fade,
			Modulate = TrailColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};
		AddChild(_trail);
	}

	public void Launch(Vector2 velocity, Stats shooterStats)
	{
		_velocity = velocity;
		_shooterStats = shooterStats;
		Rotation = velocity.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		float dt = (float)delta;
		_velocity.Y += ArcGravity * dt;
		Position += _velocity * dt;
		Rotation = _velocity.Angle();
		_trail.AddPoint(GlobalPosition);
		if (_trail.GetPointCount() > 12)
			_trail.RemovePoint(0);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_done)
			return;
		if (body.IsInGroup("player"))
		{
			if (ProjectileReflect.TryParry(body))
			{
				ImpactEffect.SpawnAt(this, GlobalPosition);
				QueueFree();
				_done = true;
				return;
			}
			Stats target = body.GetNodeOrNull<Stats>("Stats");
			if (target is not null && target != _shooterStats && !target.IsInvulnerable && _shooterStats is not null)
			{
				int damage = Mathf.RoundToInt(_shooterStats.AttackPower * DamageMultiplier);
				target.TakeDamage(damage, isProjectile: true, element: Element);
				BloodEffect.SpawnAt(this, body.GlobalPosition);
				if (body.HasMethod("ApplyKnockback"))
					body.Call("ApplyKnockback", new Vector2(Mathf.Sign(_velocity.X), -0.3f).Normalized(), 160f);
			}
			_done = true;
			QueueFree();
		}
		else if (body is StaticBody2D floor && (floor.CollisionLayer & PhysicsLayers.OneWayPlatforms) == 0)
		{
			Stick();
		}
	}

	// Lands: stays planted at its angle, then fades. A fire arrow lights the ground there.
	private void Stick()
	{
		_done = true;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		ImpactEffect.SpawnAt(this, GlobalPosition);
		if (GroundFireScene is not null && GetTree().CurrentScene is Node scene)
		{
			Vector2 at = GlobalPosition;
			Callable.From(() =>
			{
				var fire = GroundFireScene.Instantiate<Node2D>();
				scene.AddChild(fire);
				fire.GlobalPosition = at;
			}).CallDeferred();
		}
		var tween = CreateTween();
		tween.TweenInterval(StuckSeconds);
		tween.TweenProperty(this, "modulate:a", 0f, 0.4f);
		tween.TweenCallback(Callable.From(QueueFree));
	}
}
