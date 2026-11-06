using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Guerrero del mangual de Radem — spiked flail on a chain. He owns the space around him.
// - Molinete: he whirls the ball over his head (attack 7-10 on loop) while walking at you. The
//   whirl hurts above and around his head: jumping onto him or dropping on him is punished, and
//   it keeps you from standing at his shoulder. A red neon ring traces the ball's path.
// - Slam: out of the whirl, the ball comes down forward onto the floor (attack 11) with a rock
//   burst. The longer he whirls, the closer he gets — the slam is the moment to dodge.
// - Doesn't flinch while the ball is moving (HyperArmorWhileAttacking).
public partial class GuerreroRademMangual : GuerreroReino
{
	[Export] public float WhirlMinTime = 0.7f;
	[Export] public float WhirlMaxTime = 1.6f;
	[Export] public float WhirlWalkSpeed = 70f;
	[Export] public float SlamRange = 70f;
	[Export] public float SlamTell = 0.3f;

	// attack: 5-6 ball swung up, 7-10 whirling overhead, 11-12 slam forward.
	[Export] public int[] WhirlFrames = { 7, 8, 9, 10 };
	[Export] public int SlamFrame = 11;

	private bool _whirling;

	protected override async Task<bool> Kit()
	{
		WindupScale = 1f;
		if (!await Strike("attack", 5, 6, 99, 0f, Vector2.Zero, fps: 12f))
			return false;
		MaybeShout();
		_whirling = true;
		float t = 0f;
		int i = 0;
		try
		{
			while (t < WhirlMaxTime && (t < WhirlMinTime || TargetDistanceX > SlamRange))
			{
				Pose("attack", WhirlFrames[i % WhirlFrames.Length]);
				// The ball circles above and behind his head.
				HitOn(0f, new Vector2(84f, 34f), -50f, 0.6f);
				if (i % 4 == 0)
					Trail.Play(new Vector2(0f, -40f), 34f, -180f, 180f, 0.28f, 14f);
				if (!await Wait(1f / 14f))
					return false;
				t += 1f / 14f;
				i++;
			}
		}
		finally
		{
			_whirling = false;
			AttackHitbox.Deactivate();
		}
		FlashBody(SlamTell);
		if (!await Wait(SlamTell))
			return false;
		if (!await Strike("attack", SlamFrame, SlamFrame, SlamFrame, 40f, new Vector2(40f, 56f), y: 4f, damage: 1.4f, activeFrames: 1,
			trail: new TrailArc(new Vector2(0f, -20f), 56f, -120f, 60f, 0.1f, 46f)))
			return false;
		GroundSlamFx.Impact(this, GroundPoint(46f), 0.9f);
		Shake(4f, 0.16f);
		ExtraCooldown = 0.4f;
		return await Strike("attack", SlamFrame + 1, 12, 99, 0f, Vector2.Zero, fps: 8f);
	}

	// He keeps walking at you while the ball whirls.
	protected override float MoveX(Node2D player, float distanceX, float currentVelocityX, double delta)
	{
		if (_whirling)
		{
			float toward = Mathf.Sign(distanceX);
			return Mathf.Abs(distanceX) > SlamRange * 0.6f && CanStepTo(toward) ? toward * WhirlWalkSpeed : 0f;
		}
		return base.MoveX(player, distanceX, currentVelocityX, delta);
	}

	protected override bool CanTurnToFacePlayer => !Attacking || _whirling;
}
