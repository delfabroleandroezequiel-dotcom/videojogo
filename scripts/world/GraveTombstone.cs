using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// GraveNecro's summoned tombstone, built in code from the pack's TombStone sheet
// (GraveTombstoneSpriteFrames "rise", 128x58 cells, ground line at y47). Frames 0-8 a green glow
// spreading on the floor (the warning), 9-11 the burst as the stone erupts (hurts), 12-22 the
// tombstone settling. Then, if the necro still has room, a GraveSwordsman claws out beside it,
// and the stone sinks away.
public partial class GraveTombstone : Node2D
{
	private const string FramesPath = "res://resources/sprites/GraveTombstoneSpriteFrames.tres";

	public float VisualScale = 1.6f;
	public float WarningTime = 0.75f;
	public float DamageMultiplier = 1.2f;
	public Vector2 BurstHitboxSize = new(56f, 70f);
	public float StandTime = 3f;
	public Stats CasterStats;
	// Null = no skeleton this time.
	public PackedScene MinionScene;
	public int MinionHealth = 20;
	public System.Action<Enemy> MinionSpawned;

	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Scale = new Vector2(VisualScale, VisualScale),
			// Cell centre is (64,29); put the y47 ground line on this node's origin.
			Position = new Vector2(-2f * VisualScale, -(47f - 29f) * VisualScale),
		};
		AddChild(_sprite);
		_ = Run();
	}

	private async Task Run()
	{
		_sprite.Play("rise");
		_sprite.Pause();
		for (int f = 0; f <= 8; f++)
		{
			_sprite.Frame = f;
			if (!await Wait(WarningTime / 9f))
				return;
		}
		GroundSlamFx.Chip(this, GlobalPosition, 0.6f);
		for (int f = 9; f <= 11; f++)
		{
			_sprite.Frame = f;
			Burst();
			if (!await Wait(0.06f))
				return;
		}
		for (int f = 12; f <= 22; f++)
		{
			_sprite.Frame = f;
			if (!await Wait(0.07f))
				return;
		}
		SpawnMinion();
		if (!await Wait(StandTime))
			return;
		Tween sink = CreateTween();
		sink.TweenProperty(_sprite, "modulate:a", 0f, 0.5f);
		sink.Parallel().TweenProperty(_sprite, "position:y", _sprite.Position.Y + 20f, 0.5f);
		sink.TweenCallback(Callable.From(QueueFree));
	}

	private bool _burstHit;

	private void Burst()
	{
		if (_burstHit)
			return;
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = BurstHitboxSize },
			Transform = new Transform2D(0f, GlobalPosition + new Vector2(0f, -BurstHitboxSize.Y * 0.5f)),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 8))
		{
			if (hit["collider"].AsGodotObject() is not Metroidvania.Player.Player player)
				continue;
			if (player.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			_burstHit = true;
			int before = stats.CurrentHealth;
			int attack = IsInstanceValid(CasterStats) ? CasterStats.AttackPower : 10;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(attack * DamageMultiplier)), isProjectile: true);
			if (stats.CurrentHealth < before)
			{
				float away = Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X);
				player.ApplyKnockback(new Vector2(away == 0f ? 1f : away * 0.4f, -1f).Normalized(), 280f);
				BloodEffect.SpawnAt(this, player.GlobalPosition);
			}
			return;
		}
	}

	private void SpawnMinion()
	{
		if (MinionScene is null || GetTree().CurrentScene is not Node scene
			|| MinionScene.Instantiate() is not GraveSwordsman skeleton)
			return;
		skeleton.Summoned = true;
		skeleton.GrantsEcos = false;
		skeleton.CustomPersistenceId = GraveSwordsman.SummonPersistenceId;
		scene.AddChild(skeleton);
		float halfHeight = skeleton.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is RectangleShape2D rect ? rect.Size.Y * 0.5f : 28f;
		skeleton.GlobalPosition = GlobalPosition + new Vector2(0f, -halfHeight - 1f);
		skeleton.Stats.MaxHealth = MinionHealth;
		skeleton.Stats.ResetToFull();
		MinionSpawned?.Invoke(skeleton);
	}

	private async Task<bool> Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return IsInstanceValid(this);
	}
}
