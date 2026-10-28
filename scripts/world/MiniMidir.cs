using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// A small Midir (regular enemy, not a boss): the same dragon kit as LugiaBoss — claws, spit, fire
// breath, savage rush, air sweep, under-fire, phase-2 humanities — shrunk to SizeScale (0.42 ≈ 58%
// smaller) and without the beam attacks (dark beam and the near-death desperation laser).
// Shrinking: every pixel distance the moves use (ranges, mouth offsets, hit areas, heights, travel,
// speeds that set a reach) is multiplied by SizeScale before LugiaBoss builds its pieces, and the
// sprite, collision and bars are scaled to match. Values set on the instance are the full-size
// ones; the scale applies on top. Times, chances and damage are untouched.
// No sleep intro, no boss group (so no boss bar or boss zoom with several of them around) and it
// respawns like a common enemy instead of staying dead for the save.
public partial class MiniMidir : LugiaBoss
{
	[Export] public float SizeScale = 0.42f;

	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
			Shrink(SizeScale);
		StartAsleep = false;
		ShowcaseMode = false;
		// No beams: the dark beam only unlocks below BeamHealthRatio and the desperation laser below
		// DesperationHealthRatio — neither can ever be reached.
		BeamHealthRatio = -1f;
		DesperationHealthRatio = -1f;

		base._Ready();
		RemoveFromGroup("boss");
	}

	protected override bool IsDefeated() => SaveManager.Instance.IsCommonEnemyDefeated(PersistenceId);

	protected override void OnDefeated()
	{
		base.OnDefeated();
		SaveManager.Instance.MarkCommonEnemyDefeated(PersistenceId);
	}

	private void Shrink(float s)
	{
		// Body
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is { Shape: RectangleShape2D body } bodyShape)
			bodyShape.Shape = new RectangleShape2D { Size = body.Size * s };
		if (GetNodeOrNull<CollisionShape2D>("ContactArea/CollisionShape2D") is { Shape: RectangleShape2D contact } contactShape)
			contactShape.Shape = new RectangleShape2D { Size = contact.Size * s };
		if (GetNodeOrNull<Node2D>("Visual/CharacterSprite") is Node2D sprite)
		{
			sprite.Scale *= s;
			sprite.Position *= s;
		}
		foreach (string bar in new[] { "HealthBar", "StaminaBar" })
			if (GetNodeOrNull<Node2D>(bar) is Node2D b)
				b.Position *= s;

		// Movement / claws
		EngageDistance *= s;
		ClawRange *= s;
		ClawReach *= s;
		ClawRadius *= s;
		ClawLungeSpeed *= s;
		ClawArcCenter *= s;
		ClawArcRadius *= s;
		ClawGustReach *= s;
		SpitAreaCenter *= s;
		SpitAreaSize *= s;

		// Fire breath
		FireMinRange *= s;
		FireMaxRange *= s;
		GroundMouth *= s;
		FlyMouth *= s;
		// Reach of the stream ~ speed × lifetime (damping and turbulence barely bite on a short life), so
		// both shrink — speed alone left it ~twice as long as the burn cone.
		BreathSpeed *= s;
		BreathLifetime *= s;
		SweepBreathLifetime *= s;
		GroundFireHalfWidth *= s;
		BreathPlayerMargin *= s;
		FireAreaCenter *= s;
		FireAreaSize *= s;

		// Phase 2 / humanities
		PhaseBlastRadius *= s;
		OrbMinRange *= s;

		// Savage rush
		RushMinRange *= s;
		RushSpeed *= s;
		RushHitWidth *= s;
		RushHitHeight *= s;
		BigMoveMinRange *= s;

		// Flight / air sweep
		FlySpeed *= s;
		TakeOffHeight *= s;
		SweepHeight *= s;
		SweepStartOffset *= s;
		SweepTravel *= s;
		SweepSpeed *= s;
		SweepBurnHalfWidth *= s;
		SweepBurnHeight *= s;
		SweepPatchSpacing *= s;

		// Under-fire
		UnderFireRise *= s;
		UnderFireRadius *= s;
		UnderFireBurnHeight *= s;

		// Roar (claw triple ends in one)
		RoarStreakLength *= s;
		RoarWindReach *= s;
		RoarShoveRange *= s;
		RoarShakeStrength *= s;

		// Effects (fire, embers, floor flames, smoke, humanities) shrink with the body.
		FxScale *= s;

		// Body chaos and the crystal sheen
		JitterAmount *= s;
		GhostSpread *= s;
		SheenScale *= s;
	}
}
