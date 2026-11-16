using Godot;

namespace Metroidvania.World;

// Araña colgada (Las Raíces): a spider hanging from its silk under a root. When the player
// passes underneath it drops fast to DropLength, waits a beat at the bottom, then climbs back
// up slowly — a timing hazard for whoever walks or swings below. It hurts on contact (not an
// instant kill). The node is where the silk is anchored. Drawn in code (greybox, no art yet).
public partial class AranaColgante : Node2D
{
	[Export] public float RestLength = 40f;
	[Export] public float DropLength = 220f;
	// Horizontal half-width under the anchor that triggers the drop.
	[Export] public float TriggerHalfWidth = 60f;
	[Export] public float DropSpeed = 520f;
	[Export] public float ClimbSpeed = 70f;
	[Export] public float WaitAtBottom = 0.9f;
	[Export] public int Damage = 15;
	[Export] public Color BodyColor = new(0.12f, 0.1f, 0.12f, 1f);

	private enum State { Waiting, Dropping, Bottom, Climbing }

	private State _state = State.Waiting;
	private float _length;
	private float _timer;
	private Node2D _body;

	public override void _Ready()
	{
		_length = RestLength;
		_body = new Node2D { Name = "Cuerpo" };
		AddChild(_body);
		Hazard hazard = Hazard.CreateArea(_body, false, Damage, 220f);
		hazard.HitWhileOverlapping = true;
		hazard.SetPhysicsProcess(true); // CreateArea already ran Hazard._Ready with it off
		hazard.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 11f } });
		_body.Position = new Vector2(0f, _length);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		switch (_state)
		{
			case State.Waiting:
				if (PlayerBelow())
					_state = State.Dropping;
				break;
			case State.Dropping:
				_length = Mathf.MoveToward(_length, DropLength, DropSpeed * dt);
				if (_length >= DropLength)
				{
					_state = State.Bottom;
					_timer = WaitAtBottom;
				}
				break;
			case State.Bottom:
				_timer -= dt;
				if (_timer <= 0f)
					_state = State.Climbing;
				break;
			case State.Climbing:
				_length = Mathf.MoveToward(_length, RestLength, ClimbSpeed * dt);
				if (_length <= RestLength)
					_state = State.Waiting;
				break;
		}
		_body.Position = new Vector2(0f, _length);
		QueueRedraw();
	}

	private bool PlayerBelow()
	{
		if (EnemyTargeting.Current(GetTree()) is not Node2D player)
			return false;
		Vector2 d = player.GlobalPosition - GlobalPosition;
		return Mathf.Abs(d.X) <= TriggerHalfWidth && d.Y > 0f && d.Y < DropLength + 120f;
	}

	public override void _Draw()
	{
		Vector2 c = new(0f, _length);
		DrawLine(Vector2.Zero, c, new Color(0.85f, 0.85f, 0.9f, 0.55f), 1f);
		for (int side = -1; side <= 1; side += 2)
		{
			for (int i = 0; i < 4; i++)
			{
				float a = -0.6f + i * 0.4f;
				Vector2 knee = c + new Vector2(side * 10f, -6f + i * 4f + a * 4f);
				Vector2 foot = knee + new Vector2(side * 7f, 8f);
				DrawLine(c, knee, BodyColor, 2f);
				DrawLine(knee, foot, BodyColor, 2f);
			}
		}
		DrawCircle(c, 9f, BodyColor);
		DrawCircle(c + new Vector2(0f, -8f), 5f, BodyColor.Lightened(0.1f));
		DrawCircle(c + new Vector2(-2f, -9f), 1.3f, new Color(1f, 0.15f, 0.1f));
		DrawCircle(c + new Vector2(2f, -9f), 1.3f, new Color(1f, 0.15f, 0.1f));
	}
}
