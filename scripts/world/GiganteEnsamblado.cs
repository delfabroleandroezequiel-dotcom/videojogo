using Godot;

namespace Metroidvania.World;

// Visual prototype of the Gigante boss assembled from separate painted parts (torso, head, two
// arms) with the Montaña Extraña eyes set on its faceless head. No AI or attacks yet — those get
// designed with the user step by step; this only proves the rig.
//
// Each arm is two pieces cut at the elbow ring (GiganteBrazoSuperior + the 4-frame
// GiganteAntebrazo strip, fist -> open hand). The forearm pivots on the ring's centre and is drawn
// under the upper arm, so the ring hides the cut while the elbow bends. The right arm is the left
// one mirrored (Hombro scale.x = -1), so the same local angles bend both arms toward the body.
//
// Both arm pieces end in the same round disc around the elbow pivot (the ring), so the joint
// works like a ball: the forearm can rotate or foreshorten inside it without showing a cut edge.
//
// Elbow modes:
//  - Ciclo: slow eased close (wind-up), a short hold, then a fast spring back open with a little
//    overshoot. The hand closes into a fist as the elbow closes. How it closes is Bend:
//      HaciaCamara: the forearm folds toward the viewer — fake foreshortening (shorter,
//        a bit bigger, slight inward turn) and drawn in front of the upper arm. Right for a giant
//        that faces the camera.
//      Plano (default): the forearm rotates in the screen plane by ElbowCloseDegrees.
//  - Alcanzar: two-bone IK — shoulder and elbow angles solved (law of cosines) so the hand
//    reaches toward the player, eased so it reads as heavy; the hand opens while reaching.
public partial class GiganteEnsamblado : Node2D
{
	public enum ElbowMode { Ciclo, Alcanzar }
	public enum BendStyle { HaciaCamara, Plano }

	// Frame order in bossOjo_frames.png (same sheet as MountainEyeBoss).
	private const int FrameCenter = 0;
	private const int FrameLeft = 1;
	private const int FrameUp = 2;
	private const int FrameRight = 3;
	private const int FrameDownLeft = 4;
	private const int FrameDown = 5;
	private const int FrameDownRight = 6;
	private const int FrameSquint = 7;
	private const int HandFrames = 4;

	[Export] public ElbowMode Mode = ElbowMode.Ciclo;

	[ExportGroup("Ciclo")]
	[Export] public BendStyle Bend = BendStyle.Plano;
	[Export] public float ElbowCloseDegrees = 60f;
	// HaciaCamara: how much the forearm shortens (0..1), grows (closer to the camera) and turns
	// inward when fully closed, and from which closedness it's drawn over the upper arm.
	[Export] public float ForeshortenAmount = 0.3f;
	[Export] public float ForeshortenGrow = 0.15f;
	[Export] public float InwardDegrees = 18f;
	[Export] public float FrontThreshold = 0.15f;
	[Export] public float CloseTime = 1.4f;
	[Export] public float HoldTime = 0.35f;
	[Export] public float OpenTime = 0.45f;
	[Export] public float RestTime = 0.8f;
	// Seconds the right arm lags the left one, so they don't move as a mirror image.
	[Export] public float RightArmDelay = 0.9f;

	[ExportGroup("Alcanzar")]
	// Arm bone lengths in the arm textures' pixels (shoulder ring -> elbow ring -> fist centre).
	[Export] public float UpperLength = 321f;
	[Export] public float ForearmLength = 292f;
	[Export] public float ReachSmoothing = 3f;
	[Export] public Vector2 ElbowLimitDegrees = new(-150f, 10f);

	[ExportGroup("Vida")]
	[Export] public float BreatheAmplitude = 0.012f;
	[Export] public float BreatheSpeed = 0.9f;
	[Export] public float ShoulderSwayDegrees = 2.5f;
	[Export] public Vector2 SquintIntervalRange = new(3f, 5.5f);
	[Export] public float SquintDuration = 0.6f;
	[Export] public float StareDistance = 70f;

