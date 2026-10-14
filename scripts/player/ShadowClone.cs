using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// Spell 1's shadow clone (Clon básico): a neon silhouette of the player that pops in beside them,
// swings through the given combo clips one after another (same hitbox size/reach/timing and the
// caster's own AttackPower per swing), traces a neon slash streak per swing, then fades away. How
// many swings comes from Player.ShadowCloneLevel. Built entirely in code — see Setup().
public partial class ShadowClone : Node2D
{
	private AnimatedSprite2D _sprite;
	private Hitbox _hitbox;
	private EnemySlashTrail _trail;
	private Stats _casterStats;
	private Color _tint;
	private float _alpha;
	private float _hitDelay;
	private float _hitDuration;
	private float _fadeIn;
	private float _fadeOut;
	private string[] _attackAnimations;

	public void Setup(SpriteFrames frames, string[] attackAnimations, bool facingRight, Vector2 spritePosition, Vector2 spriteDrawOffset, Vector2 spriteScale,
		Stats casterStats, Color tint, float intensity, float alpha, Vector2 hitboxSize, float hitboxReach, float hitboxYOffset,
		float hitDelay, float hitDuration, float fadeIn, float fadeOut)
	{
		_casterStats = casterStats;
		_tint = tint;
		_alpha = alpha;
		_hitDelay = hitDelay;
		_hitDuration = hitDuration;
		_fadeIn = fadeIn;
		_fadeOut = fadeOut;
		_attackAnimations = attackAnimations;

		var visual = new Node2D { Scale = new Vector2(facingRight ? 1 : -1, 1) };
		AddChild(visual);

		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Position = spritePosition,
			Offset = spriteDrawOffset,
			Scale = spriteScale,
			Material = NeonSilhouette.CreateMaterial(tint, intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		visual.AddChild(_sprite);

		// Same streak as the player's attack1 (Player.RunSwordArcTrail geometry: radius 52 around
		// the weapon hand, rising from low-front to high-front), in the clone's own colour.
		_trail = new EnemySlashTrail { GlowColor = tint, CoreColor = tint.Lightened(0.6f), CoreWidth = 3f, GlowWidth = 8f };
		visual.AddChild(_trail);

		_hitbox = new Hitbox { CollisionLayer = 0, CollisionMask = 5, Position = new Vector2(facingRight ? hitboxReach : -hitboxReach, hitboxYOffset) };
		_hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = new RectangleShape2D { Size = hitboxSize }, Disabled = true });
		AddChild(_hitbox);
	}

	public override void _Ready() => _ = Run();

	private async Task Run()
	{
		Tween appear = CreateTween();
		appear.TweenProperty(_sprite, "modulate:a", _alpha, _fadeIn);

		for (int i = 0; i < _attackAnimations.Length; i++)
		{
			string animation = _attackAnimations[i];
			_sprite.Play(animation);
			// Alternate the streak's sweep so a chain reads as separate cuts (up, down, up...).
			if (i % 2 == 0)
				_trail.Play(new Vector2(12f, 0f), 52f, 70f, -60f, _hitDelay + _hitDuration);
			else
				_trail.Play(new Vector2(12f, 0f), 52f, -60f, 70f, _hitDelay + _hitDuration);

			await ToSignal(GetTree().CreateTimer(_hitDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Activate(_casterStats);

			await ToSignal(GetTree().CreateTimer(_hitDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Deactivate();

			// Let the rest of the swing play before the next one (or the fade).
			float remaining = RemainingAnimationTime(animation);
			if (remaining > 0f)
				await ToSignal(GetTree().CreateTimer(remaining), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
		}

		Tween vanish = CreateTween();
		vanish.TweenProperty(_sprite, "modulate:a", 0f, _fadeOut).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		vanish.Parallel().TweenProperty(_sprite, "scale:y", _sprite.Scale.Y * 1.15f, _fadeOut);
		await ToSignal(vanish, Tween.SignalName.Finished);
		if (IsInstanceValid(this))
			QueueFree();
	}

	private float RemainingAnimationTime(string animation)
	{
		SpriteFrames frames = _sprite.SpriteFrames;
		double speed = frames.GetAnimationSpeed(animation);
		if (speed <= 0)
			return 0f;
		float total = frames.GetFrameCount(animation) / (float)speed;
		return Mathf.Max(0f, total - _hitDelay - _hitDuration);
	}
}
