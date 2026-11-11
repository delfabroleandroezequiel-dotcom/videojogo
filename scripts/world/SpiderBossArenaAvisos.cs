using Godot;

namespace Metroidvania.World;

// Tells that come from the cave itself, not neon drawn on the floor (user, 2026-11-10: the neon
// drop column and the floor ring read cheap):
//  - A spiderling coming down hangs from a silk thread that runs up to the ceiling and snaps when
//    it lands; a couple of grains of grit fall ahead of it.
//  - The spider on the ceiling: its six red eyes glow in the dark above the player, following them,
//    and a curtain of dust and pebbles pours down from there onto the spot. When it locks, the eyes
//    flare and the dust thickens; then it drops.
// The arena is lit by a near-black CanvasModulate, so everything here is unshaded (it would vanish
// otherwise) but kept in dull cave colours: only the eyes are bright.
public partial class SpiderBossArena
{
	private static readonly Color SilkColor = new(0.72f, 0.74f, 0.78f, 0.55f);
	private static readonly Color GritColor = new(0.55f, 0.52f, 0.48f, 1f);
	private static readonly Color DustColor = new(0.66f, 0.62f, 0.55f, 0.5f);
	private static readonly Color EyeColor = new(1f, 0.16f, 0.12f, 1f);

	private static CanvasItemMaterial Unshaded() => new() { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
	private static CanvasItemMaterial UnshadedAdd() => new() { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded, BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	// Top edge of what the camera shows now (the "ceiling" as far as the player can tell).
	private float ViewTopY()
	{
		Camera2D camera = GetViewport().GetCamera2D();
		if (camera is null)
			return GroundY - 400f;
		return camera.GetScreenCenterPosition().Y - GetViewportRect().Size.Y * 0.5f / camera.Zoom.Y;
	}

	// ── Grit ───────────────────────────────────────────────────────────────────────────────

	// One pebble dropping from `from` to the floor, with a little dust puff where it lands.
	private void DropGrit(Vector2 from, float floorY, float size)
	{
		Node scene = GetTree().CurrentScene;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var points = new Vector2[5];
		for (int i = 0; i < points.Length; i++)
			points[i] = Vector2.Right.Rotated(Mathf.Tau * i / points.Length + rng.RandfRange(-0.3f, 0.3f)) * size * rng.RandfRange(0.6f, 1f);
		var pebble = new Polygon2D { Polygon = points, Color = GritColor.Lightened(rng.RandfRange(-0.15f, 0.15f)), Material = Unshaded(), ZIndex = 3 };
		scene.AddChild(pebble);
		pebble.GlobalPosition = from;
		float fall = Mathf.Max(10f, floorY - from.Y);
		float time = Mathf.Sqrt(2f * fall / 1100f);
		Tween t = pebble.CreateTween();
		t.TweenProperty(pebble, "global_position:y", floorY, time).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		t.Parallel().TweenProperty(pebble, "rotation", rng.RandfRange(-6f, 6f), time);
		t.TweenCallback(Callable.From(() => DustPuff(new Vector2(pebble.GlobalPosition.X, floorY), size * 7f)));
		t.TweenCallback(Callable.From(pebble.QueueFree));
	}

	private void DustPuff(Vector2 at, float size)
	{
		var puff = new Sprite2D { Texture = TrapKit.Dot(), Material = Unshaded(), Modulate = DustColor, ZIndex = 3 };
		GetTree().CurrentScene.AddChild(puff);
		puff.GlobalPosition = at + new Vector2(0f, -2f);
		float s = size / 64f;
		puff.Scale = new Vector2(s, s * 0.45f);
		Tween t = puff.CreateTween();
		t.TweenProperty(puff, "scale", new Vector2(s * 2.6f, s * 0.9f), 0.45f).SetEase(Tween.EaseType.Out);
		t.Parallel().TweenProperty(puff, "modulate:a", 0f, 0.45f);
		t.TweenCallback(Callable.From(puff.QueueFree));
	}

	// ── Spiderling on a thread ─────────────────────────────────────────────────────────────

	private async void FallWarning(Node2D spider)
	{
		float topY = ViewTopY() - 40f;
		float x = spider.GlobalPosition.X;
		var thread = new Line2D { Width = 1f, DefaultColor = SilkColor, Material = Unshaded(), ZIndex = 2, Antialiased = false };
		GetTree().CurrentScene.AddChild(thread);
		// Grit shaken loose where it's coming down.
		DropGrit(new Vector2(x - 5f, topY + 30f), GroundY, 2f);
		DropGrit(new Vector2(x + 6f, topY + 10f), GroundY, 1.5f);

		double waited = 0;
		while (waited < FallingSpiderLostTimeout)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || !IsInstanceValid(spider) || !IsInstanceValid(thread))
			{
				if (IsInstanceValid(thread))
					thread.QueueFree();
				return;
			}
			waited += GetPhysicsProcessDeltaTime();
			// The thread runs from the ceiling to the spider's back, with a slight sway.
			Vector2 end = spider.GlobalPosition + new Vector2(0f, -14f);
			float sway = Mathf.Sin((float)waited * 7f) * 2.5f;
			thread.Points = new[] { new Vector2(x + sway * 0.3f, topY), new Vector2((x + end.X) / 2f + sway, (topY + end.Y) / 2f), end };
			if (waited > 0.1 && spider is CharacterBody2D body && body.IsOnFloor())
			{
				PlaySoundAt(spider.GlobalPosition, "Caida");
				DustPuff(spider.GlobalPosition, 26f);
				DustPuff(spider.GlobalPosition + new Vector2(10f, 0f), 18f);
				SnapThread(thread);
				return;
			}
		}
		thread.QueueFree();
	}

