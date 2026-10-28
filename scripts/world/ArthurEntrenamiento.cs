using System.Threading.Tasks;
using Godot;
using Metroidvania.UI;

namespace Metroidvania.World;

// Arthur in the opening training arena, built on the Guerrero Rojo kit (RedWarrior) and immortal.
//   Intro    — "Es hora de entrar en calor", then the attack/roll sign.
//   Warmup   — very passive: only the single low slash, slow telegraph, long cooldowns, soft hits.
//   Serious  — after HitsToSerious hits: "ponerse en serio", roar (frenzy), parry sign, and a
//              smart brain (see Decide) driving the full kit.
//   Berserk  — after HitsToBerserk more hits: the screen drains red (the player's berserker
//              veil), a huge shake, and the moves the base kit never uses: the leaping hammer slam
//              (jump sheet), the hammer throw + dive (leap sheet) and the overhead third hit.
// Smart brain, ElfArcher-style: re-reads the player every tick and scores its options —
//   * never swings into an open parry (holds the wind-up until the window closes) and punishes a
//     whiffed parry, a heal, or the end of a roll/dash (reacting on reflex gets punished, reading
//     the tell doesn't);
//   * adapts: a player who parries a lot gets delayed swings (feints) and more whirlwinds;
//   * two hits taken in a row make him disengage with a dodge;
//   * at range he closes with the jump slam (aimed where the player will be) or, berserk, the leap.
public partial class ArthurEntrenamiento : RedWarrior
{
	[Export] public TutorialHint Hint;
	[Export] public string SpeakerNameKey = "NPC_ARTHUR_NAME";
	// The training ends when Arthur beats the player: they can't die here (held at 1 HP) and this
	// cinematic plays instead of the death screen.
	[Export] public Cinematic DefeatCinematic;

	[ExportGroup("Warmup")]
	[Export] public string[] WarmupLines = { "ARTHUR_CALENTAR_1" };
	[Export] public string WarmupHintKey = "TUTORIAL_ATAQUE_RODAR";
	[Export] public string[] WarmupHintActions = { "attack", "roll" };
	[Export] public float WarmupTelegraphScale = 2.2f;
	[Export] public float WarmupCooldownScale = 2.5f;
	[Export] public float WarmupDamageScale = 0.5f;
	// Phases advance by hits landed, not damage — the wooden sword's damage would otherwise decide
	// how long the fight lasts (6 per hit made the berserk threshold ~24 hits away).
	[Export] public int HitsToSerious = 6;

	[ExportGroup("Serious")]
	[Export] public string[] SeriousLines = { "ARTHUR_EN_SERIO_1" };
	[Export] public string SeriousHintKey = "TUTORIAL_PARRY";
	[Export] public string[] SeriousHintActions = { "block" };
	[Export] public float SeriousCooldownScale = 0.8f;
	[Export] public float SeriousDamageScale = 1f;
	[Export] public int HitsToBerserk = 8;

	[ExportGroup("Berserk")]
	[Export] public string[] BerserkLines = { "ARTHUR_BERSERK_1" };
	[Export] public float BerserkShake = 14f;
	[Export] public float BerserkShakeDuration = 1.4f;
	[Export] public float BerserkSwingShake = 4f;
	[Export] public float BerserkSlamShake = 9f;
	[Export] public float BerserkSpeedBonus = 1.2f;
	[Export] public float BerserkCooldownScale = 0.6f;
	[Export] public float BerserkDamageScale = 1.25f;
	[Export] public float BerserkTelegraphScale = 0.85f;

	[ExportGroup("Brain")]
	[Export] public float JumpSlamMinDistance = 110f;
	[Export] public float JumpSlamMaxDistance = 330f;
	[Export] public float JumpSlamCooldown = 3.5f;
	[Export] public float LeapMinDistance = 150f;
	[Export] public float LeapMaxDistance = 250f;
	[Export] public float LeapCooldown = 9f;
	// 1 = lands exactly where the player will be, 0 = where they are now.
	[Export] public float LeadAccuracy = 0.7f;
	[Export] public float PostEvadeWindow = 0.25f;
	[Export] public float ParryHappyBlocks = 3f;

	private enum Phase { Intro, Warmup, ToSerious, Serious, ToBerserk, Berserk, Done }

