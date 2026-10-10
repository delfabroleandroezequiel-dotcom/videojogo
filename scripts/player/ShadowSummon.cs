using System;
using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// Bastón summon: a neon shadow of a defeated enemy type that the player steers directly while their
// own body stands still and vulnerable. It only walks (flyers move freely in 2D instead) and throws
// that enemy's own attack (its real animation) — no jumping; archers/mages release a ShadowBolt
// rather than hitting up close. It ends after its hits are spent, when its time runs out, when the cast
// button is pressed again, or when it tries to leave the map through a LevelTransition. It has no
// hurtbox and isn't in the "player" group, so enemies ignore it. Works for any enemy scene without
// per-enemy setup: the look (SpriteFrames, sprite offset/scale/flip, body size) is read from the
// enemy's scene once and cached.
public partial class ShadowSummon : CharacterBody2D
{
	// How the shadow moves and strikes, picked from the enemy's class: archers/mages fire a ShadowBolt
	// instead of hitting up close; flyers move in 2D without gravity.
	private enum Style { Melee, Ranged, Flying }

	private sealed class Template
	{
		public Style Style;
		public SpriteFrames Frames;
		public Vector2 SpritePosition;
		public Vector2 SpriteScale;
		public Vector2 SpriteOffset;
		public bool SpriteFlipH;
		public Vector2 BodyPosition;
		public Vector2 BodySize;
		public string IdleAnimation;
		public string WalkAnimation;
		public string AttackAnimation;
		public float AttackLength;
		// >1 speeds up a long attack clip so every summon strikes snappily.
		public float SpeedScale = 1f;
	}

	private static readonly Dictionary<string, Template> Templates = new();
	// Longest one summon attack may take; longer enemy clips are played faster to fit.
	private const float MaxAttackLength = 0.8f;
	// Ignores the cast button right after spawning, so the press that cast it can't also cancel it.
	private const float CancelGrace = 0.25f;
	private const float WarnTime = 2f;
	private const float HitHeightTolerance = 70f;

	// Fired once, whatever ended the summon, so the caster can take control back.
	public event Action Finished;

	private string _enemyScenePath;
	private int _damage;
	private bool _facingRight;
	private float _reach;
	private int _hitsLeft;
	private float _timeLeft;
	private float _moveSpeed;
	private float _gravity;
	private Color _color;
	private float _intensity;

	private Template _template;
	private Node2D _visual;
	private AnimatedSprite2D _sprite;
	private readonly List<LevelTransition> _exits = new();
	private float _age;
	private bool _attacking;
	private bool _finished;

	public void Setup(string enemyScenePath, int damage, bool facingRight, float reach, int hits, float duration,
		float moveSpeed, float gravity, Color color, float intensity)
	{
		_enemyScenePath = enemyScenePath;
		_damage = damage;
		_facingRight = facingRight;
		_reach = reach;
		_hitsLeft = Mathf.Max(1, hits);
		_timeLeft = duration;
		_moveSpeed = moveSpeed;
		_gravity = gravity;
		_color = color;
		_intensity = intensity;
	}

