using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// What the berserker needs from the player's own kit to move like them. Filled by Player when it
// spawns the shadow, from its exported tuning, so the two never drift apart.
public sealed class BerserkerMoveset
{
	public int MaxJumps = 2;
	public float JumpCutMultiplier = 0.5f;
	public float FallGravityMultiplier = 1.7f;
	public float MaxFallSpeed = 560f;
	public float RollSpeed = 300f;
	public float RollDuration = 0.35f;
	public float DashSpeed = 560f;
	public float DashDuration = 0.26f;
	public float PoundSpeed = 500f;
	public Vector2 PoundHitboxSize = new(50f, 70f);
	public Vector2 UpHitboxSize = new(46f, 90f);
	public float UpHitboxReach = 40f;
	public Vector2 BaseHitboxSize = new(50f, 40f);
	// Blade-tip paths (sheet pixels, per animation) — the same ones the player's trails follow.
	public Dictionary<string, Vector2[]> TrailPaths = new();
	// (delay, drawTime) per animation, same meaning as the player's *TrailTiming exports.
	public Dictionary<string, Vector2> TrailTimings = new();
}

// Sombra berserker: the controlled shadow (ShadowPuppet's steer-it-while-the-body-kneels verb) pushed
// to the edge. Same moveset as the player — double jump, crouch + crouch slash, roll, dash, ground
// and air combos, up slash, plunging slam — but faster, hitting several times harder, stacking two
// shadow marks per hit, with super armor (nothing interrupts it). Unlike the puppet it can be hit: it
// sits on the player's physics layer with its own Stats, and every point of damage it takes is
// reported through DamageTaken (the Player pays it in shadow essence; empty essence shatters it).
// Looks unstable: red neon body that jitters, flickers and sheds afterimages, thick black gas, and
// oversized ink-and-neon blade trails along the real blade paths.
public partial class ShadowBerserker : ShadowPuppet
{
	public Action<int> DamageTaken;
	public Action<Node2D> HitLanded;
	public Action ShakeOnSwing;
	public BerserkerMoveset Moves = new();

	public Color CoreRed = new(1f, 0.12f, 0.15f, 1f);
	public Color HotRed = new(1f, 0.55f, 0.5f, 1f);
	public float JitterPixels = 2.5f;
	public float AfterimageInterval = 0.05f;
	// Trails: how much wider than the player's, and how far the blade path is pushed outward.
	public float TrailWidth = 18f;
	public float TrailReachScale = 1.3f;
	// Micro red humanities flung off each cut (MicroHumanity).
	public int HumanitiesPerSwing = 5;

	private const uint PlayerLayer = 2u;
	private const float CancelGraceTime = 0.3f;
	private static readonly string[] GroundCombo = { "attack1", "attack2", "attack3", "attack4" };
	private static readonly string[] AirCombo = { "air_attack1", "air_attack2", "air_attack3" };

	private Stats _stats;
	private CollisionShape2D _hitboxShape;
	private Vector2 _spriteBase;
	private float _jitterTimer;
	private float _ghostTimer;
	private float _hurtFlash;

	private int _jumps;
	private bool _jumpRising;
	private bool _airDashUsed;
	private bool _crouching;
	private float _moveTimer;
	private float _moveSpeed;
	private float _moveDirection;
	private bool _dashing;
	private bool _pounding;
	private bool _queued;
	private string _forcedAnimation;

	public override void _Ready()
	{
		// Hittable: enemy hitboxes look for bodies on the player's layer that carry a "Stats" node.
		// Not in the "player" group, so AI, hazards and transitions still only care about the body.
		CollisionLayer = PlayerLayer;
		_stats = new Stats { Name = "Stats", MaxHealth = 100000, InvulnerabilityDuration = 0.12f };
		AddChild(_stats);
		_stats.HealthChanged += OnHealthChanged;

		_hitbox.HitTarget += target => HitLanded?.Invoke(target);
		_hitboxShape = _hitbox.GetNode<CollisionShape2D>("CollisionShape2D");
		_spriteBase = _sprite.Position;
		BuildMotionTrail();
	}

	// Damage lands on the shadow's own huge pool; report the loss and top it back up at once — the
	// real cost is paid in essence by whoever listens.
	private void OnHealthChanged(int current, int max)
	{
		if (current >= max)
			return;
		_hurtFlash = 1f;
		_stats.ResetToFull();
		DamageTaken?.Invoke(max - current);
	}

