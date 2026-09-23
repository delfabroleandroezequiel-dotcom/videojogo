using Godot;

namespace Metroidvania.World;

// One ElfMage spell. Same fields as HechiceroAttack/MagoCaminoAttack (it fires the same bolt
// scenes) plus which of the elf's own animations casts it and where the bolt leaves from — the
// straight bolt comes off the staff thrust ("attack"), the homing ones from the orb charged above
// its head ("spell_1").
[GlobalClass]
public partial class ElfMageAttack : Resource, ISpellDefinition
{
	[Export] public bool Enabled = true;
	[Export] public string AttackName = "Bolt";
	[Export] public PackedScene ProjectileScene;
	[Export] public float ProjectileSpeed = 250f;
	[Export] public float Cooldown = 2f;
	[Export] public float Range = 350f;
	[Export] public float CastDuration = 0.67f;
	[Export] public float ReleaseDelay = 0.25f;
	[Export] public int ProjectileCount = 1;
	[Export] public float BurstInterval = 0.15f;
	[Export] public StringName Animation = "attack";
	// Relative to the body when facing right (mirrored when facing left).
	[Export] public Vector2 SpawnOffset = new(30f, -15f);

	bool ISpellDefinition.Enabled => Enabled;
	string ISpellDefinition.AttackName => AttackName;
	PackedScene ISpellDefinition.ProjectileScene => ProjectileScene;
	float ISpellDefinition.ProjectileSpeed => ProjectileSpeed;
	float ISpellDefinition.Cooldown => Cooldown;
	float ISpellDefinition.Range => Range;
	float ISpellDefinition.CastDuration => CastDuration;
	float ISpellDefinition.ReleaseDelay => ReleaseDelay;
	int ISpellDefinition.ProjectileCount => ProjectileCount;
	float ISpellDefinition.BurstInterval => BurstInterval;
}