	// Reads (and caches) how this enemy type looks and attacks, from an un-entered instance of its scene.
	private static Template GetTemplate(string scenePath)
	{
		if (Templates.TryGetValue(scenePath, out Template cached))
			return cached;
		if (!ResourceLoader.Exists(scenePath) || GD.Load<PackedScene>(scenePath)?.Instantiate() is not Node2D instance)
			return null;

		Template template = null;
		if (instance.GetNodeOrNull<AnimatedSprite2D>("Visual/CharacterSprite") is { SpriteFrames: not null } sprite)
		{
			Vector2 bodyPosition = new(0f, 10f);
			Vector2 bodySize = new(20f, 40f);
			if (instance.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is { Shape: RectangleShape2D rect } shape)
			{
				bodyPosition = shape.Position;
				bodySize = rect.Size;
			}
			SpriteFrames frames = sprite.SpriteFrames;
			Style style = instance switch
			{
				FlyingEnemy => Style.Flying,
				RangedEnemy or ElfArcher or ElfMage or Hechicero or MagoCamino => Style.Ranged,
				_ => Style.Melee,
			};
			// Ranged casters may also have a melee "attack" clip: prefer their shooting one.
			string attack = (style == Style.Ranged ? PickAnimation(frames, new[] { "shoot", "spell_1", "spell", "cast" }, "shoot") : null)
				?? PickAnimation(frames, new[] { "attack", "attack1", "attack2", "Attack", "attack_1" }, "attack");
			float length = 0.5f;
			if (attack is not null)
			{
				float speed = Mathf.Max(1f, (float)frames.GetAnimationSpeed(attack));
				length = frames.GetFrameCount(attack) / speed;
			}
			string idle = PickAnimation(frames, new[] { "idle", "Idle", "fly" }, "idle") ?? frames.GetAnimationNames()[0];
			template = new Template
			{
				Style = style,
				Frames = frames,
				SpritePosition = sprite.Position,
				SpriteScale = sprite.Scale,
				SpriteOffset = sprite.Offset,
				SpriteFlipH = sprite.FlipH,
				BodyPosition = bodyPosition,
				BodySize = bodySize,
				IdleAnimation = idle,
				// No walk clip: the idle just slides along.
				WalkAnimation = PickAnimation(frames, new[] { "walk", "run", "Walk", "Run", "move", "fly" }, "walk") ?? idle,
				AttackAnimation = attack,
				AttackLength = Mathf.Clamp(length, 0.25f, MaxAttackLength),
				SpeedScale = length > MaxAttackLength ? length / MaxAttackLength : 1f,
			};
		}
		instance.Free();
		if (template is not null)
			Templates[scenePath] = template;
		return template;
	}

	private static string PickAnimation(SpriteFrames frames, string[] exact, string contains)
	{
		foreach (string name in exact)
			if (frames.HasAnimation(name))
				return name;
		foreach (StringName name in frames.GetAnimationNames())
			if (name.ToString().ToLowerInvariant().Contains(contains))
				return name;
		return null;
	}

