using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The arena spider's phases, tied to the rockslide it's buried under (user, 2026-11-10):
//  1 (100-66%) buried: spit, poison bursts under the player, one falling spiderling.
//  2 (66-33%)  the big rock falls and a front leg comes free: a telegraphed stomp in front of it,
//              so standing at its face is no longer free.
//  3 (33-0%)   it tears out of the pile and the fight changes: no more poison bursts; it climbs out
//              of view and drops on the player (its eyes glow above, dust pours down first) and shoots webs
//              that snare unless rolled or parried. The spit and spiderlings stay.
// And in every phase: parrying the poison glob sends it back; if it hits the spider, it's stunned
// for ParryStunDuration (and the reflected glob leaves no puddle).
// Airborne, only Visual moves up: the body stays on the floor (collision off), so the camera's
// boss framing doesn't chase it into the sky.
public partial class SpiderBossArena
{
	[ExportGroup("Fases")]
	[Export] public NodePath ForcejeoPath = "../Pedrasboss/Forcejeo";
	[Export] public float ParryStunDuration = 2f;

	[ExportSubgroup("Fase 2: pisoton")]
	[Export] public float StompRange = 190f;
	[Export] public float StompReach = 200f;
	[Export] public float StompTelegraph = 0.55f;
	[Export] public float StompCooldown = 2.6f;
	[Export] public int StompDamage = 30;
	// "attack" clip (rear-up + slam): the legs hit the floor on frame 11 at 13 fps.
	[Export] public float StompHitDelay = 0.85f;
	[Export] public float StompRecover = 0.55f;

	[ExportSubgroup("Fase 3: liberada")]
	[Export] public float BreakFreeDistance = 240f;
	[Export] public float ArenaMinX = -620f;
	[Export] public float ArenaMaxX = 330f;
	[Export] public float BreakFreeQuakeTime = 1.8f;
	[Export] public Vector2 BreakFreePileOffset = new(150f, -90f);
	[Export] public float FreeIdleMin = 1.4f;
	[Export] public float FreeIdleMax = 2.2f;
	// After this many attacks it stops, winded, for RestDuration: the big opening.
	[Export] public int RestEvery = 2;
	[Export] public float RestDuration = 2.8f;
	[Export(PropertyHint.Range, "0,1")] public float DiveChance = 0.6f;
	[Export] public float DiveRiseHeight = 700f;
	[Export] public float DiveTrackTime = 0.75f;
	[Export] public float DiveLockTime = 0.4f;
	[Export] public float DiveFallTime = 0.2f;
	[Export] public float DiveWidth = 190f;
	[Export] public int DiveDamage = 40;
	[Export] public float DiveRecover = 1.7f;
	[Export] public float WebTelegraph = 0.5f;
	[Export] public float WebSpeed = 330f;
	[Export] public float WebSnareDuration = 1.3f;
	// Cocoon: presses of Attack to tear out, and the longest it can hold you.
	[Export] public int MashPresses = 8;
	[Export] public float MaxTrapTime = 4.5f;

	[ExportSubgroup("Fase 3: onda al caer")]
	[Export] public int WaveTilesPerSide = 3;
	[Export] public float WaveTileSpacing = 58f;
	[Export] public Vector2 WaveTileSize = new(52f, 36f);
	[Export] public float WaveWarningLead = 0.22f;
	[Export] public float WaveStrikeGap = 0.11f;
	[Export] public int WaveDamage = 25;

	private float _stunTimer;
	private bool _actionBusy;
	private bool _animLock;
	private bool _free;
	private int _actionId;
	private float _stompCooldownTimer;
	private Node2D _stunStars;
	private Tween _moveTween;

	private bool Stunned => _stunTimer > 0f;
	private bool StillValid(int id) => IsInstanceValid(this) && !IsQueuedForRemoval && id == _actionId;

