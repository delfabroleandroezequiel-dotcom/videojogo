using Godot;

namespace Metroidvania.World;

// Pájaro de las Raíces: a bat-like flyer that only commits to its dive while the player hangs
// from something (a swinging rope, a hand-over-hand rope, a vine). Reaching them knocks them off
// whatever they hold (Player.SoltarAgarre) into the mud below. On solid ground it just circles
// and harasses like a weak bat. Greybox: reuses the small bat body, tinted, until it has art.
public partial class PajaroRaices : BatEnemy
{
	// Dive range while the player hangs (the bat's own AttackRange is overridden every frame).
	[Export] public float GrabbedAttackRange = 170f;
	[Export] public float KnockRadius = 50f;
	[Export] public float KnockPush = 140f;
	[Export] public float KnockCooldown = 1.2f;
	[Export] public Color Tint = new(1f, 0.82f, 0.55f, 1f);

	private float _knockCooldown;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		if (Visual is not null)
			Visual.Modulate = Tint;
	}

	public override void _PhysicsProcess(double delta)
	{
		var player = EnemyTargeting.Current(GetTree()) as Metroidvania.Player.Player;
		bool grabbed = player is { EstaAgarrado: true };
		AttackRange = grabbed ? GrabbedAttackRange : 0f;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		if (_knockCooldown > 0f)
			_knockCooldown -= (float)delta;
		if (grabbed && _knockCooldown <= 0f && GlobalPosition.DistanceTo(player.GlobalPosition) <= KnockRadius)
		{
			_knockCooldown = KnockCooldown;
			float side = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
			player.SoltarAgarre((side == 0f ? 1f : side) * KnockPush);
		}
	}
}
