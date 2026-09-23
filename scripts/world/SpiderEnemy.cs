using Godot;
using System.Threading.Tasks;

namespace Metroidvania.World;

// Gives the small spiders (SpiderEnemy/SpiderEnemyDark, both instance this scene) two things
// MeleeEnemy's generic Attack() doesn't fit well enough to reuse as-is, so this overrides it
// completely instead of composing with base.Attack() (compare OrcEnemy's windup, which DOES
// still call base.Attack() — that works there because it only delays the *start* of an otherwise
// unchanged sequence; here the hitbox timing itself is what's different):
//
// 1. The bite only actually connects once the animation reaches HitStartFrame — MeleeEnemy.Attack
//    activates the hitbox from frame 0, which reads as the spider hitting during its windup/lunge
//    pose instead of at the bite itself.
// 2. A poison spit fires the moment the "attack" clip reaches its own last frame. This uses
//    FrameChanged (checking Sprite.Frame against the clip's last index), not AnimationFinished:
//    the clip is 14 frames at 18fps (~0.78s) but AttackAnimDuration is 0.75s, so the old
//    base.Attack()-based version swapped back to "idle" about a frame early and AnimationFinished
//    never fired at all. Watching the frame index directly still catches it regardless of exactly
//    when that swap happens.
public partial class SpiderEnemy : MeleeEnemy
{
	[Export] public PackedScene PoisonSpitScene;
	[Export] public float PoisonSpitHorizontalSpeed = 200f;
	// Minimum horizontal distance used for the launch-velocity solve below — guards against the
	// player standing almost directly under/over the spider, where the real distance would blow
	// up the required vertical speed into an unreasonable spike.
	[Export] public float PoisonSpitMinDistance = 60f;
	// Which frame of "attack" the bite actually connects on — the sheet's lunge doesn't land until
	// well into the clip (frames 0-9 are windup), not frame 0 like MeleeEnemy assumes by default.
	[Export] public int HitStartFrame = 10;

	private bool _poisonSpitFiredThisAttack;
	private static readonly RandomNumberGenerator SpiderJitterRng = new();

	static SpiderEnemy()
	{
		SpiderJitterRng.Randomize();
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		_poisonSpitFiredThisAttack = false;

		Sprite.Position = new Vector2(Sprite.Position.X, AttackSpriteYOffset);
		Sprite.Play("attack");

		void OnFrameChanged()
		{
			if (_poisonSpitFiredThisAttack || Sprite.Animation != "attack")
				return;
			if (Sprite.Frame != Sprite.SpriteFrames.GetFrameCount("attack") - 1)
				return;

			_poisonSpitFiredThisAttack = true;
			FirePoisonSpit();
		}
		Sprite.FrameChanged += OnFrameChanged;

		try
		{
			float animSpeed = (float)Sprite.SpriteFrames.GetAnimationSpeed("attack");
			float windupDuration = animSpeed > 0f ? HitStartFrame / animSpeed : 0f;

			await ToSignal(GetTree().CreateTimer(windupDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			AttackHitbox.Position = new Vector2(FacingRight ? AttackHitboxReach : -AttackHitboxReach, 0);
			AttackHitbox.Activate(Stats);

			await ToSignal(GetTree().CreateTimer(AttackDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;
			AttackHitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, AttackAnimDuration - windupDuration - AttackDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			Attacking = false;
			Sprite.Position = new Vector2(Sprite.Position.X, 0f);
		}
		finally
		{
			if (IsInstanceValid(Sprite))
				Sprite.FrameChanged -= OnFrameChanged;
			EnemyCombatCoordinator.ReleaseAttackSlot();
			HoldingAttackSlot = false;
		}

		float jitter = 1f + SpiderJitterRng.RandfRange(-AttackCooldownJitter, AttackCooldownJitter);
		await ToSignal(GetTree().CreateTimer(AttackCooldown * jitter), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			CanAttack = true;
	}

	private void FirePoisonSpit()
	{
		if (PoisonSpitScene is null || !IsInstanceValid(this))
			return;

		Node2D playerNode = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (playerNode is null)
			return;

		Vector2 to = playerNode.GlobalPosition - GlobalPosition;
		float dx = Mathf.Sign(to.X == 0f ? (FacingRight ? 1f : -1f) : to.X) * Mathf.Max(Mathf.Abs(to.X), PoisonSpitMinDistance);
		float vx = Mathf.Sign(dx) * PoisonSpitHorizontalSpeed;
		float time = Mathf.Abs(dx) / PoisonSpitHorizontalSpeed;

		var spit = PoisonSpitScene.Instantiate<PoisonSpit>();
		GetTree().CurrentScene.AddChild(spit);
		spit.GlobalPosition = GlobalPosition;

		// Solves for the vertical launch speed that actually lands on the player's current height
		// given ArcGravity and the flight time implied by the horizontal speed above, instead of a
		// fixed arc that only happens to look right at one specific range.
		float vy = (to.Y - 0.5f * spit.ArcGravity * time * time) / time;
		spit.Launch(new Vector2(vx, vy), Stats);
	}
}
