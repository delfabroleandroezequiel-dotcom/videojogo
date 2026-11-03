using Godot;

namespace Metroidvania.World;

// Bloque furioso (after Spelunky's crush trap / Celeste's Kevin): a stone block with a face that
// watches the axes Mode allows. Line up with it within DetectRange (and nothing solid in between)
// and it glares for WindUp, then hurls itself at you, accelerating until it slams into a wall; it
// rests there for RestTime and goes back home: a slow crawl, or in the *Agresivo modes a second
// charge that also hurts. Hurts on contact while charging. AnimatableBody2D moved through its
// local Position; it only collides with the world (mask 1) when checking for walls.
public partial class RageBlock : AnimatableBody2D
{
	public enum RageMode { Multidireccional, Bidireccional, SoloLateralAgresivo, SoloLateral, SoloArribaAbajoAgresivo, SoloArribaAbajo }

	[Export] public RageMode Mode = RageMode.Bidireccional;
	[Export] public Vector2 Size = new(72f, 72f);
	[Export] public float DetectRange = 480f;
	[Export] public float WindUp = 0.35f;
	[Export] public float Acceleration = 2200f;
	[Export] public float MaxSpeed = 760f;
	[Export] public float RestTime = 0.8f;
	[Export] public float ReturnSpeed = 140f;
	[Export] public int Damage = 35;
	[Export] public float Knockback = 480f;
	[Export] public Color NeonColor = new(1f, 0.45f, 0.15f, 1f);

	// Painted block (assets/sprites/deco/BloquesFuriosos: 640px canvas with the body centered, so any
	// of them swaps in with the same scale). Its eyes always glow, so the wind-up tell is a brighter
	// sprite plus an additive glow over them. Null falls back to DefaultArtPath: duplicating an
	// instance in the editor can save an "Art = null" override over the scene's texture.
	// ArtScale, EyeOffset and EyeGlowScale are tuned for ArtReferenceSize and follow Size from there.
	[ExportGroup("Art")]
	[Export] public Texture2D Art;
	[Export] public float ArtScale = 0.206f;
	[Export] public Color CalmTint = new(0.7f, 0.7f, 0.7f, 1f);
	[Export] public Vector2 EyeOffset = new(13f, -6f);
	[Export] public float EyeGlowScale = 0.35f;

	private const string DefaultArtPath = "res://assets/sprites/deco/BloquesFuriosos/BloqueFurioso1.png";
	private const float ArtReferenceSize = 72f;

	// The whole screen trembles during the wind-up and charge, harder the closer the block gets, and
	// the slam peaks it. Proximity eases in, so most of the build-up lands in the last stretch.
	[ExportGroup("Tremble")]
	[Export] public float TrembleFar = 3f;
	[Export] public float TrembleNear = 18f;
	[Export] public float SlamShakeFar = 7f;
	[Export] public float SlamShakeNear = 30f;
	[Export] public float BodyTrembleRatio = 0.25f;

	private enum State { Watch, WindUp, Charge, Rest, Return }

	private State _state = State.Watch;
	private Vector2 _home;
	private Vector2 _dir;
	private float _speed;
	private float _timer;
	private Node2D _visual;
	private Sprite2D _artSprite;
	private Sprite2D _eyeGlowL;
	private Sprite2D _eyeGlowR;
	private Area2D _hurt;

	public override void _Ready()
	{
		SyncToPhysics = false;
		CollisionLayer = 1;
		CollisionMask = 1;
		_home = Position;
		Build();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		switch (_state)
		{
			case State.Watch:
				if (FindTarget(out Vector2 dir))
				{
					_dir = dir;
					_state = State.WindUp;
					_timer = WindUp;
					SetAngry(true);
				}
				break;
			case State.WindUp:
				_timer -= dt;
				_visual.Position = new Vector2(Mathf.Sin(_timer * 80f), Mathf.Cos(_timer * 70f)) * 2.5f;
				Tremble();
				if (_timer <= 0f)
				{
					_visual.Position = Vector2.Zero;
					_state = State.Charge;
					_speed = 0f;
				}
				break;
			case State.Charge:
				_speed = Mathf.Min(MaxSpeed, _speed + Acceleration * dt);
				Vector2 motion = _dir * _speed * dt;
				if (TestMove(GlobalTransform, motion))
				{
					// Snap flush against the wall it hit.
					var col = new KinematicCollision2D();
					TestMove(GlobalTransform, motion, col);
					Position += _dir * Mathf.Max(0f, col.GetTravel().Length() - 0.5f);
					Slam();
					break;
				}
				Position += motion;
				HurtTouching();
				Tremble();
				break;
			case State.Rest:
				_timer -= dt;
				if (_timer <= 0f)
				{
					_state = State.Return;
					_speed = 0f;
					if (!IsAggressive)
						SetAngry(false);
				}
				break;
			case State.Return:
				ReturnHome(dt);
				break;
		}
		UpdateSolidity();
	}

	// An AnimatableBody2D shoves and carries whatever it moves into, so the block is only solid while
	// still: moving, it passes through the player and just hurts through _hurt. TestMove only reads the
	// mask, so wall checks don't care. It waits for the player to step out before going solid again,
	// or it would trap them inside.
	private void UpdateSolidity()
	{
		if (_state is State.Charge or State.Return)
			CollisionLayer = 0;
		else if (CollisionLayer == 0 && _hurt.GetOverlappingBodies().Count == 0)
			CollisionLayer = 1;
	}

