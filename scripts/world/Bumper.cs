using Godot;

namespace Metroidvania.World;

// Rebotador (after Celeste's bumpers / pinball): a glowing orb that flings whoever touches it
// straight away from its centre at BounceForce — no damage, pure control trap (it throws you into
// spikes, off a ledge, or up to a ledge you can't jump to). Optionally circles OrbitRadius around
// its spot. Origin = the bumper's centre (or the orbit centre).
public partial class Bumper : Node2D
{
	[Export] public float Radius = 26f;
	[Export] public float BounceForce = 560f;
	[Export] public float OrbitRadius;
	[Export] public float OrbitPeriod = 3f;
	[Export] public Color NeonColor = new(1f, 0.4f, 0.9f, 1f);

	private Node2D _ball;
	private Area2D _area;
	private Line2D _ring;
	private PointLight2D _light;
	private float _cooldown;
	private float _flash;
	private float _t;

	public override void _Ready()
	{
		_ball = new Node2D();
		AddChild(_ball);
		_ball.AddChild(TrapKit.Poly(TrapKit.Circle(Vector2.Zero, Radius, 24), new Color(0.18f, 0.1f, 0.2f)));
		_ring = TrapKit.Line(TrapKit.Circle(Vector2.Zero, Radius, 24), 4f, NeonColor);
		_ring.Closed = true;
		_ball.AddChild(_ring);
		_ball.AddChild(new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = NeonColor with { A = 0.6f }, Scale = Vector2.One * (Radius / 40f) });
		_light = TrapKit.Light(NeonColor, 0.8f, Radius / 25f);
		_ball.AddChild(_light);
		_area = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius + 4f } });
		_ball.AddChild(_area);
		if (OrbitRadius > 0f)
		{
			var path = TrapKit.Line(TrapKit.Circle(Vector2.Zero, OrbitRadius, 40), 1.5f, NeonColor with { A = 0.2f });
			path.Closed = true;
			AddChild(path);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_t += dt;
		if (OrbitRadius > 0f)
			_ball.Position = Vector2.Right.Rotated(Mathf.Tau * _t / OrbitPeriod) * OrbitRadius;
		_cooldown -= dt;
		_flash = Mathf.Max(0f, _flash - dt * 4f);
		_ball.Scale = Vector2.One * (1f + 0.25f * _flash);
		_light.Energy = 0.8f + 2f * _flash;
		if (_cooldown > 0f)
			return;
		foreach (Node2D b in _area.GetOverlappingBodies())
		{
			if (b is not CharacterBody2D c || !TrapKit.IsPlayer(c))
				continue;
			Vector2 dir = (c.GlobalPosition - _ball.GlobalPosition).Normalized();
			if (dir == Vector2.Zero)
				dir = Vector2.Up;
			// Horizontal part through the knockback state (so input can't cancel it), vertical directly.
			if (c.HasMethod("ApplyKnockback"))
				c.Call("ApplyKnockback", new Vector2(Mathf.Sign(dir.X == 0f ? 1f : dir.X), 0f), Mathf.Abs(dir.X) * BounceForce);
			c.Velocity = new Vector2(c.Velocity.X, dir.Y * BounceForce);
			_cooldown = 0.25f;
			_flash = 1f;
			TrapKit.Burst(this, _ball.GlobalPosition + dir * Radius, NeonColor, 14, 200f);
		}
	}
}
