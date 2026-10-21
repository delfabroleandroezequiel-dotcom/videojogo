using Godot;

namespace Metroidvania.World;

// Procedural directional hit sparks for the player's sword hits: neon streaks that fly out the way
// the blade was travelling (not a centered radial pop like HitImpactBurst), plus one bright slash
// streak across the contact point. Drawn in code with additive blending — same "own code FX" kit
// as the enemy attacks, no sprite sheets. Runs on scaled time, so a hit-stop freeze holds the burst
// mid-air and it bursts out the moment time resumes.
public partial class HitSparks : Node2D
{
	private struct Spark
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Life;
		public float MaxLife;
		public float Length;
		public float Width;
	}

	public Vector2 Direction = Vector2.Right;
	// 1 = a normal hit; the finisher and killing blows pass more (more, longer, faster sparks).
	public float Intensity = 1f;
	public Color GlowColor = new(0.35f, 0.9f, 1f, 1f);
	public Color CoreColor = new(1f, 1f, 1f, 1f);

	private const float Gravity = 520f;
	private const float SpreadRadians = 0.6f;

	private Spark[] _sparks;
	private float _streakLife;
	private float _streakMaxLife;
	private float _streakLength;

	public override void _Ready()
	{
		Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		ZIndex = 20;

		Vector2 dir = Direction == Vector2.Zero ? Vector2.Right : Direction.Normalized();
		int count = Mathf.RoundToInt(10f * Intensity);
		_sparks = new Spark[count];
		for (int i = 0; i < count; i++)
		{
			float angle = (float)GD.RandRange(-SpreadRadians, SpreadRadians);
			// A few strays kick backwards/up so it reads as a burst, not a cone.
			if (i % 6 == 5)
				angle += Mathf.Pi * (float)GD.RandRange(0.55, 0.8) * (GD.Randf() < 0.5f ? 1f : -1f);
			float speed = (float)GD.RandRange(260.0, 560.0) * Mathf.Lerp(1f, 1.35f, Mathf.Clamp(Intensity - 1f, 0f, 1f));
			float life = (float)GD.RandRange(0.16, 0.34) * Mathf.Lerp(1f, 1.4f, Mathf.Clamp(Intensity - 1f, 0f, 1f));
			_sparks[i] = new Spark
			{
				Position = dir * (float)GD.RandRange(0.0, 6.0),
				Velocity = dir.Rotated(angle) * speed,
				Life = life,
				MaxLife = life,
				Length = (float)GD.RandRange(8.0, 18.0) * Mathf.Sqrt(Intensity),
				Width = (float)GD.RandRange(1.0, 2.0),
			};
		}

		_streakMaxLife = _streakLife = 0.1f + 0.04f * Intensity;
		_streakLength = 34f * Mathf.Sqrt(Intensity);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		bool alive = false;
		for (int i = 0; i < _sparks.Length; i++)
		{
			ref Spark s = ref _sparks[i];
			if (s.Life <= 0f)
				continue;
			s.Life -= dt;
			s.Velocity.Y += Gravity * dt;
			s.Velocity *= 1f - 3f * dt;
			s.Position += s.Velocity * dt;
			alive |= s.Life > 0f;
		}
		_streakLife -= dt;
		if (!alive && _streakLife <= 0f)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 dir = Direction == Vector2.Zero ? Vector2.Right : Direction.Normalized();
		if (_streakLife > 0f)
		{
			float t = _streakLife / _streakMaxLife;
			// The slash streak crosses the contact point perpendicular-ish to the travel direction,
			// skewed forward, and shrinks as it fades.
			Vector2 axis = dir.Rotated(-1.1f);
			Vector2 half = axis * _streakLength * (0.6f + 0.4f * t);
			DrawLine(-half, half, GlowColor with { A = 0.5f * t }, 7f * t);
			DrawLine(-half, half, CoreColor with { A = t }, 2.5f * t);
		}

		foreach (Spark s in _sparks)
		{
			if (s.Life <= 0f)
				continue;
			float t = s.Life / s.MaxLife;
			Vector2 tail = s.Position - s.Velocity.Normalized() * s.Length * t;
			DrawLine(tail, s.Position, GlowColor with { A = 0.55f * t }, s.Width * 3f);
			DrawLine(tail, s.Position, CoreColor with { A = t }, s.Width);
		}
	}

	public static void SpawnAt(Node context, Vector2 globalPosition, Vector2 direction, float intensity = 1f)
	{
		if (context.GetTree().CurrentScene is not Node scene)
			return;
		var sparks = new HitSparks { Direction = direction, Intensity = intensity };
		scene.AddChild(sparks);
		sparks.GlobalPosition = globalPosition;
	}
}