	// Shake-off: after the player has been climbing it for ShakeTriggerTime, it squints and starts to
	// tremble (wind-up), then shakes hard for ShakeDuration. While shaking, holding still on it drains
	// StillDrainPerSecond extra stamina and every JoltInterval a jolt takes JoltStamina — climbing
	// (moving) skips the still drain, so moving is how you hang on. Out of stamina = thrown off with
	// the shake's motion. Amplitudes are in the Visual's own (unscaled) pixels, so they grow with it.
	[ExportGroup("Sacudon")]
	[Export] public float ShakeTriggerTime = 2.5f;
	[Export] public float ShakeWindup = 0.8f;
	[Export] public float ShakeDuration = 1.8f;
	[Export] public float ShakeCooldown = 5f;
	[Export] public float ShakeAmplitude = 16f;
	[Export] public float ShakeFrequency = 14f;
	[Export] public float StillDrainPerSecond = 70f;
	[Export] public float JoltInterval = 0.45f;
	[Export] public int JoltStamina = 22;
	[Export] public float CameraShakeStrength = 6f;

	// Hole attacks (AgujeroGigante children anywhere under Visual): every AttackInterval (random in
	// the range) one open, idle hole near the player fires, at most MaxConcurrentAttacks at once.
	// Which kinds are allowed depends on whether the player is climbing it or on the ground.
	[ExportGroup("Agujeros")]
	[Export] public Vector2 AttackInterval = new(2.2f, 3.8f);
	[Export] public int MaxConcurrentAttacks = 2;
	[Export] public float AttackRange = 1500f;
	private static readonly AgujeroGigante.Ataque[] AttacksWhileClimbing =
	{
		AgujeroGigante.Ataque.Succion, AgujeroGigante.Ataque.Viento, AgujeroGigante.Ataque.Gas,
		AgujeroGigante.Ataque.Larvas, AgujeroGigante.Ataque.Tentaculo,
	};
	private static readonly AgujeroGigante.Ataque[] AttacksOnGround =
	{
		AgujeroGigante.Ataque.Proyectiles, AgujeroGigante.Ataque.Succion, AgujeroGigante.Ataque.Gas,
		AgujeroGigante.Ataque.Tentaculo,
	};

	private enum ShakeState { Idle, Windup, Shaking, Cooldown }

	private sealed class Arm
	{
		public Node2D Shoulder;
		public Node2D Bone;
		public Node2D Elbow;
		public Sprite2D Forearm;
		public float RestUpper;
		public float RestForearm;
	}

