using Godot;

namespace Metroidvania.World;

// A shadow hit: the opposite of the sword's white neon pop. Black ink splatters out of the contact
// point (normal blend — additive can't draw darkness) with violet rim sparks and a violet ring on top
// (additive child). Scale 0.6 = a shadow mark landing; ~2+ = a detonation. Scaled time, so the
// hit-stop of a detonation holds the splash mid-burst.
public partial class ShadowImpact : Node2D
{
	private struct Blob
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Radius;
	}

	public float Size = 1f;
	public Color InkColor = new(0.03f, 0.01f, 0.05f, 1f);
	public Color RimColor = new(0.72f, 0.38f, 1f, 1f);

	private const float Life = 0.45f;
	private float _age;
	private Blob[] _blobs;
	private Node2D _glow;

	public override void _Ready()
	{
		ZIndex = 24;
		int count = Mathf.RoundToInt(10f * Size);
		_blobs = new Blob[count];
		for (int i = 0; i < count; i++)
		{
			Vector2 dir = Vector2.FromAngle((float)GD.RandRange(0, Mathf.Tau));
			_blobs[i] = new Blob
			{
				Position = dir * (float)GD.RandRange(0, 6.0) * Size,
				Velocity = dir * (float)GD.RandRange(80.0, 260.0) * Mathf.Sqrt(Size),
				Radius = (float)GD.RandRange(3.0, 8.0) * Mathf.Sqrt(Size),
			};
		}
		_glow = new GlowLayer { Owner_ = this };
		AddChild(_glow);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		if (_age >= Life)
		{
			QueueFree();
			return;
		}
		for (int i = 0; i < _blobs.Length; i++)
		{
			ref Blob b = ref _blobs[i];
			b.Velocity *= 1f - 5f * dt;
			b.Velocity.Y += 120f * dt;
			b.Position += b.Velocity * dt;
		}
		QueueRedraw();
		_glow.QueueRedraw();
	}

	public override void _Draw()
	{
		float t = 1f - _age / Life;
		DrawCircle(Vector2.Zero, 12f * Size * t, InkColor with { A = 0.85f * t });
		foreach (Blob b in _blobs)
		{
			// Ink stretches along its flight, like a splash, and shrinks as it dries.
			Vector2 tail = b.Position - b.Velocity * 0.03f;
			DrawLine(tail, b.Position, InkColor with { A = 0.9f * t }, b.Radius * 1.6f * t);
			DrawCircle(b.Position, b.Radius * t, InkColor with { A = 0.9f * t });
		}
	}

	private void DrawGlow(CanvasItem canvas)
	{
		float t = 1f - _age / Life;
		float ringT = Mathf.Min(1f, _age / (Life * 0.7f));
		float radius = Mathf.Lerp(6f, 34f * Size, 1f - (1f - ringT) * (1f - ringT));
		canvas.DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 36, RimColor with { A = 0.8f * (1f - ringT) }, 2f * Size * (1f - ringT) + 0.5f);
		foreach (Blob b in _blobs)
			canvas.DrawCircle(b.Position, b.Radius * 0.35f * t, RimColor with { A = 0.7f * t });
	}

	// The violet parts, drawn additively on a child so the ink underneath stays truly dark.
	private partial class GlowLayer : Node2D
	{
		public ShadowImpact Owner_;

		public override void _Ready()
		{
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
			ZIndex = 1;
		}

		public override void _Draw() => Owner_?.DrawGlow(this);
	}

	public static void SpawnAt(Node context, Vector2 globalPosition, float size = 1f)
	{
		if (context.GetTree().CurrentScene is not Node scene)
			return;
		var impact = new ShadowImpact { Size = size };
		scene.AddChild(impact);
		impact.GlobalPosition = globalPosition;
	}
}
