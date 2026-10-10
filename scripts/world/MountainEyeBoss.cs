using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// Boss of the Montaña Extraña: a pair of the mountain-creature's eyes set high up, side by side
// like the eyes of a face (the right one is the left one mirrored). Static — the fight is about
// climbing up to them; attacks (rays, summoned rocks…) get designed with the user step by step,
// like MushroomBoss, so for now they only watch the player, breathe and squint together every
// few seconds (the squint is meant to become the attack telegraph).
// Gaze: both eyes pick one of the sheet's 7 gaze poses (bossOjo_frames.png, built from bossOjo.png
// with every pose cropped to its content and normalized to one size, so the body doesn't jump
// between poses) from the angle to the player, held briefly so they don't flicker on a boundary.
// The mirrored eye picks its pose in mirrored space, so both look at the same spot.
// One Stats/health pool for both: hitting either eye damages the boss.
public partial class MountainEyeBoss : FlyingEnemy
{
	// Frame order in bossOjo_frames.png.
	private const int FrameCenter = 0;
	private const int FrameLeft = 1;
	private const int FrameUp = 2;
	private const int FrameRight = 3;
	private const int FrameDownLeft = 4;
	private const int FrameDown = 5;
	private const int FrameDownRight = 6;
	private const int FrameSquint = 7;

	// Slow "breathing" so the static eyes don't read as a frozen picture.
	[Export] public float BreatheAmplitude = 3f;
	[Export] public float BreatheSpeed = 1.1f;
	[Export] public Vector2 SquintIntervalRange = new(3f, 5.5f);
	[Export] public float SquintDuration = 0.6f;
	// Gaze: within this distance an eye just stares straight ahead.
	[Export] public float StareDistance = 70f;
	[Export] public float GazeHoldTime = 0.12f;

	private Sprite2D _leftEye;
	private Sprite2D _rightEye;
	private Vector2 _home;
	private float _time;
	private float _squintTimer;
	private float _squintLeft;
	private int _leftFrame = FrameCenter;
	private int _rightFrame = FrameCenter;
	private float _gazeHold;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		AddToGroup("boss");
		_leftEye = Visual.GetNode<Sprite2D>("LeftEye");
		_rightEye = Visual.GetNode<Sprite2D>("RightEye");
		_home = GlobalPosition;
		_rng.Randomize();
		_squintTimer = _rng.RandfRange(SquintIntervalRange.X, SquintIntervalRange.Y);
	}

	protected override bool IsDefeated() => SaveManager.Instance.IsBossDefeated(PersistenceId);

	protected override void OnDefeated()
	{
		SaveManager.Instance.MarkBossDefeated(PersistenceId);
		SpawnExplosion();
		SpawnLoot();
		QueueFree();
	}

	// Static: takes over FlyingEnemy's flight entirely, only breathing in place.
	protected override bool OverrideSteering(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		Anchor = _home;
		GlobalPosition = _home + new Vector2(0f, Mathf.Sin(_time * BreatheSpeed) * BreatheAmplitude);
		PlayerDetected = Player is not null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= DetectionRange;

		if (_squintLeft > 0f)
		{
			_squintLeft -= dt;
		}
		else
		{
			_squintTimer -= dt;
			if (_squintTimer <= 0f)
			{
				_squintLeft = SquintDuration;
				_squintTimer = _rng.RandfRange(SquintIntervalRange.X, SquintIntervalRange.Y);
			}
		}

		UpdateGaze(dt);
		return true;
	}

	private void UpdateGaze(float dt)
	{
		_gazeHold -= dt;
		bool squinting = _squintLeft > 0f;
		int left = squinting ? FrameSquint : GazeFrame(_leftEye, false);
		int right = squinting ? FrameSquint : GazeFrame(_rightEye, true);
		if ((left != _leftFrame || right != _rightFrame) && (_gazeHold <= 0f || squinting))
		{
			_leftFrame = left;
			_rightFrame = right;
			_gazeHold = GazeHoldTime;
		}
		_leftEye.Frame = _leftFrame;
		_rightEye.Frame = _rightFrame;
	}

	private int GazeFrame(Node2D eye, bool mirrored)
	{
		if (Player is null)
			return FrameCenter;

		Vector2 toPlayer = Player.GlobalPosition - eye.GlobalPosition;
		if (toPlayer.Length() < StareDistance)
			return FrameCenter;
		// The mirrored sprite shows every pose flipped, so pick the pose for the mirrored direction.
		if (mirrored)
			toPlayer.X = -toPlayer.X;

		// 8 sectors of 45°, 0° = right, +90° = down (Godot's Y points down). There's no
		// up-left/up-right pose on the sheet, so those fall back to plain left/right.
		int sector = Mathf.PosMod(Mathf.RoundToInt(Mathf.RadToDeg(toPlayer.Angle()) / 45f), 8);
		return sector switch
		{
			0 => FrameRight,
			1 => FrameDownRight,
			2 => FrameDown,
			3 => FrameDownLeft,
			4 => FrameLeft,
			5 => FrameLeft,
			6 => FrameUp,
			_ => FrameRight,
		};
	}
}