	private Arm _left;
	private Arm _right;
	private Node2D _torso;
	private Node2D _head;
	private Sprite2D _leftEye;
	private Sprite2D _rightEye;
	private Vector2 _headBase;
	private float _time;
	private float _squintTimer;
	private float _squintLeft;
	private Node2D _player;
	private readonly RandomNumberGenerator _rng = new();
	private Node2D _visual;
	private Vector2 _visualBase;
	private ZonaTrepable[] _zones;
	private ShakeState _shake = ShakeState.Idle;
	private float _shakeTimer;
	private float _climbedTime;
	private float _joltTimer;
	private AgujeroGigante[] _holes;
	private float _attackTimer;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_visualBase = _visual.Position;
		var zones = new System.Collections.Generic.List<ZonaTrepable>();
		CollectZones(_visual, zones);
		_zones = zones.ToArray();
		var holes = new System.Collections.Generic.List<AgujeroGigante>();
		CollectHoles(_visual, holes);
		_holes = holes.ToArray();
		foreach (AgujeroGigante hole in _holes)
			hole.Zonas = _zones;
		_attackTimer = AttackInterval.Y;
		_torso = GetNode<Node2D>("Visual/Torso");
		_head = GetNode<Node2D>("Visual/Cabeza");
		_leftEye = _head.GetNode<Sprite2D>("OjoIzquierdo");
		_rightEye = _head.GetNode<Sprite2D>("OjoDerecho");
		_headBase = _head.Position;
		_left = BuildArm("Visual/HombroIzquierdo");
		_right = BuildArm("Visual/HombroDerecho");
		_rng.Randomize();
		_squintTimer = _rng.RandfRange(SquintIntervalRange.X, SquintIntervalRange.Y);
	}

	private Arm BuildArm(string path)
	{
		var arm = new Arm { Shoulder = GetNode<Node2D>(path) };
		arm.Bone = arm.Shoulder.GetNode<Node2D>("Brazo");
		arm.Elbow = arm.Bone.GetNode<Node2D>("Codo");
		arm.Forearm = arm.Elbow.GetNode<Sprite2D>("Antebrazo");
		// Rest directions of the two bones as authored (shoulder -> elbow, elbow -> fist), used to
		// turn IK directions into node rotations.
		arm.RestUpper = arm.Elbow.Position.Angle();
		arm.RestForearm = new Vector2(29f, 290f).Angle();
		return arm;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

		float breathe = Mathf.Sin(_time * BreatheSpeed * Mathf.Tau);
		_torso.Scale = new Vector2(1f, 1f + breathe * BreatheAmplitude);
		_head.Position = _headBase + new Vector2(0f, breathe * BreatheAmplitude * -400f);

		if (Mode == ElbowMode.Ciclo || _player is null)
		{
			PoseCycle(_left, _time, breathe);
			PoseCycle(_right, _time - RightArmDelay, -breathe);
		}
		else
		{
			PoseReach(_left, dt);
			PoseReach(_right, dt);
		}

		UpdateShake(dt);
		UpdateHoleAttacks(dt);
		UpdateEyes(dt);
	}

	private static void CollectHoles(Node node, System.Collections.Generic.List<AgujeroGigante> into)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is AgujeroGigante hole)
				into.Add(hole);
			CollectHoles(child, into);
		}
	}

	// ── Agujeros ────────────────────────────────────────────────────────────────────────────────
	private void UpdateHoleAttacks(float dt)
	{
		if (_player is null || _holes.Length == 0)
			return;
		// The shake is its own attack: no holes firing during its tell or the shake itself.
		if (_shake == ShakeState.Windup || _shake == ShakeState.Shaking)
			return;
		_attackTimer -= dt;
		if (_attackTimer > 0f)
			return;
		_attackTimer = _rng.RandfRange(AttackInterval.X, AttackInterval.Y);

		int busy = 0;
		foreach (AgujeroGigante hole in _holes)
			if (hole.Ocupado)
				busy++;
		if (busy >= MaxConcurrentAttacks)
			return;

		AgujeroGigante.Ataque[] allowed = PlayerClimbingMe() ? AttacksWhileClimbing : AttacksOnGround;
		var candidates = new System.Collections.Generic.List<AgujeroGigante>();
		foreach (AgujeroGigante hole in _holes)
		{
			if (hole.Cerrado || hole.Ocupado || System.Array.IndexOf(allowed, hole.Tipo) < 0)
				continue;
			if (hole.GlobalPosition.DistanceTo(_player.GlobalPosition) <= Mathf.Min(AttackRange, hole.Alcance))
				candidates.Add(hole);
		}
		if (candidates.Count > 0)
			candidates[_rng.RandiRange(0, candidates.Count - 1)].Atacar(_player);
	}

	public int OpenHoles()
	{
		int open = 0;
		foreach (AgujeroGigante hole in _holes)
			if (!hole.Cerrado)
				open++;
		return open;
	}

	private static void CollectZones(Node node, System.Collections.Generic.List<ZonaTrepable> into)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is ZonaTrepable zone)
				into.Add(zone);
			CollectZones(child, into);
		}
	}

	// ── Sacudón ─────────────────────────────────────────────────────────────────────────────────
	private bool PlayerClimbingMe() =>
		_player is Metroidvania.Player.Player p && p.GripZone is not null && _visual.IsAncestorOf(p.GripZone);

	private void UpdateShake(float dt)
	{
		bool climbing = PlayerClimbingMe();
		_shakeTimer -= dt;
		float amplitude = 0f;
		switch (_shake)
		{
			case ShakeState.Idle:
				_climbedTime = climbing ? _climbedTime + dt : 0f;
				if (_climbedTime >= ShakeTriggerTime)
				{
					_shake = ShakeState.Windup;
					_shakeTimer = ShakeWindup;
					// The squint is the tell.
					_squintLeft = ShakeWindup + ShakeDuration;
				}
				break;
			case ShakeState.Windup:
				amplitude = ShakeAmplitude * 0.3f * (1f - _shakeTimer / ShakeWindup);
				if (_shakeTimer <= 0f)
				{
					_shake = ShakeState.Shaking;
					_shakeTimer = ShakeDuration;
					_joltTimer = JoltInterval;
					SetStillDrain(StillDrainPerSecond);
					(_player as Metroidvania.Player.Player)?.ShakeCamera(CameraShakeStrength, ShakeDuration);
				}
				break;
			case ShakeState.Shaking:
				amplitude = ShakeAmplitude;
				_joltTimer -= dt;
				if (_joltTimer <= 0f)
				{
					_joltTimer += JoltInterval;
					if (climbing)
						((Metroidvania.Player.Player)_player).ApplyGripJolt(JoltStamina);
				}
				if (_shakeTimer <= 0f)
				{
					_shake = ShakeState.Cooldown;
					_shakeTimer = ShakeCooldown;
					SetStillDrain(0f);
				}
				break;
			case ShakeState.Cooldown:
				if (_shakeTimer <= 0f)
				{
					_shake = ShakeState.Idle;
					_climbedTime = 0f;
				}
				break;
		}

		// Two detuned sines per axis: reads as a violent tremor rather than a clean wobble.
		float t = _time * ShakeFrequency * Mathf.Tau;
		Vector2 jitter = new(Mathf.Sin(t) + 0.6f * Mathf.Sin(t * 1.7f + 1.3f),
			Mathf.Sin(t * 1.3f + 0.7f) + 0.6f * Mathf.Sin(t * 2.1f));
		_visual.Position = _visualBase + jitter * (amplitude / 1.6f * _visual.Scale.X);
	}

	private void SetStillDrain(float perSecond)
	{
		foreach (ZonaTrepable zone in _zones)
			zone.ExtraStaminaWhenStill = perSecond;
	}

	// ── Ciclo ───────────────────────────────────────────────────────────────────────────────────
	private void PoseCycle(Arm arm, float time, float sway)
	{
		float period = CloseTime + HoldTime + OpenTime + RestTime;
		float t = Mathf.PosMod(time, period);
		float closed;
		if (t < CloseTime)
			closed = EaseInOutCubic(t / CloseTime);
		else if (t < CloseTime + HoldTime)
			closed = 1f;
		else if (t < CloseTime + HoldTime + OpenTime)
			closed = 1f - EaseOutBack((t - CloseTime - HoldTime) / OpenTime);
		else
			closed = 0f;

		arm.Bone.Rotation = Mathf.DegToRad(ShoulderSwayDegrees * sway);
		if (Bend == BendStyle.HaciaCamara)
		{
			arm.Elbow.Rotation = -Mathf.DegToRad(InwardDegrees) * closed;
			arm.Elbow.Scale = new Vector2(1f + ForeshortenGrow * closed, 1f - ForeshortenAmount * closed);
			arm.Elbow.ZIndex = closed > FrontThreshold ? 1 : 0;
		}
		else
		{
			arm.Elbow.Rotation = -Mathf.DegToRad(ElbowCloseDegrees) * closed;
			arm.Elbow.Scale = Vector2.One;
			arm.Elbow.ZIndex = 0;
		}
		arm.Forearm.Frame = HandFrameFor(1f - Mathf.Clamp(closed, 0f, 1f));
	}

	private static float EaseInOutCubic(float x) =>
		x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

	// Overshoots past 1 and settles: the open snap "whips" a little past straight.
	private static float EaseOutBack(float x)
	{
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
	}

	// ── Alcanzar (2-bone IK) ────────────────────────────────────────────────────────────────────
	private void PoseReach(Arm arm, float dt)
	{
		// Shoulder-local target: the mirrored shoulder's ToLocal already flips X for the right arm.
		Vector2 target = arm.Shoulder.ToLocal(_player.GlobalPosition);
		float l1 = UpperLength;
		float l2 = ForearmLength;
		float d = Mathf.Clamp(target.Length(), Mathf.Abs(l1 - l2) + 1f, l1 + l2 - 1f);
		float toTarget = target.Angle();
		float inner = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));

		// Of the two solutions, keep the one with the elbow further out (more negative local X).
		float upperA = toTarget + inner;
		float upperB = toTarget - inner;
		float upperDir = Mathf.Cos(upperA) < Mathf.Cos(upperB) ? upperA : upperB;
		Vector2 elbowPos = Vector2.FromAngle(upperDir) * l1;
		Vector2 handPos = elbowPos + (target.Normalized() * d - elbowPos).Normalized() * l2;
		float forearmDir = (handPos - elbowPos).Angle();

		float boneRot = Mathf.AngleDifference(arm.RestUpper, upperDir);
		float elbowRot = Mathf.AngleDifference(arm.RestForearm, forearmDir) - boneRot;
		elbowRot = Mathf.Clamp(Mathf.Wrap(elbowRot, -Mathf.Pi, Mathf.Pi),
			Mathf.DegToRad(ElbowLimitDegrees.X), Mathf.DegToRad(ElbowLimitDegrees.Y));

		arm.Elbow.Scale = Vector2.One;
		arm.Elbow.ZIndex = 0;
		float k = 1f - Mathf.Exp(-ReachSmoothing * dt);
		arm.Bone.Rotation = Mathf.LerpAngle(arm.Bone.Rotation, boneRot, k);
		arm.Elbow.Rotation = Mathf.LerpAngle(arm.Elbow.Rotation, elbowRot, k);

		// Hand opens as the arm straightens toward the player.
		float straight = 1f - Mathf.Clamp(-arm.Elbow.Rotation / Mathf.DegToRad(ElbowCloseDegrees), 0f, 1f);
		arm.Forearm.Frame = HandFrameFor(straight);
	}

	// 0 = fist, 1 = open hand.
	private static int HandFrameFor(float open) =>
		Mathf.Clamp(Mathf.RoundToInt(open * (HandFrames - 1)), 0, HandFrames - 1);

	// ── Eyes (same gaze rules as MountainEyeBoss) ───────────────────────────────────────────────
	private void UpdateEyes(float dt)
	{
		_squintTimer -= dt;
		_squintLeft -= dt;
		if (_squintTimer <= 0f)
		{
			_squintLeft = SquintDuration;
			_squintTimer = _rng.RandfRange(SquintIntervalRange.X, SquintIntervalRange.Y);
		}
		bool squinting = _squintLeft > 0f;
		_leftEye.Frame = squinting ? FrameSquint : GazeFrame(_leftEye, false);
		_rightEye.Frame = squinting ? FrameSquint : GazeFrame(_rightEye, true);
	}

	private int GazeFrame(Node2D eye, bool mirrored)
	{
		if (_player is null)
			return FrameCenter;
		Vector2 toPlayer = _player.GlobalPosition - eye.GlobalPosition;
		if (toPlayer.Length() < StareDistance)
			return FrameCenter;
		if (mirrored)
			toPlayer.X = -toPlayer.X;
		int sector = Mathf.PosMod(Mathf.RoundToInt(Mathf.RadToDeg(toPlayer.Angle()) / 45f), 8);
		return sector switch
		{
			0 => FrameRight,
			1 => FrameDownRight,
			2 => FrameDown,
			3 => FrameDownLeft,
			4 => FrameLeft,
			5 => FrameLeft,
			6 => FrameUp,
			_ => FrameRight,
		};
	}
}
