using Godot;

namespace Metroidvania.World;

// Mina (after Spelunky's landmine): a little plate on the floor. Stepping on it starts a beep —
// faster and faster for FuseTime — then it blows up in BlastRadius. Jump off in time. Re-arms
// after RearmTime (0 = gone for good). Origin = floor point.
public partial class Landmine : Node2D
{
	[Export] public float FuseTime = 0.7f;
	[Export] public float BlastRadius = 85f;
	[Export] public int Damage = 40;
	[Export] public float Knockback = 520f;
	[Export] public float RearmTime = 4f;
	[Export] public bool HurtsEnemies = true;
	[Export] public Color LightColor = new(1f, 0.2f, 0.15f, 1f);

	private enum State { Armed, Fuse, Gone }

	private State _state = State.Armed;
	private float _timer;
	private Area2D _plate;
	private Node2D _body;
	private Sprite2D _led;
	private PointLight2D _light;

	public override void _Ready()
	{
		_body = new Node2D();
		AddChild(_body);
		_body.AddChild(TrapKit.Poly(new[] { new Vector2(-16f, 0f), new Vector2(-12f, -7f), new Vector2(12f, -7f), new Vector2(16f, 0f) }, new Color(0.25f, 0.27f, 0.24f)));
		_body.AddChild(TrapKit.Poly(TrapKit.Rect(new Vector2(0f, -8f), new Vector2(10f, 3f)), new Color(0.4f, 0.4f, 0.38f)));
		_led = new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = LightColor with { A = 0.4f }, Position = new Vector2(0f, -10f), Scale = Vector2.One * 0.08f };
		_body.AddChild(_led);
		_light = TrapKit.Light(LightColor, 0.2f, 0.4f);
		_light.Position = new Vector2(0f, -10f);
		_body.AddChild(_light);
		_plate = new Area2D { CollisionLayer = 0, CollisionMask = HurtsEnemies ? 2u | 4u : 2u, Monitorable = false };
		_plate.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(30f, 14f) }, Position = new Vector2(0f, -7f) });
		AddChild(_plate);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		switch (_state)
		{
			case State.Armed:
				_led.Modulate = LightColor with { A = Mathf.Sin((float)Time.GetTicksMsec() * 0.004f) > 0.9f ? 1f : 0.3f };
				foreach (Node2D b in _plate.GetOverlappingBodies())
				{
					if (!TrapKit.IsPlayer(b) && !(HurtsEnemies && b.IsInGroup("enemy")))
						continue;
					_state = State.Fuse;
					_timer = FuseTime;
					break;
				}
				break;
			case State.Fuse:
				_timer -= dt;
				float rate = Mathf.Lerp(10f, 45f, 1f - _timer / FuseTime);
				bool on = Mathf.Sin(_timer * rate) > 0f;
				_led.Modulate = LightColor with { A = on ? 1f : 0.2f };
				_light.Energy = on ? 1.5f : 0.2f;
				if (_timer <= 0f)
					Explode();
				break;
			case State.Gone:
				if (RearmTime <= 0f)
					return;
				_timer -= dt;
				if (_timer <= 0f)
				{
					_state = State.Armed;
					_body.Visible = true;
					_body.Modulate = new Color(1f, 1f, 1f, 0f);
					CreateTween().TweenProperty(_body, "modulate:a", 1f, 0.5f);
				}
				break;
		}
	}

	private void Explode()
	{
		_state = State.Gone;
		_timer = RearmTime;
		_body.Visible = false;
		Vector2 at = GlobalPosition + new Vector2(0f, -10f);
		TrapKit.Blast(this, at, BlastRadius, new Color(1f, 0.6f, 0.25f));
		TrapKit.Shake(this, 8f, 0.3f);
		foreach (Node n in GetTree().GetNodesInGroup("player"))
			if (n is Node2D p && p.GlobalPosition.DistanceTo(at) <= BlastRadius + 20f)
				TrapKit.Hurt(p, Damage, p.GlobalPosition - at + Vector2.Up * 20f, Knockback);
		if (HurtsEnemies)
			foreach (Node n in GetTree().GetNodesInGroup("enemy"))
				if (n is Node2D e && e.GlobalPosition.DistanceTo(at) <= BlastRadius + 20f)
					TrapKit.Hurt(e, Damage * 2, e.GlobalPosition - at, Knockback);
	}
}
