using Godot;

namespace Metroidvania.World;

// Ambient decoration: a perched bird (Bird_Animation_Template frames) that looks around, pecks,
// flicks its tail and hops in place; when the player comes within FleeRadius it takes off and
// flies away, then frees itself. Purely visual, no gameplay interaction.
public partial class PajaroDecoracion : AnimatedSprite2D
{
	[Export] public float MinRestTime = 1.5f;
	[Export] public float MaxRestTime = 4f;
	[Export] public float FleeRadius = 180f;
	[Export] public float FlySpeed = 220f;
	[Export] public float ClimbRatio = 0.6f;
	[Export] public float FlyTime = 4f;

	private static readonly string[] Idles = { "peck", "flick", "hop" };

	private RandomNumberGenerator _rng = new();
	private Node2D _player;
	private float _timer;
	private bool _fleeing;
	private bool _airborne;
	private int _dir = 1;

	public override void _Ready()
	{
		_rng.Randomize();
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		AnimationFinished += OnAnimationFinished;
		Rest();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (_fleeing)
		{
			if (_airborne)
				Position += new Vector2(_dir, -ClimbRatio).Normalized() * FlySpeed * dt;
			_timer -= dt;
			if (_timer <= 0f)
				QueueFree();
			return;
		}

		if (_player != null && GlobalPosition.DistanceTo(_player.GlobalPosition) < FleeRadius)
		{
			Flee();
			return;
		}

		if (Animation != "look_around")
			return;
		_timer -= dt;
		if (_timer <= 0f)
			Play(Idles[_rng.RandiRange(0, Idles.Length - 1)]);
	}

	private void Rest()
	{
		Play("look_around");
		_timer = _rng.RandfRange(MinRestTime, MaxRestTime);
	}

	private void Flee()
	{
		_fleeing = true;
		_timer = FlyTime;
		_dir = _player.GlobalPosition.X > GlobalPosition.X ? -1 : 1;
		FlipH = _dir < 0;
		Play("takeoff");
	}

	private void OnAnimationFinished()
	{
		if (_fleeing)
		{
			_airborne = true;
			Play("fly");
			return;
		}
		Rest();
	}
}