	// Snaps: the lower end springs up and the silk fades.
	private static void SnapThread(Line2D thread)
	{
		Vector2[] pts = thread.Points;
		if (pts.Length < 3)
		{
			thread.QueueFree();
			return;
		}
		Vector2 top = pts[0];
		Tween t = thread.CreateTween();
		t.TweenMethod(Callable.From<float>(k =>
		{
			if (!GodotObject.IsInstanceValid(thread))
				return;
			Vector2 end = pts[2].Lerp(top + new Vector2(0f, 60f), k);
			thread.Points = new[] { top, (top + end) / 2f + new Vector2(Mathf.Sin(k * 12f) * 4f, 0f), end };
		}), 0f, 1f, 0.3f).SetEase(Tween.EaseType.Out);
		t.Parallel().TweenProperty(thread, "modulate:a", 0f, 0.3f);
		t.TweenCallback(Callable.From(thread.QueueFree));
	}

	// ── The spider overhead (phase 3 dive) ─────────────────────────────────────────────────

	// Six red eyes glowing in the dark above the target + falling dust and pebbles below them.
	private partial class OjosEnElTecho : Node2D
	{
		public SpiderBossArena Boss;
		public bool Locked;
		private CpuParticles2D _dust;
		private float _floorTimer;
		private Sprite2D _glow;
		private float _gritTimer;
		private float _time;

		public override void _Ready()
		{
			ZIndex = 4;
			// Pixel-art eye cluster: two big middle eyes, two outer, two small top ones.
			Vector2[] eyes = { new(-5, 0), new(5, 0), new(-11, 3), new(11, 3), new(-3, -5), new(3, -5) };
			float[] sizes = { 3f, 3f, 2f, 2f, 1.5f, 1.5f };
			for (int i = 0; i < eyes.Length; i++)
			{
				float h = sizes[i];
				AddChild(new Polygon2D
				{
					Polygon = new[] { eyes[i] + new Vector2(-h, -h), eyes[i] + new Vector2(h, -h), eyes[i] + new Vector2(h, h), eyes[i] + new Vector2(-h, h) },
					Color = EyeColor,
					Material = Unshaded(),
				});
			}
			_glow = new Sprite2D { Texture = TrapKit.Dot(), Material = UnshadedAdd(), Modulate = EyeColor with { A = 0.35f }, Scale = new Vector2(0.8f, 0.5f) };
			AddChild(_glow);

			var fade = new Gradient();
			fade.SetColor(0, DustColor with { A = 0.75f });
			fade.SetColor(1, DustColor with { A = 0f });
			_dust = new CpuParticles2D
			{
				Amount = 44,
				Lifetime = 1.1f,
				LocalCoords = false,
				Texture = TrapKit.Dot(),
				Material = Unshaded(),
				EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
				EmissionRectExtents = new Vector2(16f, 2f),
				Direction = Vector2.Down,
				Spread = 6f,
				Gravity = new Vector2(0f, 420f),
				InitialVelocityMin = 20f,
				InitialVelocityMax = 60f,
				ScaleAmountMin = 0.035f,
				ScaleAmountMax = 0.085f,
				ColorRamp = fade,
				Position = new Vector2(0f, 10f),
			};
			AddChild(_dust);

			// Eyes open: from nothing to lit in a blink.
			Modulate = new Color(1f, 1f, 1f, 0f);
			CreateTween().TweenProperty(this, "modulate:a", 1f, 0.18f);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			_time += dt;
			// A slow breathing glow; locked = brighter and fast.
			float pulse = Locked ? 0.75f + 0.25f * Mathf.Sin(_time * 30f) : 0.45f + 0.15f * Mathf.Sin(_time * 4f);
			_glow.Modulate = EyeColor with { A = pulse };
			_dust.SpeedScale = Locked ? 1.8f : 1f;
			// Where it all lands: dust keeps puffing up off the floor (the spot it will drop on).
			_floorTimer -= dt;
			if (_floorTimer <= 0f && IsInstanceValid(Boss))
			{
				_floorTimer = Locked ? 0.07f : 0.14f;
				Boss.DustPuff(new Vector2(GlobalPosition.X + (float)GD.RandRange(-34.0, 34.0), Boss.GroundY), (float)GD.RandRange(30.0, 48.0));
			}

			_gritTimer -= dt;
			if (_gritTimer <= 0f && IsInstanceValid(Boss))
			{
				_gritTimer = Locked ? 0.04f : 0.1f;
				Boss.DropGrit(GlobalPosition + new Vector2((float)GD.RandRange(-14.0, 14.0), 12f), Boss.GroundY, (float)GD.RandRange(1.5, 3.2));
			}
		}

		// Eyes shut and the dust settles (the drop is happening).
		public void Close()
		{
			SetProcess(false);
			_dust.Emitting = false;
			Tween t = CreateTween();
			t.TweenProperty(this, "modulate:a", 0f, 0.12f);
			t.TweenInterval(_dust.Lifetime);
			t.TweenCallback(Callable.From(QueueFree));
		}
	}

	private OjosEnElTecho MakeCeilingEyes()
	{
		var eyes = new OjosEnElTecho { Boss = this };
		GetTree().CurrentScene.AddChild(eyes);
		return eyes;
	}

	// Stomp tell (phase 2): no floor line — a puff of dust and grit kicked up where the leg will land.
	private void StompDustTell(Vector2 landing)
	{
		DustPuff(landing, 30f);
		DropGrit(landing + new Vector2(-8f, -40f), GroundY, 2f);
		DropGrit(landing + new Vector2(10f, -55f), GroundY, 1.6f);
	}
}
