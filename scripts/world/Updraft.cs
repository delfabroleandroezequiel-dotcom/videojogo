using Godot;

namespace Metroidvania.World;

// A column of wind: anything inside is pushed toward TargetVerticalSpeed. Negative lifts (stand in
// it and you rise to the top, then bob there and drift out sideways); a small positive value
// instead turns a long drop into a slow, steerable glide. The node's origin is the bottom-centre
// of the column, which rises Height pixels up. Drawn in code: faint streaks rising inside a soft
// neon haze. [Tool] so the column previews live in the editor.
[Tool]
public partial class Updraft : Node2D
{
	private float _width = 80f;
	private float _height = 300f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(16f, value); QueueRedraw(); }
	}

	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(16f, value); QueueRedraw(); }
	}

	// Vertical speed the wind pulls bodies toward (px/s): -260 lifts steadily, +80 is a slow fall.
	[Export] public float TargetVerticalSpeed = -260f;
	// How hard it pulls toward that speed (px/s²). Must beat the player's falling gravity
	// (900 × 1.7 ≈ 1530) to stop a fall, so leave it well above that.
	[Export] public float Acceleration = 2800f;
	[Export] public bool AffectsEnemies;
	[Export] public Color NeonColor = new(0.6f, 0.95f, 1f, 1f);

	private Area2D _area;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		Build();
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		var rect = new Rect2(-_width / 2f, -_height, _width, _height);
		DrawRect(rect, NeonColor with { A = 0.12f });
		DrawRect(rect, NeonColor with { A = 0.6f }, false, 2f);
		float dir = TargetVerticalSpeed < 0f ? -1f : 1f;
		for (int i = 1; i < 4; i++)
		{
			Vector2 c = new(0f, -_height * i / 4f);
			DrawLine(c - new Vector2(10f, 10f * dir), c, NeonColor, 2f);
			DrawLine(c + new Vector2(10f, -10f * dir), c, NeonColor, 2f);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float step = Acceleration * (float)delta;
		foreach (Node2D body in _area.GetOverlappingBodies())
		{
			if (body is not CharacterBody2D character)
				continue;
			if (!character.IsInGroup("player") && !(AffectsEnemies && character.IsInGroup("enemy")))
				continue;
			Vector2 velocity = character.Velocity;
			velocity.Y = Mathf.MoveToward(velocity.Y, TargetVerticalSpeed, step);
			character.Velocity = velocity;
		}
	}

	private void Build()
	{
		_area = new Area2D { CollisionLayer = 0, CollisionMask = AffectsEnemies ? 2u | 4u : 2u, Monitorable = false };
		_area.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_width, _height) },
			Position = new Vector2(0f, -_height / 2f),
		});
		AddChild(_area);

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		float hw = _width / 2f;

		// Soft haze: brighter at the source, fading out toward the far end.
		bool lifting = TargetVerticalSpeed < 0f;
		Color strong = NeonColor with { A = 0.16f };
		Color faint = NeonColor with { A = 0f };
		AddChild(new Polygon2D
		{
			Material = additive,
			Polygon = new[] { new Vector2(-hw, -_height), new Vector2(hw, -_height), new Vector2(hw, 0f), new Vector2(-hw, 0f) },
			VertexColors = lifting
				? new[] { faint, faint, strong, strong }
				: new[] { strong, strong, faint, faint },
		});

		// Motes drifting along the wind.
		float speed = Mathf.Max(60f, Mathf.Abs(TargetVerticalSpeed) * 1.3f);
		var streaks = new GpuParticles2D
		{
			Position = new Vector2(0f, lifting ? 0f : -_height),
			Amount = Mathf.Clamp((int)(_width * _height / 1800f), 8, 80),
			Lifetime = _height / speed,
			Preprocess = _height / speed,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = additive,
			Modulate = NeonColor with { A = 0.7f },
			VisibilityRect = new Rect2(-hw - 20f, lifting ? -_height - 20f : -20f, _width + 40f, _height + 40f),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(hw * 0.9f, 2f, 0f),
				Direction = new Vector3(0f, lifting ? -1f : 1f, 0f),
				Spread = 3f,
				InitialVelocityMin = speed * 0.8f,
				InitialVelocityMax = speed * 1.2f,
				Gravity = Vector3.Zero,
				ScaleMin = 0.02f,
				ScaleMax = 0.04f,
			},
		};
		AddChild(streaks);
	}
}