	// Jump sheet (220x96 cells): figure drawn at x 79 on frame 0 (offset +25 puts it where the idle
	// stands); body centre per frame minus 79 = drawn travel. 7-14 airborne, 15 lands, the hammer
	// head ~38 cell px ahead of the body.
	private static readonly float[] JumpDrift =
	{
		0f, 0f, 0f, 0f, 0f, 0f, 0.5f, 15.5f, 20f, 30.5f, 42f, 47.5f, 57.5f, 68.5f, 80f, 89.5f, 89f, 89f, 89f,
		89f, 89f, 90.5f, 92f, 92f, 92f,
	};
	private static readonly Vector2 JumpOffset = new(25f, 0f);
	private const float JumpTravelPx = 89.5f * 1.8f;
	// Leap sheet (260x128 cells, feet at y 107): 8-15 rises out of reach, 16-19 throws the hammer,
	// 20 it lands planted at cell x ~186, 22-24 two shockwaves either side (±35), 26 he dives onto it
	// with a ground slash reaching cell x ~250, 30-39 recovery.
	private static readonly float[] LeapDrift =
	{
		0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.5f, -2f, -3.5f, -3f, -3f, -3f, -2.5f, -1.5f, -1.5f, -0.5f, -4.5f, -9f,
		-13.5f, -18.5f, -19.5f, -20.5f, -22f, -23.5f, -23.5f, 96f, 96.5f, 96.5f, 96.5f, 96.5f, 100.5f, 107f,
		105.5f, 106.5f, 107f, 108f, 109f, 109f, 109f,
	};
	private static readonly Vector2 LeapOffset = new(45f, -16f);
	private const float FigureCellX = 79f;
	private const float SheetScale = 1.8f;
	private const float FeetY = 29.2f;

