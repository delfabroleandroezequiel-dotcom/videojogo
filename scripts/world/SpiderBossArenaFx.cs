using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The arena spider's weight: neon tells on every attack, camera shake, and its own voice/body
// sounds (ElevenLabs Sound Effects, Sonidos/Bosses/AranaGigante). Kept apart from the fight logic
// in SpiderBossArena.cs; everything here is presentation and can be tuned without touching it.
// Neon = additive lines/glows in the same green as its telegraph blink (feedback: neon VFX style).
public partial class SpiderBossArena
{
	[ExportGroup("Peso y neon")]
	[Export] public string SoundCategory = "Bosses/AranaGigante";
	[Export] public Color NeonColor = new(0.35f, 1f, 0.45f, 1f);
	[Export] public Color PhaseColor = new(1f, 0.2f, 0.25f, 1f);
	// The spit's wind-up here is long enough to read (the cave spider keeps its 0.3 s).
	[Export] public float ArenaSpitWindup = 0.6f;
	[Export] public float SpitChargeRingRadius = 70f;
	[Export(PropertyHint.Range, "0,1")] public float HurtSoundChance = 0.45f;
	// Unused since the silk-thread tell (kept so scenes that set it still load cleanly).
	[Export] public float FallWarningDuration = 0.55f;

	private const ulong HurtSoundMinIntervalMsec = 600;
	private ulong _nextHurtSoundMsec;

	protected override float SpitWindupDuration => ArenaSpitWindup;

	private void SetupWeight()
	{
		Stats.HitTaken += _ => HurtSound();
		Stats.Died += () =>
		{
			PlaySound("Muerte");
			TrapKit.Shake(this, 10f, 0.8f);
		};
	}

	private void PlaySound(string key, float maxSeconds = 0f) =>
		Sfx.PlayIfExistsAt(this, SoundCategory, key, maxDistance: 1600f, maxSeconds: maxSeconds);

	private void PlaySoundAt(Vector2 position, string key)
	{
		var anchor = new Node2D();
		GetTree().CurrentScene.AddChild(anchor);
		anchor.GlobalPosition = position;
		Sfx.PlayIfExistsAt(anchor, SoundCategory, key, maxDistance: 1600f);
		anchor.QueueFree();
	}

	private void HurtSound()
	{
		if (Dormant || Stats.CurrentHealth <= 0 || Time.GetTicksMsec() < _nextHurtSoundMsec || GD.Randf() > HurtSoundChance)
			return;
		_nextHurtSoundMsec = Time.GetTicksMsec() + HurtSoundMinIntervalMsec;
		PlaySound("Quejido");
	}

	// Pushing itself up: the legs hit the rock.
	private void WakeFx()
	{
		PlaySound("Patas");
		TrapKit.Shake(this, 4f, 0.3f);
	}

	private void AwakenFx()
	{
		PlaySound("Rugido");
		TrapKit.Shake(this, 9f, 0.7f);
		RoarFlash(NeonColor);
	}

	private void PhaseFx()
	{
		PlaySound("Rugido");
		TrapKit.Shake(this, 8f, 0.6f);
		RoarFlash(PhaseColor);
		TrapKit.Burst(this, GlobalPosition, PhaseColor, 40, 320f, 0.6f, 0.09f);
	}

	// The body flares (SelfModulate, so the arena's dark Visual tint stays) with a wide glow behind.
	private void RoarFlash(Color color)
	{
		if (Sprite is null)
			return;
		Tween t = CreateTween();
		t.TweenProperty(Sprite, "self_modulate", (color * 2.2f) with { A = 1f }, 0.08f);
		t.TweenProperty(Sprite, "self_modulate", Colors.White, 0.5f);
		var glow = TrapKit.Light(color, 2.2f, 6f);
		AddChild(glow);
		Tween g = glow.CreateTween();
		g.TweenProperty(glow, "energy", 0f, 0.7f);
		g.TweenCallback(Callable.From(glow.QueueFree));
	}

	// ── Spit ───────────────────────────────────────────────────────────────────────────────

