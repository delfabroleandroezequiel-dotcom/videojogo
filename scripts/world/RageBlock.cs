using Godot;

namespace Metroidvania.World;

// Bloque furioso (after Spelunky's crush trap / Celeste's Kevin): a stone block with a face that
// watches its four axes. Line up with it within DetectRange (and nothing solid in between) and it
// glares for WindUp, then hurls itself at you, accelerating until it slams into a wall; it rests
// there for RestTime and crawls back home. Hurts on contact while charging. AnimatableBody2D moved
// through its local Position; it only collides with the world (mask 1) when checking for walls.
public partial class RageBlock : AnimatableBody2D
{
	[Export] public Vector2 Size = new(72f, 72f);
	[Export] public float DetectRange = 480f;
	[Export] public float WindUp = 0.35f;
	[Export] public float Acceleration = 2200f;
	[Export] public float MaxSpeed = 760f;
	[Export] public float RestTime = 0.8f;
	[Export] public float ReturnSpeed = 140f;
	[Export] public int Damage = 35;
	[Export] public float Knockback = 480f;
	[Export] public Color NeonColor = new(1f, 0.45f, 0.15f, 1f);

	private enum State { Watch, WindUp, Charge, Rest, Return }

	private State _state = State.Watch;
	private Vector2 _home;
	private Vector2 _dir;
	private float _speed;
	private float _timer;
	private Node2D _visual;
	private Line2D _eyesL;
	private Line2D _eyesR;
	private Area2D _hurt;

	public override void _Ready()
	{
		SyncToPhysics = false;
		CollisionLayer = 1;
		CollisionMask = 1;
		_home = Position;
		Build();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		switch (_state)
		{
			case State.Watch:
				if (FindTarget(out Vector2 dir))
				{
					_dir = dir;
					_state = State.WindUp;
					_timer = WindUp;
					SetAngry(true);
				}
				break;
			case State.WindUp:
				_timer -= dt;
				_visual.Position = new Vector2(Mathf.Sin(_timer * 80f), Mathf.Cos(_timer * 70f)) * 2.5f;
				if (_timer <= 0f)
				{
					_visual.Position = Vector2.Zero;
					_state = State.Charge;
					_speed = 0f;
				}
				break;
			case State.Charge:
				_speed = Mathf.Min(MaxSpeed, _speed + Acceleration * dt);
				Vector2 motion = _dir * _speed * dt;
				if (TestMove(GlobalTransform, motion))
				{
					// Snap flush against the wall it hit.
					var col = new KinematicCollision2D();
					TestMove(GlobalTransform, motion, col);
					Position += _dir * Mathf.Max(0f, col.GetTravel().Length() - 0.5f);
					Slam();
					break;
				}
				Position += motion;
				HurtTouching();
				break;
			case State.Rest:
				_timer -= dt;
				if (_timer <= 0f)
				{
					_state = State.Return;
					SetAngry(false);
				}
				break;
			case State.Return:
				Vector2 to = _home - Position;
				float step = ReturnSpeed * dt;
				if (to.Length() <= step)
				{
					Position = _home;
					_state = State.Watch;
				}
				else
					Position += to.Normalized() * step;
				break;
		}
	}

	private bool FindTarget(out Vector2 dir)
	{
		dir = Vector2.Zero;
		var player = TrapKit.FindPlayer(this);
		if (player is null)
			return false;
		Vector2 d = player.GlobalPosition - GlobalPosition;
		Vector2 half = Size / 2f;
		if (Mathf.Abs(d.Y) < half.Y + 10f && Mathf.Abs(d.X) <= DetectRange)
			dir = new Vector2(Mathf.Sign(d.X), 0f);
		else if (Mathf.Abs(d.X) < half.X + 10f && Mathf.Abs(d.Y) <= DetectRange)
			dir = new Vector2(0f, Mathf.Sign(d.Y));
		else
			return false;
		// Line of sight: no world in between.
		var q = PhysicsRayQueryParameters2D.Create(GlobalPosition + dir * (half.X + 2f), player.GlobalPosition, 1);
		q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		return GetWorld2D().DirectSpaceState.IntersectRay(q).Count == 0;
	}

	private void HurtTouching()
	{
		foreach (Node2D b in _hurt.GetOverlappingBodies())
			if (TrapKit.IsPlayer(b) || b.IsInGroup("enemy"))
				TrapKit.Hurt(b, Damage, _dir + Vector2.Up * 0.3f, Knockback);
	}

	private void Slam()
	{
		_state = State.Rest;
		_timer = RestTime;
		HurtTouching();
		TrapKit.Shake(this, 7f, 0.25f);
		TrapKit.Burst(this, GlobalPosition + _dir * (Size.X / 2f), new Color(0.8f, 0.75f, 0.65f), 26, 260f);
	}

	private void SetAngry(bool angry)
	{
		Color c = angry ? NeonColor : NeonColor with { A = 0.35f };
		_eyesL.DefaultColor = c;
		_eyesR.DefaultColor = c;
		_eyesL.Points = angry ? new[] { new Vector2(-18f, -12f), new Vector2(-6f, -6f) } : new[] { new Vector2(-18f, -8f), new Vector2(-6f, -8f) };
		_eyesR.Points = angry ? new[] { new Vector2(18f, -12f), new Vector2(6f, -6f) } : new[] { new Vector2(18f, -8f), new Vector2(6f, -8f) };
	}

	private void Build()
	{
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } });
		_visual = new Node2D();
		AddChild(_visual);
		_visual.AddChild(TrapKit.Poly(TrapKit.Rect(Vector2.Zero, Size), new Color(0.24f, 0.22f, 0.22f)));
		Vector2 h = Size / 2f;
		var outline = new[] { new Vector2(-h.X, -h.Y), new Vector2(h.X, -h.Y), new Vector2(h.X, h.Y), new Vector2(-h.X, h.Y), new Vector2(-h.X, -h.Y) };
		_visual.AddChild(TrapKit.Line(outline, 2f, NeonColor with { A = 0.6f }));
		_eyesL = TrapKit.Line(System.Array.Empty<Vector2>(), 4f, NeonColor);
		_eyesR = TrapKit.Line(System.Array.Empty<Vector2>(), 4f, NeonColor);
		_visual.AddChild(_eyesL);
		_visual.AddChild(_eyesR);
		_visual.AddChild(TrapKit.Line(new[] { new Vector2(-12f, 14f), new Vector2(-4f, 10f), new Vector2(4f, 14f), new Vector2(12f, 10f) }, 3f, new Color(0.1f, 0.08f, 0.08f), false));
		SetAngry(false);
		_hurt = new Area2D { CollisionLayer = 0, CollisionMask = 2u | 4u, Monitorable = false };
		_hurt.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size + new Vector2(10f, 10f) } });
		AddChild(_hurt);
	}
}