	private async System.Threading.Tasks.Task<bool> Wait(float seconds, int id)
	{
		await ToSignal(GetTree().CreateTimer(seconds, false), SceneTreeTimer.SignalName.Timeout);
		return StillValid(id);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_animLock || Stunned)
			return;
		base.UpdateAnimation(velocity);
	}

	// Returns true when the phase logic owns this frame (stun or an action running): the base AI
	// (spit, facing, movement) doesn't run.
	private bool PhasesOwnFrame(float dt)
	{
		_stompCooldownTimer -= dt;
		TickPoise(dt);
		if (Stunned)
		{
			_stunTimer -= dt;
			if (_stunTimer <= 0f)
				EndStun();
			return true;
		}
		return _actionBusy;
	}

	// After the base AI ran: facing and starting the phase's own attacks.
	private void PhasesAfterBase()
	{
		if (_free)
			FacePlayer();
		else
			LockFacingLeft();
		if (_actionBusy || IsQueuedForRemoval)
			return;
		if (!_free && _currentPhase >= 2 && _stompCooldownTimer <= 0f && PlayerInStompRange())
			Stomp();
	}

	private void FacePlayer()
	{
		Node2D player = EnemyTargeting.Current(GetTree());
		if (player is null)
			return;
		FacingRight = player.GlobalPosition.X >= GlobalPosition.X;
		Visual.Scale = new Vector2(FacingRight ? 1 : -1, 1);
	}

	private float Facing => FacingRight ? 1f : -1f;
	// The floor surface under the spider. Its origin sits ~45 px above the floor (the sprite's own
	// offset), so using GlobalPosition.Y left every floor effect and floor hitbox floating.
	private float GroundY
	{
		get
		{
			Vector2 from = GlobalPosition + new Vector2(0f, -20f);
			var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0f, 300f), 1u, new Godot.Collections.Array<Rid> { GetRid() });
			var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
			return hit.Count > 0 ? ((Vector2)hit["position"]).Y : GlobalPosition.Y + FloorBelowOrigin;
		}
	}

	// Fallback when no floor is found under it.
	[Export] public float FloorBelowOrigin = 45f;

	private void PlayAnim(string animation)
	{
		if (Sprite?.SpriteFrames?.HasAnimation(animation) == true)
		{
			Sprite.Stop();
			Sprite.Play(animation);
		}
	}

	private void EndAction()
	{
		_actionBusy = false;
		_animLock = false;
	}

	// ── Parry: the reflected glob stuns ────────────────────────────────────────────────────

	private void WatchReflected(PoisonSpit spit)
	{
		spit.PuddleWhenReflected = false;
		spit.ReflectedHit += body =>
		{
			if (body == this)
				Stun();
		};
	}

	private void Stun()
	{
		if (!CutOffForStun(ParryStunDuration))
			return;
		PlaySound("Quejido");
		TrapKit.Shake(this, 6f, 0.35f);
		TrapKit.Burst(this, GlobalPosition + new Vector2(0f, -70f), NeonColor, 30, 260f, 0.5f, 0.08f);
		ShowStunStars();
	}

	// Cuts off whatever it was doing and holds it in "hurt" for `duration`. Not while asleep or in
	// the middle of tearing out of the rockslide (that sequence must finish).
	private bool CutOffForStun(float duration)
	{
		if (IsQueuedForRemoval || Dormant || _breakingFree)
			return false;
		_actionId++;
		EndAction();
		ResetAirborne();
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		_stunTimer = duration;
		PlayAnim("hurt");
		return true;
	}

	// ── Poise: keep hitting it and it staggers ─────────────────────────────────────────────

	[ExportGroup("Postura")]
	// Hits that break its stance; each second without being hit takes PoiseDecay off.
	[Export] public float PoiseMax = 7f;
	[Export] public float PoiseDecay = 1.2f;
	[Export] public float StaggerDuration = 1.6f;
	// Cool-down so it can't be stun-locked.
	[Export] public float StaggerCooldown = 6f;
	private float _poise;
	private float _poiseCooldown;
	private bool _breakingFree;

	private void OnHitForPoise()
	{
		if (Dormant || Stunned || _breakingFree || _poiseCooldown > 0f || Stats.CurrentHealth <= 0)
			return;
		_poise += 1f;
		if (_poise >= PoiseMax)
			Stagger();
	}

	private void TickPoise(float dt)
	{
		_poise = Mathf.Max(0f, _poise - PoiseDecay * dt);
		_poiseCooldown -= dt;
	}

	// Stance broken: a white flash through the body, chitin chips flying, a short freeze, and it
	// reels in "hurt" for StaggerDuration: free hits.
	private void Stagger()
	{
		if (!CutOffForStun(StaggerDuration))
			return;
		_poise = 0f;
		_poiseCooldown = StaggerCooldown;
		PlaySound("Quejido");
		PlaySound("Patas");
		TrapKit.Shake(this, 9f, 0.4f);
		SlowMotion(0.15f, 0.12f);
		TrapKit.Burst(this, GlobalPosition + new Vector2(0f, -50f), new Color(0.45f, 0.08f, 0.1f, 1f), 36, 300f, 0.55f, 0.08f);
		TrapKit.Burst(this, GlobalPosition + new Vector2(0f, -50f), new Color(1f, 0.95f, 0.9f, 1f), 16, 380f, 0.25f, 0.05f);
		if (Sprite is not null)
		{
			Tween t = CreateTween();
			t.TweenProperty(Sprite, "self_modulate", new Color(3f, 3f, 3f, 1f), 0.04f);
			t.TweenProperty(Sprite, "self_modulate", Colors.White, 0.35f);
		}
	}

	private void EndStun()
	{
		_stunTimer = 0f;
		_stunStars?.QueueFree();
		_stunStars = null;
		_stompCooldownTimer = Mathf.Max(_stompCooldownTimer, 0.8f);
	}

	// Three neon motes circling over its head: "dizzy, hit it now".
	private void ShowStunStars()
	{
		_stunStars?.QueueFree();
		_stunStars = new Node2D { Position = new Vector2(0f, -120f), ZIndex = 6 };
		AddChild(_stunStars);
		for (int i = 0; i < 3; i++)
		{
			var mote = new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = NeonColor, Scale = Vector2.One * 0.09f };
			mote.Position = Vector2.Right.Rotated(Mathf.Tau * i / 3f) * new Vector2(34f, 12f);
			_stunStars.AddChild(mote);
		}
		_stunStars.AddChild(TrapKit.Light(NeonColor, 1.2f, 1.4f));
		Tween spin = _stunStars.CreateTween().SetLoops();
		spin.TweenMethod(Callable.From<float>(a =>
		{
			if (!IsInstanceValid(_stunStars))
				return;
			for (int i = 0; i < 3; i++)
				if (_stunStars.GetChild(i) is Node2D m)
					m.Position = new Vector2(Mathf.Cos(a + Mathf.Tau * i / 3f) * 34f, Mathf.Sin(a + Mathf.Tau * i / 3f) * 12f);
		}), 0f, Mathf.Tau, 0.7f);
	}

	// ── Phase 2: stomp with the freed leg ──────────────────────────────────────────────────

	private bool PlayerInStompRange()
	{
		Node2D player = EnemyTargeting.Current(GetTree());
		if (player is null)
			return false;
		float ahead = (player.GlobalPosition.X - GlobalPosition.X) * Facing;
		return ahead > -20f && ahead <= StompRange && Mathf.Abs(player.GlobalPosition.Y - GroundY) < 160f;
	}

	private async void Stomp()
	{
		int id = ++_actionId;
		_actionBusy = true;
		_animLock = true;
		Velocity = Vector2.Zero;
		_stompCooldownTimer = StompCooldown;

		// Tell: the body blinks, the leg rears up and dust kicks up where it will land.
		OnAttackTelegraph(StompTelegraph + StompHitDelay);
		Vector2 from = new(GlobalPosition.X + Facing * 20f, GroundY);
		Vector2 to = new(GlobalPosition.X + Facing * StompReach, GroundY);
		StompDustTell(to - new Vector2(Facing * 40f, 0f));
		PlaySound("Carga", 0.5f);
		if (!await Wait(StompTelegraph, id))
			return;

		PlayAnim("attack");
		if (!await Wait(StompHitDelay, id))
			return;

		Vector2 centre = (from + to) / 2f;
		var hazard = Hazard.CreateArea(GetTree().CurrentScene, instantKill: false, damage: StompDamage, knockbackForce: 420f);
		hazard.GlobalPosition = centre + new Vector2(0f, -45f);
		hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Mathf.Abs(to.X - from.X), 90f) } });
		GetTree().CreateTimer(0.15f, false).Timeout += () => { if (IsInstanceValid(hazard)) hazard.QueueFree(); };
		GroundSlamFx.Impact(this, to - new Vector2(Facing * 40f, 0f), 1.1f);
		TrapKit.Shake(this, 7f, 0.35f);
		PlaySound("Patas");

		if (!await Wait(StompRecover, id))
			return;
		EndAction();
	}

	// ── Phase 3: tearing free ──────────────────────────────────────────────────────────────

	// The moment it tears out of the rockslide has to feel like the cave coming down: a quake that
	// builds over three heaves against the pile (rocks raining from the ceiling all over the arena),
	// then the pile bursts (white flash, a beat of slow motion, chunks flying) and it lands on the
	// open floor hard enough to send spikes out both ways. The falling rocks are scenery (no damage):
	// too many to read fairly.
	private async void BreakFree()
	{
		int id = ++_actionId;
		_actionBusy = true;
		_animLock = true;
		_free = true;
		_breakingFree = true;
		// Started now so a stun mid-sequence (which cancels this action) can not leave it free but idle.
		FreeLoop();
		DerrumbeForcejeo forcejeo = GetNodeOrNull<DerrumbeForcejeo>(ForcejeoPath);
		RainRocks(BreakFreeQuakeTime + 0.8f);
		IntensifyMusic(BreakFreeQuakeTime + 0.65f);

		// Three heaves, each harder.
		for (int heave = 0; heave < 3; heave++)
		{
			forcejeo?.Tug();
			RoarFlash(PhaseColor);
			TrapKit.Shake(this, 8f + heave * 3f, BreakFreeQuakeTime / 3f + 0.2f);
			if (heave != 1)
				PlaySound("Rugido", 1.2f);
			if (!await Wait(BreakFreeQuakeTime / 3f, id))
				return;
		}

		// The pile gives.
		forcejeo?.Release();
		Vector2 pile = GlobalPosition + BreakFreePileOffset;
		ScreenFlash(0.55f, 0.35f);
		SlowMotion(0.3f, 0.45f);
		TrapKit.Shake(this, 16f, 0.9f);
		PlaySound("Explosion");
		PlaySound("Rugido");
		GroundSlamFx.Impact(this, new Vector2(pile.X, GroundY), 2f, WavePalette);
		TrapKit.Burst(this, pile, new Color(0.55f, 0.5f, 0.45f, 1f), 50, 420f, 0.7f, 0.1f);
		for (int i = 0; i < 14; i++)
			ThrowChunk(pile);

		PlayAnim("lunge");
		// One big leap out of the rubble onto the open floor.
		float startX = GlobalPosition.X;
		float landX = Mathf.Clamp(startX - BreakFreeDistance, ArenaMinX, ArenaMaxX);
		Tween leap = _moveTween = CreateTween();
		leap.TweenMethod(Callable.From<float>(t =>
		{
			GlobalPosition = new Vector2(Mathf.Lerp(startX, landX, t), GlobalPosition.Y);
			Visual.Position = new Vector2(0f, -Mathf.Sin(t * Mathf.Pi) * 170f);
		}), 0f, 1f, 0.65f);
		if (!await Wait(0.65f, id))
			return;
		Visual.Position = Vector2.Zero;
		Land(1.6f);
		RunSpikeWave(GlobalPosition.X, 1f);
		RunSpikeWave(GlobalPosition.X, -1f);
		PlayAnim("idle");
		if (!await Wait(0.9f, id))
			return;
		PlaySound("Rugido");
		RoarFlash(PhaseColor);
		TrapKit.Shake(this, 7f, 0.5f);
		if (!await Wait(0.6f, id))
			return;
		_breakingFree = false;
		EndAction();
	}

	private void Land(float strength)
	{
		GroundSlamFx.Impact(this, new Vector2(GlobalPosition.X, GroundY), 1.2f * strength);
		TrapKit.Shake(this, 9f * strength, 0.4f);
		PlaySound("Patas");
	}

	// Rocks dropping from the ceiling across the whole arena for `duration` seconds.
	private async void RainRocks(float duration)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		float elapsed = 0f;
		while (elapsed < duration && IsInstanceValid(this) && !IsQueuedForRemoval)
		{
			float x = rng.RandfRange(ArenaMinX - 120f, ArenaMaxX + 300f);
			float size = rng.RandfRange(3f, 9f);
			Node2D rock = GroundSlamFx.MakeRock(size, WavePalette);
			rock.ZIndex = 3;
			GetTree().CurrentScene.AddChild(rock);
			rock.GlobalPosition = new Vector2(x, ViewTopY() - 20f);
			float time = Mathf.Sqrt(2f * Mathf.Max(20f, GroundY - rock.GlobalPosition.Y) / 1300f);
			Tween t = rock.CreateTween();
			t.TweenProperty(rock, "global_position:y", GroundY - size * 0.5f, time).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
			t.Parallel().TweenProperty(rock, "rotation", rng.RandfRange(-8f, 8f), time);
			t.TweenCallback(Callable.From(() => GroundSlamFx.Chip(this, new Vector2(rock.GlobalPosition.X, GroundY), 0.35f + size * 0.04f, WavePalette)));
			t.TweenProperty(rock, "modulate:a", 0f, 0.5f).SetDelay(0.6f);
			t.TweenCallback(Callable.From(rock.QueueFree));
			float gap = rng.RandfRange(0.03f, 0.08f);
			elapsed += gap;
			await ToSignal(GetTree().CreateTimer(gap, false), SceneTreeTimer.SignalName.Timeout);
		}
	}

	// A chunk of the pile flung out in an arc.
	private void ThrowChunk(Vector2 from)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		float size = rng.RandfRange(6f, 14f);
		Node2D rock = GroundSlamFx.MakeRock(size, WavePalette);
		rock.ZIndex = 4;
		GetTree().CurrentScene.AddChild(rock);
		rock.GlobalPosition = from;
		float landX = from.X + rng.RandfRange(-420f, 160f);
		float height = rng.RandfRange(80f, 220f);
		float time = rng.RandfRange(0.55f, 0.9f);
		float startY = from.Y;
		Tween t = rock.CreateTween();
		t.TweenMethod(Callable.From<float>(k =>
		{
			if (!IsInstanceValid(rock))
				return;
			float y = Mathf.Lerp(startY, GroundY - size * 0.5f, k) - Mathf.Sin(k * Mathf.Pi) * height;
			rock.GlobalPosition = new Vector2(Mathf.Lerp(from.X, landX, k), y);
		}), 0f, 1f, time);
		t.Parallel().TweenProperty(rock, "rotation", rng.RandfRange(-10f, 10f), time);
		t.TweenCallback(Callable.From(() => GroundSlamFx.Chip(this, new Vector2(landX, GroundY), 0.6f, WavePalette)));
		t.TweenProperty(rock, "modulate:a", 0f, 0.6f).SetDelay(1.2f);
		t.TweenCallback(Callable.From(rock.QueueFree));
	}

	private void ScreenFlash(float alpha, float duration)
	{
		var layer = new CanvasLayer { Layer = 18 };
		var rect = new ColorRect { Color = new Color(1f, 1f, 1f, alpha), MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(rect);
		GetTree().CurrentScene.AddChild(layer);
		Tween fade = layer.CreateTween().SetIgnoreTimeScale();
		fade.TweenProperty(rect, "color:a", 0f, duration);
		fade.TweenCallback(Callable.From(layer.QueueFree));
	}

	private async void SlowMotion(float scale, float realSeconds)
	{
		Engine.TimeScale = scale;
		await ToSignal(GetTree().CreateTimer(realSeconds, true, false, true), SceneTreeTimer.SignalName.Timeout);
		if (Mathf.IsEqualApprox(Engine.TimeScale, scale))
			Engine.TimeScale = 1.0;
	}

	// While free it fights in rounds: a beat on the ground (hittable, may spit), an attack, and
	// after every RestEvery attacks it stops to catch its breath: the big opening.
	private async void FreeLoop()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		int attacks = 0;
		while (IsInstanceValid(this) && !IsQueuedForRemoval)
		{
			if (!await UntilIdle())
				return;
			// On the ground between attacks: the window.
			await ToSignal(GetTree().CreateTimer(rng.RandfRange(FreeIdleMin, FreeIdleMax), false), SceneTreeTimer.SignalName.Timeout);
			if (!await UntilIdle())
				return;
			if (EnemyTargeting.Current(GetTree()) is null)
				continue;
			if (rng.Randf() < DiveChance)
				Dive();
			else
				ShootWeb();
			attacks++;
			if (RestEvery > 0 && attacks % RestEvery == 0)
			{
				if (!await UntilIdle())
					return;
				Rest();
			}
		}
	}

	// Waits out a stun or any action in progress; false if the boss is gone.
	private async System.Threading.Tasks.Task<bool> UntilIdle()
	{
		while (Stunned || _actionBusy || _isWebSpittingNow)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return false;
		}
		return IsInstanceValid(this) && !IsQueuedForRemoval;
	}

	// Winded: stands panting (slow idle, no spit), green breath puffing out. Hit it now.
	private async void Rest()
	{
		int id = ++_actionId;
		_actionBusy = true;
		_animLock = true;
		PlayAnim("idle");
		if (Sprite is not null)
			Sprite.SpeedScale = 0.45f;
		PlaySound("Quejido");
		float elapsed = 0f;
		while (elapsed < RestDuration)
		{
			Vector2 mouth = GlobalPosition + new Vector2(Facing * 55f, -45f);
			TrapKit.Burst(this, mouth, NeonColor with { A = 0.6f }, 6, 50f, 0.6f, 0.05f);
			DustPuff(mouth + new Vector2(Facing * 10f, 0f), 18f);
			if (!await Wait(0.45f, id))
			{
				if (Sprite is not null)
					Sprite.SpeedScale = 1f;
				return;
			}
			elapsed += 0.45f;
		}
		if (Sprite is not null)
			Sprite.SpeedScale = 1f;
		EndAction();
	}

	private bool _isWebSpittingNow => Sprite is not null && Sprite.Animation == "spit" && Sprite.IsPlaying();

	private async void Dive()
	{
		int id = ++_actionId;
		_actionBusy = true;
		_animLock = true;
		Velocity = Vector2.Zero;

		// Up and out of view: the body can't be hit while it's gone.
		PlayAnim("lunge");
		PlaySound("Rugido", 0.8f);
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		Tween rise = _moveTween = CreateTween();
		rise.TweenProperty(Visual, "position:y", -DiveRiseHeight, 0.35f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		if (!await Wait(0.35f, id))
			return;

		// Up there in the dark only its eyes show, following the player with dust pouring down
		// from them; then they flare and stop: that's where it lands.
		var eyes = MakeCeilingEyes();
		float elapsed = 0f;
		float targetX = GlobalPosition.X;
		while (elapsed < DiveTrackTime + DiveLockTime)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!StillValid(id))
			{
				eyes.Close();
				return;
			}
			float dt = (float)GetPhysicsProcessDeltaTime();
			elapsed += dt;
			if (elapsed < DiveTrackTime && EnemyTargeting.Current(GetTree()) is Node2D player)
				targetX = Mathf.Clamp(Mathf.MoveToward(targetX, player.GlobalPosition.X, 900f * dt), ArenaMinX, ArenaMaxX);
			eyes.GlobalPosition = new Vector2(targetX, ViewTopY() + 22f);
			if (elapsed >= DiveTrackTime && !eyes.Locked)
			{
				eyes.Locked = true;
				PlaySoundAt(new Vector2(targetX, GroundY), "Caida");
			}
		}
		eyes.Close();

		// Teleport the body under the eyes and drop the drawing onto it.
		GlobalPosition = new Vector2(targetX, GlobalPosition.Y);
		FacePlayer();
		Tween fall = _moveTween = CreateTween();
		fall.TweenProperty(Visual, "position:y", 0f, DiveFallTime).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		if (!await Wait(DiveFallTime, id))
			return;
		Visual.Position = Vector2.Zero;
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 4);
		var hazard = Hazard.CreateArea(GetTree().CurrentScene, instantKill: false, damage: DiveDamage, knockbackForce: 480f);
		hazard.GlobalPosition = new Vector2(targetX, GroundY - 50f);
		hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(DiveWidth, 110f) } });
		GetTree().CreateTimer(0.15f, false).Timeout += () => { if (IsInstanceValid(hazard)) hazard.QueueFree(); };
		Land(1.2f);
		DustPuff(new Vector2(targetX - 40f, GroundY), 40f);
		DustPuff(new Vector2(targetX + 40f, GroundY), 40f);
		// The impact runs out along the floor to both sides (like Bandido5's landing): jump it.
		RunSpikeWave(targetX, 1f);
		RunSpikeWave(targetX, -1f);
		PlayAnim("idle");

		// Recovery: the window to punish.
		if (!await Wait(DiveRecover, id))
			return;
		EndAction();
	}

	// Collision off and the drawing back on the floor (stun or death mid-air).
	private void ResetAirborne()
	{
		_moveTween?.Kill();
		if (Visual is not null)
			Visual.Position = Vector2.Zero;
		if (!IsQueuedForRemoval)
			SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 4);
	}

	private async void ShootWeb()
	{
		int id = ++_actionId;
		_actionBusy = true;
		_animLock = true;
		FacePlayer();
		OnAttackTelegraph(WebTelegraph);
		PlaySound("Carga", WebTelegraph + 0.1f);
		PlayAnim("spit");
		if (!await Wait(WebTelegraph, id))
			return;

		if (EnemyTargeting.Current(GetTree()) is Node2D player)
		{
			var web = new WebShot { Owner2 = this };
			GetTree().CurrentScene.AddChild(web);
			Vector2 mouth = GlobalPosition + new Vector2(Facing * 60f, -40f);
			web.GlobalPosition = mouth;
			web.Velocity = (player.GlobalPosition + new Vector2(0f, -20f) - mouth).Normalized() * WebSpeed;
			PlaySound("Escupitajo");
		}
		if (!await Wait(0.45f, id))
			return;
		EndAction();
	}

}