	private Phase _phase = Phase.Intro;
	private int _warmupHits;
	private int _seriousHits;
	private bool _transitionStarted;
	private float _jumpCooldown;
	private float _leapCooldown = 2f;
	private float _feintTimer;
	private float _sinceEvadeEnded = 99f;
	private bool _wasEvading;
	private bool _wasBlocking;
	private float _blockStarts;
	private float _lastHitTime = -99f;
	private int _hitsInRow;
	private float _clock;
	private Node _veil;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		Stats.HealthChanged += OnHealthChanged;
		Stats.HitTaken += _ => OnHit();
		Callable.From(StartIntro).CallDeferred();
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_veil) && _veil is Metroidvania.Player.BerserkerVeil veil)
			veil.Release();
	}

	private void StartIntro()
	{
		if (PlayerReads.Find(this)?.GetNodeOrNull<Metroidvania.Shared.Stats>("Stats") is { } playerStats)
		{
			playerStats.MinHealth = 1;
			playerStats.HealthChanged += (current, _) =>
			{
				if (current <= 1 && _phase != Phase.Done)
					Defeat(playerStats);
			};
		}
		Say(WarmupLines, () =>
		{
			_phase = Phase.Warmup;
			Hint?.ShowHint(WarmupHintKey, WarmupHintActions);
		});
	}

	// Immortal the TrainingDummy way: refill before Stats' own death check so Died never fires.
	private void OnHealthChanged(int current, int max)
	{
		if (current >= max)
			return;
		Stats.SetCurrentHealth(max);
		if (_phase == Phase.Warmup && ++_warmupHits >= HitsToSerious)
			_phase = Phase.ToSerious;
		else if (_phase == Phase.Serious && ++_seriousHits >= HitsToBerserk)
			_phase = Phase.ToBerserk;
	}

	private void OnHit()
	{
		_hitsInRow = _clock - _lastHitTime < 1.5f ? _hitsInRow + 1 : 1;
		_lastHitTime = _clock;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_clock += dt;
		_jumpCooldown -= dt / CooldownScale;
		_leapCooldown -= dt / CooldownScale;
		ReadPlayer(dt);
		base._PhysicsProcess(delta);
		// Wait for the current swing to finish before talking, so he doesn't freeze mid-slash.
		if (_transitionStarted || IsBusy)
			return;
		if (_phase == Phase.ToSerious)
		{
			_transitionStarted = true;
			Hint?.HideHint();
			Say(SeriousLines, GetSerious);
		}
		else if (_phase == Phase.ToBerserk)
		{
			_transitionStarted = true;
			Hint?.HideHint();
			Say(BerserkLines, GoBerserk);
		}
	}

	// Rising edges of the player's blocks (decaying), and how long since their last roll/dash ended.
	private void ReadPlayer(float dt)
	{
		_blockStarts = Mathf.Max(0f, _blockStarts - dt / 6f);
		_sinceEvadeEnded += dt;
		if (PlayerReads.Find(this) is not { } player)
			return;
		bool evading = player.IsDashing || player.IsRolling;
		if (_wasEvading && !evading)
			_sinceEvadeEnded = 0f;
		_wasEvading = evading;
		if (player.IsBlocking && !_wasBlocking)
			_blockStarts += 1f;
		_wasBlocking = player.IsBlocking;
	}

	private async void GetSerious()
	{
		await Roar();
		if (!IsInstanceValid(this))
			return;
		_phase = Phase.Serious;
		_transitionStarted = false;
		Hint?.ShowHint(SeriousHintKey, SeriousHintActions);
	}

	private async void GoBerserk()
	{
		RoarShake = BerserkShake;
		RoarShakeDuration = BerserkShakeDuration;
		Shake(BerserkShake * 0.6f, 0.5f);
		if (GetTree().CurrentScene is { } scene)
		{
			_veil = new Metroidvania.Player.BerserkerVeil { Name = "VeloBerserkArthur" };
			scene.AddChild(_veil);
		}
		await Roar();
		if (!IsInstanceValid(this))
			return;
		SwingShake = BerserkSwingShake;
		SlamShake = BerserkSlamShake;
		_phase = Phase.Berserk;
		_transitionStarted = false;
		_leapCooldown = 1.5f;
	}

	private void Defeat(Metroidvania.Shared.Stats playerStats)
	{
		_phase = Phase.Done;
		playerStats.GodMode = true;
		Hint?.HideHint();
		if (IsInstanceValid(_veil) && _veil is Metroidvania.Player.BerserkerVeil veil)
			veil.Release();
		_veil = null;
		PlayDefeat();
	}

	// Lets the swing that landed finish first.
	private async void PlayDefeat()
	{
		while (IsInstanceValid(this) && IsBusy)
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		if (IsInstanceValid(this) && DefeatCinematic is not null)
			await DefeatCinematic.Play();
	}

	protected override float ComputeMoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		_phase == Phase.Done ? 0f : base.ComputeMoveX(player, distanceX, currentVelocityX, delta);

	private void Say(string[] lines, System.Action then)
	{
		if (lines.Length == 0 || DialogueBox.Instance is null)
			then();
		else
			DialogueBox.Instance.Show(SpeakerNameKey, lines, then);
	}

	// ── Phase tuning ────────────────────────────────────────────────────────────────────────

	private bool Smart => _phase is Phase.Serious or Phase.Berserk;

	protected override bool MoveAllowed(Move move) => _phase switch
	{
		Phase.Warmup => move == Move.Slash,
		// The frenzy roar is scripted (GetSerious/GoBerserk), never the health-ratio one.
		Phase.Serious or Phase.Berserk => move != Move.Frenzy,
		_ => false,
	};

	protected override float TelegraphScale => _phase switch
	{
		Phase.Warmup => WarmupTelegraphScale,
		Phase.Berserk => BerserkTelegraphScale,
		_ => 1f,
	};

	protected override float CooldownScale => _phase switch
	{
		Phase.Warmup => WarmupCooldownScale,
		Phase.Berserk => BerserkCooldownScale,
		_ => SeriousCooldownScale,
	};

	protected override float DamageScale => _phase switch
	{
		Phase.Warmup => WarmupDamageScale,
		Phase.Berserk => BerserkDamageScale,
		_ => SeriousDamageScale,
	};

	protected override float SpeedBonus => _phase == Phase.Berserk ? BerserkSpeedBonus : 1f;
	protected override bool OverheadFinisher => _phase == Phase.Berserk;

	// Hold the wind-up while the player's parry is open toward him (they'll whiff it), and for the
	// feint delay against a player who parries a lot.
	protected override bool HoldTelegraph()
	{
		if (!Smart || PlayerReads.Find(this) is not { } player)
			return false;
		_feintTimer -= 0.05f;
		return _feintTimer > 0f || PlayerReads.IsBlockingToward(player, GlobalPosition.X);
	}

	protected override float MaxTelegraphHold => Smart ? 0.7f : 0f;

	// ── Brain ───────────────────────────────────────────────────────────────────────────────

	protected override bool Decide(Metroidvania.Player.Player player, float distanceX, float absDistance)
	{
		if (!Smart)
			return false;
		float toward = Mathf.Sign(distanceX);
		float away = -toward;
		bool parryHappy = _blockStarts >= ParryHappyBlocks;

		// Hurt twice in a row: get out.
		if (_hitsInRow >= 2 && DodgeCooldownTimer <= 0f && HasRoomToDodge(away))
		{
			_hitsInRow = 0;
			_ = Dodge(away);
			return true;
		}
		// Their swing starting right next to him: slip it (more often the more aggressive they are).
		if (Whiff.PlayerStartedSwing && absDistance <= DodgeTriggerRange && DodgeCooldownTimer <= 0f
			&& Random.Randf() < DodgeChance && HasRoomToDodge(away))
		{
			_ = Dodge(away);
			return true;
		}
		// Never swing into i-frames.
		if (player.IsDashing || player.IsRolling)
			return true;

		bool opening = PlayerReads.WhiffedParryToward(player, GlobalPosition.X) || player.IsHealing
			|| _sinceEvadeEnded < PostEvadeWindow || Whiff.JustWhiffed;
		if (opening)
		{
			if (absDistance <= SlashRange + 10f && AttackCooldownTimer <= AttackCooldown * 0.6f)
			{
				Whiff.Consume();
				_feintTimer = 0f;
				_ = DoubleSlash(0.1f, allowSecond: true);
				return true;
			}
			if (absDistance >= JumpSlamMinDistance && absDistance <= JumpSlamMaxDistance && _jumpCooldown <= 0f)
			{
				_ = JumpSlam(player);
				return true;
			}
		}

		// Don't walk a swing into an open parry: wait for it to close (and punish the whiff above).
		if (PlayerReads.IsBlockingToward(player, GlobalPosition.X) && absDistance <= SlashRange + 20f)
			return true;

		if (_phase == Phase.Berserk && absDistance >= LeapMinDistance && absDistance <= LeapMaxDistance
			&& _leapCooldown <= 0f && Random.Randf() < 0.5f)
		{
			_ = Leap();
			return true;
		}
		bool retreating = !PlayerReads.IsClosingIn(player, GlobalPosition.X, 0f) && Mathf.Abs(player.Velocity.X) > 40f;
		if (absDistance >= JumpSlamMinDistance && absDistance <= JumpSlamMaxDistance && _jumpCooldown <= 0f
			&& (retreating || Random.Randf() < 0.35f))
		{
			_ = JumpSlam(player);
			return true;
		}

		if (absDistance > SlashRange || AttackCooldownTimer > 0f)
			return true;

		// In range, ready: score the options.
		float slashScore = 1f;
		float whirlScore = WhirlCooldownTimer <= 0f
			? 0.3f + (parryHappy ? 0.7f : 0f) + (absDistance < 50f ? 0.4f : 0f)
			: 0f;
		if (Random.Randf() * (slashScore + whirlScore) < whirlScore)
		{
			_ = Whirlwind();
			return true;
		}
		_feintTimer = parryHappy && Random.Randf() < 0.6f ? Random.RandfRange(0.15f, 0.35f) : 0f;
		_ = DoubleSlash(SlashTelegraph * TelegraphScale, allowSecond: true);
		return true;
	}

	// ── Jump slam (jump sheet) ──────────────────────────────────────────────────────────────

	// Leaps (aimed where the player will be by landing time) and drives the hammer into the ground.
	private async Task JumpSlam(Metroidvania.Player.Player player)
	{
		if (!Begin())
			return;
		_jumpCooldown = JumpSlamCooldown;
		try
		{
			Sprite.Play("jump");
			Sprite.Pause();
			float predicted = player.GlobalPosition.X + player.Velocity.X * 0.55f * LeadAccuracy;
			float travel = Mathf.Max(0f, Mathf.Abs(predicted - GlobalPosition.X) - 60f);
			BeginRootMotion(JumpDrift, JumpOffset, Mathf.Clamp(travel / JumpTravelPx, 0.25f, 1.6f));
			if (!await Frames(0, 3, 0.045f))
				return;
			FlashBody(0.3f * TelegraphScale);
			if (!await Frames(4, 6, 0.1f * TelegraphScale))
				return;
			Stats.ExternalInvulnerable = true;
			if (!await Frames(7, 14, 0.045f / Speed()))
				return;
			Stats.ExternalInvulnerable = false;
			Sprite.Frame = 15;
			float dir = FacingRight ? 1f : -1f;
			GroundSlamFx.Impact(this, GlobalPosition + new Vector2(dir * 69f, FeetY), SlamImpactScale * 1.2f, SlamPalette);
			Shake(SlamShake * 1.3f, 0.3f);
			bool hit = Strike(new Vector2(28f, 7f), new Vector2(96f, 46f), Damage(1.3f), 320f);
			if (!await Wait(0.06f))
				return;
			for (int f = 16; f <= 17; f++)
			{
				Sprite.Frame = f;
				if (!hit)
					hit = Strike(new Vector2(28f, 7f), new Vector2(96f, 46f), Damage(1.3f), 320f);
				if (!await Wait(0.06f))
					return;
			}
			if (!await Frames(18, 24, 0.05f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				EndRootMotion();
				Finish();
			}
		}
		AttackCooldownTimer = AttackCooldown * 0.5f;
	}

	// ── Leap: hammer throw + dive (leap sheet) ──────────────────────────────────────────────

	// Local forward distance from the body to a cell x on a leap frame (root motion already moved the
	// body by that frame's drift, and the offset hides it).
	private static float LeapX(float cellX, int frame) => (cellX - FigureCellX - LeapDrift[frame]) * SheetScale - 1.8f;

	private async Task Leap()
	{
		if (!Begin())
			return;
		_leapCooldown = LeapCooldown;
		_jumpCooldown = Mathf.Max(_jumpCooldown, 1.5f);
		try
		{
			Sprite.Play("leap");
			Sprite.Pause();
			BeginRootMotion(LeapDrift, LeapOffset);
			FlashBody(0.6f * TelegraphScale);
			Shake(SwingShake, 0.4f);
			if (!await Frames(0, 7, 0.07f * TelegraphScale))
				return;
			Stats.ExternalInvulnerable = true;
			if (!await Frames(8, 19, 0.05f / Speed()))
				return;
			float dir = FacingRight ? 1f : -1f;

			// 20: the thrown hammer plants itself.
			Sprite.Frame = 20;
			float hammerX = LeapX(186f, 20);
			GroundSlamFx.Impact(this, GlobalPosition + new Vector2(dir * hammerX, FeetY), SlamImpactScale * 1.4f, SlamPalette);
			Shake(SlamShake, 0.35f);
			Strike(new Vector2(hammerX, 10f), new Vector2(70f, 44f), Damage(1.2f), 300f);
			if (!await Wait(0.06f))
				return;
			Sprite.Frame = 21;
			if (!await Wait(0.05f))
				return;

			// 22-24: shockwaves out both sides of it.
			Sprite.Frame = 22;
			GroundSlamFx.Chip(this, GlobalPosition + new Vector2(dir * (hammerX - 63f), FeetY), 0.8f, SlamPalette);
			GroundSlamFx.Chip(this, GlobalPosition + new Vector2(dir * (hammerX + 63f), FeetY), 0.8f, SlamPalette);
			Strike(new Vector2(LeapX(186f, 22), 12f), new Vector2(150f, 40f), Damage(1f), 260f, ignoreFacing: false);
			if (!await Frames(23, 25, 0.05f))
				return;

			// 26: dives onto the hammer with a ground slash running forward.
			Stats.ExternalInvulnerable = false;
			Sprite.Frame = 26;
			GroundSlamFx.Impact(this, GlobalPosition + new Vector2(dir * 20f, FeetY), SlamImpactScale * 1.5f, SlamPalette);
			Shake(SlamShake * 1.4f, 0.45f);
			float reach = LeapX(250f, 26);
			bool hit = Strike(new Vector2((reach - 20f) * 0.5f, 8f), new Vector2(reach + 20f, 48f), Damage(1.5f), 380f);
			if (!await Wait(0.06f))
				return;
			for (int f = 27; f <= 29; f++)
			{
				Sprite.Frame = f;
				if (!hit)
					hit = Strike(new Vector2((reach - 20f) * 0.5f, 8f), new Vector2(reach + 20f, 48f), Damage(1.5f), 380f);
				if (!await Wait(0.06f))
					return;
			}
			if (!await Frames(30, 39, 0.05f))
				return;
		}
		finally
		{
			if (IsInstanceValid(this))
			{
				Stats.ExternalInvulnerable = false;
				EndRootMotion();
				Finish();
			}
		}
		AttackCooldownTimer = AttackCooldown * 0.7f;
	}
}
