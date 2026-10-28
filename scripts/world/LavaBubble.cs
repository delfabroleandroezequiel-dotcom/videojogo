using Godot;

namespace Metroidvania.World;

// Chispa saltarina (after Mario's podoboos): a fireball that leaps out of a pit every Interval,
// rising JumpHeight px straight up and falling back in. Burns on touch. Telegraphed by a glow and
// bubbling at the launch point just before it jumps. Origin = the launch point (inside the pit).
public partial class LavaBubble : Node2D
{
	[Export] public float JumpHeight = 260f;
	[Export] public float Gravity = 1300f;
	[Export] public float Interval = 2.2f;
	[Export] public float StartOffset;
	[Export] public float WarningTime = 0.5f;
	[Export] public int Damage = 25;
	[Export] public float Knockback = 320f;
	[Export] public Color FireColor = new(1f, 0.55f, 0.15f, 1f);

	private float _timer;
	private bool _flying;
	private float _y;
	private float _vy;
	private Node2D _ball;
	private Area2D _hurt;
	private GpuParticles2D _trail;
	private PointLight2D _glow;

	public override void _Ready()
	{
		_timer = Mathf.PosMod(StartOffset, Interval);
		_glow = TrapKit.Light(FireColor, 0.3f, 1f);
		AddChild(_glow);
		_ball = new Node2D { Visible = false };
		AddChild(_ball);
		_ball.AddChild(new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = FireColor, Scale = Vector2.One * 0.45f });
		_ball.AddChild(TrapKit.Poly(TrapKit.Circle(Vector2.Zero, 11f, 14), new Color(1f, 0.85f, 0.4f)));
		_ball.AddChild(TrapKit.Poly(new[] { new Vector2(-5f, -3f), new Vector2(-2f, -3f), new Vector2(-2f, 1f), new Vector2(-5f, 1f) }, new Color(0.3f, 0.1f, 0f)));
		_ball.AddChild(TrapKit.Poly(new[] { new Vector2(2f, -3f), new Vector2(5f, -3f), new Vector2(5f, 1f), new Vector2(2f, 1f) }, new Color(0.3f, 0.1f, 0f)));
		_ball.AddChild(TrapKit.Light(FireColor, 1.4f, 1.2f));
		_trail = new GpuParticles2D
		{
			Amount = 30, Lifetime = 0.4, Emitting = false, Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = FireColor, LocalCoords = false,
			ProcessMaterial = new ParticleProcessMaterial { Spread = 180f, InitialVelocityMin = 10f, InitialVelocityMax = 40f, Gravity = new Vector3(0f, -60f, 0f), ScaleMin = 0.04f, ScaleMax = 0.09f },
		};
		_ball.AddChild(_trail);
		_hurt = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_hurt.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 13f } });
		_ball.AddChild(_hurt);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (!_flying)
		{
			_timer += dt;
			bool warn = _timer >= Interval - WarningTime;
			_glow.Energy = warn ? 1f + Mathf.Sin(_timer * 40f) * 0.5f : 0.3f;
			if (_timer >= Interval)
			{
				_timer = 0f;
				_flying = true;
				_y = 0f;
				_vy = -Mathf.Sqrt(2f * Gravity * JumpHeight);
				_ball.Visible = true;
				_trail.Emitting = true;
				TrapKit.Burst(this, GlobalPosition, FireColor, 12, 160f);
			}
			return;
		}
		_vy += Gravity * dt;
		_y += _vy * dt;
		_ball.Position = new Vector2(0f, _y);
		_ball.Rotation = _vy > 0f ? Mathf.Pi : 0f;
		foreach (Node2D b in _hurt.GetOverlappingBodies())
			if (TrapKit.IsPlayer(b))
				TrapKit.Hurt(b, Damage, new Vector2(Mathf.Sign(b.GlobalPosition.X - _ball.GlobalPosition.X), -0.5f), Knockback);
		if (_y >= 0f && _vy > 0f)
		{
			_flying = false;
			_ball.Visible = false;
			_trail.Emitting = false;
			TrapKit.Burst(this, GlobalPosition, FireColor, 10, 120f);
		}
	}
}
