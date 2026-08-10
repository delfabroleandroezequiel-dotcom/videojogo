using Godot;

namespace Metroidvania.Player;

// A single 2x2px mouth dot, drawn by this node instead of baked into the idle sprite sheet, so
// protagonistnuevo's export doesn't need a mouth-open variant of every frame. Blink timing runs
// on its own random Timer, independent of BodySprite's 8-frame idle loop, so it doesn't read as
// a tell that repeats every cycle. IdleMouthOffsets was measured pixel-by-pixel from
// assets/sprites/protagonistnuevo/iddle (idle_00..07, 98x70, texture center at 49,35) — the mouth
// shifts 1-2px between frames from the character's breathing bob, so this node re-centers itself
// on BodySprite.FrameChanged instead of sitting at one fixed offset.
// Idle-only: the run sheet leans the head forward enough that the mouth pixel isn't isolated from
// the hat/hair silhouette anymore (measured, not visible in any run_00-07 frame), so there's no
// offset table for it — this stays hidden outside "idle" instead of drawing at a wrong spot.
public partial class MouthBlink : Node2D
{
	[Export] public AnimatedSprite2D BodySprite;
	[Export] public float MinInterval = 1.5f;
	[Export] public float MaxInterval = 3.5f;
	[Export] public float OpenDuration = 0.12f;

	private static readonly Vector2[] IdleMouthOffsets =
	{
		new(3.5f, -7f),
		new(3.5f, -6f),
		new(3.5f, -6f),
		new(4.5f, -5f),
		new(4.5f, -5f),
		new(4.5f, -6f),
		new(3.5f, -6f),
		new(3.5f, -7f),
	};

	private Timer _timer;
	private bool _open;

	public override void _Ready()
	{
		BodySprite ??= GetParentOrNull<AnimatedSprite2D>();

		Visible = false;
		BodySprite.FrameChanged += OnFrameChanged;
		BodySprite.AnimationChanged += OnAnimationChanged;
		OnFrameChanged();

		_timer = new Timer { OneShot = true };
		AddChild(_timer);
		_timer.Timeout += OnTimeout;
		ScheduleNextBlink();
	}

	private void OnFrameChanged()
	{
		if (BodySprite.Animation != "idle")
			return;

		int frame = BodySprite.Frame;
		if (frame >= 0 && frame < IdleMouthOffsets.Length)
			Position = IdleMouthOffsets[frame];
	}

	private void OnAnimationChanged()
	{
		if (BodySprite.Animation != "idle")
			Visible = false;
	}

	private void ScheduleNextBlink()
	{
		_timer.WaitTime = (float)GD.RandRange(MinInterval, MaxInterval);
		_timer.Start();
	}

	private void OnTimeout()
	{
		_open = !_open;
		Visible = _open && BodySprite.Animation == "idle";
		if (_open)
		{
			_timer.WaitTime = OpenDuration;
			_timer.Start();
		}
		else
		{
			ScheduleNextBlink();
		}
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(-1, -1, 2, 2), Colors.Black);
	}
}
