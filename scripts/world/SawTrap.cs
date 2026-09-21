using Godot;

namespace Metroidvania.World;

// Spinning saw blade that slides back and forth along a straight rail — a moving touch-damage
// hazard. The rail is just TravelOffset (end point relative to this node, in local space), so the
// saw is placed at one end of the rail and the offset points at the other; TravelOffset = zero
// leaves it spinning in place. Damage goes through the same Hazard area every other trap uses
// (InstantKill / Damage / KnockbackForce are forwarded to it), with HitWhileOverlapping on so a
// saw that catches the player keeps cutting for as long as they overlap it.
//
// Nothing here follows level geometry: the path is a fixed offset, so it needs tuning in the
// editor against the actual corridor (the rail is drawn in the editor and in-game so you can see
// where it ends up). [Tool] only so the blade size and rail preview live-update in the Inspector —
// spinning and sliding only run in-game. The _initialized guard keeps the property setters from
// touching child nodes while Godot is still restoring the scene's saved values (before _Ready).
[Tool]
public partial class SawTrap : Node2D
{
	private float _sawDiameter = 64f;
	private float _hitRadiusRatio = 0.85f;
	private Vector2 _travelOffset = Vector2.Zero;
	private bool _showRail = true;

	// Visual size of the blade on screen; the sprite scales itself to this whatever the texture size.
	[Export]
	public float SawDiameter
	{
		get => _sawDiameter;
		set { _sawDiameter = value; Layout(); }
	}

	// Damage circle as a fraction of the visual radius — the tips of the teeth are the dangerous
	// part, so it stays a little smaller than the sprite to be forgiving.
	[Export(PropertyHint.Range, "0.3,1,0.01")]
	public float HitRadiusRatio
	{
		get => _hitRadiusRatio;
		set { _hitRadiusRatio = value; Layout(); }
	}

	// Radians per second; negative spins the other way.
	[Export] public float SpinSpeed = 12f;

	// Where the saw slides to, relative to this node. Zero = stays in place.
	[Export]
	public Vector2 TravelOffset
	{
		get => _travelOffset;
		set { _travelOffset = value; QueueRedraw(); }
	}

	[Export] public float TravelSpeed = 140f;

	// Seconds the saw waits at each end of the rail before turning around.
	[Export] public float EndPause = 0.3f;

	// 0 = starts at this node, 1 = starts at the far end. Offset several saws to desynchronise them.
	[Export(PropertyHint.Range, "0,1,0.01")] public float StartProgress = 0f;

	[Export]
	public bool ShowRail
	{
		get => _showRail;
		set { _showRail = value; QueueRedraw(); }
	}

	[Export] public Color RailColor = new(0.06f, 0.06f, 0.07f, 0.9f);
	[Export] public float RailWidth = 6f;

	[ExportGroup("Damage")]
	[Export] public bool InstantKill = false;
	[Export] public int Damage = 25;
	[Export] public float KnockbackForce = 350f;

	private Node2D _mover;
	private Sprite2D _visual;
	private CollisionShape2D _hitShape;
	private float _progress;
	private int _direction = 1;
	private float _pauseTimer;
	private bool _initialized;

	public override void _Ready()
	{
		_mover = GetNode<Node2D>("Mover");
		_visual = GetNode<Sprite2D>("Mover/Visual");
		_hitShape = GetNode<CollisionShape2D>("Mover/HazardArea/CollisionShape2D");

		if (!Engine.IsEditorHint())
		{
			var hazard = GetNode<Hazard>("Mover/HazardArea");
			hazard.InstantKill = InstantKill;
			hazard.Damage = Damage;
			hazard.KnockbackForce = KnockbackForce;
			hazard.HitWhileOverlapping = true;
			hazard.SetPhysicsProcess(true);

			_progress = Mathf.Clamp(StartProgress, 0f, 1f);
			_mover.Position = TravelOffset * _progress;
		}

		_initialized = true;
		Layout();
		QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		float dt = (float)delta;
		_visual.Rotation += SpinSpeed * dt;

		float railLength = TravelOffset.Length();
		if (railLength <= 0.01f || TravelSpeed <= 0f)
			return;

		if (_pauseTimer > 0f)
		{
			_pauseTimer -= dt;
			return;
		}

		_progress += _direction * TravelSpeed * dt / railLength;
		if (_progress >= 1f || _progress <= 0f)
		{
			_progress = Mathf.Clamp(_progress, 0f, 1f);
			_direction = -_direction;
			_pauseTimer = EndPause;
		}

		_mover.Position = TravelOffset * _progress;
	}

	public override void _Draw()
	{
		if (!ShowRail || TravelOffset.LengthSquared() < 0.01f)
			return;

		DrawLine(Vector2.Zero, TravelOffset, RailColor, RailWidth);
		DrawCircle(Vector2.Zero, RailWidth * 0.75f, RailColor);
		DrawCircle(TravelOffset, RailWidth * 0.75f, RailColor);
	}

	private void Layout()
	{
		if (!_initialized)
			return;

		float visualScale = SawDiameter / _visual.Texture.GetWidth();
		_visual.Scale = new Vector2(visualScale, visualScale);

		// Always a fresh shape: the .tscn's sub-resource is shared by every instance of this scene,
		// so resizing it in place would resize every saw in the game.
		_hitShape.Shape = new CircleShape2D { Radius = SawDiameter * 0.5f * HitRadiusRatio };
	}
}
