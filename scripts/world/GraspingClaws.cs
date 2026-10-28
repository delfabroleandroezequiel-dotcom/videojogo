using Godot;

namespace Metroidvania.World;

// Garras del suelo: a cursed stretch of floor. While the player walks it, every Interval a sigil
// opens under their feet (Telegraph), then bony claws burst up out of it: hurt + a short Snare.
// Keep moving and the claws close on empty floor. Origin = left end on the floor, Width long.
public partial class GraspingClaws : Node2D
{
	[Export] public float Width = 700f;
	[Export] public float Interval = 1.6f;
	[Export] public float Telegraph = 0.7f;
	[Export] public float ClawTime = 0.45f;
	[Export] public float Radius = 34f;
	[Export] public int Damage = 20;
	[Export] public float SnareTime = 0.5f;
	[Export] public Color SigilColor = new(0.85f, 0.2f, 0.25f, 1f);

	private float _timer;
	private Node2D _sigil;
	private Node2D _claws;
	private float _sigilTime = -1f;
	private float _clawTime = -1f;

	public override void _Ready()
	{
		AddChild(TrapKit.Line(new[] { Vector2.Zero, new Vector2(Width, 0f) }, 3f, SigilColor with { A = 0.25f }));
		_sigil = new Node2D { Visible = false };
		AddChild(_sigil);
		var ring = TrapKit.Line(TrapKit.Circle(Vector2.Zero, 1f, 24), 0.1f, SigilColor);
		ring.Closed = true;
		ring.Scale = new Vector2(Radius, Radius * 0.3f);
		_sigil.AddChild(ring);
		_sigil.AddChild(new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = SigilColor with { A = 0.6f }, Scale = new Vector2(Radius / 30f, Radius / 110f) });
		_claws = new Node2D { Visible = false };
		AddChild(_claws);
		for (int i = -2; i <= 2; i++)
		{
			float x = i * Radius * 0.4f;
			float h = 50f - Mathf.Abs(i) * 8f;
			_claws.AddChild(TrapKit.Poly(new[] { new Vector2(x - 5f, 0f), new Vector2(x + i * 3f, -h), new Vector2(x + 5f, 0f) }, new Color(0.85f, 0.82f, 0.75f)));
			_claws.AddChild(TrapKit.Line(new[] { new Vector2(x - 5f, 0f), new Vector2(x + i * 3f, -h), new Vector2(x + 5f, 0f) }, 1.5f, SigilColor));
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		var player = TrapKit.FindPlayer(this) as CharacterBody2D;
		Vector2 local = player is null ? Vector2.Zero : ToLocal(player.GlobalPosition);
		bool onStrip = player is not null && player.IsOnFloor() && local.X >= 0f && local.X <= Width && Mathf.Abs(local.Y + 32f) < 20f;

		if (_sigilTime >= 0f)
		{
			_sigilTime -= dt;
			_sigil.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(_sigilTime * 30f) > 0f ? 1f : 0.5f);
			if (_sigilTime < 0f)
			{
				_sigil.Visible = false;
				_claws.Position = _sigil.Position;
				_claws.Visible = true;
				_claws.Scale = new Vector2(1f, 0.1f);
				CreateTween().TweenProperty(_claws, "scale:y", 1f, 0.07f);
				_clawTime = ClawTime;
				TrapKit.Burst(this, _claws.GlobalPosition, new Color(0.6f, 0.55f, 0.5f), 14, 180f);
				if (player is not null && Mathf.Abs(ToLocal(player.GlobalPosition).X - _claws.Position.X) <= Radius && Mathf.Abs(ToLocal(player.GlobalPosition).Y + 32f) < 40f)
				{
					if (TrapKit.Hurt(player, Damage, Vector2.Up, 200f) && player.HasMethod("Snare"))
						player.Call("Snare", SnareTime);
				}
			}
			return;
		}
		if (_clawTime >= 0f)
		{
			_clawTime -= dt;
			if (_clawTime < 0f)
			{
				Tween t = CreateTween();
				t.TweenProperty(_claws, "scale:y", 0.05f, 0.15f);
				t.TweenCallback(Callable.From(() => _claws.Visible = false));
			}
			return;
		}
		if (!onStrip)
		{
			_timer = Mathf.Min(_timer, Interval * 0.5f);
			return;
		}
		_timer += dt;
		if (_timer < Interval)
			return;
		_timer = 0f;
		_sigil.Position = new Vector2(local.X, 0f);
		_sigil.Visible = true;
		_sigilTime = Telegraph;
	}
}
