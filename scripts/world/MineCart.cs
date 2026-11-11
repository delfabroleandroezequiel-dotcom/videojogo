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

	// Saltos: indices of RailPoints segments (i = point i → i+1) with NO rail under them — lay those
	// points as a low arc over a pit and the cart hops it (slope physics: slower up, faster down).
	// The cart never brakes inside one.
	[Export] public int[] TramosSalto = System.Array.Empty<int>();

	// Cambio de vía: an alternative stretch that replaces RailPoints[DesvioDesde..DesvioHasta] (the
	// fork and rejoin points stay on the main rail). CambiarVia() (a Lever) throws the switch while
	// the cart is still before the fork. -1 = no switch.
	[Export] public Vector2[] RailDesvio = System.Array.Empty<Vector2>();
	[Export] public int DesvioDesde = -1;
	[Export] public int DesvioHasta = -1;

	// Long rides: when the rider is lost mid-ride the cart brakes (Frenada px/s²) and waits there for
	// them, instead of ReturnWhenEmpty's roll-back home. Use with ReturnWhenEmpty = false.
	[Export] public bool EsperarAlJinete;
	[Export] public float Frenada = 450f;

	// Salto con el jinete (DKC): jumping off the deck makes the cart hop too, lower than the player's
	// own jump (so the rider always lands back in the tub). With it, TramosSalto stop being drawn arcs:
	// they are real pits with no rail, and a cart that is not airborne over one falls in.
	[Export] public bool SaltaConJinete;
	[Export] public float SaltoVelocidad = 360f;
	[Export] public float SaltoGravedad = 1100f;
	// Coyote time over a pit edge (s): the cart rolls on thin air this long before it drops.
	[Export] public float SaltoTolerancia = 0.12f;

	// The rail just ends (broken bridge, collapsed track): the cart rolls off the far end and falls,
	// crashing a moment later.
	[Export] public bool CaeAlFinal;

	// Cambio de vagoneta: an empty cart on a parallel rail that sets off when the leader passes it and
	// keeps level with it (same screen X), so the rider can jump across. Once someone boards it, it
	// runs on its own physics.
	[Export] public NodePath Acompanar;

	// Deck width (the player stands on its top edge, DeckHeight above the rail).
	[Export] public float Width = 112f;
	[Export] public float DeckHeight = 30f;

	// Side walls of the tub (height above the deck) so a ride doesn't end with one careless step;
	// a full jump still clears them. Only solid while the cart is boarding/rolling — parked, you can
	// step on and off from either side.
	[Export] public float WallHeight = 48f;
	// Draw order of the cart art over the player (Player.tscn z_index = 1), so the rider looks inside the tub.
	[Export] public int ArtZIndex = 2;

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

	private enum State { Parked, Boarding, Rolling, Falling, Gone }

	private bool _airborne;
	private float _hopY;
	private float _hopVel;
	private bool _riderWasStanding;
	private Vector2 _fallVel;
	private float _fallTimer;
	private MineCart _leader;
	private bool _leaderStarted;

	public bool IsFalling => _state == State.Falling || _state == State.Gone;

	private State _state = State.Parked;
	private Vector2[] _path = System.Array.Empty<Vector2>();
	private bool[] _pathGaps = System.Array.Empty<bool>();
	private bool _onBranch;
	private float _forkDistance;
	private bool _braking;
	private float _facing = 1f;
	private Node2D _mainRails;
	private Node2D _branchRails;
	private float _distance;
	private Vector2 _railPos;
	private float _railLength;
	private float _timer;
	private float _emptyTimer;
	private Vector2 _origin;
	private Node2D _visual;
	private Node2D _tilt;
	private readonly List<Node2D> _wheels = new();
	private const string CartArtPath = "res://assets/sprites/deco/pixellab/mina/Vagoneta.png";
	// Centre of the lantern's lit pixels in Vagoneta.png.
	private static readonly Vector2 CartLampPixel = new(85.6f, 12.1f);
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
		RebuildPath();
		_distance = StartAtEnd == 1 ? _railLength : 0f;
		Direction = StartAtEnd == 1 ? -1 : 1;
		_railPos = _origin + MineRail.Sample(_path, _distance, out Vector2 startTangent);
		Position = _railPos;
		if (Mathf.Abs(startTangent.X) > 0.01f)
			_facing = Mathf.Sign(startTangent.X * Direction);

		// The rails stay in the level, not on the cart: built next to it in the parent's space.
		// With a switch (RailDesvio) both branches are laid; the one not in use is drawn dimmed.
		Node parent = GetParent();
		Callable.From(() =>
		{
			_mainRails = MineRail.BuildVisual(parent, _railPoints, GapFlags(_railPoints.Length - 1, TramosSalto), Name + "_Rieles");
			_mainRails.Position = _origin;
			if (HasBranch)
			{
				_branchRails = MineRail.BuildVisual(parent, BranchVisualPoints(), null, Name + "_RielesDesvio");
				_branchRails.Position = _origin;
			}
			UpdateSwitchVisual();
		}).CallDeferred();
		BuildDeckZone();
		if (Acompanar != null && !Acompanar.IsEmpty)
			_leader = GetNodeOrNull<MineCart>(Acompanar);
	}

	// ── Path: main rail, optional switch branch, jump gaps ─────────────────────────────────────

	private bool HasBranch => RailDesvio != null && RailDesvio.Length > 0 && DesvioDesde >= 0 && DesvioHasta > DesvioDesde && DesvioHasta < _railPoints.Length;

	private static bool[] GapFlags(int segments, int[] gaps)
	{
		var flags = new bool[Mathf.Max(0, segments)];
		if (gaps != null)
			foreach (int i in gaps)
				if (i >= 0 && i < flags.Length)
					flags[i] = true;
		return flags;
	}

	// The branch as its own polyline: fork point → RailDesvio → rejoin point.
	private Vector2[] BranchVisualPoints()
	{
		var pts = new List<Vector2> { _railPoints[DesvioDesde] };
		pts.AddRange(RailDesvio);
		pts.Add(_railPoints[DesvioHasta]);
		return pts.ToArray();
	}

	// Effective path = main rail, or main[0..DesvioDesde] + RailDesvio + main[DesvioHasta..] when the
	// switch is thrown. The prefix is shared, so switching before the fork never moves the cart.
	private void RebuildPath()
	{
		bool[] mainGaps = GapFlags(_railPoints.Length - 1, TramosSalto);
		if (!_onBranch || !HasBranch)
		{
			_path = _railPoints;
			_pathGaps = mainGaps;
		}
		else
		{
			var pts = new List<Vector2>();
			var gaps = new List<bool>();
			for (int i = 0; i <= DesvioDesde; i++)
			{
				pts.Add(_railPoints[i]);
				if (i < DesvioDesde)
					gaps.Add(mainGaps[i]);
			}
			foreach (Vector2 p in RailDesvio)
			{
				pts.Add(p);
				gaps.Add(false);
			}
			for (int i = DesvioHasta; i < _railPoints.Length; i++)
			{
				gaps.Add(i == DesvioHasta ? false : mainGaps[i - 1]);
				pts.Add(_railPoints[i]);
			}
			_path = pts.ToArray();
			_pathGaps = gaps.ToArray();
		}
		_railLength = MineRail.Length(_path);
		if (HasBranch)
			_forkDistance = MineRail.Length(_railPoints[..(DesvioDesde + 1)]);
	}

	// Lever (Activated → here): throws the switch, but only while the cart hasn't reached the fork
	// on its way out — once past it, the lever just clacks.
	public void CambiarVia()
	{
		if (!HasBranch)
			return;
		if (_distance > _forkDistance - 1f)
		{
			Sfx.PlayIfExistsAt(this, "World/Doors", "Chest Close");
			return;
		}
		_onBranch = !_onBranch;
		RebuildPath();
		UpdateSwitchVisual();
		Sfx.PlayAt(this, "World/Mining", "mine", 900f);
	}

	private void UpdateSwitchVisual()
	{
		if (!HasBranch)
			return;
		if (_mainRails != null && IsInstanceValid(_mainRails))
			_mainRails.Modulate = _onBranch ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
		if (_branchRails != null && IsInstanceValid(_branchRails))
			_branchRails.Modulate = _onBranch ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
	}

	private bool InGap()
	{
		int seg = MineRail.SegmentAt(_path, _distance);
		return seg >= 0 && seg < _pathGaps.Length && _pathGaps[seg];
	}

	// ── Physics ────────────────────────────────────────────────────────────────────────────────

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		// Measured on the rail, not from Position: with SyncToPhysics Godot puts Position back to the
		// last physics state right after it is set, so Position - before would always read zero.
		Vector2 before = _railPos;

		UpdateRider();
		UpdateWalls();

		switch (_state)
		{
			case State.Parked:
				RailVelocity = Vector2.Zero;
				// Companion cart: sets off the moment the leader draws level with it (or at once if it
				// starts behind, to chase it down).
				if (_leader != null && !_leaderStarted && IsInstanceValid(_leader) && _leader.IsRolling
					&& Mathf.Abs(_leader.RailVelocity.X) > 1f
					&& (_leader.GlobalPosition.X - GlobalPosition.X) * Mathf.Sign(_leader.RailVelocity.X) >= 0f)
				{
					_leaderStarted = true;
					Depart();
					break;
				}
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
				// EsperarAlJinete (long rides): instead, brake to a stop where the rider was lost (never
				// mid-jump) and wait there; stepping back on carries on in the same direction.
				else if (EsperarAlJinete && !_riderAttached)
				{
					_lostTimer += dt;
					_braking = _lostTimer > 0.5f && !InGap();
				}
				else
				{
					_lostTimer = 0f;
					_braking = false;
				}
				if (_leader != null)
					FollowLeader();
				Roll(dt);
				if (SaltaConJinete && _state == State.Rolling)
					UpdateHop(dt);
				if (_braking && Speed <= 1f && _state == State.Rolling)
				{
					_state = State.Parked;
					_braking = false;
					_lostTimer = 0f;
					_arrivedWithRider = false;
					_waitForRiderToLeave = false;
					RailVelocity = Vector2.Zero;
					Sfx.PlayAt(this, "World/Mining", "mine", 900f);
				}
				break;

			case State.Falling:
				_fallTimer += dt;
				_fallVel.Y += 1400f * dt;
				_railPos += _fallVel * dt;
				Position = _railPos;
				if (_tilt != null)
					_tilt.Rotation += (_fallVel.X < 0f ? -1f : 1f) * 2.5f * dt;
				if (_fallTimer > 1.1f)
					Crash();
				return;

			case State.Gone:
				return;
		}
		_riderWasStanding = RiderStandingOnDeck();

		Vector2 moved = _railPos - before;
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
		MineRail.Sample(_path, _distance, out Vector2 tangent);
		Vector2 travel = tangent * Direction;
		if (_braking)
			Speed = Mathf.MoveToward(Speed, 0f, Frenada * dt);
		else if (_leader != null)
		{
			// Companion: FollowLeader already set the speed.
		}
		else
		{
			Speed += SlopeGravity * travel.Y * dt - Friction * dt;
			Speed = Mathf.Clamp(Speed, MinSpeed, MaxSpeed);
		}

		_distance += Speed * Direction * dt;
		bool arrived = false;
		if (_distance >= _railLength) { _distance = _railLength; arrived = true; }
		if (_distance <= 0f) { _distance = 0f; arrived = true; }

		Vector2 target = _origin + MineRail.Sample(_path, _distance, out _);
		RailVelocity = (target - _railPos) / Mathf.Max(dt, 0.0001f);
		_railPos = target;
		Position = target + new Vector2(0f, _hopY);

		if (arrived && CaeAlFinal && Direction == 1)
		{
			StartFalling();
			return;
		}
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

	// ── Salto, caída, acompañante ──────────────────────────────────────────────────────────────

	private void UpdateHop(float dt)
	{
		// The rider just left the deck going up (a jump, not a fall): the cart hops with them.
		if (!_airborne && _riderWasStanding && _rider != null && IsInstanceValid(_rider) && !_rider.IsOnFloor() && _rider.Velocity.Y < -150f)
		{
			_airborne = true;
			_hopVel = -SaltoVelocidad;
			Sfx.PlayAt(this, "World/Mining", "mine", 1300f);
		}
		if (_airborne)
		{
			_hopVel += SaltoGravedad * dt;
			_hopY += _hopVel * dt;
			if (_hopY >= 0f)
			{
				_hopY = 0f;
				_airborne = false;
				if (InGap())
				{
					StartFalling();
					return;
				}
				_shake = 3f;
			}
		}
		else if (InGap())
		{
			// Coyote time: a jump a hair late, just past the edge, still hops the cart.
			_gapGrace += dt;
			if (_gapGrace > SaltoTolerancia)
			{
				StartFalling();
				return;
			}
		}
		else
			_gapGrace = 0f;
		Position = _railPos + new Vector2(0f, _hopY);
	}

	private float _gapGrace;

	private void StartFalling()
	{
		_state = State.Falling;
		_fallVel = RailVelocity + new Vector2(0f, _airborne ? _hopVel : 0f);
		_railPos = Position;
		_airborne = false;
		_hopY = 0f;
		_fallTimer = 0f;
		_riderAttached = false;
		if (_sparks != null)
			_sparks.Emitting = false;
		Sfx.PlayAt(this, "World/Doors", "Portcullis Gate");
	}

	private void Crash()
	{
		_state = State.Gone;
		RailVelocity = Vector2.Zero;
		GroundSlamFx.Impact(GetParent(), GlobalPosition, 1f);
		Sfx.PlayAt(this, "World/Mining", "mine", 1100f);
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player p && p.GlobalPosition.DistanceTo(GlobalPosition) < 700f)
			p.ShakeCamera(6f, 0.3f);
		Visible = false;
		foreach (Node child in GetChildren())
			if (child is CollisionShape2D c)
				c.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
	}

	// Keep level with the leader (same X) until someone boards this cart.
	private void FollowLeader()
	{
		if (_riderAttached || !IsInstanceValid(_leader) || _leader.IsFalling)
		{
			_leader = null;
			return;
		}
		MineRail.Sample(_path, _distance, out Vector2 tangent);
		float tx = Mathf.Max(0.2f, Mathf.Abs(tangent.X));
		float dx = (_leader.GlobalPosition.X - GlobalPosition.X) * Mathf.Sign(tangent.X * Direction);
		// Catch up (or wait) at most 150 px/s faster/slower than the leader: a cart that starts behind
		// reads as chasing it down instead of teleporting alongside.
		Speed = Mathf.Max(0f, (Mathf.Abs(_leader.RailVelocity.X) + Mathf.Clamp(dx * 4f, -150f, 150f)) / tx);
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

		_walls.Clear();
		foreach (float side in new[] { -1f, 1f })
		{
			string name = side < 0f ? "ParedIzquierda" : "ParedDerecha";
			GetNodeOrNull<Node>(name)?.Free();
			var wall = new CollisionShape2D
			{
				Name = name,
				Shape = new RectangleShape2D { Size = new Vector2(6f, WallHeight) },
				Position = new Vector2(side * (Width / 2f - 3f), -DeckHeight - WallHeight / 2f),
				Disabled = true,
			};
			AddChild(wall);
			_walls.Add(wall);
		}
	}

	private readonly List<CollisionShape2D> _walls = new();
	private bool _wallsOn;

	private void UpdateWalls()
	{
		bool on = _state != State.Parked;
		if (on == _wallsOn)
			return;
		_wallsOn = on;
		foreach (CollisionShape2D wall in _walls)
			wall.SetDeferred(CollisionShape2D.PropertyName.Disabled, !on);
	}

	private void BuildVisual()
	{
		_visual?.QueueFree();
		_wheels.Clear();
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);
		_tilt = new Node2D { Name = "Inclinacion" };
		_visual.AddChild(_tilt);

		// PixelLab mine cart (Vagoneta.png, wheels and lantern drawn in), scaled to Width and standing
		// with its wheels on the rail (y = 0). The player rides inside the tub: the deck collision sits
		// below the rim on purpose.
		var art = GD.Load<Texture2D>(CartArtPath);
		float k = Width / art.GetWidth();
		_tilt.AddChild(new Sprite2D
		{
			Texture = art,
			TextureFilter = TextureFilterEnum.Nearest,
			ZIndex = ArtZIndex,
			Scale = new Vector2(k, k),
			Position = new Vector2(0f, -art.GetHeight() * k / 2f),
		});

		// The "it's about to move" tell (blinks while boarding) and a light for dark mines, on the art's lantern.
		var lampPos = (CartLampPixel - new Vector2(art.GetWidth() / 2f, art.GetHeight())) * k;
		_lampGlow = new Sprite2D { Texture = Dot, Material = Additive, Position = lampPos, Scale = Vector2.One * 0.14f, Modulate = new Color(1f, 0.7f, 0.3f), ZIndex = ArtZIndex + 1 };
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
				EmissionRectExtents = new Vector2(Width / 2f - 12f, 1f),
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
		MineRail.Sample(_path.Length > 0 ? _path : _railPoints, _distance, out Vector2 tangent);
		// Tilt follows the slope only; a rail laid leftwards would otherwise flip the cart ~180°.
		// The art faces where it's going (lantern in front), so it's mirrored when rolling left.
		if (tangent.X < 0f)
			tangent = -tangent;
		if (_state == State.Rolling && Mathf.Abs(RailVelocity.X) > 1f)
			_facing = Mathf.Sign(RailVelocity.X);
		_tilt.Scale = new Vector2(_facing, 1f);
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
		if (HasBranch)
			DrawPolyline(BranchVisualPoints(), new Color(1f, 0.85f, 0.4f, 0.8f), 2f);
	}

	private static Vector2[] Circle(float r, int steps)
	{
		var pts = new Vector2[steps];
		for (int i = 0; i < steps; i++)
			pts[i] = new Vector2(r, 0f).Rotated(Mathf.Tau * i / steps);
		return pts;
	}
}
