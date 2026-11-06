using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Jabalinero de Radem — light skirmisher with a bundle of javelins.
// - At range he hurls a javelin on a high arc aimed where you'll be (leads your run). The arc is
//   readable: it climbs, so stepping under it or past it works; standing still doesn't.
// - Runs to keep that range (Radem skirmisher: he runs, not walks).
// - Caught close, he doesn't flee: he stabs twice with the javelin in hand, fast and short.
// - A thrown javelin sticks in the ground where it misses for a moment.
public partial class GuerreroRademJabalinero : GuerreroReino
{
	[Export] public float PreferredDistance = 220f;
	[Export] public float TooCloseDistance = 70f;
	[Export] public float ThrowWindup = 0.5f;
	[Export] public float ThrowFlightTime = 0.75f;
	[Export] public float JavelinGravity = 520f;
	[Export] public float JavelinDamage = 1.3f;
	[Export] public float StabWindup = 0.25f;
	[Export] public Vector2 HandOffset = new(14f, -22f);

	// attack: 1-3 javelin drawn back, 4 release. attack2: 2 cocked, 3 and 6 stabs.
	[Export] public int ThrowWindupFrame = 3;
	[Export] public int ReleaseFrame = 4;
	[Export] public int StabWindupFrame = 2;

	protected override async Task<bool> Kit() =>
		TargetDistanceX < TooCloseDistance + 10f ? await Stabs() : await Throw();

	private async Task<bool> Throw()
	{
		if (!await Strike("attack", 1, ThrowWindupFrame - 1, 99, 0f, Vector2.Zero, fps: 10f))
			return false;
		if (!await Telegraph("attack", ThrowWindupFrame, ThrowWindup * WindupScale))
			return false;
		WindupScale = 1f;
		TurnToTarget();
		Pose("attack", ReleaseFrame);
		Launch();
		MaybeShout();
		ExtraCooldown = 0.4f;
		return await Strike("attack", ReleaseFrame + 1, 8, 99, 0f, Vector2.Zero, fps: 12f);
	}

	private void Launch()
	{
		if (Target is null)
			return;
		Vector2 origin = GlobalPosition + new Vector2(HandOffset.X * Forward, HandOffset.Y);
		// Lead the run, then solve the arc for a fixed flight time.
		Vector2 aimAt = Target.GlobalPosition + new Vector2(Target.Velocity.X * ThrowFlightTime * 0.7f, -6f);
		Vector2 d = aimAt - origin;
		float t = ThrowFlightTime;
		var velocity = new Vector2(d.X / t, (d.Y - 0.5f * JavelinGravity * t * t) / t);
		var javelin = new ProyectilArqueado
		{
			ArcGravity = JavelinGravity,
			DamageMultiplier = JavelinDamage,
			ShaftLength = 30f,
			TrailColor = TrailGlowColor,
		};
		GetTree().CurrentScene.AddChild(javelin);
		javelin.GlobalPosition = origin;
		javelin.Launch(velocity, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	private async Task<bool> Stabs()
	{
		if (!await Telegraph("attack2", StabWindupFrame, StabWindup))
			return false;
		var stab = new TrailArc(new Vector2(-10f, -6f), 52f, -4f, 4f, 0.06f, 6f);
		if (!await Strike("attack2", 3, 5, 3, 30f, new Vector2(40f, 14f), y: -6f, activeFrames: 1, damage: 0.7f, trail: stab, fps: 16f))
			return false;
		Lunge(140f, 0.08f);
		return await Strike("attack2", 6, 8, 6, 30f, new Vector2(40f, 14f), y: -6f, activeFrames: 1, damage: 0.7f, trail: stab, fps: 16f);
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta) =>
		SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance, 1.1f);
}