	// Replaces the plain puppet's walk/jump/attack1 controller with the player's full moveset.
	public override void _PhysicsProcess(double delta)
	{
		if (_finished)
			return;
		float dt = (float)delta;
		_age += dt;
		_timeLeft -= dt;
		if (_timeLeft <= 0f || (_age > CancelGraceTime && Input.IsActionJustPressed("spell")))
		{
			Finish();
			return;
		}

		Vector2 velocity = Velocity;
		bool onFloor = IsOnFloor();
		if (onFloor)
		{
			_jumps = 0;
			_airDashUsed = false;
		}

		if (_pounding)
		{
			Velocity = new Vector2(0f, Moves.PoundSpeed * 1.2f);
			MoveAndSlide();
			if (IsOnFloor())
				_ = PoundSlam();
			return;
		}

		if (_moveTimer > 0f)
		{
			_moveTimer -= dt;
			velocity.X = _moveDirection * _moveSpeed;
			velocity.Y = _dashing ? 0f : (onFloor ? 0f : velocity.Y + _gravity * dt);
			Velocity = velocity;
			MoveAndSlide();
			if (_moveTimer <= 0f)
				_forcedAnimation = null;
			return;
		}

		float direction = Input.GetAxis("move_left", "move_right");
		_crouching = onFloor && Input.IsActionPressed("move_down") && !_attacking;

		if (!onFloor)
		{
			float gravity = velocity.Y > 0f ? _gravity * Moves.FallGravityMultiplier : _gravity;
			velocity.Y = Mathf.Min(velocity.Y + gravity * dt, Moves.MaxFallSpeed);
		}
		else if (velocity.Y > 0f)
		{
			velocity.Y = 0f;
		}

		if (_crouching)
			velocity.X = 0f;
		else if (_attacking)
			velocity.X = onFloor ? 0f : direction * _speed * 0.6f;
		else
			velocity.X = direction * _speed;
		if (direction != 0f && !_attacking)
			_facingRight = direction > 0f;

		// Jump / double jump (walking off a ledge spends the ground jump), with the same jump cut.
		if (Input.IsActionJustPressed("jump") && !_crouching && !_attacking)
		{
			if (!onFloor && _jumps == 0)
				_jumps = 1;
			if (_jumps < Moves.MaxJumps)
			{
				velocity.Y = _jumpVelocity;
				_jumps++;
				_jumpRising = true;
				if (_jumps >= 2 && _frames.HasAnimation("double_jump"))
					PlayOnce("double_jump");
			}
		}
		if (_jumpRising)
		{
			if (velocity.Y >= 0f)
				_jumpRising = false;
			else if (!Input.IsActionPressed("jump"))
			{
				velocity.Y *= Moves.JumpCutMultiplier;
				_jumpRising = false;
			}
		}

		if (!_attacking && Input.IsActionJustPressed("roll") && onFloor)
			StartMove(Moves.RollSpeed * 1.25f, Moves.RollDuration, dash: false);
		else if (!_attacking && Input.IsActionJustPressed("dash") && (onFloor || !_airDashUsed))
		{
			_airDashUsed |= !onFloor;
			StartMove(Moves.DashSpeed * 1.1f, Moves.DashDuration, dash: true);
		}

		if (Input.IsActionJustPressed("attack"))
		{
			if (_attacking)
				_queued = true;
			else if (!onFloor && Input.IsActionPressed("move_down"))
				StartPound();
			else if (Input.IsActionPressed("move_up"))
				_ = RunCombo(new[] { "air_attack1" }, Moves.UpHitboxSize, new Vector2(0f, -Moves.UpHitboxReach));
			else if (_crouching)
				_ = RunCombo(new[] { "crouch_attack" }, Moves.BaseHitboxSize, new Vector2(_hitboxReach, 14f));
			else if (!onFloor)
				_ = RunCombo(AirCombo, Moves.BaseHitboxSize * 1.2f, new Vector2(_hitboxReach, _hitboxYOffset));
			else
				_ = RunCombo(GroundCombo, Moves.BaseHitboxSize * 1.2f, new Vector2(_hitboxReach, _hitboxYOffset));
		}

		Velocity = velocity;
		MoveAndSlide();
		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1);
		if (!_attacking)
			PlayLocomotion();
	}

	private void PlayLocomotion()
	{
		// A one-off clip (double jump, roll, dash) holds until it ends — or, for the airborne ones
		// whose clip loops, until touching down.
		bool airborneClip = _forcedAnimation == "double_jump";
		if (_forcedAnimation is not null && _sprite.Animation == _forcedAnimation && _sprite.IsPlaying()
			&& !(airborneClip && IsOnFloor()))
			return;
		_forcedAnimation = null;
		string anim = _crouching ? "crouch"
			: !IsOnFloor() ? (Velocity.Y < 0f ? "jump" : "fall")
			: Mathf.Abs(Velocity.X) > 1f ? "run" : "idle";
		if (_frames.HasAnimation(anim) && _sprite.Animation != anim)
			_sprite.Play(anim);
	}

	private void PlayOnce(string anim)
	{
		_forcedAnimation = anim;
		_sprite.Play(anim);
	}

	private void StartMove(float speed, float duration, bool dash)
	{
		_dashing = dash;
		_moveSpeed = speed;
		_moveTimer = duration;
		_moveDirection = _facingRight ? 1f : -1f;
		string anim = dash ? "dash" : "roll";
		if (_frames.HasAnimation(anim))
			PlayOnce(anim);
	}

	// Plays a chain of clips (ground combo, air combo or a single move), each with its hitbox and
	// trail; a press during a swing queues the next clip.
	private async Task RunCombo(string[] clips, Vector2 hitboxSize, Vector2 hitboxOffset)
	{
		_attacking = true;
		for (int i = 0; i < clips.Length && !_finished; i++)
		{
			_queued = false;
			string anim = clips[i];
			if (!_frames.HasAnimation(anim))
				break;
			_sprite.Play(anim);
			SpawnTrail(anim, i);
			ShakeOnSwing?.Invoke();

			await ToSignal(GetTree().CreateTimer(_hitDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || _finished)
				return;
			_hitboxShape.Shape = new RectangleShape2D { Size = hitboxSize };
			_hitbox.Position = new Vector2(_facingRight ? hitboxOffset.X : -hitboxOffset.X, hitboxOffset.Y);
			_hitbox.ShadowMarksPerHit = MarksPerHit;
			_hitbox.Activate(_casterStats, damageMultiplier: DamageMultiplier);
			await ToSignal(GetTree().CreateTimer(_hitDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Deactivate();

			double speed = _frames.GetAnimationSpeed(anim);
			float rest = speed > 0 ? Mathf.Max(0f, _frames.GetFrameCount(anim) / (float)speed - _hitDelay - _hitDuration) : 0f;
			if (rest > 0f)
				await ToSignal(GetTree().CreateTimer(rest), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || !_queued)
				break;
		}
		if (IsInstanceValid(this))
			_attacking = false;
	}

	private void StartPound()
	{
		_attacking = true;
		_pounding = true;
		_sprite.Play(_frames.HasAnimation("down_attack_loop") ? "down_attack_loop" : "fall");
		_hitboxShape.Shape = new RectangleShape2D { Size = Moves.PoundHitboxSize };
		_hitbox.Position = new Vector2(0f, Moves.PoundHitboxSize.Y * 0.4f);
		_hitbox.ShadowMarksPerHit = MarksPerHit;
		_hitbox.Activate(_casterStats, damageMultiplier: DamageMultiplier);
	}

	// Hitting the floor out of the plunge: a wide slam around the feet.
	private async Task PoundSlam()
	{
		_pounding = false;
		_hitbox.Deactivate();
		string anim = _frames.HasAnimation("down_attack_slam") ? "down_attack_slam" : "down_attack_landing";
		if (_frames.HasAnimation(anim))
			_sprite.Play(anim);
		SpawnTrail("down_attack_slam", 3);
		ShadowImpact.SpawnAt(this, GlobalPosition + new Vector2(0f, 10f), 2f);
		ShakeOnSwing?.Invoke();
		_hitboxShape.Shape = new RectangleShape2D { Size = new Vector2(Moves.PoundHitboxSize.X * 3f, Moves.PoundHitboxSize.Y * 0.6f) };
		_hitbox.Position = new Vector2(0f, 4f);
		_hitbox.Activate(_casterStats, damageMultiplier: DamageMultiplier);
		await ToSignal(GetTree().CreateTimer(_hitDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_hitbox.Deactivate();
		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_attacking = false;
	}

	// The player's measured blade path for this clip when there is one (pushed outward so the trail
	// overshoots the blade), else a wide procedural arc in front of the body.
	private void SpawnTrail(string anim, int comboIndex)
	{
		Vector2[] curve;
		Vector2 timing = Moves.TrailTimings.TryGetValue(anim, out Vector2 t) ? t : new Vector2(0.04f, 0.1f);
		if (Moves.TrailPaths.TryGetValue(anim, out Vector2[] path) && path is { Length: >= 2 }
			&& _frames.GetFrameTexture(anim, 0) is { } frame)
		{
			Vector2 frameSize = frame.GetSize();
			Vector2 center = _spriteBase + _sprite.Offset * _sprite.Scale;
			var local = new List<Vector2>();
			foreach (Vector2 px in path)
			{
				Vector2 p = _spriteBase + (px - frameSize / 2f + _sprite.Offset) * _sprite.Scale;
				local.Add(center + (p - center) * TrailReachScale);
			}
			curve = BladeTrail.Smooth(local);
		}
		else
		{
			curve = BladeTrail.Smooth(ArcFor(comboIndex));
		}
		var trail = new BladeTrail { Curve = curve, Delay = timing.X, DrawTime = timing.Y, Width = TrailWidth, Glow = CoreRed, Core = HotRed.Lightened(0.4f), ZIndex = 5, HumanityCount = HumanitiesPerSwing, HumanityColor = CoreRed };
		_visual.AddChild(trail);
	}

	// Fallback swing shapes (facing right; Visual flips them): overhead chop, rising cut, flat sweep,
	// big cleave.
	private Vector2[] ArcFor(int comboIndex)
	{
		Vector2 c = new(_hitboxReach * 0.4f, -14f);
		float r = Mathf.Max(48f, _hitboxReach * 1.5f);
		(float from, float to) = (comboIndex % 4) switch
		{
			0 => (-140f, 50f),
			1 => (70f, -100f),
			2 => (-80f, 80f),
			_ => (-165f, 85f),
		};
		var points = new Vector2[7];
		for (int i = 0; i < points.Length; i++)
		{
			float a = Mathf.DegToRad(Mathf.Lerp(from, to, i / (float)(points.Length - 1)));
			points[i] = c + Vector2.FromAngle(a) * r;
		}
		return points;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_hurtFlash = Mathf.Max(0f, _hurtFlash - dt * 6f);
		UpdateMotionTrail();

		_jitterTimer -= dt;
		if (_jitterTimer <= 0f)
		{
			_jitterTimer = 0.04f;
			_sprite.Position = _spriteBase + new Vector2((float)GD.RandRange(-JitterPixels, JitterPixels), (float)GD.RandRange(-JitterPixels, JitterPixels));
			// Mostly deep red, now and then a hot flash or a dropout to black — never steady.
			float roll = GD.Randf();
			Color c = roll < 0.12f ? HotRed : roll < 0.2f ? new Color(0.15f, 0f, 0.02f, 1f) : CoreRed;
			_sprite.SelfModulate = c.Lerp(Colors.White, _hurtFlash * 0.8f);
		}

		_ghostTimer -= dt;
		if (_ghostTimer <= 0f)
		{
			_ghostTimer = AfterimageInterval;
			SpawnAfterimage();
		}
	}

	private const int TrailLength = 14;
	private Line2D _trail;
	private Line2D _trailCore;

	// A thick soft-edged ribbon dragged behind the body while it moves, wider the faster it goes.
	private void BuildMotionTrail()
	{
		_trail = MotionRibbon(CoreRed, 0.55f);
		_trailCore = MotionRibbon(HotRed, 0.8f);
	}

	private Line2D MotionRibbon(Color color, float alpha)
	{
		var fade = new Gradient();
		fade.SetColor(0, color with { A = 0f });
		fade.SetColor(1, color with { A = alpha });
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0f));
		taper.AddPoint(new Vector2(1f, 1f));
		var line = new Line2D
		{
			TopLevel = true,
			Gradient = fade,
			WidthCurve = taper,
			Texture = BladeTrail.SoftEdgeTexture,
			TextureMode = Line2D.LineTextureMode.Stretch,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = ZIndex - 1,
			JointMode = Line2D.LineJointMode.Round,
			Antialiased = true,
		};
		AddChild(line);
		return line;
	}

	private void UpdateMotionTrail()
	{
		if (_trail is null)
			return;
		Vector2 p = GlobalPosition + _spriteBase + new Vector2(0f, -6f);
		_trail.AddPoint(p);
		_trailCore.AddPoint(p);
		while (_trail.GetPointCount() > TrailLength)
		{
			_trail.RemovePoint(0);
			_trailCore.RemovePoint(0);
		}
		float speed = Velocity.Length() / Mathf.Max(1f, _speed);
		_trail.Width = Mathf.Lerp(_trail.Width, 12f + 36f * Mathf.Clamp(speed, 0f, 1.4f), 0.25f);
		_trailCore.Width = _trail.Width * 0.35f;
	}

	private void SpawnAfterimage()
	{
		if (GetTree().CurrentScene is not Node scene || _sprite.SpriteFrames is null)
			return;
		Texture2D frame = _sprite.SpriteFrames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
		if (frame is null)
			return;
		bool dark = GD.Randf() < 0.4f;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Offset = _sprite.Offset,
			Material = dark ? null : _sprite.Material,
			Modulate = dark ? new Color(0.02f, 0f, 0.01f, 0.55f) : new Color(1f, 0.15f, 0.2f, 0.45f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalPosition = _sprite.GlobalPosition + new Vector2((float)GD.RandRange(-6.0, 6.0), (float)GD.RandRange(-3.0, 3.0));
		ghost.GlobalScale = _sprite.GlobalScale;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.18f);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}
}
