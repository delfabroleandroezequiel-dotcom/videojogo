using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// PixelLab variant of BandidoCorpulento with a warhammer. Slams the hammer into the floor and the
// impact sends a second, delayed shockwave rolling forward along the ground, so the danger zone
// grows after the hammer lands: jump the wave or stay out of its line. Can't be flinched while
// swinging (HyperArmorWhileAttacking in the scene).
public partial class CorpulentoMartillo : MeleeEnemy
{
	[Export] public float WindupDuration = 0.65f;
	// attack plays at 14fps (CorpulentoMartilloSpriteFrames.tres): hammer overhead on TelegraphFrame 3,
	// hitting the floor on frame 4.
	[Export] public float HitFrameDelay = 1f / 14f;
	[Export] public float ShockwaveDelay = 0.18f;
	[Export] public float ShockwaveReach = 90f;
	[Export] public float ShockwaveDuration = 0.15f;

	// Angles: 0° = front, -90° = up, +90° = down. Hammer arc, then a flat streak along the ground for
	// the wave out to its reach.
	[ExportGroup("Trails")]
	[Export] public Color TrailGlowColor = new(0.9f, 0.05f, 0.05f, 1f);
	[Export] public Color TrailCoreColor = new(1f, 0.6f, 0.5f, 1f);
	[Export] public Vector2 SlamCenter = new(4f, -4f);
	[Export] public float SlamRadius = 54f;
	[Export] public float SlamRadiusY = 48f;
	[Export] public float SlamStartAngle = -130f;
	[Export] public float SlamEndAngle = 80f;
	[Export] public Vector2 WaveCenter = new(-40f, 28f);
	[Export] public float WaveRadius = 160f;
	[Export] public float WaveRadiusY = 6f;
	[Export] public float WaveStartAngle = -4f;
	[Export] public float WaveEndAngle = 4f;

	private EnemySlashTrail _slamTrail;
	private EnemySlashTrail _waveTrail;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;
		_slamTrail = new EnemySlashTrail { CoreWidth = 4f, GlowWidth = 14f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 30 };
		_waveTrail = new EnemySlashTrail { CoreWidth = 3f, GlowWidth = 12f, GlowColor = TrailGlowColor, CoreColor = TrailCoreColor, SparkAmount = 24 };
		Visual.AddChild(_slamTrail);
		Visual.AddChild(_waveTrail);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null)
			return;
		string anim = Attacking ? "attack" : Mathf.Abs(velocity.X) > 5f ? "walk" : "idle";
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	protected override async Task Attack()
	{
		Attacking = true;
		CanAttack = false;
		HoldTelegraphFrame("attack");
		float windup = WindupDuration * WindupScale;
		WindupScale = 1f;
		await ToSignal(GetTree().CreateTimer(windup), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Sprite.Play("attack");
		_slamTrail.Play(SlamCenter, SlamRadius, SlamStartAngle, SlamEndAngle, 0.13f, SlamRadiusY);
		await ToSignal(GetTree().CreateTimer(HitFrameDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;
		Shockwave();
		await base.Attack();
	}

	// The wave reuses the AttackHitbox a second time, pushed out along the floor.
	private async void Shockwave()
	{
		await ToSignal(GetTree().CreateTimer(AttackDuration + ShockwaveDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval || IsStunned || AttackWasInterrupted)
			return;
		_waveTrail.Play(WaveCenter, WaveRadius, WaveStartAngle, WaveEndAngle, ShockwaveDuration, WaveRadiusY);
		AttackHitbox.Position = new Vector2(FacingRight ? ShockwaveReach : -ShockwaveReach, 20f);
		AttackHitbox.Activate(Stats);
		await ToSignal(GetTree().CreateTimer(ShockwaveDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			AttackHitbox.Deactivate();
	}
}