	public override void _Ready()
	{
		_template = GetTemplate(_enemyScenePath);
		if (_template is null)
		{
			Finish();
			return;
		}

		// Collides with the world only: enemies and the player walk through it.
		CollisionLayer = 0;
		CollisionMask = 1;
		AddChild(new CollisionShape2D
		{
			Position = _template.BodyPosition,
			Shape = new RectangleShape2D { Size = _template.BodySize },
		});

		_visual = new Node2D { Scale = new Vector2(_facingRight ? 1f : -1f, 1f) };
		AddChild(_visual);
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = _template.Frames,
			Position = _template.SpritePosition,
			Scale = _template.SpriteScale,
			Offset = _template.SpriteOffset,
			FlipH = _template.SpriteFlipH,
			Material = NeonSilhouette.CreateMaterial(_color, _intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		_visual.AddChild(_sprite);
		_sprite.Play(_template.IdleAnimation);
		CreateTween().TweenProperty(_sprite, "modulate:a", 0.9f, 0.12f);

		if (GetTree().CurrentScene is Node scene)
			CollectExits(scene);
	}

	private void CollectExits(Node node)
	{
		if (node is LevelTransition exit)
			_exits.Add(exit);
		foreach (Node child in node.GetChildren())
			CollectExits(child);
	}

	// Drops the summon onto the floor under `from` (the caster sets it right after adding it).
	public void PlaceOnFloor(Vector2 from)
	{
		// Flyers appear in the air at the caster's chest height instead.
		if (_template?.Style == Style.Flying)
		{
			GlobalPosition = from + new Vector2(0f, -20f);
			return;
		}
		float bottom = _template is null ? 30f : _template.BodyPosition.Y + _template.BodySize.Y / 2f;
		float? floorY = FloorBelow(from);
		GlobalPosition = floorY is float y ? new Vector2(from.X, y - bottom - 1f) : from;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_finished)
			return;
		float dt = (float)delta;
		_age += dt;
		_timeLeft -= dt;

		if (_timeLeft <= 0f || IsLeavingMap() || (_age > CancelGrace && Input.IsActionJustPressed("summon_cast")))
		{
			Finish();
			return;
		}

		// Flickers through its last seconds so running out of time isn't a surprise.
		if (_timeLeft < WarnTime)
			_sprite.SelfModulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_timeLeft * 12f)));

		bool flying = _template.Style == Style.Flying;
		Vector2 velocity = Velocity;
		if (flying)
			velocity.Y = _attacking ? 0f : Input.GetAxis("move_up", "move_down") * _moveSpeed;
		else
			velocity.Y = IsOnFloor() ? 0f : velocity.Y + _gravity * dt;
		if (_attacking)
		{
			velocity.X = 0f;
		}
		else
		{
			float direction = Input.GetAxis("move_left", "move_right");
			velocity.X = direction * _moveSpeed;
			if (!Mathf.IsZeroApprox(direction))
			{
				_facingRight = direction > 0f;
				_visual.Scale = new Vector2(_facingRight ? 1f : -1f, 1f);
			}
			bool moving = !Mathf.IsZeroApprox(velocity.X) || (flying && !Mathf.IsZeroApprox(velocity.Y));
			string animation = moving ? _template.WalkAnimation : _template.IdleAnimation;
			if (_sprite.Animation != animation)
				_sprite.Play(animation);
			if (Input.IsActionJustPressed("attack"))
				Attack();
		}
		Velocity = velocity;
		MoveAndSlide();
	}

	private bool IsLeavingMap()
	{
		foreach (LevelTransition exit in _exits)
		{
			if (!IsInstanceValid(exit))
				continue;
			Vector2 size = new Vector2(exit.Width, exit.Height) * exit.GlobalScale.Abs();
			if (new Rect2(exit.GlobalPosition - size / 2f, size).HasPoint(GlobalPosition))
				return true;
		}
		return false;
	}

	private async void Attack()
	{
		_attacking = true;
		_hitsLeft--;
		if (_template.AttackAnimation is not null)
		{
			_sprite.SpeedScale = _template.SpeedScale;
			_sprite.Play(_template.AttackAnimation);
		}

		// The hit (or the shot's release) lands a bit before the middle of the swing.
		await ToSignal(GetTree().CreateTimer(_template.AttackLength * 0.45f, false, true), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _finished)
			return;
		if (_template.Style == Style.Ranged)
			FireBolt();
		else
			DealDamage();

		await ToSignal(GetTree().CreateTimer(_template.AttackLength * 0.55f, false, true), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _finished)
			return;
		_sprite.SpeedScale = 1f;
		_attacking = false;
		if (_hitsLeft <= 0)
			Finish();
	}

	// Fired from the body's centre; it lives in the scene, so it outlasts the summon vanishing.
	private void FireBolt()
	{
		if (GetParent() is not Node parent)
			return;
		var bolt = new ShadowBolt();
		bolt.Setup(new Vector2(_facingRight ? 1f : -1f, 0f), _damage, _color);
		parent.AddChild(bolt);
		bolt.GlobalPosition = GlobalPosition + _template.BodyPosition + new Vector2(_facingRight ? 12f : -12f, 0f);
	}

	private void DealDamage()
	{
		float facing = _facingRight ? 1f : -1f;
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy || enemy.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 } stats)
				continue;
			float ahead = (enemy.GlobalPosition.X - GlobalPosition.X) * facing;
			if (ahead >= -10f && ahead <= _reach * 1.5f && Mathf.Abs(enemy.GlobalPosition.Y - GlobalPosition.Y) <= HitHeightTolerance)
				stats.TakeDamage(_damage, ignoreInvulnerability: true);
		}
	}

	// Safe to call more than once (hit on the caster, timeout and last swing can coincide).
	public void Finish()
	{
		if (_finished)
			return;
		_finished = true;
		Finished?.Invoke();
		if (_sprite is null)
		{
			QueueFree();
			return;
		}
		Tween vanish = CreateTween();
		vanish.TweenProperty(_sprite, "modulate:a", 0f, 0.2f);
		vanish.TweenCallback(Callable.From(QueueFree));
	}

	private float? FloorBelow(Vector2 from)
	{
		// Starts a bit above so a cast with the feet on the floor line still finds that floor.
		var query = PhysicsRayQueryParameters2D.Create(from + new Vector2(0f, -40f), from + new Vector2(0f, 300f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? ((Vector2)hit["position"]).Y : null;
	}
}
