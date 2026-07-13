using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// One resource per enemy TYPE (not per placed instance) — e.g. one profile shared by every
// "Slime Rojo" across every map. Assigning it on an Enemy scene overrides that scene's own
// inline defaults at _Ready(), so balancing an enemy is "edit this .tres", not "find every
// scene that has one and edit each by hand".
[GlobalClass]
public partial class EnemyProfile : Resource
{
	[Export] public int MaxHealth = 25;
	[Export] public int AttackPower = 15;
	[Export] public int Defense = 0;
	[Export] public int MaxStamina = 30;
	[Export] public int StaminaRegenPerSecond = 20;
	[Export] public DamageElement Weakness = DamageElement.Normal;
	[Export] public float WeaknessDamageMultiplier = 1.5f;

	[Export] public float MoveSpeed = 80f;
	[Export] public float DetectionRange = 400f;
	[Export] public float StopDistance = 0f;
	[Export] public float Gravity = 900f;
	[Export] public float KnockbackDuration = 0.2f;
	[Export] public float ContactDamageMultiplier = 0.3f;

	[Export] public LootEntry[] LootTable = System.Array.Empty<LootEntry>();
}
