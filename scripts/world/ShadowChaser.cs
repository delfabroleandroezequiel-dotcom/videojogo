using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Sombra perseguidora (after Celeste's Badeline chase): enter the zone and a shadow copy of you
// rises Delay seconds later at the spot you came in, replaying your exact path (position, animation,
// facing). Stop, double back or dawdle and it catches you — touching it hurts and sends it back to
// the start of the replay. Leave the zone and it fades. Origin = top-left of the zone. Fits the
// game's Shadow theme: it's drawn from the player's own sprite, darkened, with a violet outline.
public partial class ShadowChaser : Node2D
{
	[Export] public Vector2 Size = new(1200f, 360f);
	[Export] public float Delay = 1.4f;
	[Export] public int Damage = 25;
	[Export] public float Knockback = 380f;
	[Export] public float HitRadius = 26f;
	[Export] public Color ShadowTint = new(0.12f, 0.05f, 0.2f, 0.9f);
	[Export] public Color GlowColor = new(0.65f, 0.35f, 1f, 1f);

	private struct Frame { public Vector2 Pos; public StringName Anim; public int Index; public bool Flip; }

	private readonly List<Frame> _trail = new();
	private bool _active;
	private int _cursor;
	private int _delayFrames;
	private Node2D _shadow;
	private AnimatedSprite2D _sprite;
	private AnimatedSprite2D _glow;
	private AnimatedSprite2D _source;
	private Vector2 _sourceOffset;
	private float _cooldown;

	public override void _Ready()
	{
		AddChild(TrapKit.Poly(TrapKit.Rect(Size / 2f, Size), GlowColor with { A = 0.04f }));
		_shadow = new Node2D { Visible = false, TopLevel = true, ZIndex = 4 };
		AddChild(_shadow);
	}

	public override void _PhysicsProcess(double delta)
	{
		var player = TrapKit.FindPlayer(this);
		if (player is null)
			return;
		_source ??= player.GetNodeOrNull<AnimatedSprite2D>("Visual/CharacterSprite");
		bool inside = new Rect2(GlobalPosition, Size).HasPoint(player.GlobalPosition);

		if (inside && !_active)
			Begin();
		else if (!inside && _active)
			End();
		if (!_active)
			return;

		_trail.Add(new Frame
		{
			Pos = player.GlobalPosition,
			Anim = _source?.Animation ?? new StringName("idle"),
			Index = _source?.Frame ?? 0,
			Flip = _source?.FlipH ?? false,
		});
		_cooldown -= (float)delta;

		if (_trail.Count - _cursor <= _delayFrames)
			return;
		Frame f = _trail[_cursor++];
		if (!_shadow.Visible)
			Appear();
		_shadow.GlobalPosition = f.Pos;
		if (_sprite is not null && _sprite.SpriteFrames.HasAnimation(f.Anim))
		{
			_sprite.Animation = f.Anim;
			_sprite.Frame = f.Index;
			_sprite.FlipH = f.Flip;
			_glow.Animation = f.Anim;
			_glow.Frame = f.Index;
			_glow.FlipH = f.Flip;
		}

		if (_cooldown <= 0f && f.Pos.DistanceTo(player.GlobalPosition) <= HitRadius)
		{
			TrapKit.Hurt(player, Damage, new Vector2(Mathf.Sign(player.GlobalPosition.X - f.Pos.X), -0.4f), Knockback);
			TrapKit.Burst(this, f.Pos, GlowColor, 20, 200f);
			// Back to the start of the replay, so it has to chase you all over again.
			_trail.RemoveRange(0, _cursor);
			_cursor = 0;
			_shadow.Visible = false;
			_cooldown = Delay;
		}
	}

	private void Begin()
	{
		_active = true;
		_trail.Clear();
		_cursor = 0;
		_delayFrames = Mathf.RoundToInt(Delay * Engine.PhysicsTicksPerSecond);
		if (_sprite is null && _source is not null)
		{
			_sourceOffset = _source.Position;
			_glow = new AnimatedSprite2D { SpriteFrames = _source.SpriteFrames, Scale = _source.Scale * 1.08f, Position = _sourceOffset, Offset = _source.Offset, Centered = _source.Centered, Material = TrapKit.Additive(), Modulate = GlowColor with { A = 0.55f } };
			_sprite = new AnimatedSprite2D { SpriteFrames = _source.SpriteFrames, Scale = _source.Scale, Position = _sourceOffset, Offset = _source.Offset, Centered = _source.Centered, Modulate = ShadowTint };
			_shadow.AddChild(_glow);
			_shadow.AddChild(_sprite);
			_shadow.AddChild(TrapKit.Light(GlowColor, 0.6f, 0.9f));
		}
	}

	private void Appear()
	{
		_shadow.Visible = true;
		_shadow.Modulate = new Color(1f, 1f, 1f, 0f);
		CreateTween().TweenProperty(_shadow, "modulate:a", 1f, 0.3f);
		TrapKit.Burst(this, _trail[0].Pos, GlowColor, 16, 140f);
	}

	private void End()
	{
		_active = false;
		_trail.Clear();
		_cursor = 0;
		if (_shadow.Visible)
		{
			Tween t = CreateTween();
			t.TweenProperty(_shadow, "modulate:a", 0f, 0.4f);
			t.TweenCallback(Callable.From(() => _shadow.Visible = false));
		}
	}
}