	private bool IsAggressive => Mode is RageMode.Multidireccional or RageMode.SoloLateralAgresivo or RageMode.SoloArribaAbajoAgresivo;
	private bool WatchesSides => Mode is not (RageMode.SoloArribaAbajoAgresivo or RageMode.SoloArribaAbajo);
	private bool WatchesVertical => Mode is not (RageMode.SoloLateralAgresivo or RageMode.SoloLateral);

	private void ReturnHome(float dt)
	{
		Vector2 to = _home - Position;
		bool aggressive = IsAggressive;
		if (aggressive)
			_speed = Mathf.Min(MaxSpeed, _speed + Acceleration * dt);
		float step = (aggressive ? _speed : ReturnSpeed) * dt;
		if (to.Length() <= step)
		{
			Position = _home;
			_state = State.Watch;
			if (aggressive)
			{
				_visual.Position = Vector2.Zero;
				TrapKit.Shake(this, Mathf.Lerp(SlamShakeFar, SlamShakeNear, PlayerProximity()), 0.35f);
				SetAngry(false);
			}
			return;
		}
		Position += to.Normalized() * step;
		if (aggressive)
		{
			_dir = to.Normalized();
			HurtTouching();
			Tremble();
		}
	}

	private bool FindTarget(out Vector2 dir)
	{
		dir = Vector2.Zero;
		var player = TrapKit.FindPlayer(this);
		if (player is null)
			return false;
		Vector2 d = player.GlobalPosition - GlobalPosition;
		Vector2 half = Size / 2f;
		if (WatchesSides && Mathf.Abs(d.Y) < half.Y + 10f && Mathf.Abs(d.X) <= DetectRange)
			dir = new Vector2(Mathf.Sign(d.X), 0f);
		else if (WatchesVertical && Mathf.Abs(d.X) < half.X + 10f && Mathf.Abs(d.Y) <= DetectRange)
			dir = new Vector2(0f, Mathf.Sign(d.Y));
		else
			return false;
		// Line of sight: no world in between.
		var q = PhysicsRayQueryParameters2D.Create(GlobalPosition + dir * (half.X + 2f), player.GlobalPosition, 1);
		q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		return GetWorld2D().DirectSpaceState.IntersectRay(q).Count == 0;
	}

	// 0 at DetectRange or farther, 1 on top of the player.
	private float PlayerProximity()
	{
		var player = TrapKit.FindPlayer(this);
		if (player is null)
			return 0f;
		float t = 1f - Mathf.Clamp(GlobalPosition.DistanceTo(player.GlobalPosition) / DetectRange, 0f, 1f);
		return t * t;
	}

	// Called every frame: each call restarts the camera's short decay, so it holds at this strength.
	private void Tremble()
	{
		float strength = Mathf.Lerp(TrembleFar, TrembleNear, PlayerProximity());
		TrapKit.Shake(this, strength, 0.12f);
		if (_state != State.WindUp)
		{
			float body = strength * BodyTrembleRatio;
			_visual.Position = new Vector2((float)GD.RandRange(-body, body), (float)GD.RandRange(-body, body));
		}
	}

	private void HurtTouching()
	{
		foreach (Node2D b in _hurt.GetOverlappingBodies())
			if (TrapKit.IsPlayer(b) || b.IsInGroup("enemy"))
				TrapKit.Hurt(b, Damage, _dir + Vector2.Up * 0.3f, Knockback);
	}

	private void Slam()
	{
		_state = State.Rest;
		_timer = RestTime;
		HurtTouching();
		_visual.Position = Vector2.Zero;
		TrapKit.Shake(this, Mathf.Lerp(SlamShakeFar, SlamShakeNear, PlayerProximity()), 0.35f);
		TrapKit.Burst(this, GlobalPosition + _dir * (Size.X / 2f), new Color(0.8f, 0.75f, 0.65f), 26, 260f);
	}

	private void SetAngry(bool angry)
	{
		_artSprite.SelfModulate = angry ? Colors.White : CalmTint;
		_eyeGlowL.Visible = angry;
		_eyeGlowR.Visible = angry;
	}

	private void Build()
	{
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size } });
		_visual = new Node2D();
		AddChild(_visual);
		BuildArt();
		SetAngry(false);
		_hurt = new Area2D { CollisionLayer = 0, CollisionMask = 2u | 4u, Monitorable = false };
		_hurt.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = Size + new Vector2(10f, 10f) } });
		AddChild(_hurt);
	}

	private void BuildArt()
	{
		Vector2 fit = Size / ArtReferenceSize;
		_artSprite = new Sprite2D
		{
			Texture = Art ?? GD.Load<Texture2D>(DefaultArtPath),
			Scale = fit * ArtScale,
			TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
		};
		_visual.AddChild(_artSprite);
		_eyeGlowL = MakeEyeGlow(new Vector2(-EyeOffset.X, EyeOffset.Y) * fit, EyeGlowScale * fit.X);
		_eyeGlowR = MakeEyeGlow(EyeOffset * fit, EyeGlowScale * fit.X);
	}

	private Sprite2D MakeEyeGlow(Vector2 position, float scale)
	{
		var glow = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = NeonColor,
			Position = position,
			Scale = Vector2.One * scale,
		};
		_visual.AddChild(glow);
		return glow;
	}
}
