using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Common read-only view of a caster's attack resource (HechiceroAttack, MagoCaminoAttack,
// ElfMageAttack each stay their own Resource type so their Inspector blocks can diverge; they just
// expose the same basics so SpellBrain can reason about any of them).
public interface ISpellDefinition
{
	bool Enabled { get; }
	string AttackName { get; }
	PackedScene ProjectileScene { get; }
	float ProjectileSpeed { get; }
	float Cooldown { get; }
	float Range { get; }
	float CastDuration { get; }
	float ReleaseDelay { get; }
	int ProjectileCount { get; }
	float BurstInterval { get; }
}

// What the caster sees right now when picking a spell.
public readonly record struct SpellSituation(
	float Distance, bool PlayerAirborne, bool PlayerClosingIn, bool Cornered, bool PlayerBlockingToward);

// Picks a caster's next spell by scoring every ready, in-range, enabled spell against the current
// situation instead of rolling uniformly at random:
//  * single straight bolt — the default when the player is grounded and readable;
//  * homing bolt — player airborne, far away, or blocking (it curves in);
//  * volleys (ProjectileCount > 1) — the player keeps dodging (miss streak), is rushing in, or the
//    caster is cornered.
// Keeps each spell's own cooldown and a "miss pressure" fed by the projectiles' Resolved events.
// Only chooses — each caster still owns how it casts (animation, timing, where the bolt spawns).
public sealed class SpellBrain
{
	public float MissPressure { get; private set; }

	private readonly Dictionary<ISpellDefinition, float> _cooldowns = new();
	private readonly RandomNumberGenerator _rng = new();
	private static readonly Dictionary<string, bool> HomingByScenePath = new();

	public SpellBrain() => _rng.Randomize();

	public void Tick(float delta)
	{
		foreach (ISpellDefinition spell in new List<ISpellDefinition>(_cooldowns.Keys))
			_cooldowns[spell] -= delta;
	}

	public bool IsReady(ISpellDefinition spell) => !_cooldowns.TryGetValue(spell, out float left) || left <= 0f;

	public void MarkCast(ISpellDefinition spell) => _cooldowns[spell] = spell.Cooldown;

	// A projectile from a cast of `projectilesInCast` resolved: any hit clears the pressure, each
	// miss adds its share (a fully dodged 3-volley counts as one whole miss).
	public void Report(bool hit, int projectilesInCast) =>
		MissPressure = hit ? 0f : MissPressure + 1f / Mathf.Max(1, projectilesInCast);

	public static bool IsHoming(ISpellDefinition spell)
	{
		PackedScene scene = spell?.ProjectileScene;
		if (scene is null)
			return false;
		if (HomingByScenePath.TryGetValue(scene.ResourcePath, out bool homing))
			return homing;

		Node probe = scene.Instantiate();
		homing = probe is HomingProjectile;
		probe.Free();
		HomingByScenePath[scene.ResourcePath] = homing;
		return homing;
	}

	// `rangeOf` lets a caster apply its own range override (e.g. a placed sniper's ShootRange).
	public ISpellDefinition Choose(IEnumerable<ISpellDefinition> spells, SpellSituation situation,
		System.Func<ISpellDefinition, float> rangeOf)
	{
		List<(ISpellDefinition spell, float score)> options = new();
		float total = 0f;
		foreach (ISpellDefinition spell in spells)
		{
			if (spell is null || !spell.Enabled || !IsReady(spell) || situation.Distance > rangeOf(spell))
				continue;

			float score = Score(spell, situation);
			if (score <= 0f)
				continue;
			options.Add((spell, score));
			total += score;
		}

		if (options.Count == 0)
			return null;

		float roll = _rng.Randf() * total;
		foreach ((ISpellDefinition spell, float score) in options)
		{
			roll -= score;
			if (roll <= 0f)
				return spell;
		}
		return options[^1].spell;
	}

	private float Score(ISpellDefinition spell, SpellSituation s)
	{
		bool volley = spell.ProjectileCount > 1;
		bool homing = IsHoming(spell);

		if (volley)
			return 0.3f + 0.5f * MissPressure + (s.PlayerClosingIn ? 0.4f : 0f) + (s.Cornered ? 0.8f : 0f)
				+ (s.PlayerAirborne ? 0.3f : 0f);
		if (homing)
			return 0.6f + (s.PlayerAirborne ? 0.8f : 0f) + (s.Distance > 260f ? 0.4f : 0f) + 0.3f * MissPressure
				+ (s.PlayerBlockingToward ? 0.5f : 0f);
		return 1f + (s.PlayerAirborne ? -0.5f : 0.3f) + (s.PlayerBlockingToward ? -0.6f : 0f);
	}
}
