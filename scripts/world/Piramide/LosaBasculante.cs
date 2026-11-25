using Godot;

namespace Metroidvania.World;

// Losa basculante (La Pirámide): a floor slab on a hinge. Its top is the node's y. Stand on it and
// after Delay (it creaks and shudders: the tell) it swings down around its Hinge side and you drop
// through to whatever the room put below (a pit with spikes, a lower corridor, a secret). It swings
// back up after OpenTime, and only solidifies again once nobody is inside its outline.
// Disimulada = drawn with the same colour as the surrounding greybox floor and no seams (a false
// floor); off = a visible seam and darker joints, so a careful player can read it.
public partial class LosaBasculante : Node2D
{
	[Export] public float Width = 120f;
	[Export] public float Thickness = 24f;
	[Export] public float Delay = 0.35f;
	[Export] public float OpenTime = 1.8f;
	[Export] public int Hinge = -1; // -1 = hinge on the left edge, +1 = right
	[Export] public bool Disimulada;
	[Export] public Color FloorColor = new(0.55f, 0.46f, 0.31f, 1f);

	private enum Phase { Closed, Creaking, Open, Closing }

	private Phase _phase = Phase.Closed;
	private float _timer;
	private float _angle; // degrees, 0 closed, 82 open
	private StaticBody2D _body;
	private CollisionShape2D _collision;
	private Area2D _top;
	private Area2D _inside;
	private float _time;

	public override void _Ready()
	{
		ZIndex = 0;
		_body = new StaticBody2D();
		_collision = new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Width, Thickness) },
			Position = new Vector2(0f, Thickness / 2f),
		};
		_body.AddChild(_collision);
		AddChild(_body);
		_top = MakeArea(new Vector2(Width - 10f, 10f), new Vector2(0f, -6f));
		_inside = MakeArea(new Vector2(Width, Thickness + 70f), new Vector2(0f, -30f));
	}

	private Area2D MakeArea(Vector2 size, Vector2 pos)
	{
		var a = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitoring = true };
		a.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Position = pos });
		AddChild(a);
		return a;
	}

	private bool PlayerIn(Area2D a)
	{
		foreach (Node2D b in a.GetOverlappingBodies())
			if (b.IsInGroup("player"))
				return true;
		return false;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_timer -= dt;
		switch (_phase)
		{
			case Phase.Closed:
				if (PlayerIn(_top))
				{
					_phase = Phase.Creaking;
					_timer = Delay;
				}
				break;
			case Phase.Creaking:
				if (_timer <= 0f)
				{
					_phase = Phase.Open;
					_timer = OpenTime;
					_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
					Metroidvania.Shared.Sfx.PlayIfExistsAt(this, "World/Doors", "Portcullis Gate");
				}
				break;
			case Phase.Open:
				_angle = Mathf.MoveToward(_angle, 82f, dt * 700f);
				if (_timer <= 0f && !PlayerIn(_inside))
				{
					_phase = Phase.Closing;
					_timer = 0.4f;
				}
				break;
			case Phase.Closing:
				_angle = Mathf.MoveToward(_angle, 0f, dt * 82f / 0.4f);
				if (_angle <= 0f)
				{
					if (PlayerIn(_inside))
					{
						_phase = Phase.Open;
						_timer = 0.3f;
						break;
					}
					_phase = Phase.Closed;
					_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
				}
				break;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		float shake = _phase == Phase.Creaking ? Mathf.Sin(_time * 80f) * 1.5f : 0f;
		Vector2 pivot = new(Hinge * Width / 2f, 0f);
		float rad = Mathf.DegToRad(_angle) * -Hinge;
		DrawSetTransform(pivot + new Vector2(0f, shake), rad, Vector2.One);
		var rect = new Rect2(-pivot.X - Width / 2f, 0f, Width, Thickness);
		DrawRect(rect, Disimulada ? FloorColor : FloorColor.Darkened(0.12f));
		if (!Disimulada)
		{
			DrawRect(new Rect2(rect.Position, new Vector2(Width, 3f)), FloorColor.Lightened(0.15f));
			DrawLine(rect.Position, rect.Position + new Vector2(0f, Thickness), FloorColor.Darkened(0.4f), 2f);
			DrawLine(rect.End - new Vector2(0f, Thickness), rect.End, FloorColor.Darkened(0.4f), 2f);
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
