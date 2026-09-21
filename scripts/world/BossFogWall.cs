using Godot;

namespace Metroidvania.World;

// White fog wall that seals a boss arena from the moment the scene loads until the boss dies:
// it fades in and turns solid as soon as the scene is entered (no detection trigger involved), so
// the player can't back out; when BossPath's boss dies it fades out and frees itself.
//
// Size is free per instance (Width/Height/Anchor), same idea as ShadowBlock/CorridorBlock: a tall
// thin one seals a doorway, a wide low one seals a gap in a floor. Place it across the arena's
// entrance. The one thing that delays closing is the player still overlapping the wall's rectangle
// (ActivationClearance) — e.g. spawning right in the doorway — so it never closes on top of them;
// it closes the moment they step clear.
//
// If the boss is already defeated when the scene loads (Boss.cs removes itself), this frees itself
// too, so a cleared arena stays open. [Tool] only for the editor size preview; nothing runtime-y
// runs in the editor.
[Tool]
public partial class BossFogWall : Node2D
{
	private const float BlobTextureSize = 256f;

	private float _width = 96f;

	[Export(PropertyHint.Range, "8,4000,1,or_greater")]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(8f, value); QueueRedraw(); }
	}

	private float _height = 400f;

	[Export(PropertyHint.Range, "8,4000,1,or_greater")]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(8f, value); QueueRedraw(); }
	}

	private Vector2 _anchor = new(0.5f, 0.5f);

	// Which point of the rectangle sits on this node's position, same as ShadowBlock.Anchor:
	// (0.5, 1) grows upward from the floor, (0, 0.5) grows right from a doorway edge, etc.
	[Export]
	public Vector2 Anchor
	{
		get => _anchor;
		set { _anchor = new Vector2(Mathf.Clamp(value.X, 0f, 1f), Mathf.Clamp(value.Y, 0f, 1f)); QueueRedraw(); }
	}

	// The boss whose fight this seals — must be an Enemy (Boss.cs and subclasses).
	[ExportGroup("Boss")]
	[Export] public NodePath BossPath;
	// How far outside the wall's rectangle the player must be before it's allowed to close (only
	// matters if the player starts the scene inside or next to the wall).
	[Export] public float ActivationClearance = 40f;

	[ExportGroup("Behavior")]
	[Export] public float FadeInDuration = 0.8f;
	[Export] public float FadeOutDuration = 1.2f;
	[Export(PropertyHint.Layers2DPhysics)] public uint BarrierLayer = 1;

	[ExportGroup("Look")]
	[Export] public Color FogColor = new(1f, 1f, 1f, 1f);
	// Flat soft rectangle under the drifting blobs, so the wall reads as solid even where blobs thin out.
	[Export(PropertyHint.Range, "0,1,0.01")] public float BaseOpacity = 0.55f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float BlobOpacity = 0.7f;
	// Blob diameter as a multiple of the wall's thinner side.
	[Export] public float BlobSizeFactor = 1.4f;
	[Export] public int MaxBlobs = 48;
	[Export] public float DriftSpeed = 0.35f;

	private enum State
	{
		Idle,
		Active,
		Cleared,
	}

	private struct Blob
	{
		public Sprite2D Sprite;
		public Vector2 Home;
		public float PhaseX;
		public float PhaseY;
		public float SpeedMultiplier;
	}

	private State _state = State.Idle;
	private Enemy _boss;
	private StaticBody2D _barrier;
	private Blob[] _blobs = System.Array.Empty<Blob>();
	private float _driftAmplitude;
	private float _elapsed;
	private bool _bossChecked;
	private bool _subscribed;
	private Tween _tween;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		_boss = BossPath is null || BossPath.IsEmpty ? null : GetNodeOrNull<Enemy>(BossPath);
		if (_boss is null)
		{
			GD.PushWarning($"{Name}: BossPath doesn't point to an Enemy; removing the fog wall.");
			QueueFree();
			return;
		}

		_rng.Randomize();
		BuildBarrier();
		BuildVisuals();
		Visible = false;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (_subscribed && IsInstanceValid(_boss) && _boss.Stats is not null)
			_boss.Stats.Died -= OnBossDied;
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;

		Rect2 rect = LocalRect();
		DrawRect(rect, new Color(1f, 1f, 1f, 0.25f));
		DrawRect(rect, new Color(0.7f, 0.9f, 1f, 0.9f), false, 2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _state == State.Cleared)
			return;

		// Deferred to the first physics frame: Boss.cs removes itself in its own _Ready when already
		// defeated, and depending on tree order that may not have run yet when this node's _Ready did.
		if (!_bossChecked)
		{
			_bossChecked = true;
			if (!IsInstanceValid(_boss) || _boss.IsQueuedForDeletion() || _boss.Stats is null)
			{
				QueueFree();
				return;
			}

			_boss.Stats.Died += OnBossDied;
			_subscribed = true;
		}

		if (_state != State.Idle)
			return;

		if (GetTree().GetFirstNodeInGroup("player") is not Node2D player)
			return;

		if (LocalRect().Grow(ActivationClearance).HasPoint(ToLocal(player.GlobalPosition)))
			return;

		Activate();
	}

	public override void _Process(double delta)
	{
		_elapsed += (float)delta;
		for (int i = 0; i < _blobs.Length; i++)
		{
			float t = _elapsed * DriftSpeed * _blobs[i].SpeedMultiplier;
			_blobs[i].Sprite.Position = _blobs[i].Home + new Vector2(
				Mathf.Sin(t + _blobs[i].PhaseX),
				Mathf.Cos(t * 0.8f + _blobs[i].PhaseY)) * _driftAmplitude;
		}
	}

	private Rect2 LocalRect() =>
		new(-new Vector2(_width * _anchor.X, _height * _anchor.Y), new Vector2(_width, _height));

	private void Activate()
	{
		_state = State.Active;
		// Solid immediately, not after the fade — otherwise the player could slip out mid fade-in.
		_barrier.SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, BarrierLayer);

		Visible = true;
		SetProcess(true);
		Modulate = new Color(Modulate, 0f);
		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenProperty(this, "modulate:a", 1f, FadeInDuration);
	}

	private void OnBossDied()
	{
		if (_state == State.Cleared)
			return;

		bool neverClosed = _state == State.Idle;
		_state = State.Cleared;
		// Stats.Died fires from inside damage callbacks, where physics state can't change directly.
		_barrier.SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0u);

		if (neverClosed)
		{
			QueueFree();
			return;
		}

		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenProperty(this, "modulate:a", 0f, FadeOutDuration);
		_tween.TweenCallback(Callable.From(QueueFree));
	}

	private void BuildBarrier()
	{
		Rect2 rect = LocalRect();
		_barrier = new StaticBody2D { CollisionLayer = 0, CollisionMask = 0, Position = rect.GetCenter() };
		AddChild(_barrier);
		// Fresh shape per instance, never a shared resource.
		_barrier.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rect.Size } });
	}

	private void BuildVisuals()
	{
		float feather = Mathf.Clamp(Mathf.Min(_width, _height) * 0.25f, 6f, 40f);
		AddChild(new ShadowBlock
		{
			Width = _width,
			Height = _height,
			Anchor = _anchor,
			ShadowColor = FogColor,
			Opacity = BaseOpacity,
			FeatherLeft = feather,
			FeatherTop = feather,
			FeatherRight = feather,
			FeatherBottom = feather,
		});

		// White radial blob built from Godot primitives — FogBlobGradient.tres is baked green, so a
		// white modulate on it would still come out green.
		var gradient = new Gradient
		{
			Offsets = new[] { 0f, 0.6f, 1f },
			Colors = new[] { new Color(1f, 1f, 1f, 0.85f), new Color(1f, 1f, 1f, 0.4f), new Color(1f, 1f, 1f, 0f) },
		};
		var texture = new GradientTexture2D
		{
			Gradient = gradient,
			Width = (int)BlobTextureSize,
			Height = (int)BlobTextureSize,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f),
			FillTo = new Vector2(1f, 0.5f),
		};

		float diameter = Mathf.Max(Mathf.Min(_width, _height) * BlobSizeFactor, 64f);
		int count = Mathf.Clamp(Mathf.CeilToInt(_width * _height / (diameter * diameter * 0.3f)), 4, Mathf.Max(4, MaxBlobs));
		_driftAmplitude = diameter * 0.12f;

		Rect2 rect = LocalRect();
		_blobs = new Blob[count];
		for (int i = 0; i < count; i++)
		{
			Vector2 home = rect.Position + new Vector2(_rng.RandfRange(0f, _width), _rng.RandfRange(0f, _height));
			var sprite = new Sprite2D
			{
				Texture = texture,
				Modulate = new Color(FogColor, FogColor.A * BlobOpacity),
				Position = home,
				Scale = Vector2.One * (diameter / BlobTextureSize),
			};
			AddChild(sprite);
			_blobs[i] = new Blob
			{
				Sprite = sprite,
				Home = home,
				PhaseX = _rng.RandfRange(0f, Mathf.Tau),
				PhaseY = _rng.RandfRange(0f, Mathf.Tau),
				SpeedMultiplier = _rng.RandfRange(0.7f, 1.3f),
			};
		}
	}
}
