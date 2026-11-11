using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Phase 3's web and dive aftermath (user, 2026-11-10):
//  - The web (PixelLab art): if it catches you, you're wrapped in a silk cocoon and have to mash
//    Attack to tear out while it keeps fighting. Roll through the web or parry it (cuts it).
//  - The ceiling dive's landing runs out along the floor to both sides as a row of rock spikes,
//    like Bandido5's leap (own copy here, per the boss-uniqueness rule): jump it.
public partial class SpiderBossArena
{
	private const string WebShotTexturePath = "res://assets/external/Chars/PixelLab/AranaGiganteOscura/telarana_disparo.png";
	private const string CocoonTexturePath = "res://assets/external/Chars/PixelLab/AranaGiganteOscura/capullo.png";

	private static readonly GroundSlamFx.Palette WavePalette = new()
	{
		Hot = new Color(0.45f, 1f, 0.5f, 1f),
		Core = new Color(0.85f, 1f, 0.85f, 1f),
		Rock = new Color(0.3f, 0.28f, 0.27f, 1f),
		Dust = new Color(0.6f, 0.56f, 0.5f, 0.55f),
		Light = new Color(0.4f, 1f, 0.5f, 1f),
	};

	// ── Spikes rolling out from the landing ────────────────────────────────────────────────

