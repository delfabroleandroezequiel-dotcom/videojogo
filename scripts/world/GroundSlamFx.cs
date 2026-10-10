using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Hand-built "the ground breaks here" effects for slam attacks — everything is drawn in code
// (Line2D glow+core, Polygon2D rock, GpuParticles on SoftDotGradient, PointLight2D), no sprite
// sheets, so it matches the neon/procedural look of the enemy trails. Pure visuals: hitboxes and
// camera shake stay with the attacker.
//
//   Impact(...)      big hit — ground flash, ray burst, double shock ring, red-hot cracks that
//                    cool down, rock chunks with bounce physics, rolling dust, sparks and a light.
//   EarthSpikes(...) jagged rock spikes erupting from the floor for a shockwave tile (sized to
//                    the hazard's own box, so what you see is what hurts), heated edges, then
//                    crumbling back into the ground.
//   WarningCrack(...) a thin glowing crack flickering on a tile right before it erupts — the
//                    crest of a travelling shockwave.
public static class GroundSlamFx
{
	public struct Palette
	{
		public Color Hot;      // glow of rays, cracks, spike edges
		public Color Core;     // white-hot centre
		public Color Rock;     // base rock color
		public Color Dust;
		public Color Light;
	}

	public static readonly Palette Default = new()
	{
		Hot = new Color(1f, 0.38f, 0.08f, 1f),
		Core = new Color(1f, 0.92f, 0.7f, 1f),
		Rock = new Color(0.4f, 0.31f, 0.24f, 1f),
		Dust = new Color(0.62f, 0.52f, 0.42f, 0.6f),
		Light = new Color(1f, 0.55f, 0.25f, 1f),
	};

	private static readonly RandomNumberGenerator Rng = new();
	private static CanvasItemMaterial _additive;
	private static Texture2D _dot;

	static GroundSlamFx()
	{
		Rng.Randomize();
	}

	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

	// ── Big impact ──────────────────────────────────────────────────────────────────────────────

	public static void Impact(Node context, Vector2 ground, float scale = 1f, Palette? palette = null)
	{
		Node scene = context?.GetTree()?.CurrentScene;
		if (scene is null)
			return;
		Palette p = palette ?? Default;

		GroundFlash(scene, ground, scale, p);
		RayBurst(scene, ground, scale, p, 7);
		ShockRing(scene, ground, 50f * scale, 10f * scale, 3f, 0f, p.Hot);
		ShockRing(scene, ground, 72f * scale, 13f * scale, 1.5f, 0.07f, p.Core);
		Cracks(scene, ground, scale, p, 2);
		Debris(scene, ground, scale, p, Mathf.RoundToInt(10 * scale), 260f * scale);
		Dust(scene, ground, scale, p, 18);
		Sparks(scene, ground, scale, p, 16);
		Flash(scene, ground + new Vector2(0f, -14f * scale), 0.85f * scale, 2.2f, 0.38f, p.Light);
	}

	// ── Shockwave tile ──────────────────────────────────────────────────────────────────────────

