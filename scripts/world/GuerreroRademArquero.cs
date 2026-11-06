using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Arquero de Radem — hooded desert archer with a composite bow. Radem's archer is no sniper: he
// harries, moves and sets the ground on fire.
// - Quick shot: a flat, fast arrow, short draw. His default while you're at mid range.
// - Fire arrow: when you stand still (or turtle behind a parry) he lofts an arrow on a high arc
//   that lights the floor where it lands (GroundFire) — it flushes you out of a spot.
// - Runs to keep his range; if you close the gap fast he leaps back, firing a quick shot as he
//   lands if he still faces you.
public partial class GuerreroRademArquero : GuerreroReino
{
	[Export] public PackedScene ArrowScene;
	[Export] public PackedScene GroundFireScene;
	[Export] public float ArrowSpeed = 600f;
	[Export] public float ArrowDamage = 1f;
	[Export] public float FireArrowFlightTime = 0.9f;
	[Export] public float FireArrowGravity = 600f;
	[Export] public float FireArrowDamage = 0.8f;
	[Export] public float QuickDraw = 0.38f;
	[Export] public float FireDraw = 0.7f;
	[Export] public float PreferredDistance = 230f;
	[Export] public float TooCloseDistance = 110f;
	[Export] public Vector2 BowOffset = new(22f, -14f);

	// attack: 1-3 drawing and nocking, 4 release, 5-8 follow-through.
	[Export] public int DrawFrame = 3;
	[Export] public int ReleaseFrame = 4;

	private float _stillTime;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval || Target is null)
			return;
		_stillTime = Mathf.Abs(Target.Velocity.X) < 20f ? _stillTime + (float)delta : 0f;
	}

	protected override async Task<bool> Kit()
	{
		bool fire = _stillTime > 0.6f || (Target is { } p && p.IsBlocking) || Roll() < 0.25f;
		if (!await Strike("attack", 1, DrawFrame - 1, 99, 0f, Vector2.Zero, fps: 12f))
			return false;
		if (!await Telegraph("attack", DrawFrame, (fire ? FireDraw : QuickDraw) * WindupScale))
			return false;
		WindupScale = 1f;
		TurnToTarget();
		Pose("attack", ReleaseFrame);
		if (fire)
			ShootFireArrow();
		else
			ShootQuick();
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
		ExtraCooldown = fire ? 0.5f : 0f;
		return await Strike("attack", ReleaseFrame + 1, 8, 99, 0f, Vector2.Zero, fps: 12f);
	}

	private Vector2 Origin => GlobalPosition + new Vector2(BowOffset.X * Forward, BowOffset.Y);

	private void ShootQuick()
	{
		if (ArrowScene is null || Target is null)
			return;
		Vector2 dir = PlayerReads.PredictAimDirection(Origin, Target, ArrowSpeed, 0.7f, 0.1f, new Vector2(0f, -10f));
		if (Mathf.Sign(dir.X) != Forward)
			dir = new Vector2(Forward, 0f);
		var arrow = ArrowScene.Instantiate<ElfArrow>();
		arrow.Speed = ArrowSpeed;
		arrow.DamageMultiplier = ArrowDamage;
		arrow.TrailColor = TrailGlowColor;
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = Origin;
		arrow.Launch(dir, Stats);
	}

	private void ShootFireArrow()
	{
		if (Target is null)
			return;
		Vector2 d = Target.GlobalPosition + new Vector2(0f, 20f) - Origin;
		float t = FireArrowFlightTime;
		var velocity = new Vector2(d.X / t, (d.Y - 0.5f * FireArrowGravity * t * t) / t);
		var arrow = new ProyectilArqueado
		{
			ArcGravity = FireArrowGravity,
			DamageMultiplier = FireArrowDamage,
			ShaftLength = 18f,
			TrailColor = new Color(1f, 0.45f, 0.08f, 1f),
			TipColor = new Color(1f, 0.6f, 0.2f, 1f),
			GroundFireScene = GroundFireScene,
			Element = DamageElement.Fire,
			StuckSeconds = 0.4f,
		};
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = Origin;
		arrow.Launch(velocity, Stats);
	}

	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (!Attacking && Mathf.Abs(distanceX) < TooCloseDistance * 0.6f && IsOnFloor() && Target is { } p
			&& PlayerReads.IsClosingIn(p, GlobalPosition.X))
		{
			float away = -Mathf.Sign(distanceX);
			if (TryHop(new Vector2(away * 260f, -300f)))
				return away * 260f;
		}
		return SpacingMoveX(distanceX, currentVelocityX, PreferredDistance, TooCloseDistance, 1.1f);
	}
}
