using Godot;

namespace Metroidvania.World;

// Poison cloud left on a Gigante hole (AgujeroGigante.Gas): a child of the hole, so it rides the
// body. Swells in, churns, poisons the player while they're inside (re-applied every second, same
// ApplyPoison contract as PoisonPool), then fades. Radio is in the hole's parent-part pixels.
public partial class GasAgujero : Area2D
{
	[Export] public float Radio = 170f;
	[Export] public float Duracion = 5f;
	[Export] public int TickDamage = 4;
	[Export] public float TickInterval = 0.5f;
	[Export] public float PoisonDuration = 2.5f;

	private Node2D _visual;
	private Metroidvania.Player.Player _inside;
	private float _reapply;
	private float _time;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 2;
		Monitorable = false;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radio } });
		BodyEntered += b => { if (b is Metroidvania.Player.Player p) { _inside = p; _reapply = 0f; } };
		BodyExited += b => { if (b == _inside) _inside = null; };

		_visual = new Node2D { Scale = Vector2.Zero };
		AddChild(_visual);
		// A few overlapping blobs, drifting at their own pace, read as a churning cloud.
		for (int i = 0; i < 6; i++)
		{
			Color c = i % 2 == 0 ? new Color(0.45f, 0.9f, 0.3f, 0.32f) : new Color(0.6f, 0.35f, 0.85f, 0.28f);
			Polygon2D blob = NeonFx.Circle(Radio * (float)GD.RandRange(0.45, 0.7), c, additive: true);
			blob.Position = Vector2.FromAngle(i * Mathf.Tau / 6f) * Radio * 0.35f;
			_visual.AddChild(blob);
		}

		Tween t = CreateTween();
		t.TweenProperty(_visual, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenInterval(Mathf.Max(0f, Duracion - 1.2f));
		t.TweenProperty(_visual, "modulate:a", 0f, 0.8f);
		t.TweenCallback(Callable.From(QueueFree));
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		int i = 0;
		foreach (Node child in _visual.GetChildren())
		{
			if (child is Node2D blob)
				blob.Rotation = _time * (0.4f + 0.15f * i);
			i++;
		}
		_visual.Rotation = Mathf.Sin(_time * 0.7f) * 0.2f;

		if (_inside is null || _visual.Modulate.A < 0.3f)
			return;
		_reapply -= dt;
		if (_reapply <= 0f)
		{
			_reapply = 1f;
			_inside.ApplyPoison(TickDamage, TickInterval, PoisonDuration);
		}
	}
}
