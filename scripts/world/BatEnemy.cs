using Godot;

namespace Metroidvania.World;

public partial class BatEnemy : Enemy
{
	[Export] public float HoverAmplitude = 14f;
	[Export] public float HoverSpeed = 2f;

	private float _time;
	private Vector2 _basePosition;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_basePosition = GlobalPosition;
		_time = GD.Randf() * Mathf.Tau;
		Sprite?.Play("fly");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		_time += (float)delta;
		GlobalPosition = _basePosition + new Vector2(0, Mathf.Sin(_time * HoverSpeed) * HoverAmplitude);

		ApplyContactDamage();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
	}
}
