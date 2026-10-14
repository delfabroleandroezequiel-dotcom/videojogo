using System;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// Clon manipulable: a neon copy of the player steered with the player's own controls while the body
// kneels behind (same "concentrate and steer" verb as the Bastón summons — Player drives the camera
// and ends it when the body is hit). Walks, jumps and swings attack1; with the combo unlocked (+1)
// repeated presses chain attack1..4; at +3 it bursts when it ends. Ends on its timer, on pressing
// the spell key again, or when Finish() is called. Built in code — see Setup().
public partial class ShadowPuppet : CharacterBody2D
{
	private const float CancelGrace = 0.3f;

	public event Action Finished;

	private SpriteFrames _frames;
	private Stats _casterStats;
	private Color _tint;
	private float _speed;
	private float _jumpVelocity;
	private float _gravity;
	private float _timeLeft;
	private string[] _comboAnimations;
	private float _hitDelay;
	private float _hitDuration;
	private float _hitboxReach;
	private float _hitboxYOffset;
	private float _explosionRadius;
	private int _explosionDamage;

	private Node2D _visual;
	private AnimatedSprite2D _sprite;
	private Hitbox _hitbox;
	private bool _facingRight;
	private bool _attacking;
	private bool _comboQueued;
	private float _age;
	private bool _finished;

	public void Setup(SpriteFrames frames, Vector2 spritePosition, Vector2 spriteOffset, Vector2 spriteScale, Vector2 bodySize,
		uint collisionMask, bool facingRight, Stats casterStats, Color tint, float intensity, float alpha, float speed,
		float jumpVelocity, float gravity, float duration, string[] comboAnimations, Vector2 hitboxSize, float hitboxReach,
		float hitboxYOffset, float hitDelay, float hitDuration, float explosionRadius, int explosionDamage)
	{
		_frames = frames;
		_casterStats = casterStats;
		_tint = tint;
		_speed = speed;
		_jumpVelocity = jumpVelocity;
		_gravity = gravity;
		_timeLeft = duration;
		_comboAnimations = comboAnimations;
		_hitDelay = hitDelay;
		_hitDuration = hitDuration;
		_hitboxReach = hitboxReach;
		_hitboxYOffset = hitboxYOffset;
		_explosionRadius = explosionRadius;
		_explosionDamage = explosionDamage;
		_facingRight = facingRight;

		CollisionLayer = 0;
		CollisionMask = collisionMask;
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = bodySize } });

		_visual = new Node2D();
		AddChild(_visual);
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Position = spritePosition,
			Offset = spriteOffset,
			Scale = spriteScale,
			Material = NeonSilhouette.CreateMaterial(tint, intensity),
			Modulate = new Color(1f, 1f, 1f, alpha),
		};
		_visual.AddChild(_sprite);

		_hitbox = new Hitbox { CollisionLayer = 0, CollisionMask = 5 };
		_hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = new RectangleShape2D { Size = hitboxSize }, Disabled = true });
		AddChild(_hitbox);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_finished)
			return;
		float dt = (float)delta;
		_age += dt;
		_timeLeft -= dt;
		if (_timeLeft <= 0f || (_age > CancelGrace && Input.IsActionJustPressed("spell")))
		{
			Finish();
			return;
		}

		Vector2 velocity = Velocity;
		velocity.Y = IsOnFloor() && velocity.Y > 0f ? 0f : velocity.Y + _gravity * dt;
		float direction = _attacking ? 0f : Input.GetAxis("move_left", "move_right");
		velocity.X = direction * _speed;
		if (direction != 0f)
			_facingRight = direction > 0f;
		if (!_attacking && IsOnFloor() && Input.IsActionJustPressed("jump"))
			velocity.Y = _jumpVelocity;
		if (Input.IsActionJustPressed("attack"))
		{
			if (!_attacking)
				_ = Combo();
			else
				_comboQueued = true;
		}
		Velocity = velocity;
		MoveAndSlide();

		_visual.Scale = new Vector2(_facingRight ? 1 : -1, 1);
		if (!_attacking)
			_sprite.Play(!IsOnFloor() ? (Velocity.Y < 0f ? "jump" : "fall") : Mathf.Abs(Velocity.X) > 1f ? "run" : "idle");
	}

	// Swings attack1; while the combo is unlocked, a press during a swing queues the next clip.
	private async Task Combo()
	{
		_attacking = true;
		for (int i = 0; i < _comboAnimations.Length && !_finished; i++)
		{
			_comboQueued = false;
			string animation = _comboAnimations[i];
			if (!_frames.HasAnimation(animation))
				break;
			_sprite.Play(animation);
			_hitbox.Position = new Vector2(_facingRight ? _hitboxReach : -_hitboxReach, _hitboxYOffset);

			await ToSignal(GetTree().CreateTimer(_hitDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || _finished)
				return;
			_hitbox.Activate(_casterStats);
			await ToSignal(GetTree().CreateTimer(_hitDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Deactivate();

			double speed = _frames.GetAnimationSpeed(animation);
			float rest = speed > 0 ? Mathf.Max(0f, _frames.GetFrameCount(animation) / (float)speed - _hitDelay - _hitDuration) : 0f;
			if (rest > 0f)
				await ToSignal(GetTree().CreateTimer(rest), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || !_comboQueued)
				break;
		}
		if (IsInstanceValid(this))
			_attacking = false;
	}

	public void Finish()
	{
		if (_finished)
			return;
		_finished = true;
		_hitbox.Deactivate();
		Finished?.Invoke();
		if (_explosionRadius > 0f)
		{
			ShadowBurst.Explode(this, GlobalPosition, _explosionRadius, _explosionDamage, _tint);
			QueueFree();
			return;
		}
		Tween vanish = CreateTween();
		vanish.TweenProperty(_sprite, "modulate:a", 0f, 0.25f);
		vanish.TweenCallback(Callable.From(QueueFree));
	}
}
