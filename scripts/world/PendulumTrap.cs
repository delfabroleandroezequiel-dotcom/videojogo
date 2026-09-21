using Godot;

namespace Metroidvania.World;

// Spiked ball on a chain that swings like a pendulum — a timing trap: the player reads the swing
// and slips through the gap instead of just avoiding a fixed hitbox. Place this node at the
// anchor point (the top of the chain); the ball hangs ChainLength below it when at rest.
//
// The whole rig hangs off "Pivot", which rotates around the anchor, so chain, ball sprite and
// damage circle all swing together as one rigid piece. It's an Area2D-style hazard (not a body
// anyone stands on), so rotating it can't fling the player the way rotating a platform would.
// The angle follows a sine wave — slowest at the ends of the swing, fastest at the bottom — which
// is what a real pendulum does and gives the player a natural window to cross at the extremes.
// The chain is a tiled Line2D (pendulo_cadena.png), so ChainLength can be anything without
// stretching the art.
//
// [Tool] so chain length / ball size / swing arc live-update in the Inspector (the arc is drawn
// in the editor only); the swing itself only runs in-game. The _initialized guard keeps the
// property setters from touching child nodes while Godot is still restoring the scene's saved
// values (before _Ready).
[Tool]
public partial class PendulumTrap : Node2D
{
	private float _chainLength = 96f;
	private float _ballDiameter = 60f;
	private float _hitRadiusRatio = 0.85f;
	private float _maxAngleDegrees = 60f;
	private bool _drawMount = true;

	// Distance from the anchor to the centre of the ball.
	[Export]
	public float ChainLength
	{
		get => _chainLength;
		set { _chainLength = value; Layout(); QueueRedraw(); }
	}

	// Visual size of the spiked ball; chain thickness scales with it so the two always match.
	[Export]
	public float BallDiameter
	{
		get => _ballDiameter;
		set { _ballDiameter = value; Layout(); }
	}

	// Damage circle as a fraction of the visual radius (spikes stick out, so it stays a bit smaller).
	[Export(PropertyHint.Range, "0.3,1,0.01")]
	public float HitRadiusRatio
	{
		get => _hitRadiusRatio;
		set { _hitRadiusRatio = value; Layout(); }
	}

	// Swing amplitude either side of straight down.
	[Export(PropertyHint.Range, "5,120,1")]
	public float MaxAngleDegrees
	{
		get => _maxAngleDegrees;
		set { _maxAngleDegrees = value; QueueRedraw(); }
	}

	// Seconds for one full back-and-forth.
	[Export] public float SwingPeriod = 2.4f;

	// 0..1 shift within the swing cycle — offset several pendulums so they don't all move in sync.
	[Export(PropertyHint.Range, "0,1,0.01")] public float StartPhase = 0f;

	// Small anchor plate drawn where the chain is attached.
	[Export]
	public bool DrawMount
	{
		get => _drawMount;
		set { _drawMount = value; QueueRedraw(); }
	}

	[ExportGroup("Damage")]
	[Export] public bool InstantKill = false;
	[Export] public int Damage = 30;
	[Export] public float KnockbackForce = 400f;

	private Node2D _pivot;
	private Line2D _chain;
	private Node2D _ball;
	private Sprite2D _ballVisual;
	private CollisionShape2D _hitShape;
	private float _time;
	private bool _initialized;

	public override void _Ready()
	{
		_pivot = GetNode<Node2D>("Pivot");
		_chain = GetNode<Line2D>("Pivot/Chain");
		_ball = GetNode<Node2D>("Pivot/Ball");
		_ballVisual = GetNode<Sprite2D>("Pivot/Ball/Visual");
		_hitShape = GetNode<CollisionShape2D>("Pivot/Ball/HazardArea/CollisionShape2D");

		if (!Engine.IsEditorHint())
		{
			var hazard = GetNode<Hazard>("Pivot/Ball/HazardArea");
			hazard.InstantKill = InstantKill;
			hazard.Damage = Damage;
			hazard.KnockbackForce = KnockbackForce;
			hazard.HitWhileOverlapping = true;
			hazard.SetPhysicsProcess(true);

			_time = StartPhase * SwingPeriod;
			_pivot.Rotation = SwingAngle();
		}

		_initialized = true;
		Layout();
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		_time += (float)delta;
		_pivot.Rotation = SwingAngle();
	}

	// Sine swing: angle = amplitude * sin(2*pi * t / period).
	private float SwingAngle()
	{
		if (SwingPeriod <= 0.01f)
			return 0f;

		return Mathf.DegToRad(MaxAngleDegrees) * Mathf.Sin(Mathf.Tau * _time / SwingPeriod);
	}

	public override void _Draw()
	{
		if (DrawMount)
		{
			DrawCircle(Vector2.Zero, 6f, new Color(0.12f, 0.11f, 0.11f));
			DrawCircle(Vector2.Zero, 3f, new Color(0.35f, 0.32f, 0.3f));
		}

		// The swing range, editor only: a thin arc through the ball's centre at both extremes.
		if (Engine.IsEditorHint())
		{
			float max = Mathf.DegToRad(MaxAngleDegrees);
			DrawArc(Vector2.Zero, ChainLength, Mathf.Pi * 0.5f - max, Mathf.Pi * 0.5f + max, 32, new Color(1f, 0.85f, 0.3f, 0.5f), 1f);
		}
	}

	private void Layout()
	{
		if (!_initialized)
			return;

		float ballScale = BallDiameter / _ballVisual.Texture.GetWidth();
		_ballVisual.Scale = new Vector2(ballScale, ballScale);
		_ball.Position = new Vector2(0f, ChainLength);

		// Always a fresh shape: the .tscn's sub-resource is shared by every instance of this scene,
		// so resizing it in place would resize every pendulum in the game.
		_hitShape.Shape = new CircleShape2D { Radius = BallDiameter * 0.5f * HitRadiusRatio };

		// The chain runs to the ball's centre and is drawn behind it, so its end is hidden by the
		// ball body. Line width follows the ball scale so the chain keeps the art's proportions.
		_chain.Points = new[] { Vector2.Zero, new Vector2(0f, ChainLength) };
		_chain.Width = _chain.Texture.GetWidth() * ballScale;
	}
}
