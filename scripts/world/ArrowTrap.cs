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
	// How far the arrow flies (px) before it disappears. 0 = leave ArrowProjectile's own Lifetime
	// alone (Speed * Lifetime, i.e. 950px at the defaults). Converted to a lifetime from
	// ProjectileSpeed, so changing the speed keeps the distance the same.
	[Export] public float MaxDistance;
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
		// Has to be set before AddChild: ArrowProjectile starts its Lifetime timer in _Ready.
		if (MaxDistance > 0f && ProjectileSpeed > 0f)
			projectile.Lifetime = MaxDistance / ProjectileSpeed;
		GetTree().CurrentScene.AddChild(projectile);
		projectile.GlobalPosition = GetNode<Marker2D>("Muzzle").GlobalPosition;
		projectile.Speed = ProjectileSpeed;
		projectile.Damage = Damage;
		projectile.KnockbackForce = KnockbackForce;
		projectile.Launch(Direction);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}
}
