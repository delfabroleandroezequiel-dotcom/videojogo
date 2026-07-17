using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

// Rooted in place (set MoveSpeed = 0 on the scene/profile) — its threat is a telegraphed spore
// burst centered on itself rather than a chase. The burst only becomes damaging partway through
// the release animation (once the cloud sprite has actually bloomed), matching what the player
// sees rather than punishing them the instant the animation starts.
public partial class MushroomEnemy : Enemy
{
	[Export] public float SporeRange = 90f;
	[Export] public float SporeCooldown = 2.2f;
	[Export] public float TelegraphDuration = 0.35f;
	[Export] public float BurstActiveDuration = 0.35f;
	[Export] public float ReleaseAnimDuration = 0.9f;
	[Export] public float HurtAnimDuration = 0.3f;
	[Export] public string SmokePoisonFramesPath = "res://resources/sprites/SmokePoisonSpriteFrames.tres";
	[Export] public float SmokePoisonScale = 0.7f;

	private Hitbox _hitbox;
	private bool _releasing;
	private bool _canRelease = true;
	private float _hurtTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_hitbox = GetNode<Hitbox>("BurstHitbox");
		Stats.HitTaken += (isProjectile) => _hurtTimer = HurtAnimDuration;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		if (_hurtTimer > 0f)
			_hurtTimer -= (float)delta;

		if (_releasing || !_canRelease)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null || GlobalPosition.DistanceTo(player.GlobalPosition) > SporeRange)
			return;

		if (player is Metroidvania.Player.Player p && p.IsDashing)
			return;

		if (EnemyCombatCoordinator.TryAcquireAttackSlot())
			Release();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _releasing ? "release" : (_hurtTimer > 0f ? "hurt" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	private async void Release()
	{
		_releasing = true;
		_canRelease = false;

		try
		{
			await ToSignal(GetTree().CreateTimer(TelegraphDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			_hitbox.Activate(Stats);
			VfxSpawner.SpawnAt(this, GlobalPosition, SmokePoisonFramesPath, "smoke_poison", scale: SmokePoisonScale);

			await ToSignal(GetTree().CreateTimer(BurstActiveDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, ReleaseAnimDuration - TelegraphDuration - BurstActiveDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_releasing = false;
		}
		finally
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
		}

		await ToSignal(GetTree().CreateTimer(SporeCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canRelease = true;
	}
}
