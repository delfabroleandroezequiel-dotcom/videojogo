using Godot;

namespace Metroidvania.World;

// Procedural parry clash at the contact point, replacing the old lingering bubble: a star of neon
// rays snapping out of the blade contact, a thin shockwave ring, and — the readable part — motes of
// shadow essence pulled from the clash into the player, so filling the essence bar is something you
// see happen. A perfect parry goes bigger: more and longer rays, a white-hot core, more motes.
// Drawn in code with additive blending, same kit as HitSparks. Runs on scaled time, so the parry's
// hit-stop holds the flash at full size for a beat before it bursts.
public partial class ParryClash : Node2D
{
	private struct Mote
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Delay;
		public bool Absorbed;
	}

	public bool Perfect;
	public Vector2 Facing = Vector2.Right;
	// Motes home in on this node (the player); null = they just scatter and fade.
	public Node2D Absorber;
	public Color NormalColor = new(0.35f, 0.9f, 1f, 1f);
	public Color PerfectColor = new(0.75f, 0.45f, 1f, 1f);
	public Color EssenceColor = new(0.7f, 0.35f, 1f, 1f);

	private const float RayLife = 0.16f;
	private const float RingLife = 0.24f;
	private const float MoteMaxLife = 0.9f;

	private float _age;
	private float[] _rayAngles;
	private float[] _rayLengths;
	private Mote[] _motes;

	public override void _Ready()
	{
		Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		ZIndex = 25;

		int rays = Perfect ? 8 : 5;
		_rayAngles = new float[rays];
		_rayLengths = new float[rays];
		float baseAngle = Facing.Angle();
		for (int i = 0; i < rays; i++)
		{
			// Evenly spread with jitter, biased toward the facing side (where the blow came from).
			float a = baseAngle + Mathf.Tau * i / rays + (float)GD.RandRange(-0.25, 0.25);
			_rayAngles[i] = a;
			float facingBias = 0.6f + 0.4f * Mathf.Max(0f, Mathf.Cos(a - baseAngle));
			_rayLengths[i] = (Perfect ? 46f : 28f) * facingBias * (float)GD.RandRange(0.75, 1.15);
		}

		int motes = Perfect ? 14 : 6;
		_motes = new Mote[motes];
		for (int i = 0; i < motes; i++)
		{
			_motes[i] = new Mote
			{
				Position = Vector2.Zero,
				Velocity = Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau)) * (float)GD.RandRange(80.0, 200.0),
				Delay = (float)GD.RandRange(0.0, 0.08),
			};
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;

		bool motesAlive = false;
		for (int i = 0; i < _motes.Length; i++)
		{
			ref Mote m = ref _motes[i];
			if (m.Absorbed)
				continue;
			if (_age < m.Delay)
			{
				motesAlive = true;
				continue;
			}
			if (IsInstanceValid(Absorber))
			{
				// Burst outward, then curl into the absorber with growing pull.
				Vector2 target = ToLocal(Absorber.GlobalPosition + new Vector2(0f, -12f));
				Vector2 toTarget = target - m.Position;
				float pull = Mathf.Lerp(200f, 2600f, Mathf.Clamp((_age - m.Delay) / 0.45f, 0f, 1f));
				m.Velocity += toTarget.Normalized() * pull * dt;
				m.Velocity *= 1f - 2.5f * dt;
				if (toTarget.Length() < 10f)
					m.Absorbed = true;
			}
			else
			{
				m.Velocity *= 1f - 3f * dt;
			}
			m.Position += m.Velocity * dt;
			if (_age > MoteMaxLife)
				m.Absorbed = true;
			motesAlive |= !m.Absorbed;
		}

		if (_age > RingLife && !motesAlive)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		Color color = Perfect ? PerfectColor : NormalColor;

		if (_age < RayLife)
		{
			float t = 1f - _age / RayLife;
			float grow = Mathf.Min(1f, _age / 0.03f + 0.35f);
			for (int i = 0; i < _rayAngles.Length; i++)
			{
				Vector2 tip = Vector2.FromAngle(_rayAngles[i]) * _rayLengths[i] * grow;
				DrawLine(Vector2.Zero, tip, color with { A = 0.5f * t }, (Perfect ? 7f : 5f) * t);
				DrawLine(Vector2.Zero, tip, Colors.White with { A = t }, (Perfect ? 2.5f : 1.8f) * t);
			}
			// Hot core: white disc, bigger on a perfect.
			DrawCircle(Vector2.Zero, (Perfect ? 9f : 5f) * t, Colors.White with { A = t });
			DrawCircle(Vector2.Zero, (Perfect ? 16f : 10f) * t, color with { A = 0.35f * t });
		}

		if (_age < RingLife)
		{
			float t = _age / RingLife;
			float radius = Mathf.Lerp(6f, Perfect ? 58f : 36f, 1f - (1f - t) * (1f - t));
			DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 40, color with { A = 0.8f * (1f - t) }, (Perfect ? 3f : 2f) * (1f - t) + 0.5f);
		}

		foreach (Mote m in _motes)
		{
			if (m.Absorbed || _age < m.Delay)
				continue;
			Vector2 tail = m.Position - m.Velocity * 0.025f;
			DrawLine(tail, m.Position, EssenceColor with { A = 0.6f }, 4f);
			DrawLine(tail, m.Position, Colors.White with { A = 0.9f }, 1.5f);
		}
	}

	public static void SpawnAt(Node context, Vector2 globalPosition, Vector2 facing, bool perfect, Node2D absorber)
	{
		if (context.GetTree().CurrentScene is not Node scene)
			return;
		var clash = new ParryClash { Perfect = perfect, Facing = facing, Absorber = absorber };
		scene.AddChild(clash);
		clash.GlobalPosition = globalPosition;
	}
}
