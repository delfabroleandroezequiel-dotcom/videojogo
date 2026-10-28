using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Trampoline: land on it falling and it throws you back up at BounceVelocity, no button needed.
// The node's origin is the surface it stands on — the pad (and its trigger) sit above it, the
// optional base block below it. On a floor leave HasBase off (the floor is the base); floating
// over a pit turn it on so the pad has something solid under it if you walk onto its side.
// The trigger is a non-solid Area2D a little taller than the pad, so a falling body gets thrown
// before it ever touches the floor: no landing lag, no jump squat eating the bounce. Since the
// body never lands, a bounce doesn't refill jumps — whatever jumps were left before it still are
// (so after a plain jump onto it the double jump is still there for the top of the arc).
// Drawn in code like the neon kit: a dark housing, a spring zig-zag and a glowing top that
// squashes and flashes on every bounce. [Tool] so Width/height preview live in the editor.
[Tool]
public partial class BouncePad : Node2D
{
	private float _width = 64f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(24f, value); QueueRedraw(); }
	}

	// Upward speed given on a bounce (px/s). Rise height ≈ v² / (2 · Player.Gravity 900):
	// 600 ≈ 200px, 700 ≈ 270px, 800 ≈ 355px, 900 ≈ 450px.
	[Export]
	public float BounceVelocity
	{
		get => _bounceVelocity;
		set { _bounceVelocity = Mathf.Max(100f, value); QueueRedraw(); }
	}

	private float _bounceVelocity = 700f;

	// Optional sideways throw (px/s, + = right) for pads that fling you across a gap. The player's
	// own air control takes over again on the next frame, so keep it modest.
	[Export] public float SideVelocity;

	// Bounces enemies that fall on it too (bats and fliers are never thrown, they don't fall).
	[Export] public bool BouncesEnemies = true;

	[Export]
	public bool HasBase
	{
		get => _hasBase;
		set { _hasBase = value; QueueRedraw(); }
	}

	private bool _hasBase;

	[Export] public float BaseHeight = 16f;
	[Export] public Color NeonColor = new(0.35f, 1f, 0.55f, 1f);
	// Sfx category/key played on each bounce (empty key = silent; pick a real sound by ear).
	[Export] public string SoundCategory = "";
	[Export] public string SoundKey = "";

	private const float PadHeight = 14f;
	private const float TriggerHeight = 30f;
	// The same body can't be re-thrown for this long (it's still inside the trigger on the way up).
	private const float RebounceLockout = 0.15f;

	private Area2D _trigger;
	private Node2D _top;
	private Line2D _spring;
	private PointLight2D _light;
	private GpuParticles2D _burst;
	private float _squash;
	private readonly Dictionary<ulong, float> _lockouts = new();

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
		// Editor preview: the pad and a dashed line showing roughly how high it throws.
		float half = _width / 2f;
		DrawRect(new Rect2(-half, -PadHeight, _width, PadHeight), NeonColor.Darkened(0.3f));
		if (_hasBase)
			DrawRect(new Rect2(-half, 0f, _width, BaseHeight), new Color(0.12f, 0.13f, 0.17f));
		float rise = _bounceVelocity * _bounceVelocity / (2f * 900f);
		DrawDashedLine(new Vector2(0f, -PadHeight), new Vector2(0f, -PadHeight - rise), NeonColor with { A = 0.5f }, 2f, 10f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;

		if (_lockouts.Count > 0)
		{
			var ids = new List<ulong>(_lockouts.Keys);
			foreach (ulong id in ids)
			{
				_lockouts[id] -= dt;
				if (_lockouts[id] <= 0f)
					_lockouts.Remove(id);
			}
		}

		foreach (Node2D body in _trigger.GetOverlappingBodies())
		{
			if (body is not CharacterBody2D character)
				continue;
			bool isPlayer = character.IsInGroup("player");
			if (!isPlayer && !(BouncesEnemies && character.IsInGroup("enemy")))
				continue;
			// Only something coming down onto it — walking past its side or rising through it doesn't count.
			if (character.Velocity.Y < -1f || _lockouts.ContainsKey(character.GetInstanceId()))
				continue;
			if (character.GlobalPosition.Y > GlobalPosition.Y - PadHeight * 0.5f)
				continue;
			Bounce(character);
		}

		if (_squash > 0f)
		{
			_squash = Mathf.Max(0f, _squash - dt * 6f);
			ApplySquash();
		}
	}

	private void Bounce(CharacterBody2D body)
	{
		_lockouts[body.GetInstanceId()] = RebounceLockout;
		Vector2 velocity = body.Velocity;
		velocity.Y = -_bounceVelocity;
		if (SideVelocity != 0f)
			velocity.X = SideVelocity;
		body.Velocity = velocity;

		_squash = 1f;
		ApplySquash();
		_burst.Restart();
		_burst.Emitting = true;
		if (!string.IsNullOrEmpty(SoundKey))
			Sfx.PlayAt(this, SoundCategory, SoundKey);
	}

	// Squash the spring and flash the top: 1 = just bounced, 0 = at rest.
	private void ApplySquash()
	{
		float k = _squash;
		_top.Position = new Vector2(0f, -PadHeight + 6f * k);
		_top.Scale = new Vector2(1f + 0.12f * k, 1f - 0.3f * k);
		_top.Modulate = Colors.White.Lerp(new Color(2.2f, 2.2f, 2.2f), k);
		_spring.Points = SpringPoints(PadHeight - 6f * k);
		_light.Energy = 0.6f + 1.4f * k;
	}

	private Vector2[] SpringPoints(float height)
	{
		float half = _width / 2f - 6f;
		const int zigs = 6;
		var points = new Vector2[zigs + 1];
		for (int i = 0; i <= zigs; i++)
		{
			float x = Mathf.Lerp(-half, half, i / (float)zigs);
			float y = i % 2 == 0 ? -2f : -height + 2f;
			points[i] = new Vector2(x, y);
		}
		return points;
	}

	private void Build()
	{
		float half = _width / 2f;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

		if (_hasBase)
		{
			var baseBody = new StaticBody2D { CollisionLayer = 1, CollisionMask = 0 };
			baseBody.AddChild(new CollisionShape2D
			{
				Shape = new RectangleShape2D { Size = new Vector2(_width, BaseHeight) },
				Position = new Vector2(0f, BaseHeight / 2f),
			});
			AddChild(baseBody);
			AddChild(new Polygon2D
			{
				Color = new Color(0.12f, 0.13f, 0.17f, 1f),
				Polygon = new[] { new Vector2(-half, 0f), new Vector2(half, 0f), new Vector2(half, BaseHeight), new Vector2(-half, BaseHeight) },
			});
		}

		// Housing: two small feet the spring stands on.
		AddChild(new Polygon2D
		{
			Color = new Color(0.16f, 0.17f, 0.22f, 1f),
			Polygon = new[] { new Vector2(-half, 0f), new Vector2(-half, -4f), new Vector2(half, -4f), new Vector2(half, 0f) },
		});

		_spring = new Line2D { Width = 2f, DefaultColor = NeonColor with { A = 0.7f }, Material = additive, Points = SpringPoints(PadHeight) };
		AddChild(_spring);

		_top = new Node2D { Position = new Vector2(0f, -PadHeight) };
		_top.AddChild(new Polygon2D
		{
			Color = new Color(0.1f, 0.12f, 0.12f, 1f),
			Polygon = new[] { new Vector2(-half, -4f), new Vector2(half, -4f), new Vector2(half, 2f), new Vector2(-half, 2f) },
		});
		_top.AddChild(new Line2D
		{
			Points = new[] { new Vector2(-half + 2f, -4f), new Vector2(half - 2f, -4f) },
			Width = 3f,
			DefaultColor = NeonColor,
			Material = additive,
		});
		_top.AddChild(new Line2D
		{
			Points = new[] { new Vector2(-half + 2f, -4f), new Vector2(half - 2f, -4f) },
			Width = 9f,
			DefaultColor = NeonColor with { A = 0.25f },
			Material = additive,
		});
		AddChild(_top);

		_light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = NeonColor,
			Energy = 0.6f,
			TextureScale = Mathf.Max(0.8f, _width / 80f),
			Position = new Vector2(0f, -PadHeight),
		};
		AddChild(_light);

		_burst = new GpuParticles2D
		{
			Position = new Vector2(0f, -PadHeight - 4f),
			Amount = 18,
			Lifetime = 0.35,
			OneShot = true,
			Explosiveness = 1f,
			Emitting = false,
			Texture = dot,
			Material = additive,
			Modulate = NeonColor,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(half, 1f, 0f),
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 25f,
				InitialVelocityMin = 60f,
				InitialVelocityMax = 160f,
				Gravity = new Vector3(0f, 300f, 0f),
				ScaleMin = 0.03f,
				ScaleMax = 0.06f,
			},
		};
		AddChild(_burst);

		_trigger = new Area2D { CollisionLayer = 0, CollisionMask = BouncesEnemies ? 2u | 4u : 2u, Monitorable = false };
		_trigger.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_width - 4f, TriggerHeight) },
			Position = new Vector2(0f, -TriggerHeight / 2f),
		});
		AddChild(_trigger);
	}
}
