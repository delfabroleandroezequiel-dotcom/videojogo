using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public partial class Enemy : CharacterBody2D
{
	private Stats _stats;

	public override void _Ready()
	{
		_stats = GetNode<Stats>("Stats");
		_stats.Died += OnDied;
	}

	private void OnDied()
	{
		QueueFree();
	}
}
