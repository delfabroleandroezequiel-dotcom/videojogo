using Godot;

namespace Metroidvania.World;

// The Gigante holes' shot: a plain Projectile (parryable, mirrors back into enemies — i.e. back
// into the hole) drawn procedurally as a neon orb instead of an arrow sprite.
public partial class ProyectilAgujero : Projectile
{
	[Export] public Color ColorNeon = new(0.35f, 1f, 0.45f);
	[Export] public float Radio = 9f;

	public override void _Ready()
	{
		AddChild(NeonFx.Circle(Radio * 2.2f, new Color(ColorNeon, 0.35f), additive: true));
		AddChild(NeonFx.Circle(Radio, ColorNeon, additive: true));
		AddChild(NeonFx.Circle(Radio * 0.45f, new Color(1f, 1f, 0.95f), additive: true));
		base._Ready();
	}
}
