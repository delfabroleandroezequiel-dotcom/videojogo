using Godot;

namespace Metroidvania.World;

// Raíz que se hunde (Las Raíces): a short root platform floating over the mud. Stand on it and,
// after a brief beat, it starts sinking toward the mud; step off and it slowly floats back up.
// Stay too long and it takes you down into the barro with it. The node is the platform's
// top-centre. Pure Y translation through local Position (AnimatableBody2D + sync_to_physics),
// never rotation: anything else launches a rider.
public partial class RaizHundible : AnimatableBody2D
{
	[Export] public float Width = 100f;
	[Export] public float Thickness = 24f;
	// Seconds standing on it before it starts to give way.
	[Export] public float Delay = 0.25f;
	[Export] public float SinkSpeed = 45f;
	[Export] public float RiseSpeed = 70f;
	[Export] public float MaxSink = 110f;
	[Export] public Color RootColor = new(0.36f, 0.25f, 0.14f, 1f);

	private Vector2 _home;
	private float _sink;
	private float _standTime;
	private Area2D _sensor;

	public override void _Ready()
	{
		_home = Position;
		SyncToPhysics = true;
		AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Width, Thickness) },
			Position = new Vector2(0f, Thickness / 2f),
		});
		_sensor = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_sensor.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Width - 8f, 10f) },
			Position = new Vector2(0f, -5f),
		});
		AddChild(_sensor);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (IsWeighed())
		{
			_standTime += dt;
			if (_standTime >= Delay)
				_sink = Mathf.Min(MaxSink, _sink + SinkSpeed * dt);
		}
		else
		{
			_standTime = 0f;
			_sink = Mathf.Max(0f, _sink - RiseSpeed * dt);
		}
		Position = _home + new Vector2(0f, _sink);
	}

	private bool IsWeighed()
	{
		foreach (Node2D body in _sensor.GetOverlappingBodies())
			if (body is CharacterBody2D character && character.IsInGroup("player") && character.IsOnFloor())
				return true;
		return false;
	}

	public override void _Draw()
	{
		float half = Width / 2f;
		DrawRect(new Rect2(-half, 0f, Width, Thickness), RootColor);
		DrawRect(new Rect2(-half, Thickness - 6f, Width, 6f), RootColor.Darkened(0.35f));
		// A couple of knots and dangling rootlets so it reads as a root, not a plank.
		DrawCircle(new Vector2(-half * 0.45f, Thickness * 0.4f), 4f, RootColor.Lightened(0.15f));
		DrawCircle(new Vector2(half * 0.3f, Thickness * 0.55f), 3f, RootColor.Lightened(0.15f));
		DrawLine(new Vector2(-half * 0.6f, Thickness), new Vector2(-half * 0.65f, Thickness + 12f), RootColor.Darkened(0.2f), 2f);
		DrawLine(new Vector2(half * 0.5f, Thickness), new Vector2(half * 0.55f, Thickness + 9f), RootColor.Darkened(0.2f), 2f);
	}
}