	protected override void OnSpitWindup(float duration)
	{
		PlaySound("Carga", duration + 0.1f);
		var root = new Node2D { ZIndex = 6 };
		AddChild(root);
		// A ring closing on the spider's mouth plus a growing glow: "it's coming, now".
		var ring = TrapKit.Line(TrapKit.Circle(Vector2.Zero, 1f, 28), 0.06f, NeonColor);
		ring.Closed = true;
		ring.Scale = Vector2.One * SpitChargeRingRadius;
		var core = new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = NeonColor, Scale = Vector2.One * 0.05f };
		var light = TrapKit.Light(NeonColor, 0.3f, 1.2f);
		root.AddChild(ring);
		root.AddChild(core);
		root.AddChild(light);
		Tween t = root.CreateTween().SetParallel();
		t.TweenProperty(ring, "scale", Vector2.One * 8f, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
		t.TweenProperty(ring, "width", 0.25f, duration);
		t.TweenProperty(core, "scale", Vector2.One * 0.6f, duration).SetEase(Tween.EaseType.In);
		t.TweenProperty(light, "energy", 2.4f, duration);
		t.Chain().TweenCallback(Callable.From(root.QueueFree));
	}

	protected override void OnSpitReleased(PoisonSpit spit)
	{
		PlaySound("Escupitajo");
		WatchReflected(spit);
		TrapKit.Shake(this, 3f, 0.15f);
		TrapKit.Burst(this, spit.GlobalPosition, NeonColor, 18, 200f, 0.3f, 0.06f);

		// A neon tail and its own light, so the glob reads against the dark arena.
		var trail = new CpuParticles2D
		{
			Amount = 40,
			Lifetime = 0.35f,
			LocalCoords = false,
			Texture = TrapKit.Dot(),
			Material = TrapKit.Additive(),
			Gravity = Vector2.Zero,
			Spread = 180f,
			InitialVelocityMin = 0f,
			InitialVelocityMax = 15f,
			ScaleAmountMin = 0.05f,
			ScaleAmountMax = 0.1f,
			ZIndex = 5,
		};
		var fade = new Gradient();
		fade.SetColor(0, NeonColor);
		fade.SetColor(1, NeonColor with { A = 0f });
		trail.ColorRamp = fade;
		spit.AddChild(trail);
		spit.AddChild(TrapKit.Light(NeonColor, 1.4f, 1.6f));
	}

	// ── Poison burst under the player ──────────────────────────────────────────────────────

	// A flattened neon circle on the floor that blinks faster and faster, with a fill growing to
	// the edge: the moment it's full, it goes off. Freed by the caller when the telegraph ends.
	private Node2D BurstWarning(Vector2 at, float duration)
	{
		PlaySoundAt(at, "Burbujeo");
		var root = new Node2D { ZIndex = 4, Scale = new Vector2(1f, 0.35f) };
		GetTree().CurrentScene.AddChild(root);
		root.GlobalPosition = at;
		var ring = TrapKit.Line(TrapKit.Circle(Vector2.Zero, ExplosionRadius, 36), 3f, NeonColor);
		ring.Closed = true;
		var fill = TrapKit.Poly(TrapKit.Circle(Vector2.Zero, ExplosionRadius, 36), NeonColor with { A = 0.35f });
		fill.Material = TrapKit.Additive();
		fill.Scale = Vector2.One * 0.05f;
		root.AddChild(fill);
		root.AddChild(ring);
		root.CreateTween().TweenProperty(fill, "scale", Vector2.One, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);

		// Blinks: each one shorter than the last.
		Tween blink = root.CreateTween();
		float left = duration;
		float step = duration * 0.28f;
		while (left > 0.04f)
		{
			float half = Mathf.Min(step, left) * 0.5f;
			blink.TweenProperty(ring, "modulate:a", 0.25f, half);
			blink.TweenProperty(ring, "modulate:a", 1f, half);
			left -= half * 2f;
			step = Mathf.Max(0.05f, step * 0.65f);
		}
		return root;
	}

	private void BurstImpactFx(Vector2 at)
	{
		PlaySoundAt(at, "Explosion");
		TrapKit.Blast(this, at, ExplosionRadius * 1.3f, NeonColor);
		GroundSlamFx.Impact(this, at, 0.9f);
		TrapKit.Shake(this, 7f, 0.3f);
	}

}
