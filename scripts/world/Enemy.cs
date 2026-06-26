using Godot;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class Enemy : CharacterBody2D
{
	private Stats _stats;

	public override void _Ready()
	{
		_stats = GetNode<Stats>("Stats");
		_stats.Died += OnDied;

		StatBar healthBar = GetNode<StatBar>("HealthBar");
		StatBar staminaBar = GetNode<StatBar>("StaminaBar");
		_stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		_stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);
	}

	private void OnDied()
	{
		QueueFree();
	}
}
