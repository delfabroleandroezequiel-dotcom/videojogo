using Godot;

namespace Metroidvania.Shared;

// Numbers for the Ecos stat leveling (see SaveManager.Ecos / LevelUpUI): what each level of Vida,
// Stamina and Daño adds, the soft cap past which a stat's levels give less, and how the price of
// the next level grows with the total level. Lives in resources/balance/LevelingConfig.tres.
[GlobalClass]
public partial class LevelingConfig : Resource
{
	public const string DefaultPath = "res://resources/balance/LevelingConfig.tres";

	[Export] public float HealthPerLevel = 8f;
	[Export] public float StaminaPerLevel = 6f;
	[Export] public float AttackPerLevel = 1f;
	// From this many levels in one stat on, each further level of it is worth SoftCapFactor.
	[Export] public int SoftCapLevel = 10;
	[Export] public float SoftCapFactor = 0.5f;
	// Price of the next level = BaseCost × (1 + CostGrowth)^(total levels bought), rounded to 10.
	[Export] public int BaseCost = 100;
	[Export] public float CostGrowth = 0.12f;
	// Enemies with EcosReward < 0 grant MaxHealth × this (at least MinEcosReward).
	[Export] public float EcosPerEnemyHealth = 0.5f;
	[Export] public int MinEcosReward = 5;

	private static LevelingConfig _cached;

	public static LevelingConfig Get() =>
		_cached ??= ResourceLoader.Exists(DefaultPath) ? GD.Load<LevelingConfig>(DefaultPath) : new LevelingConfig();

	public int CostForTotalLevel(int totalLevel) =>
		Mathf.Max(10, Mathf.RoundToInt(BaseCost * Mathf.Pow(1f + CostGrowth, totalLevel) / 10f) * 10);

	// Total bonus from `levels` levels of a stat worth `perLevel` each, soft cap included.
	public int BonusFor(int levels, float perLevel)
	{
		int full = Mathf.Min(levels, SoftCapLevel);
		int reduced = Mathf.Max(0, levels - SoftCapLevel);
		return Mathf.RoundToInt(full * perLevel + reduced * perLevel * SoftCapFactor);
	}

	public float PerLevel(string statId) => statId switch
	{
		StatIds.Vitality => HealthPerLevel,
		StatIds.Endurance => StaminaPerLevel,
		StatIds.Strength => AttackPerLevel,
		_ => 0f,
	};
}

// The three stats Ecos can raise (keys of SaveManager's stat levels / SaveData.StatLevels).
public static class StatIds
{
	public const string Vitality = "vitality";
	public const string Endurance = "endurance";
	public const string Strength = "strength";
	public static readonly string[] All = { Vitality, Endurance, Strength };
}
