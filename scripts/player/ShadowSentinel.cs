using System;
using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.Player;

// Clon centinela (Andromeda's chain): a neon silhouette planted in guard where it was cast. While
// the player stays within GuardRadius of it, the first attack or projectile that would hit them is
// parried by the sentinel instead (Player.TryBlockIncomingHit / TryParryProjectile ask TryGuard),
// so it counts and mirrors exactly like a parry of their own. It then answers with one counter
// slash at the nearest enemy (a Clon básico swing, via the caster's callback) and is gone. Unused,
// it fades after Duration. The counter callback gets the target's spot (at the sentinel's height)
// and which way to face. Built in code — see Setup().
public partial class ShadowSentinel : Node2D
{
	private static readonly List<ShadowSentinel> Active = new();

	private AnimatedSprite2D _sprite;
	private Color _tint;
	private float _alpha;
	private float _timeLeft;
	private float _guardRadius;
	private float _counterRange;
	private Action<Vector2, bool> _spawnCounter;
	private bool _spent;

	public void Setup(SpriteFrames frames, string guardAnimation, bool facingRight, Vector2 spritePosition, Vector2 spriteDrawOffset,
		Vector2 spriteScale, Color tint, float intensity, float alpha, float duration, float guardRadius, float counterRange,
		Action<Vector2, bool> spawnCounter)
	{
		_tint = tint;
		_alpha = alpha;
		_timeLeft = duration;
		_guardRadius = guardRadius;
		_counterRange = counterRange;
		_spawnCounter = spawnCounter;

		var visual = new Node2D { Scale = new Vector2(facingRight ? 1 : -1, 1) };
		AddChild(visual);
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Position = spritePosition,
			Offset = spriteDrawOffset,
			Scale = spriteScale,
			Material = NeonSilhouette.CreateMaterial(tint, intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		visual.AddChild(_sprite);
		if (frames.HasAnimation(guardAnimation))
			_sprite.Play(guardAnimation);
	}

	// The active sentinel guarding a player at `playerPosition`, consumed on the spot: it parries,
	// counters and vanishes. False = nobody's covering them.
	public static bool TryGuard(Vector2 playerPosition)
	{
		foreach (ShadowSentinel sentinel in Active)
		{
			if (!IsInstanceValid(sentinel) || sentinel._spent || sentinel.GlobalPosition.DistanceTo(playerPosition) > sentinel._guardRadius)
				continue;
			sentinel.Parry();
			return true;
		}
		return false;
	}

	public override void _Ready()
	{
		Active.Add(this);
		CreateTween().TweenProperty(_sprite, "modulate:a", _alpha, 0.1f);
	}

	public override void _ExitTree() => Active.Remove(this);

	public override void _PhysicsProcess(double delta)
	{
		if (_spent)
			return;
		_timeLeft -= (float)delta;
		if (_timeLeft <= 0f)
			Vanish(0.3f);
	}

	private void Parry()
	{
		_spent = true;
		Active.Remove(this);
		Flash();
		// The counter lunges out to the attacker: the caster places the swing one reach short of it.
		if (ClosestEnemy() is { } enemy)
			_spawnCounter?.Invoke(new Vector2(enemy.GlobalPosition.X, GlobalPosition.Y), enemy.GlobalPosition.X >= GlobalPosition.X);
		Vanish(0.08f);
	}

	private Node2D ClosestEnemy()
	{
		Node2D closest = null;
		float closestDistance = _counterRange;
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy || enemy.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 })
				continue;
			float distance = enemy.GlobalPosition.DistanceTo(GlobalPosition);
			if (distance < closestDistance)
			{
				closest = enemy;
				closestDistance = distance;
			}
		}
		return closest;
	}

	// A quick neon flash on the sentinel itself, so it reads as the one who took the hit.
	private void Flash()
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = _tint,
			Energy = 2.2f,
			TextureScale = 0.8f,
		};
		scene.AddChild(light);
		light.GlobalPosition = GlobalPosition;
		Tween fade = light.CreateTween();
		fade.TweenProperty(light, "energy", 0f, 0.3f);
		fade.TweenCallback(Callable.From(light.QueueFree));
	}

	private void Vanish(float time)
	{
		_spent = true;
		SetPhysicsProcess(false);
		Tween vanish = CreateTween();
		vanish.TweenProperty(_sprite, "modulate:a", 0f, time);
		vanish.Parallel().TweenProperty(_sprite, "scale:y", _sprite.Scale.Y * 1.15f, time);
		vanish.TweenCallback(Callable.From(QueueFree));
	}
}
