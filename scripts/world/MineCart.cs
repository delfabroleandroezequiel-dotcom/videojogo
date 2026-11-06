using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Vagoneta: a rideable mine cart that runs along a visible rail (RailPoints, relative to where the
// node is placed — the first point is normally (0,0)). Step on it and, after a short BoardDelay
// (lamp blinks, rattle), it rolls to the OTHER end of the rail and stops there; step on it again
// at that end and it rolls back. With ReturnWhenEmpty a cart that loses its rider mid-ride rolls back
// home (so a retry from the hoguera finds it waiting); a cart that ARRIVES with its rider stays
// there. CallToStart/CallToEnd (levers at each end) summon it when it's parked at the other one.
//
// Speed is physical, not scripted: gravity along the rail's slope speeds it up downhill and slows
// it uphill (clamped to MinSpeed..MaxSpeed, so it never stalls on a rise). That's what gives a ride
// its rhythm: fast descents, slow climbs where the fights happen (bats can keep up there).
//
// Riding rules (Player.cs is not touched):
//  - Standing on the deck: the AnimatableBody2D carries the player like any moving platform.
//  - In the air above the deck (jumping a low beam, dodging a bat): Player.cs zeroes horizontal
//    speed without input, so the cart itself carries an attached rider by its own frame delta
//    (MoveAndCollide, collision-safe). Jumping straight up lands back on the cart, as expected.
//  - Same for enemies that land on it (EmboscadaVagoneta drops bandits on it): MoveAndSlide on the
//    deck carries them; knock them off over a chasm and they're gone.
// The collision body only ever translates (never rotates — see Puente.cs); the visual tilts.
[Tool]
public partial class MineCart : AnimatableBody2D
{
	[Signal] public delegate void DepartedEventHandler(int direction);
	[Signal] public delegate void ArrivedEventHandler(int direction);

	private Vector2[] _railPoints = { Vector2.Zero, new(400f, 0f) };

	// Rail polyline relative to the node's placed position. Downhill segments speed the cart up.
	[Export]
	public Vector2[] RailPoints
	{
		get => _railPoints;
		set { _railPoints = value ?? System.Array.Empty<Vector2>(); QueueRedraw(); }
	}

	// Deck width (the player stands on its top edge, DeckHeight above the rail).
	[Export] public float Width = 112f;
	[Export] public float DeckHeight = 30f;

	[Export] public float BoardDelay = 0.6f;
	[Export] public float StartSpeed = 90f;
	[Export] public float MinSpeed = 70f;
	[Export] public float MaxSpeed = 330f;
	// Fraction of gravity that acts along the slope (1 = frictionless incline).
	[Export] public float SlopeGravity = 520f;
	// Constant drag that bleeds speed on flats, px/s².
	[Export] public float Friction = 25f;
	[Export] public bool ReturnWhenEmpty = true;
	[Export] public float ReturnDelay = 2.5f;

	// Where the cart starts: 0 = rail start, 1 = rail end.
	[Export(PropertyHint.Range, "0,1,1")] public int StartAtEnd;

	public float Speed { get; private set; }
	public int Direction { get; private set; } = 1;
	public bool IsRolling => _state == State.Rolling;
	public Vector2 RailVelocity { get; private set; }
	public float Distance => _distance;
	public float RailLength => _railLength;

	private enum State { Parked, Boarding, Rolling }

	private State _state = State.Parked;
	private float _distance;
	private float _railLength;
	private float _timer;
	private float _emptyTimer;
	private Vector2 _origin;
	private Node2D _visual;
	private Node2D _tilt;
	private readonly List<Node2D> _wheels = new();
	private Sprite2D _lampGlow;
	private PointLight2D _lampLight;
	private CpuParticles2D _sparks;
	private Area2D _deckZone;
	private CharacterBody2D _rider;
	private bool _riderAttached;
	private bool _waitForRiderToLeave;
	private float _lostTimer;
	private bool _arrivedWithRider;
	private float _shake;

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	private static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public override void _Ready()
	{
		SyncToPhysics = true;
		BuildBody();
		BuildVisual();
		if (Engine.IsEditorHint())
			return;

		_origin = Position;
		_railLength = MineRail.Length(_railPoints);
		_distance = StartAtEnd == 1 ? _railLength : 0f;
		Direction = StartAtEnd == 1 ? -1 : 1;
		Position = _origin + MineRail.Sample(_railPoints, _distance, out _);

		// The rails stay in the level, not on the cart: built next to it in the parent's space.
		Node parent = GetParent();
		Callable.From(() =>
		{
			Node2D rails = MineRail.BuildVisual(parent, _railPoints, Name + "_Rieles");
			rails.Position = _origin;
		}).CallDeferred();
		BuildDeckZone();
	}

	// ── Physics ────────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		Vector2 before = Position;

		UpdateRider();

		switch (_state)
		{
			case State.Parked:
				RailVelocity = Vector2.Zero;
				bool onDeck = RiderStandingOnDeck();
				// Just arrived with a rider: they have to step off onto the ground before it'll leave again,
				// otherwise it would bounce straight back the moment it stops.
				if (_waitForRiderToLeave)
				{
					if (_rider == null || (!onDeck && _rider.IsOnFloor()))
						_waitForRiderToLeave = false;
					_emptyTimer = 0f;
				}
				else if (onDeck)
				{
					_state = State.Boarding;
					_timer = BoardDelay;
				}
				else if (ReturnWhenEmpty && !AtHome() && !_arrivedWithRider)
				{
					_emptyTimer += dt;
					if (_emptyTimer >= ReturnDelay)
						Depart();
				}
				break;

			case State.Boarding:
				_timer -= dt;
				_shake = 1.2f;
				if (!RiderStandingOnDeck() && _rider == null)
					_state = State.Parked;
				else if (_timer <= 0f)
					Depart();
				break;

			case State.Rolling:
				// Rider knocked off mid-ride (or jumped off): after a beat the cart gives up and rolls
				// back home, so a retry doesn't mean waiting for it at the far end.
				if (ReturnWhenEmpty && _rider == null && Direction == HomeDirection * -1)
				{
					_lostTimer += dt;
					if (_lostTimer > 1f)
					{
						Direction = HomeDirection;
						_lostTimer = 0f;
					}
				}
				else
					_lostTimer = 0f;
				Roll(dt);
				break;
		}

		Vector2 moved = Position - before;
		// Airborne rider above the deck: carry them along with the cart's own frame.
		if (_riderAttached && _rider != null && IsInstanceValid(_rider) && !_rider.IsOnFloor() && moved != Vector2.Zero)
			_rider.MoveAndCollide(moved);

		UpdateVisual(dt, moved);
	}

	private void Depart()
	{
		_emptyTimer = 0f;
		// At an end, always head for the other one.
		if (_distance >= _railLength - 0.5f)
			Direction = -1;
		else if (_distance <= 0.5f)
			Direction = 1;
		Speed = StartSpeed;
		_state = State.Rolling;
		Sfx.PlayAt(this, "World/Doors", "Portcullis Gate");
		EmitSignal(SignalName.Departed, Direction);
	}

	private void Roll(float dt)
	{
		MineRail.Sample(_railPoints, _distance, out Vector2 tangent);
		Vector2 travel = tangent * Direction;
		Speed += SlopeGravity * travel.Y * dt - Friction * dt;
		Speed = Mathf.Clamp(Speed, MinSpeed, MaxSpeed);

		_distance += Speed * Direction * dt;
		bool arrived = false;
		if (_distance >= _railLength) { _distance = _railLength; arrived = true; }
		if (_distance <= 0f) { _distance = 0f; arrived = true; }

		Vector2 target = _origin + MineRail.Sample(_railPoints, _distance, out _);
		RailVelocity = (target - Position) / Mathf.Max(dt, 0.0001f);
		Position = target;

		if (arrived)
		{
			_state = State.Parked;
			_emptyTimer = 0f;
			_arrivedWithRider = _riderAttached;
			_waitForRiderToLeave = _riderAttached;
			RailVelocity = Vector2.Zero;
			_shake = 4f;
			Sfx.PlayAt(this, "World/Mining", "mine", 900f);
			if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p && _riderAttached)
				p.ShakeCamera(4f, 0.2f);
			EmitSignal(SignalName.Arrived, Direction);
		}
	}

	// Call levers (Lever.Activated → here): summon the parked, empty cart to that end of the rail.
	public void CallToStart() => CallTo(-1);
	public void CallToEnd() => CallTo(1);

	private void CallTo(int end)
	{
		if (_state != State.Parked || RiderStandingOnDeck())
			return;
		bool alreadyThere = end > 0 ? _distance >= _railLength - 0.5f : _distance <= 0.5f;
		if (!alreadyThere)
		{
			_waitForRiderToLeave = false;
			Depart();
		}
	}

	private int HomeDirection => StartAtEnd == 1 ? 1 : -1;

	private bool AtHome() => StartAtEnd == 1 ? _distance >= _railLength - 0.5f : _distance <= 0.5f;

	// ── Rider tracking ─────────────────────────────────────────────────────────────────────────

	private void BuildDeckZone()
	{
		_deckZone = new Area2D { Name = "ZonaCubierta", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_deckZone.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Width + 60f, 220f) },
			Position = new Vector2(0f, -DeckHeight - 110f),
		});
		AddChild(_deckZone);
		_deckZone.BodyEntered += body => { if (body.IsInGroup("player") && body is CharacterBody2D c) _rider = c; };
		_deckZone.BodyExited += body =>
		{
			if (body == _rider)
			{
				_rider = null;
				_riderAttached = false;
			}
		};
	}

	// Feet (player origin + 32) on the deck top, within the deck's width.
	private bool RiderStandingOnDeck()
	{
		if (_rider == null || !IsInstanceValid(_rider) || !_rider.IsOnFloor())
			return false;
		float deckTop = GlobalPosition.Y - DeckHeight;
		float feet = _rider.GlobalPosition.Y + 32f;
		return Mathf.Abs(feet - deckTop) < 6f && Mathf.Abs(_rider.GlobalPosition.X - GlobalPosition.X) < Width / 2f + 12f;
	}

	private void UpdateRider()
	{
		if (_rider == null || !IsInstanceValid(_rider))
		{
			_riderAttached = false;
			return;
		}
		if (RiderStandingOnDeck())
			_riderAttached = true;
		else if (_rider.IsOnFloor())
			_riderAttached = false; // landed somewhere else
	}

	// ── Body + visual ──────────────────────────────────────────────────────────────────────────

	private void BuildBody()
	{
		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape == null)
		{
			shape = new CollisionShape2D { Name = "CollisionShape2D" };
			AddChild(shape);
		}
		// Fresh shape every time — never mutate a shared resource (see LevelTransition).
		shape.Shape = new RectangleShape2D { Size = new Vector2(Width, 10f) };
		shape.Position = new Vector2(0f, -DeckHeight + 5f);
		shape.OneWayCollision = true;
	}

	private void BuildVisual()
	{
		_visual?.QueueFree();
		_wheels.Clear();
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);
		_tilt = new Node2D { Name = "Inclinacion" };
		_visual.AddChild(_tilt);

		var iron = new Color(0.27f, 0.25f, 0.27f);
		var rust = new Color(0.45f, 0.24f, 0.13f);
		float w = Width / 2f;
		float top = -DeckHeight;
		// Tub: wider at the top, like a real mine car.
		_tilt.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(-w, top), new Vector2(w, top), new Vector2(w - 8f, -8f), new Vector2(-w + 8f, -8f) },
			Color = iron,
		});
		_tilt.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(-w + 4f, top + 5f), new Vector2(w - 4f, top + 5f), new Vector2(w - 9f, -12f), new Vector2(-w + 9f, -12f) },
			Color = rust,
		});
		_tilt.AddChild(new Line2D { Points = new[] { new Vector2(-w - 2f, top), new Vector2(w + 2f, top) }, Width = 3f, DefaultColor = iron.Lightened(0.35f) });
		foreach (float x in new[] { -w * 0.4f, 0f, w * 0.4f })
			_tilt.AddChild(new Line2D { Points = new[] { new Vector2(x, top + 3f), new Vector2(x * 0.9f, -9f) }, Width = 2f, DefaultColor = iron.Darkened(0.3f) });

		foreach (float x in new[] { -w + 16f, w - 16f })
		{
			var wheel = new Node2D { Position = new Vector2(x, -6f) };
			wheel.AddChild(new Polygon2D { Polygon = Circle(6f, 10), Color = new Color(0.15f, 0.15f, 0.17f) });
			wheel.AddChild(new Line2D { Points = new[] { new Vector2(-5f, 0f), new Vector2(5f, 0f) }, Width = 1.5f, DefaultColor = new Color(0.5f, 0.5f, 0.55f) });
			_tilt.AddChild(wheel);
			_wheels.Add(wheel);
		}

		// Lamp on a post: the "it's about to move" tell (blinks while boarding) and a light for dark mines.
		var lampPos = new Vector2(w - 6f, top - 14f);
		_tilt.AddChild(new Line2D { Points = new[] { new Vector2(w - 6f, top), lampPos }, Width = 2f, DefaultColor = iron });
		_lampGlow = new Sprite2D { Texture = Dot, Material = Additive, Position = lampPos, Scale = Vector2.One * 0.14f, Modulate = new Color(1f, 0.7f, 0.3f) };
		_tilt.AddChild(_lampGlow);
		if (!Engine.IsEditorHint())
		{
			_lampLight = new PointLight2D { Texture = Dot, Position = lampPos, TextureScale = 2.2f, Energy = 0.8f, Color = new Color(1f, 0.7f, 0.35f) };
			_tilt.AddChild(_lampLight);
			_sparks = new CpuParticles2D
			{
				Texture = Dot,
				Material = Additive,
				Emitting = false,
				Amount = 24,
				Lifetime = 0.35f,
				Position = new Vector2(0f, -2f),
				EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
				EmissionRectExtents = new Vector2(w - 12f, 1f),
				Direction = new Vector2(-1f, -0.6f),
				Spread = 30f,
				InitialVelocityMin = 60f,
				InitialVelocityMax = 140f,
				Gravity = new Vector2(0f, 500f),
				ScaleAmountMin = 0.02f,
				ScaleAmountMax = 0.05f,
				Color = new Color(1f, 0.75f, 0.35f),
			};
			_visual.AddChild(_sparks);
		}
	}

	private void UpdateVisual(float dt, Vector2 moved)
	{
		if (_tilt == null)
			return;
		MineRail.Sample(_railPoints, _distance, out Vector2 tangent);
		_tilt.Rotation = Mathf.LerpAngle(_tilt.Rotation, tangent.Angle(), Mathf.Min(1f, dt * 10f));
		foreach (Node2D wheel in _wheels)
			wheel.Rotation += moved.Length() * Direction / 6f;

		_shake = Mathf.MoveToward(_shake, 0f, dt * 10f);
		_visual.Position = _shake > 0f ? new Vector2((float)GD.RandRange(-_shake, _shake), (float)GD.RandRange(-_shake, _shake) * 0.5f) : Vector2.Zero;

		bool blink = _state == State.Boarding && Mathf.PosMod(_timer, 0.2f) < 0.1f;
		_lampGlow.Modulate = blink ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.7f, 0.3f);
		if (_sparks != null)
			_sparks.Emitting = _state == State.Rolling && Speed > MaxSpeed * 0.65f;
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint() || _railPoints.Length < 2)
			return;
		// Editor gizmo: the rail it will follow.
		DrawPolyline(_railPoints, new Color(0.6f, 0.9f, 1f, 0.8f), 2f);
	}

	private static Vector2[] Circle(float r, int steps)
	{
		var pts = new Vector2[steps];
		for (int i = 0; i < steps; i++)
			pts[i] = new Vector2(r, 0f).Rotated(Mathf.Tau * i / steps);
		return pts;
	}
}
