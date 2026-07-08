using Godot;

namespace Metroidvania.World;

public partial class TorchLight : PointLight2D
{
	[Export] public Vector2 FollowOffset = new(0, -20);

	private Node2D _player;

	public override void _Ready()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
	}

	public override void _Process(double delta)
	{
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (_player is null)
			return;

		GlobalPosition = _player.GlobalPosition + FollowOffset;
	}
}
