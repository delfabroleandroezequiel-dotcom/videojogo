using Godot;

namespace Metroidvania.World;

// Boss.cs already provides the generic wander/retreat/lunge/combo/enrage chassis (see BossLobo).
// This only adds what makes THIS boss a spider: a ranged poison spit alongside its melee/lunge,
// and summoning spiderling reinforcements at HP breakpoints (rather than a repeating timer) so
// adds read as a deliberate phase change instead of an infinite spam mechanic.
//
// The spit used to be a web shot (plain Projectile, straight line); it's now PoisonSpit's same
// glob visual as the small spiders' own poison attack, just launched with ArcGravity=0 on its own
// scene (PoisonSpitBoss.tscn) so it still travels dead straight instead of lobbing — a boss-scale
// "spit," not a parabola — and scaled up 25% over the small spiders' version.
//
// This is the CuevaBosqueLobo1 encounter specifically — SpiderBossArena is a separate, independent
// boss (its own harder kit: no melee, escalating phases, infinite spiderling spam) that happens to
// reuse this scene as its starting point but diverges completely in SpiderBossArena.cs. Don't fold
// arena-only behavior back in here; this one stays as its own simpler fight.
public partial class SpiderBoss : Boss
{
	[Export] public PackedScene PoisonSpitScene;
	[Export] public float WebSpitRange = 280f;
	[Export] public float WebSpitCooldown = 3.5f;
	[Export] public float WebSpitReleaseDelay = 0.3f;
	[Export] public float WebSpitRecoverDuration = 0.3f;
	[Export] public float PoisonSpitSpeed = 220f;

	[Export] public PackedScene SpiderlingScene;
	[Export] public int SpiderlingsPerSummon = 2;
	[Export] public float SpiderlingSpreadX = 70f;
	[Export] public float[] SummonHealthThresholds = { 0.66f, 0.33f };

	// Neon-green blink before every attack (bite, lunge, spit) — same tell as the small spiders
	// (SpiderEnemy), its own copy here per the boss-uniqueness rule. SelfModulate + an additive glow,
	// so it never touches Visual.Modulate (enrage tint / SpiderBossArena's dark tint live there).
	// The bite and lunge used to land 0.12s / 0.22s after starting — too fast to read — so each now
	// gets a short lead-in (MeleeTelegraphLead / LungeLead) before the original timing.
	[ExportGroup("Telegraph")]
	[Export] public Color TelegraphTint = new(0.55f, 3.2f, 0.75f, 1f);
	[Export] public Color TelegraphGlowColor = new(0.3f, 1f, 0.4f, 0.9f);
	[Export] public float TelegraphGlowScale = 2.6f;
	[Export] public int TelegraphBlinks = 3;
	[Export] public float MeleeTelegraphLead = 0.3f;
	[Export] public float LungeLead = 0.3f;

	private Sprite2D _telegraphGlow;
	private Tween _telegraphTween;
	private float _webSpitCooldownTimer;	private bool _isWebSpitting;
	private int _nextSummonIndex;

	// SpiderSpriteFrames.tres only has "attack" (no "attack1") — Boss.cs's default is tuned for
	// BossLobo/BanditBoss's naming instead.
	protected override string DefaultAttackAnimation => "attack";

	// Neutral here (this encounter's cooldown never changes) — exists purely so SpiderBossArena can
	// scale its own web spit faster per phase without touching this class or its private timer.
	protected virtual float WebSpitCooldownMultiplier => 1f;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_webSpitCooldownTimer = WebSpitCooldown * 0.5f;

		_telegraphGlow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = TelegraphGlowColor with { A = 0f },
			Scale = Vector2.One * TelegraphGlowScale,
			ShowBehindParent = true,
		};
		Visual.AddChild(_telegraphGlow);
	}

	protected override float LungeTelegraphLead => LungeLead;
	protected override void OnAttackTelegraph(float duration) => Telegraph(duration);

	private void Telegraph(float duration)
	{
		if (Sprite is null || duration <= 0f)
			return;
		_telegraphTween?.Kill();
		_telegraphTween = CreateTween();
		float half = duration / Mathf.Max(1, TelegraphBlinks) * 0.5f;
		for (int i = 0; i < TelegraphBlinks; i++)
		{
			_telegraphTween.TweenProperty(Sprite, "self_modulate", TelegraphTint, half);
			_telegraphTween.Parallel().TweenProperty(_telegraphGlow, "modulate:a", TelegraphGlowColor.A, half);
			_telegraphTween.TweenProperty(Sprite, "self_modulate", Colors.White, half);
			_telegraphTween.Parallel().TweenProperty(_telegraphGlow, "modulate:a", 0f, half);
		}
	}

	// Bite: blink over a short lead-in, then Boss.Attack's own (unchanged) timing.
	protected override async void Attack(bool isCombo = false)
	{
		float lead = isCombo ? MeleeTelegraphLead * 0.5f : MeleeTelegraphLead;
		if (lead > 0f)
		{
			_attacking = true;
			_canAttack = false;
			Telegraph(lead + AttackHitboxDelay);
			await ToSignal(GetTree().CreateTimer(lead), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
		}
		base.Attack(isCombo);
	}
	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		CheckSummonThreshold();

		_webSpitCooldownTimer -= (float)delta;
		if (_isWebSpitting || _webSpitCooldownTimer > 0f || PoisonSpitScene is null)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
			return;

		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (distance <= WebSpitRange)
			SpitWeb(player.GlobalPosition);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_isWebSpitting)
		{
			if (Sprite is not null && Sprite.Animation != "attack")
				Sprite.Play("attack");
			return;
		}

		base.UpdateAnimation(velocity);
	}

	private void CheckSummonThreshold()
	{
		if (SpiderlingScene is null || _nextSummonIndex >= SummonHealthThresholds.Length)
			return;

		float threshold = SummonHealthThresholds[_nextSummonIndex];
		if (Stats.CurrentHealth > Stats.MaxHealth * threshold)
			return;

		_nextSummonIndex++;
		SummonSpiderlings();
	}

	private void SummonSpiderlings()
	{
		for (int i = 0; i < SpiderlingsPerSummon; i++)
		{
			Node spiderling = SpiderlingScene.Instantiate();
			GetTree().CurrentScene.AddChild(spiderling);

			if (spiderling is Node2D spiderling2D)
			{
				float side = i % 2 == 0 ? 1f : -1f;
				float spread = SpiderlingSpreadX * (1 + i / 2);
				spiderling2D.GlobalPosition = GlobalPosition + new Vector2(side * spread, 0f);
			}
		}
	}

	private async void SpitWeb(Vector2 targetPosition)
	{
		_isWebSpitting = true;
		_webSpitCooldownTimer = WebSpitCooldown * WebSpitCooldownMultiplier;
		Telegraph(WebSpitReleaseDelay);

		await ToSignal(GetTree().CreateTimer(WebSpitReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		PoisonSpit spit = PoisonSpitScene.Instantiate<PoisonSpit>();
		GetTree().CurrentScene.AddChild(spit);
		spit.GlobalPosition = GlobalPosition;
		// ArcGravity is 0 on PoisonSpitBoss.tscn, so this straight-line velocity is the whole
		// trajectory — no lob, just a direct spit toward wherever the player was at release time.
		spit.Launch((targetPosition - GlobalPosition).Normalized() * PoisonSpitSpeed, Stats);

		await ToSignal(GetTree().CreateTimer(WebSpitRecoverDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isWebSpitting = false;
	}
}
