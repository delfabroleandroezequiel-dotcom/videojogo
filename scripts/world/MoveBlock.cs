using Godot;

namespace Metroidvania.World;

// Bloque deslizante (after Celeste's move blocks): a platform with an arrow. Stand on it and after
// StartDelay it sets off along Direction, speeding up, carrying you — until it hits something,
// shakes and shatters, dropping you. It reforms at home RespawnTime later. AnimatableBody2D with
// sync_to_physics so it carries the player; moved through its local Position.
public partial class MoveBlock : AnimatableBody2D
{
	[Export] public Vector2 Size = new(112f, 32f);
	[Export] public Vector2 Direction = Vector2.Right;
	[Export] public float StartDelay = 0.25f;
	[Export] public float Acceleration = 700f;
	[Export] public float MaxSpeed = 360f;
	[Export] public float BreakShake = 0.35f;
	[Export] public float RespawnTime = 2f;
	[Export] public Color NeonColor = new(0.3f, 0.9f, 1f, 1f);

	private enum State { Idle, Starting, Moving, Breaking, Gone }

	private State _state = State.Idle;
	private Vector2 _home;
	private Vector2 _dir;
	private float _speed;
	private float _timer;
	private Node2D _visual;
	private CollisionShape2D _shape;
	private Area2D _rider;

	public override void _Ready()
	{
		SyncToPhysics = true;
		CollisionLayer = 1;
		CollisionMask = 1;
		_home = Position;
		_dir = Direction == Vector2.Zero ? Vector2.Right : Direction.Normalized();
		Build();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		switch (_state)
		{
			case State.Idle:
				foreach (Node2D b in _rider.GetOverlappingBodies())
					if (TrapKit.IsPlayer(b) && b is CharacterBody2D c && c.IsOnFloor())
					{
						_state = State.Starting;
						_timer = StartDelay;
					}
				break;
			case State.Starting:
				_timer -= dt;
				_visual.Position = new Vector2(Mathf.Sin(_timer * 90f), 0f) * 1.5f;
				if (_timer <= 0f)
				{
					_visual.Position = Vector2.Zero;
					_state = State.Moving;
					_speed = 0f;
				}
				break;
			case State.Moving:
				_speed = Mathf.Min(MaxSpeed, _speed + Acceleration * dt);
				Vector2 motion = _dir * _speed * dt;
				if (TestMove(GlobalTransform, motion))
				{
					_state = State.Breaking;
					_timer = BreakShake;
					break;
				}
				Position += motion;
				break;
			case State.Breaking:
				_timer -= dt;
				_visual.Position = new Vector2(Mathf.Sin(_timer * 120f), Mathf.Cos(_timer * 100f)) * 2f;
				if (_timer <= 0f)
					Shatter();
				break;
			case State.Gone:
				_timer -= dt;
				if (_timer <= 0f)
					Reform();
				break;
		}
	}

	private void Shatter()
	{
		_state = State.Gone;
		_timer = RespawnTime;
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		_visual.Visible = false;
		TrapKit.Burst(this, GlobalPosition, NeonColor, 30, 240f);
		TrapKit.Burst(this, GlobalPosition, new Color(0.5f, 0.5f, 0.55f), 20, 180f, 0.6f, 0.1f);
	}

	private void Reform()
	{
		_state = State.Idle;
		Position = _home;
		_visual.Position = Vector2.Zero;
		_visual.Visible = true;
		_visual.Modulate = new Color(1f, 1f, 1f, 0f);
		CreateTween().TweenProperty(_visual, "modulate:a", 1f, 0.4f);
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
	}

	private void Build()
	{
		_shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } };
		AddChild(_shape);
		_visual = new Node2D();
		AddChild(_visual);
		_visual.AddChild(TrapKit.Poly(TrapKit.Rect(Vector2.Zero, Size), new Color(0.16f, 0.18f, 0.22f)));
		Vector2 h = Size / 2f;
		_visual.AddChild(TrapKit.Line(new[] { new Vector2(-h.X, -h.Y), new Vector2(h.X, -h.Y), new Vector2(h.X, h.Y), new Vector2(-h.X, h.Y), new Vector2(-h.X, -h.Y) }, 2f, NeonColor with { A = 0.7f }));
		// arrow pointing along Direction
		float a = _dir.Angle();
		Vector2 R(Vector2 v) => v.Rotated(a);
		_visual.AddChild(TrapKit.Line(new[] { R(new Vector2(-12f, 0f)), R(new Vector2(10f, 0f)) }, 3f, NeonColor));
		_visual.AddChild(TrapKit.Line(new[] { R(new Vector2(2f, -7f)), R(new Vector2(11f, 0f)), R(new Vector2(2f, 7f)) }, 3f, NeonColor));
		_rider = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_rider.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Size.X - 6f, 12f) }, Position = new Vector2(0f, -Size.Y / 2f - 6f) });
		AddChild(_rider);
	}
}
