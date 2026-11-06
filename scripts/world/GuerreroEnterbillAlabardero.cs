using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Alabardero de la guardia de Enterbill — halberd, the longest reach of the line.
// - Close/mid: a wide horizontal sweep at waist height (attack). Fast for its size.
// - Far, or when you keep backing off: lifts the halberd overhead for a long, obvious flash and
//   chops it into the ground (attack2). The chop cracks the ground forward: a low shockwave runs
//   ahead of the blade, so stepping back alone doesn't save you — jump it.
// - Doesn't flinch while committed to the chop (it's a guard, not a skirmisher).
public partial class GuerreroEnterbillAlabardero : GuerreroReino
{
	[Export] public float SweepWindup = 0.4f;
	[Export] public float ChopWindup = 0.75f;
	[Export] public float ChopRange = 70f;
	[Export] public float ShockwaveLength = 130f;
	[Export] public float ShockwaveSpeed = 360f;

	// attack: 1 drawn back, 2-4 sweep extended. attack2: 1-4 raising, 5 blade into the ground.
	[Export] public int SweepWindupFrame = 1;
	[Export] public int SweepFrame = 2;
	[Export] public int ChopWindupFrame = 4;
	[Export] public int ChopFrame = 5;

	protected override async Task<bool> Kit()
	{
		bool far = TargetDistanceX > ChopRange || (Target is { } p && PlayerReads.IsClosingIn(p, GlobalPosition.X) == false && Roll() < 0.35f);
		return far ? await Chop() : await Sweep();
	}

	private async Task<bool> Sweep()
	{
		float windup = SweepWindup * WindupScale;
		WindupScale = 1f;
		if (!await Telegraph("attack", SweepWindupFrame, windup))
			return false;
		MaybeShout();
		return await Strike("attack", SweepFrame, 8, SweepFrame, 36f, new Vector2(70f, 30f), y: -2f, activeFrames: 2,
			trail: new TrailArc(new Vector2(-6f, -2f), 82f, -30f, 12f, 0.12f, 26f));
	}

	private async Task<bool> Chop()
	{
		if (!await Strike("attack2", 1, ChopWindupFrame - 1, 99, 0f, Vector2.Zero, fps: 10f))
			return false;
		if (!await Telegraph("attack2", ChopWindupFrame, ChopWindup))
			return false;
		Bark("Ataque", 0.8f);
		bool committed = HyperArmorWhileAttacking;
		HyperArmorWhileAttacking = true;
		try
		{
			if (!await Strike("attack2", ChopFrame, ChopFrame, ChopFrame, 40f, new Vector2(44f, 60f), y: 0f, damage: 1.3f,
				trail: new TrailArc(new Vector2(0f, -10f), 62f, -95f, 45f, 0.09f, 52f)))
				return false;
			Vector2 ground = GroundPoint(56f);
			GroundSlamFx.Impact(this, ground, 0.8f);
			Shake(4f, 0.18f);
			_ = Shockwave(ground);
			ExtraCooldown = 0.4f;
			return await Strike("attack2", ChopFrame + 1, 8, 99, 0f, Vector2.Zero, fps: 9f);
		}
		finally
		{
			HyperArmorWhileAttacking = committed;
		}
	}

	// A low crack that runs forward along the floor: separate hitbox so the halberd's own one stays
	// at the blade.
	private async Task Shockwave(Vector2 start)
	{
		var wave = (Metroidvania.Player.Hitbox)AttackHitbox.Duplicate();
		GetTree().CurrentScene.AddChild(wave);
		var shape = new RectangleShape2D { Size = new Vector2(26f, 14f) };
		wave.GetNode<CollisionShape2D>("CollisionShape2D").Shape = shape;
		wave.GlobalPosition = start + new Vector2(0f, -7f);
		wave.Activate(Stats, damageMultiplier: 0.7f);
		float dir = Forward;
		float travelled = 0f;
		while (travelled < ShockwaveLength && IsInstanceValid(wave))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(wave))
				return;
			float step = ShockwaveSpeed * (float)GetPhysicsProcessDeltaTime();
			wave.GlobalPosition += new Vector2(dir * step, 0f);
			travelled += step;
			if ((int)(travelled / 26f) != (int)((travelled - step) / 26f))
				GroundSlamFx.Chip(wave, wave.GlobalPosition + new Vector2(0f, 7f), 0.4f);
		}
		if (IsInstanceValid(wave))
			wave.QueueFree();
	}
}
