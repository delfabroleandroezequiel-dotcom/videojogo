using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Ground-slam bandit: same swing as any other MeleeEnemy, but the impact also detonates a short
// accordion of hits marching away from him in the direction he's facing — three placeholder
// bursts standing in for real ground-crack VFX until that art exists.
public partial class BandidoCorpulento : MeleeEnemy
{
	// The Attack.png sheet plays at 14fps (see HeavyBandit3SpriteFrames.tres) — frame 9 is where
	// the weapon actually connects and frame 10 is where the ground-slam should visibly kick off,
	// so both delays below are expressed as frame counts / 14 rather than guessed seconds.
	[Export] public float WindupDuration = 9f / 14f;
	[Export] public float SlamEffectDelayAfterHit = 1f / 14f;

	[Export] public float SlamTileSpacing = 52f;
	[Export] public int SlamTileCount = 3;
	[Export] public float SlamStrikeGap = 0.35f;
	[Export] public float SlamActiveDuration = 0.25f;
	[Export] public int SlamDamage = 15;
	[Export] public Vector2 SlamHitboxSize = new(48f, 30f);

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		Sprite.Play("attack");

		await ToSignal(GetTree().CreateTimer(WindupDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		// Fire-and-forget: the hit lands the instant base.Attack() below activates the hitbox
		// (frame 9), and the ground-slam bursts start SlamEffectDelayAfterHit later (frame 10),
		// running alongside the rest of the swing instead of waiting for it to fully finish.
		RunGroundSlam();
		await base.Attack();
	}

	private async void RunGroundSlam()
	{
		float sign = FacingRight ? 1f : -1f;
		for (int i = 0; i < SlamTileCount; i++)
		{
			Vector2 position = GlobalPosition + new Vector2(sign * SlamTileSpacing * (i + 1), 0f);
			StrikeTile(position, SlamEffectDelayAfterHit + i * SlamStrikeGap);
		}
	}

	private async void StrikeTile(Vector2 position, float startDelay)
	{
		if (startDelay > 0f)
		{
			await ToSignal(GetTree().CreateTimer(startDelay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
		}

		SpawnPlaceholderBurst(position);

		Hazard hazard = Hazard.CreateArea((Node2D)GetTree().CurrentScene, instantKill: false, damage: SlamDamage, knockbackForce: 240f);
		hazard.GlobalPosition = position;
		hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = SlamHitboxSize } });

		await ToSignal(GetTree().CreateTimer(SlamActiveDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(hazard))
			hazard.QueueFree();
	}

	// Placeholder only — a plain expanding/fading dust-colored burst standing in until the ground
	// slam has real impact art to swap in.
	private void SpawnPlaceholderBurst(Vector2 position)
	{
		var burst = new Polygon2D
		{
			Polygon = BuildCirclePoints(18f, 12),
			Color = new Color(0.5f, 0.32f, 0.18f, 0.9f),
		};
		GetTree().CurrentScene.AddChild(burst);
		burst.GlobalPosition = position;

		Tween tween = burst.CreateTween();
		tween.TweenProperty(burst, "scale", Vector2.One * 2.4f, 0.3f);
		tween.Parallel().TweenProperty(burst, "modulate:a", 0f, 0.3f);
		tween.TweenCallback(Callable.From(burst.QueueFree));
	}

	private static Vector2[] BuildCirclePoints(float radius, int segments)
	{
		var points = new Vector2[segments];
		for (int i = 0; i < segments; i++)
		{
			float angle = Mathf.Tau * i / segments;
			points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}

		return points;
	}
}