	// size = the tile's hazard box (width x height); spikes rise to about that height.
	public static void EarthSpikes(Node context, Vector2 ground, Vector2 size, float hold, Palette? palette = null)
	{
		Node scene = context?.GetTree()?.CurrentScene;
		if (scene is null)
			return;
		Palette p = palette ?? Default;

		var root = new Node2D { ZIndex = 1 };
		scene.AddChild(root);
		root.GlobalPosition = ground + new Vector2(0f, 2f);

		// 3-4 spikes across the tile, the middle ones tallest, each leaning a little outward.
		int count = Rng.RandiRange(3, 4);
		float half = size.X * 0.5f;
		var edges = new List<Line2D>();
		for (int i = 0; i < count; i++)
		{
			float t = count == 1 ? 0.5f : (float)i / (count - 1);
			float x = Mathf.Lerp(-half * 0.8f, half * 0.8f, t) + Rng.RandfRange(-4f, 4f);
			float centerWeight = 1f - Mathf.Abs(t - 0.5f) * 1.2f;
			float height = size.Y * Rng.RandfRange(0.75f, 1.05f) * Mathf.Lerp(0.7f, 1.25f, centerWeight);
			float width = size.X / count * Rng.RandfRange(0.9f, 1.25f);
			float lean = (t - 0.5f) * 10f + Rng.RandfRange(-3f, 3f);
			Vector2[] shape = JaggedSpike(width, height, lean);

			var spike = new Node2D { Position = new Vector2(x, 0f), Scale = new Vector2(1f, 0f) };
			root.AddChild(spike);
			Color rock = p.Rock.Lightened(Rng.RandfRange(-0.08f, 0.12f));
			spike.AddChild(new Polygon2D { Polygon = shape, Color = rock });
			// Darker face on one side so the spike reads as a 3D shard, not a flat triangle.
			spike.AddChild(new Polygon2D { Polygon = FacePolygon(shape), Color = rock.Darkened(0.35f) });
			var edge = new Line2D
			{
				Points = Closed(shape),
				Width = 2f,
				DefaultColor = p.Hot,
				Material = Additive,
				JointMode = Line2D.LineJointMode.Sharp,
			};
			spike.AddChild(edge);
			edges.Add(edge);

			// Erupt: overshoot then settle, staggered a hair per spike.
			Tween rise = spike.CreateTween();
			rise.TweenInterval(i * 0.015f);
			rise.TweenProperty(spike, "scale", new Vector2(1f, 1.12f), 0.06f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			rise.TweenProperty(spike, "scale", Vector2.One, 0.06f);
		}

		Cracks(scene, ground, 0.55f, p, 1);
		Dust(scene, ground, 0.6f, p, 8);
		Sparks(scene, ground, 0.6f, p, 6);
		Debris(scene, ground, 0.6f, p, 4, 180f);
		Flash(scene, ground + new Vector2(0f, -size.Y * 0.5f), 0.5f, 1.2f, 0.25f, p.Light);

		// Heated edges cool off while it holds, then the whole cluster crumbles back down.
		Tween life = root.CreateTween();
		foreach (Line2D edge in edges)
			life.Parallel().TweenProperty(edge, "modulate:a", 0.15f, hold + 0.1f);
		life.TweenProperty(root, "position:y", root.Position.Y + size.Y * 0.6f, 0.2f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		life.Parallel().TweenProperty(root, "modulate:a", 0f, 0.2f);
		life.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(root) && root.IsInsideTree())
				Debris(root.GetTree().CurrentScene, ground, 0.5f, p, 4, 110f);
			root.QueueFree();
		}));
	}

	public static void WarningCrack(Node context, Vector2 ground, float width, float duration, Palette? palette = null)
	{
		Node scene = context?.GetTree()?.CurrentScene;
		if (scene is null)
			return;
		Palette p = palette ?? Default;

		var crack = new Line2D
		{
			Points = JaggedLine(new Vector2(-width * 0.5f, 0f), new Vector2(width * 0.5f, 0f), 7, 2.5f),
			Width = 2f,
			DefaultColor = p.Hot,
			Material = Additive,
			ZIndex = 1,
		};
		scene.AddChild(crack);
		crack.GlobalPosition = ground + new Vector2(0f, 1f);
		crack.Modulate = new Color(1f, 1f, 1f, 0f);

		Tween tween = crack.CreateTween();
		tween.TweenProperty(crack, "modulate:a", 1f, duration * 0.4f);
		tween.TweenProperty(crack, "modulate:a", 0.4f, duration * 0.2f);
		tween.TweenProperty(crack, "modulate:a", 1f, duration * 0.4f);
		tween.TweenProperty(crack, "modulate:a", 0f, 0.15f);
		tween.TweenCallback(Callable.From(crack.QueueFree));
	}

	// ── Fissure ─────────────────────────────────────────────────────────────────────────────────

	// A crack racing along the floor: the attacker drives it (it knows where the floor is) by
	// calling AddPoint at the advancing front, then Finish() to let it cool and fade.
	public sealed class FissureTrail
	{
		private readonly Node2D _root;
		private readonly Line2D _dark;
		private readonly Line2D _glow;
		private readonly Line2D _rim;
		private readonly Palette _p;

		internal FissureTrail(Node scene, Palette p)
		{
			_p = p;
			_root = new Node2D { ZIndex = 1 };
			scene.AddChild(_root);
			_root.GlobalPosition = Vector2.Zero;
			_dark = new Line2D { Width = 3f, DefaultColor = new Color(0.07f, 0.04f, 0.03f, 0.95f), JointMode = Line2D.LineJointMode.Sharp };
			_glow = new Line2D { Width = 8f, DefaultColor = p.Hot, Material = Additive, JointMode = Line2D.LineJointMode.Sharp };
			_rim = new Line2D { Width = 2f, DefaultColor = p.Core, Material = Additive, JointMode = Line2D.LineJointMode.Sharp };
			_root.AddChild(_dark);
			_root.AddChild(_glow);
			_root.AddChild(_rim);
		}

		// Adds the next point of the crack (world space), jittered so it reads as broken rock.
		public void AddPoint(Vector2 ground)
		{
			if (!GodotObject.IsInstanceValid(_root))
				return;
			Vector2 point = ground + new Vector2(0f, 1.5f + Rng.RandfRange(-2f, 2f));
			_dark.AddPoint(point);
			_glow.AddPoint(point);
			// The white-hot rim only lives at the front: keep its last few points.
			_rim.AddPoint(point);
			while (_rim.GetPointCount() > 5)
				_rim.RemovePoint(0);
		}

		public void Finish()
		{
			if (!GodotObject.IsInstanceValid(_root))
				return;
			Tween tween = _root.CreateTween();
			tween.TweenProperty(_rim, "modulate:a", 0f, 0.12f);
			tween.Parallel().TweenProperty(_glow, "default_color", new Color(0.5f, 0.05f, 0.02f, 1f), 0.7f);
			tween.Parallel().TweenProperty(_glow, "width", 4f, 0.7f);
			tween.TweenProperty(_root, "modulate:a", 0f, 0.5f);
			tween.TweenCallback(Callable.From(_root.QueueFree));
		}
	}

	public static FissureTrail BeginFissure(Node context, Palette? palette = null)
	{
		Node scene = context?.GetTree()?.CurrentScene;
		return scene is null ? null : new FissureTrail(scene, palette ?? Default);
	}

	// Small chip of breaking floor (the fissure's advancing front, a rock landing): a puff of
	// dust, a few sparks and pebbles.
	public static void Chip(Node context, Vector2 ground, float scale = 0.5f, Palette? palette = null)
	{
		Node scene = context?.GetTree()?.CurrentScene;
		if (scene is null)
			return;
		Palette p = palette ?? Default;
		Dust(scene, ground, scale, p, 4);
		Sparks(scene, ground, scale, p, 4);
		Debris(scene, ground, scale, p, 3, 160f * scale + 60f);
	}

	// A loose irregular rock (for thrown gravel etc.), with a darker underside and a hot rim.
	public static Node2D MakeRock(float size, Palette? palette = null)
	{
		Palette p = palette ?? Default;
		Vector2[] shape = RandomRock(size);
		var rock = new Node2D();
		Color color = p.Rock.Lightened(Rng.RandfRange(-0.1f, 0.15f));
		rock.AddChild(new Polygon2D { Polygon = shape, Color = color });
		var shade = new Vector2[shape.Length];
		for (int i = 0; i < shape.Length; i++)
			shade[i] = new Vector2(shape[i].X * 0.8f, Mathf.Max(shape[i].Y, 0f) * 0.9f + 0.5f);
		rock.AddChild(new Polygon2D { Polygon = shade, Color = color.Darkened(0.35f) });
		rock.AddChild(new Line2D { Points = Closed(shape), Width = 1f, DefaultColor = p.Hot with { A = 0.7f }, Material = Additive });
		return rock;
	}

	// ── Pieces ──────────────────────────────────────────────────────────────────────────────────

	// Squashed white-hot disc right on the floor.
	private static void GroundFlash(Node scene, Vector2 ground, float scale, Palette p)
	{
		var flash = new Sprite2D
		{
			Texture = Dot,
			Material = Additive,
			Modulate = p.Core,
			Scale = new Vector2(0.3f, 0.12f) * scale,
			ZIndex = 2,
		};
		scene.AddChild(flash);
		flash.GlobalPosition = ground + new Vector2(0f, -3f);

		Tween tween = flash.CreateTween();
		tween.TweenProperty(flash, "scale", new Vector2(2.3f, 0.7f) * scale, 0.09f).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(flash, "modulate", new Color(p.Hot, 0f), 0.22f);
		tween.TweenCallback(Callable.From(flash.QueueFree));
	}

	// Short glowing rays fanning up out of the impact, shooting out then retracting to their tips.
	private static void RayBurst(Node scene, Vector2 ground, float scale, Palette p, int rays)
	{
		for (int i = 0; i < rays; i++)
		{
			float t = rays == 1 ? 0.5f : (float)i / (rays - 1);
			float angle = Mathf.DegToRad(Mathf.Lerp(-160f, -20f, t) + Rng.RandfRange(-8f, 8f));
			float length = Rng.RandfRange(28f, 70f) * scale * (1f - Mathf.Abs(t - 0.5f) * 0.6f);
			Vector2 dir = Vector2.Right.Rotated(angle);

			var glow = MakeTaperedLine(5f * scale, p.Hot);
			var core = MakeTaperedLine(1.6f * scale, p.Core);
			var ray = new Node2D { ZIndex = 2 };
			ray.AddChild(glow);
			ray.AddChild(core);
			scene.AddChild(ray);
			ray.GlobalPosition = ground + new Vector2(0f, -2f);

			float from = 0f;
			float to = 0f;
			void Apply()
			{
				Vector2[] pts = { dir * from, dir * to };
				glow.Points = pts;
				core.Points = pts;
			}
			Tween tween = ray.CreateTween();
			tween.TweenMethod(Callable.From<float>(v => { to = v * length; Apply(); }), 0f, 1f, 0.06f).SetEase(Tween.EaseType.Out);
			tween.TweenMethod(Callable.From<float>(v => { from = v * length; Apply(); }), 0f, 0.95f, 0.16f).SetEase(Tween.EaseType.In);
			tween.Parallel().TweenProperty(ray, "modulate:a", 0f, 0.16f);
			tween.TweenCallback(Callable.From(ray.QueueFree));
		}
	}

	// Flat ellipse (the floor seen from the side) ringing outward along the ground.
	private static void ShockRing(Node scene, Vector2 ground, float rx, float ry, float width, float delay, Color color)
	{
		const int segments = 32;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}

		var ring = new Line2D
		{
			Points = points,
			Width = width,
			DefaultColor = color,
			Material = Additive,
			Scale = Vector2.One * 0.2f,
			ZIndex = 1,
		};
		scene.AddChild(ring);
		ring.GlobalPosition = ground + new Vector2(0f, -2f);
		ring.Modulate = new Color(1f, 1f, 1f, delay > 0f ? 0f : 1f);

		Tween tween = ring.CreateTween();
		if (delay > 0f)
		{
			tween.TweenInterval(delay);
			tween.TweenProperty(ring, "modulate:a", 1f, 0.01f);
		}
		tween.TweenProperty(ring, "scale", Vector2.One, 0.3f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.32f).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(ring.QueueFree));
	}

	// Jagged cracks running along the floor surface from the impact, with a branch or two. A dark
	// core line (the crack itself) under an additive glow that starts white-hot, cools to red,
	// then everything fades.
	private static void Cracks(Node scene, Vector2 ground, float scale, Palette p, int perSide)
	{
		var root = new Node2D { ZIndex = 1 };
		scene.AddChild(root);
		root.GlobalPosition = ground + new Vector2(0f, 1f);
		var glows = new List<Line2D>();

		for (int side = -1; side <= 1; side += 2)
		{
			for (int i = 0; i < perSide; i++)
			{
				float length = Rng.RandfRange(45f, 95f) * scale;
				Vector2 start = new(side * Rng.RandfRange(0f, 6f), Rng.RandfRange(0f, 2f));
				Vector2 end = start + new Vector2(side * length, Rng.RandfRange(-1f, 4f));
				Vector2[] main = JaggedLine(start, end, 8, 3f * Mathf.Max(0.6f, scale));
				AddCrack(root, main, 2f, p, glows);

				// Branch off a midpoint, angled down into the floor face.
				Vector2 from = main[Rng.RandiRange(2, main.Length - 3)];
				Vector2 to = from + new Vector2(side * Rng.RandfRange(10f, 24f) * scale, Rng.RandfRange(4f, 10f) * scale);
				AddCrack(root, JaggedLine(from, to, 4, 2f), 1.2f, p, glows);
			}
		}

		Tween tween = root.CreateTween();
		foreach (Line2D glow in glows)
		{
			tween.Parallel().TweenProperty(glow, "default_color", p.Hot, 0.12f);
		}
		tween.TweenInterval(0.01f);
		foreach (Line2D glow in glows)
			tween.Parallel().TweenProperty(glow, "default_color", new Color(0.55f, 0.06f, 0.02f, 1f), 0.6f);
		tween.TweenProperty(root, "modulate:a", 0f, 0.45f);
		tween.TweenCallback(Callable.From(root.QueueFree));
	}

	private static void AddCrack(Node2D root, Vector2[] points, float width, Palette p, List<Line2D> glows)
	{
		root.AddChild(new Line2D
		{
			Points = points,
			Width = width,
			DefaultColor = new Color(0.08f, 0.05f, 0.04f, 0.9f),
			JointMode = Line2D.LineJointMode.Sharp,
		});
		var glow = new Line2D
		{
			Points = points,
			Width = width * 2.6f,
			DefaultColor = p.Core,
			Material = Additive,
			JointMode = Line2D.LineJointMode.Sharp,
		};
		root.AddChild(glow);
		glows.Add(glow);
	}

	// Rock chunks (irregular polygons) thrown up, spinning, bouncing once on the floor line, fading.
	private static void Debris(Node scene, Vector2 ground, float scale, Palette p, int count, float speed)
	{
		if (count <= 0)
			return;
		var shards = new List<(Polygon2D Node, Vector2 Velocity, float Spin)>();
		for (int i = 0; i < count; i++)
		{
			float size = Rng.RandfRange(2.5f, 6.5f) * Mathf.Max(0.6f, scale);
			var shard = new Polygon2D
			{
				Polygon = RandomRock(size),
				Color = p.Rock.Lightened(Rng.RandfRange(-0.15f, 0.2f)),
				ZIndex = 2,
			};
			scene.AddChild(shard);
			shard.GlobalPosition = ground + new Vector2(Rng.RandfRange(-12f, 12f) * scale, -3f);
			float angle = Mathf.DegToRad(-90f + Rng.RandfRange(-65f, 65f));
			Vector2 velocity = Vector2.Right.Rotated(angle) * Rng.RandfRange(0.45f, 1f) * speed;
			shards.Add((shard, velocity, Rng.RandfRange(-12f, 12f)));
		}
		SimulateDebris(shards, ground.Y);
	}

	private static async void SimulateDebris(List<(Polygon2D Node, Vector2 Velocity, float Spin)> shards, float floorY)
	{
		SceneTree tree = shards[0].Node.GetTree();
		const float gravity = 1100f;
		const float lifetime = 0.9f;
		var bounced = new bool[shards.Count];
		float age = 0f;
		while (age < lifetime)
		{
			await shards[0].Node.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
			float dt = (float)(1.0 / Engine.PhysicsTicksPerSecond);
			age += dt;
			bool anyAlive = false;
			for (int i = 0; i < shards.Count; i++)
			{
				var (node, velocity, spin) = shards[i];
				if (!GodotObject.IsInstanceValid(node))
					continue;
				anyAlive = true;
				velocity.Y += gravity * dt;
				Vector2 pos = node.GlobalPosition + velocity * dt;
				if (pos.Y > floorY - 1f && velocity.Y > 0f)
				{
					pos.Y = floorY - 1f;
					velocity = bounced[i] ? new Vector2(velocity.X * 0.5f, 0f) : new Vector2(velocity.X * 0.55f, -velocity.Y * 0.3f);
					spin *= 0.5f;
					bounced[i] = true;
				}
				node.GlobalPosition = pos;
				node.Rotation += spin * dt;
				if (age > lifetime * 0.55f)
					node.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - (age - lifetime * 0.55f) / (lifetime * 0.45f), 0f, 1f));
				shards[i] = (node, velocity, spin);
			}
			if (!anyAlive)
				return;
			if (!GodotObject.IsInstanceValid(shards[0].Node))
			{
				// The awaited node is gone; re-anchor on any survivor.
				int alive = shards.FindIndex(s => GodotObject.IsInstanceValid(s.Node));
				if (alive < 0)
					return;
				(shards[0], shards[alive]) = (shards[alive], shards[0]);
				(bounced[0], bounced[alive]) = (bounced[alive], bounced[0]);
			}
		}
		foreach (var (node, _, _) in shards)
		{
			if (GodotObject.IsInstanceValid(node))
				node.QueueFree();
		}
	}

	// Soft dust rolling out sideways along the floor and drifting up a little.
	private static void Dust(Node scene, Vector2 ground, float scale, Palette p, int amount)
	{
		var scaleCurve = new Curve();
		scaleCurve.AddPoint(new Vector2(0f, 0.35f));
		scaleCurve.AddPoint(new Vector2(1f, 1f));
		var ramp = new Gradient();
		ramp.SetColor(0, p.Dust);
		ramp.SetColor(1, new Color(p.Dust, 0f));

		var process = new ParticleProcessMaterial
		{
			Direction = new Vector3(1f, -0.25f, 0f),
			Spread = 180f,
			InitialVelocityMin = 60f * scale,
			InitialVelocityMax = 170f * scale,
			Gravity = new Vector3(0f, -25f, 0f),
			DampingMin = 120f,
			DampingMax = 200f,
			ScaleMin = 0.35f * scale,
			ScaleMax = 0.7f * scale,
			ScaleCurve = new CurveTexture { Curve = scaleCurve },
			ColorRamp = new GradientTexture1D { Gradient = ramp },
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(16f * scale, 3f, 0f),
		};
		OneShot(scene, ground + new Vector2(0f, -8f * scale), process, amount, 0.85, 0.9f, null, 0);
	}

	// Hot sparks spat upward that arc down and die out.
	private static void Sparks(Node scene, Vector2 ground, float scale, Palette p, int amount)
	{
		var ramp = new Gradient();
		ramp.Offsets = new[] { 0f, 0.35f, 1f };
		ramp.Colors = new[] { p.Core, p.Hot, new Color(p.Hot.R * 0.6f, 0.02f, 0f, 0f) };
		var process = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, -1f, 0f),
			Spread = 70f,
			InitialVelocityMin = 150f * scale,
			InitialVelocityMax = 340f * scale,
			Gravity = new Vector3(0f, 750f, 0f),
			ScaleMin = 0.06f,
			ScaleMax = 0.14f,
			ColorRamp = new GradientTexture1D { Gradient = ramp },
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(10f * scale, 2f, 0f),
		};
		OneShot(scene, ground + new Vector2(0f, -3f), process, amount, 0.55, 1f, Additive, 3);
	}

	private static void OneShot(Node scene, Vector2 position, ParticleProcessMaterial process, int amount, double lifetime,
		float explosiveness, Material material, int zIndex)
	{
		var particles = new GpuParticles2D
		{
			Amount = Mathf.Max(1, amount),
			Lifetime = lifetime,
			OneShot = true,
			Explosiveness = explosiveness,
			Randomness = 0.5f,
			LocalCoords = false,
			ProcessMaterial = process,
			Texture = Dot,
			Material = material,
			ZIndex = zIndex,
		};
		scene.AddChild(particles);
		particles.GlobalPosition = position;
		particles.Emitting = true;
		particles.GetTree().CreateTimer(lifetime + 0.4).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(particles))
				particles.QueueFree();
		};
	}

	private static void Flash(Node scene, Vector2 position, float textureScale, float energy, float duration, Color color)
	{
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = color,
			Energy = energy,
			TextureScale = textureScale,
		};
		scene.AddChild(light);
		light.GlobalPosition = position;

		Tween tween = light.CreateTween();
		tween.TweenProperty(light, "energy", 0f, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(light.QueueFree));
	}

	// ── Geometry helpers ────────────────────────────────────────────────────────────────────────

	private static Line2D MakeTaperedLine(float width, Color color)
	{
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 1f));
		taper.AddPoint(new Vector2(1f, 0.15f));
		return new Line2D
		{
			Width = width,
			WidthCurve = taper,
			DefaultColor = color,
			Material = Additive,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};
	}

	// A shard: base on y=0, tip at (lean, -height), sides broken into a few jagged notches.
	private static Vector2[] JaggedSpike(float width, float height, float lean)
	{
		var pts = new List<Vector2> { new(-width * 0.5f, 0f) };
		const int notches = 3;
		for (int i = 1; i <= notches; i++)
		{
			float t = (float)i / (notches + 1);
			Vector2 onEdge = new Vector2(-width * 0.5f, 0f).Lerp(new Vector2(lean, -height), t);
			pts.Add(onEdge + new Vector2(Rng.RandfRange(-2.5f, 1.5f), 0f));
		}
		pts.Add(new Vector2(lean, -height));
		for (int i = notches; i >= 1; i--)
		{
			float t = (float)i / (notches + 1);
			Vector2 onEdge = new Vector2(width * 0.5f, 0f).Lerp(new Vector2(lean, -height), t);
			pts.Add(onEdge + new Vector2(Rng.RandfRange(-1.5f, 2.5f), 0f));
		}
		pts.Add(new Vector2(width * 0.5f, 0f));
		return pts.ToArray();
	}

	// Right half of a spike (from the tip down the right edge to the base centre) — its shaded face.
	private static Vector2[] FacePolygon(Vector2[] spike)
	{
		int tip = spike.Length / 2;
		var face = new List<Vector2>();
		for (int i = tip; i < spike.Length; i++)
			face.Add(spike[i]);
		face.Add(new Vector2(spike[tip].X * 0.5f, 0f));
		return face.ToArray();
	}

	private static Vector2[] JaggedLine(Vector2 from, Vector2 to, int segments, float jitter)
	{
		var pts = new Vector2[segments + 1];
		Vector2 normal = (to - from).Orthogonal().Normalized();
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			float offset = i == 0 || i == segments ? 0f : Rng.RandfRange(-jitter, jitter);
			pts[i] = from.Lerp(to, t) + normal * offset;
		}
		return pts;
	}

	private static Vector2[] RandomRock(float size)
	{
		int corners = Rng.RandiRange(4, 6);
		var pts = new Vector2[corners];
		float start = Rng.Randf() * Mathf.Tau;
		for (int i = 0; i < corners; i++)
		{
			float a = start + Mathf.Tau * i / corners + Rng.RandfRange(-0.3f, 0.3f);
			pts[i] = Vector2.Right.Rotated(a) * size * Rng.RandfRange(0.6f, 1f);
		}
		return pts;
	}

	private static Vector2[] Closed(Vector2[] pts)
	{
		var closed = new Vector2[pts.Length + 1];
		pts.CopyTo(closed, 0);
		closed[^1] = pts[0];
		return closed;
	}
}
