using Godot;

namespace Metroidvania.World;

public partial class ParallaxFollow : Node2D
{
	[Export] public Vector2 ParallaxFactor = new(1, 1);

	// Track the camera's screen centre instead of the player: unlike the player, it stops at the
	// room's camera limits, so a layer with factor 1 on an axis stays locked to the screen.
	[Export] public bool FollowCamera;

	// > 0: scales this node so a child texture of this height (in its own pixels) exactly fills the
	// visible screen height at the current camera zoom (for screen-sized layer art like the cave
	// parallax set, 384x216). 0 = leave the scale alone.
	[Export] public float FitViewHeightSource;

	// Extra scale on top of the fit, so a fast vertical move can't open a gap at the screen's top or
	// bottom edge (1 = exact fit).
	[Export] public float FitOverscan = 1f;

	// On a Sprite2D: shift only its drawing (Sprite2D.offset) instead of moving the node, so its
	// children (platforms, buildings, triggers hung off a background sprite) stay put. The offset is
	// (target - OffsetAnchor) * ParallaxFactor: the sprite looks as authored when the camera/player
	// is at OffsetAnchor.
	[Export] public bool ShiftOffsetOnly;
	[Export] public Vector2 OffsetAnchor;

	private Node2D _player;
	private Vector2 _basePosition;

	public override void _Ready()
	{
		_basePosition = GlobalPosition;
		// Run after the camera has moved this frame (it updates in its own process step, with
		// smoothing): reading it first left the layer a frame behind, showing a sliver of the clear
		// colour at the top/bottom edge on fast jumps and falls.
		if (FollowCamera)
			ProcessPriority = 1000;
	}

	public override void _Process(double delta)
	{
		Camera2D camera = FollowCamera || FitViewHeightSource > 0f ? GetViewport().GetCamera2D() : null;
		if (FitViewHeightSource > 0f && camera is not null)
		{
			float fit = GetViewportRect().Size.Y / camera.Zoom.Y / FitViewHeightSource * FitOverscan;
			Scale = new Vector2(fit, fit);
		}

		Vector2 target;
		if (FollowCamera)
		{
			if (camera is null)
				return;
			target = camera.GetScreenCenterPosition();
		}
		else
		{
			_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (_player is null)
				return;
			target = _player.GlobalPosition;
		}

		if (ShiftOffsetOnly)
			Set(Sprite2D.PropertyName.Offset, (target - OffsetAnchor) * ParallaxFactor);
		else
			GlobalPosition = _basePosition + target * ParallaxFactor;
	}
}
