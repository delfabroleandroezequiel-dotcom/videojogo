using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Wall-mounted arrow trap: fires an ArrowProjectile out of Muzzle every FireInterval seconds,
// forever, starting FireInterval - StartOffset seconds after _Ready. StartOffset is what
// ArrowTrapAccordion staggers per instance — 0 on every trap fires them all in sync ("todas
// juntas"), a growing offset per trap fires them as a wave ("accordion") — same knob SpikeTrap
// exposes for the same reason. No visual of its own: dress the wall hole with real art
// separately, this piece only owns the firing logic and the muzzle point.
public partial class ArrowTrap : Node2D
{
	[Export] public PackedScene ProjectileScene;
	[Export] public Vector2 Direction = Vector2.Right;
	[Export] public float FireInterval = 1.8f;
	[Export] public float StartOffset;
	[Export] public float ProjectileSpeed = 380f;
	[Export] public int Damage = 15;
	[Export] public float KnockbackForce = 260f;

	private float _timer;

	public override void _Ready()
	{
		_timer = FireInterval - StartOffset;
	}

	public override void _Process(double delta)
	{
		_timer += (float)delta;
		if (_timer < FireInterval)
			return;

		_timer -= FireInterval;
		Fire();
	}

	private void Fire()
	{
		if (ProjectileScene is null)
			return;

		ArrowProjectile projectile = ProjectileScene.Instantiate<ArrowProjectile>();
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = GetNode<Marker2D>("Muzzle").GlobalPosition;
		projectile.Speed = ProjectileSpeed;
		projectile.Damage = Damage;
		projectile.KnockbackForce = KnockbackForce;
		projectile.Launch(Direction);
		Sfx.PlayAt(this, Sfx.FalloGolpe);
	}
}
