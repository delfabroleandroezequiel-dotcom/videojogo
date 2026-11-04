using Godot;

namespace Metroidvania.World;

// Drop under an Npc to make it walk back and forth between two X offsets from where it was placed
// (a guard on his round), pausing at each end. It drives the Npc through the same
// CinematicControl/CinematicVelocityX hook cinematics use, so Npc.cs stays untouched; when the
// player comes close it hands control back so the Npc stops, turns to face them and can be talked to.
public partial class NpcPatrulla : Node
{
	[Export] public float LeftOffset = -160f;
	[Export] public float RightOffset = 160f;
	[Export] public float Speed = 45f;
	[Export] public float PauseAtEnds = 1.6f;
	[Export] public float StopForPlayerDistance = 70f;
	[Export] public string WalkAnimation = "walk";
	[Export] public string IdleAnimation = "idle";

	private Npc _npc;
	private Node2D _visual;
	private AnimatedSprite2D _sprite;
	private float _originX;
	private float _direction = 1f;
	private float _pauseTimer;

	public override void _Ready()
	{
		_npc = GetParent<Npc>();
		_visual = _npc.GetNode<Node2D>("Visual");
		_sprite = _npc.GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		_originX = _npc.GlobalPosition.X;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsInstanceValid(_npc))
			return;

		if (PlayerIsClose() || Cinematic.Playing)
		{
			_npc.CinematicControl = false;
			Play(IdleAnimation);
			return;
		}

		_npc.CinematicControl = true;
		if (_pauseTimer > 0f)
		{
			_pauseTimer -= (float)delta;
			_npc.CinematicVelocityX = 0f;
			Play(IdleAnimation);
			if (_pauseTimer <= 0f)
				_direction = -_direction;
			return;
		}

		float x = _npc.GlobalPosition.X - _originX;
		if ((_direction > 0f && x >= RightOffset) || (_direction < 0f && x <= LeftOffset))
		{
			_pauseTimer = PauseAtEnds;
			_npc.CinematicVelocityX = 0f;
			return;
		}

		_npc.CinematicVelocityX = _direction * Speed;
		_visual.Scale = new Vector2(_direction, 1f);
		Play(WalkAnimation);
	}

	private bool PlayerIsClose() =>
		GetTree().GetFirstNodeInGroup("player") is Node2D player
		&& player.GlobalPosition.DistanceTo(_npc.GlobalPosition) <= StopForPlayerDistance;

	private void Play(string animation)
	{
		if (_sprite.Animation != animation && _sprite.SpriteFrames.HasAnimation(animation))
			_sprite.Play(animation);
	}
}
