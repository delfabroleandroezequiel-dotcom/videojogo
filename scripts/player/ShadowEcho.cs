using System;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.Player;

// One physics frame of the player, as recorded for Clon eco (see Player.RecordEchoFrame).
public readonly record struct EchoFrame(Vector2 Position, bool FacingRight, StringName Animation, int Frame,
	bool HitboxActive, Vector2 HitboxPosition, Vector2 HitboxSize);

// Clon eco: a neon silhouette that appears with the player and shadows them live, a fixed delay
// behind — every physics tick it copies the player's frame from DelayFrames ago (position, facing,
// animation frame) and swings a real hitbox wherever the player's sword hitbox was live. So for its
// lifetime every move and combo lands twice. Built in code — see Setup().
public partial class ShadowEcho : Node2D
{
	// framesAgo → that recorded frame, or null once it isn't available (player gone).
	private Func<int, EchoFrame?> _source;
	private int _delayFrames;
	private float _timeLeft;
	private Stats _casterStats;
	private float _alpha;
	private float _fadeOut;

	private Node2D _visual;
	private AnimatedSprite2D _sprite;
	private Hitbox _hitbox;
	private RectangleShape2D _hitboxShape;
	private bool _hitboxLive;
	private bool _finished;

	public void Setup(Func<int, EchoFrame?> source, int delayFrames, float lifetime, SpriteFrames spriteFrames,
		Vector2 spritePosition, Vector2 spriteOffset, Vector2 spriteScale, Stats casterStats, Color tint, float intensity,
		float alpha, float fadeOut)
	{
		_source = source;
		_delayFrames = delayFrames;
		_timeLeft = lifetime;
		_casterStats = casterStats;
		_alpha = alpha;
		_fadeOut = fadeOut;

		_visual = new Node2D();
		AddChild(_visual);
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = spriteFrames,
			Position = spritePosition,
			Offset = spriteOffset,
			Scale = spriteScale,
			Material = NeonSilhouette.CreateMaterial(tint, intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		_visual.AddChild(_sprite);

		_hitboxShape = new RectangleShape2D();
		_hitbox = new Hitbox { CollisionLayer = 0, CollisionMask = 5, TopLevel = true };
		_hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = _hitboxShape, Disabled = true });
		AddChild(_hitbox);
	}

	public override void _Ready()
	{
		if (_source(_delayFrames) is EchoFrame first)
			ApplyFrame(first);
		Tween appear = CreateTween();
		appear.TweenProperty(_sprite, "modulate:a", _alpha, 0.08f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_finished)
			return;
		_timeLeft -= (float)delta;
		if (_timeLeft <= 0f || _source(_delayFrames) is not EchoFrame frame)
		{
			Finish();
			return;
		}
		ApplyFrame(frame);
	}

	private void ApplyFrame(EchoFrame frame)
	{
		GlobalPosition = frame.Position;
		_visual.Scale = new Vector2(frame.FacingRight ? 1 : -1, 1);
		if (_sprite.SpriteFrames.HasAnimation(frame.Animation))
		{
			if (_sprite.Animation != frame.Animation)
				_sprite.Animation = frame.Animation;
			_sprite.Frame = frame.Frame;
		}

		if (frame.HitboxActive)
		{
			_hitbox.GlobalPosition = frame.HitboxPosition;
			_hitboxShape.Size = frame.HitboxSize;
			if (!_hitboxLive)
				_hitbox.Activate(_casterStats);
		}
		else if (_hitboxLive)
		{
			_hitbox.Deactivate();
		}
		_hitboxLive = frame.HitboxActive;
	}

	private void Finish()
	{
		_finished = true;
		if (_hitboxLive)
			_hitbox.Deactivate();
		Tween vanish = CreateTween();
		vanish.TweenProperty(_sprite, "modulate:a", 0f, _fadeOut);
		vanish.TweenCallback(Callable.From(QueueFree));
	}
}
