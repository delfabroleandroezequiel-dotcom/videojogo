using Godot;

namespace Metroidvania.World;

// A tiny red "humanity" shed off the berserker's blade trail: the same little Dark Souls figure as
// HumanityOrb (round head, teardrop body trailing into two wisps, two eyes) but a few pixels tall,
// with a frayed red neon outline that re-jitters constantly. It's flung off the cut, slows, drifts
// up wobbling and leaning, flickers, then shrinks and fades. Drawn in code (dark fill normal blend,
// glow on an additive child) so a swing can scatter several without the orb's Line2D/particle cost.
public partial class MicroHumanity : Node2D
{
	public Vector2 Velocity;
	public float Life = 0.9f;
	public float Size = 0.4f;
	public Color Glow = new(1f, 0.15f, 0.2f, 1f);
	public Color Fill = new(0.03f, 0f, 0.02f, 0.92f);

	// HumanityOrb's body outline (head drawn separately), in the figure's own units.
	private static readonly Vector2[] Body =
	{
		new(-4f, -13f), new(-11f, -8f), new(-13f, 0f), new(-11f, 10f), new(-7f, 20f), new(-4f, 30f),
		new(-2f, 36f), new(0f, 28f), new(2f, 36f), new(4f, 30f), new(7f, 20f), new(11f, 10f),
		new(13f, 0f), new(11f, -8f), new(4f, -13f),
	};
	private static readonly Vector2 HeadCenter = new(0f, -23f);
	private const float HeadRadius = 9f;

	private float _age;
	private float _frayTimer;
	private float _phase;
	private Vector2[] _bodyFrayed = new Vector2[Body.Length];
	private Vector2[] _headFrayed = new Vector2[14];
	private GlowLayer _glowLayer;

	public override void _Ready()
	{
		ZIndex = 23;
		Scale = Vector2.One * Size;
		_phase = (float)GD.RandRange(0, Mathf.Tau);
		Fray();
		_glowLayer = new GlowLayer { Owner_ = this };
		AddChild(_glowLayer);
	}

	private void Fray()
	{
		for (int i = 0; i < Body.Length; i++)
			_bodyFrayed[i] = Body[i] + new Vector2((float)GD.RandRange(-1.6, 1.6), (float)GD.RandRange(-1.6, 1.6));
		for (int i = 0; i < _headFrayed.Length; i++)
			_headFrayed[i] = HeadCenter + Vector2.Right.Rotated(Mathf.Tau * i / _headFrayed.Length) * (HeadRadius + (float)GD.RandRange(-1.2, 1.2));
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
		// Flung, braking hard, then rising on its own with a nervous sway.
		Velocity *= 1f - 4f * dt;
		Velocity.Y -= 40f * dt;
		Position += (Velocity + new Vector2(Mathf.Sin(_age * 9f + _phase) * 14f, 0f)) * dt;
		Rotation = Mathf.Clamp(Velocity.X / 200f, -0.6f, 0.6f) + Mathf.Sin(_age * 7f + _phase) * 0.15f;
		float t = _age / Life;
		Scale = Vector2.One * Size * (t < 0.15f ? Mathf.Lerp(0.4f, 1f, t / 0.15f) : Mathf.Lerp(1f, 0.55f, (t - 0.15f) / 0.85f));

		_frayTimer -= dt;
		if (_frayTimer <= 0f)
		{
			_frayTimer = 0.04f;
			Fray();
		}
		QueueRedraw();
		_glowLayer.QueueRedraw();
	}

	private float Alpha()
	{
		float t = _age / Life;
		float flicker = 0.8f + 0.2f * Mathf.Sin(_age * 30f + _phase);
		return (t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f) * flicker;
	}

	public override void _Draw()
	{
		float a = Alpha();
		DrawColoredPolygon(_bodyFrayed, Fill with { A = Fill.A * a });
		DrawColoredPolygon(_headFrayed, Fill with { A = Fill.A * a });
	}

	private void DrawGlow(CanvasItem canvas)
	{
		float a = Alpha();
		var body = new Vector2[_bodyFrayed.Length + 1];
		_bodyFrayed.CopyTo(body, 0);
		body[^1] = _bodyFrayed[0];
		var head = new Vector2[_headFrayed.Length + 1];
		_headFrayed.CopyTo(head, 0);
		head[^1] = _headFrayed[0];
		// Soft outer glow, then the bright frayed edge.
		canvas.DrawPolyline(body, Glow with { A = 0.25f * a }, 7f);
		canvas.DrawPolyline(head, Glow with { A = 0.25f * a }, 7f);
		canvas.DrawPolyline(body, Glow with { A = 0.95f * a }, 2.2f);
		canvas.DrawPolyline(head, Glow with { A = 0.95f * a }, 2.2f);
		foreach (float x in new[] { -3.5f, 3.5f })
			canvas.DrawCircle(new Vector2(x, -24f), 1.8f, new Color(1f, 0.9f, 0.85f, a));
		canvas.DrawCircle(new Vector2(0f, -4f), 26f, Glow with { A = 0.06f * a });
	}

	private partial class GlowLayer : Node2D
	{
		public MicroHumanity Owner_;

		public override void _Ready() => Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

		public override void _Draw() => Owner_?.DrawGlow(this);
	}

	public static void SpawnAt(Node context, Vector2 globalPosition, Vector2 velocity, float size, Color glow)
	{
		if (context.GetTree().CurrentScene is not Node scene)
			return;
		var h = new MicroHumanity
		{
			Velocity = velocity,
			Size = size,
			Glow = glow,
			Life = (float)GD.RandRange(0.7, 1.1),
		};
		scene.AddChild(h);
		h.GlobalPosition = globalPosition;
	}
}
