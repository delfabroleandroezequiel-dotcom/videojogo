using Godot;

namespace Metroidvania.World;

// Ola de lava (Las Lavas, "El Crisol que vuelca"): a huge crucible hanging at the node tilts and
// pours, and a wave of lava rolls Length px along the floor (Direction +1 = right). The wave is a
// band WaveHeight tall: anything standing higher (a refuge shelf, a crust) lets it pass under.
// Rhythm: Interval quiet → WarningTime the crucible creaks and tips, glowing spills over its lip
// (the tell) → the wave travels at Speed → it drains at the far end. Hurts (Damage + shove along the
// wave), never kills outright. Node origin = the crucible's pivot; the wave starts at
// (0, FloorY) relative to it. Drawn in code: rough iron crucible, wave = noisy crest + glow.
public partial class OlaDeLava : Node2D
{
	[Export] public float Length = 2000f;
	[Export] public float FloorY = 300f;
	[Export] public float WaveHeight = 110f;
	[Export] public float WaveWidth = 140f;
	[Export] public float Speed = 420f;
	[Export] public int Direction = 1;
	[Export] public float Interval = 3.5f;
	[Export] public float WarningTime = 1.2f;
	[Export] public float StartOffset;
	[Export] public int Damage = 35;
	[Export] public float KnockbackForce = 520f;
	[Export] public Color LavaColor = new(1f, 0.42f, 0.1f, 1f);

	private enum Phase { Quiet, Tipping, Rolling }

	private Phase _phase = Phase.Quiet;
	private float _timer;
	private float _front;
	private float _tilt;
	private Hazard _hazard;
	private CollisionShape2D _shape;
	private CpuParticles2D _spill;
	private float _time;

	public bool IsRolling => _phase == Phase.Rolling;
	public float FrontX => _front;

	public override void _Ready()
	{
		_hazard = Hazard.CreateArea(this, false, Damage, KnockbackForce);
		_hazard.HitWhileOverlapping = true;
		_shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(WaveWidth, WaveHeight) } };
		_hazard.AddChild(_shape);
		_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);

		_spill = new CpuParticles2D
		{
			Emitting = false,
			Amount = 40,
			Lifetime = 0.7f,
			Position = new Vector2(Direction * 46f, 10f),
			Direction = new Vector2(Direction, 0.6f),
			Spread = 18f,
			Gravity = new Vector2(0f, 900f),
			InitialVelocityMin = 80f,
			InitialVelocityMax = 180f,
			ScaleAmountMin = 3f,
			ScaleAmountMax = 6f,
			Color = LavaColor,
		};
		AddChild(_spill);
		_timer = Interval - StartOffset;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_timer -= dt;
		switch (_phase)
		{
			case Phase.Quiet:
				_tilt = Mathf.MoveToward(_tilt, 0f, dt * 1.2f);
				if (_timer <= 0f)
				{
					_phase = Phase.Tipping;
					_timer = WarningTime;
				}
				break;
			case Phase.Tipping:
				_tilt = Mathf.MoveToward(_tilt, 0.85f, dt * 0.85f / WarningTime);
				_spill.Emitting = _timer < WarningTime * 0.6f;
				if (_timer <= 0f)
				{
					_phase = Phase.Rolling;
					_front = 0f;
					_hazard.SetDeferred(Area2D.PropertyName.Monitoring, true);
				}
				break;
			case Phase.Rolling:
				_front += Speed * dt;
				_spill.Emitting = _front < Length * 0.25f;
				if (_front >= Length + WaveWidth)
				{
					_phase = Phase.Quiet;
					_timer = Interval;
					_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);
					_spill.Emitting = false;
				}
				break;
		}
		float cx = Direction * (Mathf.Min(_front, Length) - WaveWidth / 2f);
		_shape.Position = new Vector2(cx, FloorY - WaveHeight / 2f);
		QueueRedraw();
	}

	public override void _Draw()
	{
		// Crucible: a heavy iron bowl tilting toward Direction.
		DrawSetTransform(Vector2.Zero, _tilt * Direction, Vector2.One);
		var bowl = new Vector2[] { new(-60f, -30f), new(60f, -30f), new(48f, 30f), new(26f, 46f), new(-26f, 46f), new(-48f, 30f) };
		DrawColoredPolygon(bowl, new Color(0.14f, 0.12f, 0.12f, 1f));
		DrawRect(new Rect2(-56f, -30f, 112f, 10f), new Color(LavaColor, 0.9f));
		DrawLine(new Vector2(-66f, -36f), new Vector2(66f, -36f), new Color(0.3f, 0.26f, 0.24f, 1f), 6f);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		DrawLine(new Vector2(-70f, 0f), new Vector2(-70f, -400f), new Color(0.2f, 0.17f, 0.16f, 1f), 5f);
		DrawLine(new Vector2(70f, 0f), new Vector2(70f, -400f), new Color(0.2f, 0.17f, 0.16f, 1f), 5f);

		if (_phase != Phase.Rolling)
			return;
		// The wave: a band from where it's drained to the front, crest uneven and boiling.
		float tail = Mathf.Max(0f, _front - Length * 0.35f);
		float head = Mathf.Min(_front, Length);
		int n = 18;
		var pts = new Vector2[n + 3];
		for (int i = 0; i <= n; i++)
		{
			float x = Mathf.Lerp(tail, head, i / (float)n);
			float k = i / (float)n;
			float h = WaveHeight * Mathf.Sin(k * Mathf.Pi * 0.5f) * (0.85f + 0.15f * Mathf.Sin(_time * 9f + i * 1.7f));
			pts[i] = new Vector2(Direction * x, FloorY - h);
		}
		pts[n + 1] = new Vector2(Direction * head, FloorY);
		pts[n + 2] = new Vector2(Direction * tail, FloorY);
		DrawColoredPolygon(pts, new Color(LavaColor.Darkened(0.25f), 0.95f));
		var crest = new Vector2[n + 1];
		System.Array.Copy(pts, crest, n + 1);
		DrawPolyline(crest, new Color(1f, 0.85f, 0.45f, 1f), 3f);
		DrawPolyline(crest, new Color(LavaColor, 0.35f), 10f);
	}
}