	private async void RunSpikeWave(float originX, float sign)
	{
		for (int i = 1; i <= WaveTilesPerSide; i++)
		{
			float x = originX + sign * WaveTileSpacing * i;
			if (x < ArenaMinX - 40f || x > ArenaMaxX + 160f)
				return;
			var ground = new Vector2(x, GroundY);
			GroundSlamFx.WarningCrack(this, ground, WaveTileSize.X, WaveWarningLead, WavePalette);
			await ToSignal(GetTree().CreateTimer(WaveWarningLead + (i == 1 ? 0f : WaveStrikeGap), false), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			GroundSlamFx.EarthSpikes(this, ground, WaveTileSize, 0.3f, WavePalette);
			var hazard = Hazard.CreateArea(GetTree().CurrentScene, instantKill: false, damage: WaveDamage, knockbackForce: 300f);
			hazard.GlobalPosition = ground - new Vector2(0f, WaveTileSize.Y * 0.5f);
			hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = WaveTileSize } });
			GetTree().CreateTimer(0.18f, false).Timeout += () => { if (IsInstanceValid(hazard)) hazard.QueueFree(); };
		}
	}

	// ── Caught in the web: the cocoon ──────────────────────────────────────────────────────
	// Wrapped up: can't move, attack, parry or drink; mash Attack to tear out (each press shakes the
	// cocoon and rips silk off it; a ring of silk fills with progress). Meanwhile it keeps fighting,
	// so staying in there is the punishment. Frees itself after MaxTrapTime anyway.

	private void OnWebCaught(Metroidvania.Player.Player player)
	{
		if (IsQueuedForRemoval || player.Atrapado)
			return;
		var cocoon = new CapulloTrampa { Boss = this, Prey = player };
		player.AddChild(cocoon);
		TrapKit.Shake(this, 4f, 0.2f);
	}

	private partial class CapulloTrampa : Node2D
	{
		public SpiderBossArena Boss;
		public Metroidvania.Player.Player Prey;
		private Sprite2D _silk;
		private Line2D _ring;
		private int _presses;
		private float _time;
		private bool _done;

		private static readonly Color Silk = new(0.85f, 0.87f, 0.9f, 1f);

		public override void _Ready()
		{
			ZIndex = 5;
			Position = new Vector2(0f, 2f);
			Prey.Atrapado = true;
			Prey.Snare(Boss.MaxTrapTime + 1f);
			if (ResourceLoader.Exists(CocoonTexturePath))
			{
				_silk = new Sprite2D { Texture = GD.Load<Texture2D>(CocoonTexturePath), TextureFilter = TextureFilterEnum.Nearest };
				AddChild(_silk);
			}
			// Progress: a thin silk ring around it that closes as it tears free.
			_ring = new Line2D { Width = 2f, DefaultColor = Silk with { A = 0.85f }, Material = Unshaded(), ZIndex = 1 };
			AddChild(_ring);
			// Wraps on with a quick squash, like the silk snapping tight.
			Scale = new Vector2(1.4f, 0.5f);
			CreateTween().TweenProperty(this, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			Boss.DustPuff(Prey.GlobalPosition, 24f);
			Sfx.PlayIfExistsAt(Boss, Boss.SoundCategory, "Desgarro");
		}

		public override void _Process(double delta)
		{
			if (_done)
				return;
			_time += (float)delta;
			if (!IsInstanceValid(Prey) || Prey.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 })
			{
				Free_(false);
				return;
			}
			// The spider died: the silk goes slack.
			if (!IsInstanceValid(Boss) || Boss.Stats is not { CurrentHealth: > 0 })
			{
				Free_(true);
				return;
			}
			if (Input.IsActionJustPressed("attack"))
				Struggle();
			if (_presses >= Boss.MashPresses || _time >= Boss.MaxTrapTime)
				Free_(true);
		}

		private void Struggle()
		{
			_presses++;
			float k = (float)_presses / Mathf.Max(1, Boss.MashPresses);
			// Jolt: a twist and a bulge, harder as it gives.
			float twist = (GD.Randf() < 0.5f ? -1f : 1f) * (0.12f + 0.12f * k);
			Tween t = CreateTween();
			t.TweenProperty(this, "rotation", twist, 0.04f);
			t.Parallel().TweenProperty(this, "scale", new Vector2(1.12f + 0.1f * k, 0.92f), 0.04f);
			t.TweenProperty(this, "rotation", 0f, 0.1f).SetTrans(Tween.TransitionType.Back);
			t.Parallel().TweenProperty(this, "scale", Vector2.One, 0.1f);
			// Silk tearing off; the cocoon thins out, showing more of him.
			TrapKit.Burst(this, GlobalPosition + new Vector2((float)GD.RandRange(-12.0, 12.0), (float)GD.RandRange(-20.0, 16.0)), Silk, 8, 150f, 0.35f, 0.04f);
			if (_silk is not null)
				_silk.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.55f, k));
			UpdateRing(k);
			Sfx.PlayIfExistsAt(Boss, Boss.SoundCategory, "Desgarro");
		}

		private void UpdateRing(float k)
		{
			int points = Mathf.Max(2, Mathf.RoundToInt(32 * k) + 1);
			var arc = new Vector2[points];
			for (int i = 0; i < points; i++)
			{
				float a = -Mathf.Pi / 2f + Mathf.Tau * k * i / (points - 1);
				arc[i] = new Vector2(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 38f);
			}
			_ring.Points = arc;
		}

		// Breaks out (or the boss died / time ran out): silk everywhere, free to move.
		private void Free_(bool burst)
		{
			if (_done)
				return;
			_done = true;
			if (IsInstanceValid(Prey))
			{
				Prey.Atrapado = false;
				Prey.ReleaseSnare();
			}
			if (burst && IsInstanceValid(Boss))
			{
				TrapKit.Burst(Boss, GlobalPosition, Silk, 30, 260f, 0.5f, 0.06f);
				Sfx.PlayIfExistsAt(Boss, Boss.SoundCategory, "Liberarse");
			}
			Tween t = CreateTween();
			t.TweenProperty(this, "scale", new Vector2(1.35f, 1.25f), 0.08f);
			t.Parallel().TweenProperty(this, "modulate:a", 0f, 0.15f);
			t.TweenCallback(Callable.From(QueueFree));
		}

		public override void _ExitTree()
		{
			if (!_done && IsInstanceValid(Prey))
			{
				Prey.Atrapado = false;
				Prey.ReleaseSnare();
			}
		}
	}

	// The web shot: rolling through it (i-frames) or parrying it (cuts it) are the answers.
	private partial class WebShot : Area2D
	{
		public SpiderBossArena Owner2;
		public Vector2 Velocity;
		private float _life = 3f;
		private bool _done;

		public override void _Ready()
		{
			CollisionLayer = 0;
			CollisionMask = 1 | ProjectileReflect.PlayerLayer;
			AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 16f } });
			if (ResourceLoader.Exists(WebShotTexturePath))
				AddChild(new Sprite2D { Texture = GD.Load<Texture2D>(WebShotTexturePath), TextureFilter = TextureFilterEnum.Nearest });
			// Its own light: the arena is near-black and the silk has to read.
			AddChild(TrapKit.Light(new Color(0.75f, 1f, 0.75f, 1f), 0.9f, 0.9f));
			BodyEntered += OnBody;
		}

		public override void _PhysicsProcess(double delta)
		{
			Position += Velocity * (float)delta;
			// The art has its motion streaks trailing left: point it along its flight.
			Rotation = Velocity.Angle();
			_life -= (float)delta;
			if (_life <= 0f)
				Pop();
		}

		private void OnBody(Node2D body)
		{
			if (_done)
				return;
			if (body is Metroidvania.Player.Player player)
			{
				if (ProjectileReflect.TryParry(body))
				{
					Pop();
					return;
				}
				// Rolling or dashing (invulnerable) slips through it.
				if (body.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: true })
					return;
				Pop();
				if (IsInstanceValid(Owner2))
					Owner2.OnWebCaught(player);
				return;
			}
			if (body is StaticBody2D)
				Pop();
		}

		private void Pop()
		{
			if (_done)
				return;
			_done = true;
			TrapKit.Burst(this, GlobalPosition, new Color(0.85f, 0.87f, 0.9f, 1f), 14, 160f, 0.3f, 0.05f);
			QueueFree();
		}
	}
}
