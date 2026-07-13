using Godot;

namespace Metroidvania.World;

public partial class ParallaxFollow : Node2D
{
	[Export] public Vector2 ParallaxFactor = new(1, 1);

	private Node2D _player;
	private Vector2 _basePosition;

	public override void _Ready()
	{
		_basePosition = GlobalPosition;
	}

	public override void _Process(double delta)
	{
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (_player is null)
			return;

		GlobalPosition = _basePosition + _player.GlobalPosition * ParallaxFactor;
	}
}
