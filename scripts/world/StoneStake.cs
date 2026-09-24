using Godot;
using Metroidvania.Player;
using Metroidvania.Shared;

namespace Metroidvania.World;

// One stone stake of MushroomBoss's advancing line. Uses the pack's own stone art: the stones drawn
// beside the mushroom in cast_stone frames 12-19 (cropped out, see MushroomBoss.BuildStakeFrames).
// Frame 0 (a few stones peeking out of the dirt) is held as the tell on the spot, then the rest plays
// as the eruption, the hitbox is live while it's up, and it sinks/fades away.
public partial class StoneStake : Node2D
{
	private AnimatedSprite2D _sprite;
	private Hitbox _hitbox;
	private Stats _caster;
	private float _tell;
	private float _active;
	private float _linger;

	public void Setup(SpriteFrames frames, float scale, bool flip, Stats caster, Vector2 hitboxSize,
		float tell, float active, float linger, System.Action onHit)
	{
		_caster = caster;
		_tell = tell;
		_active = active;
		_linger = linger;

		Texture2D first = frames.GetFrameTexture("erupt", 0);
		float height = first?.GetHeight() ?? 82f;
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Scale = Vector2.One * scale,
			FlipH = flip,
			// Bottom of the art on this node's origin (the ground point).
			Position = new Vector2(0f, -height * 0.5f * scale),
		};
		AddChild(_sprite);

		_hitbox = new Hitbox { CollisionLayer = 0, CollisionMask = 2, Position = new Vector2(0f, -hitboxSize.Y * 0.5f) };
		_hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = new RectangleShape2D { Size = hitboxSize }, Disabled = true });
		AddChild(_hitbox);
		if (onHit is not null)
			_hitbox.HitDealt += () => onHit();
	}

	public override async void _Ready()
	{
		_sprite.Play("erupt");
		_sprite.Pause();
		_sprite.Frame = 0;
		await ToSignal(GetTree().CreateTimer(_tell), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;

		_sprite.Play("erupt");
		_sprite.Frame = 1;
		_hitbox.Activate(_caster);
		await ToSignal(GetTree().CreateTimer(_active), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		_hitbox.Deactivate();

		Tween sink = CreateTween();
		sink.TweenInterval(_linger);
		sink.TweenProperty(_sprite, "modulate:a", 0f, 0.25f);
		sink.TweenCallback(Callable.From(QueueFree));
	}
}
