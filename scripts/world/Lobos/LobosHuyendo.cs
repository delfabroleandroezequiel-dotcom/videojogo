using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// Wolves running south past the player, scared of whatever is coming down from the north (book
// cap. 2, Tobías: "lobos muertos de miedo"). Pure scenery: every Interval seconds one wolf drops in
// at SpawnX, runs toward the Nix side along the real floor (gravity + floor collision only, no
// hurtbox, no AI) and vanishes at DespawnX. Only once the player holds RequiredItemId (Arthur's
// sword: the morning Delfrad leaves). PixelLab wolf cells face right, so running left = FlipH.
public partial class LobosHuyendo : Node2D
{
	[Export] public string RequiredItemId = "item_espada_arthur";
	[Export] public SpriteFrames[] Frames = { };
	[Export] public string RunAnimation = "run";
	[Export] public float SpriteScale = 1.2f;
	[Export] public float SpawnX = 1740f;
	[Export] public float DespawnX = 20f;
	[Export] public float SpawnY = 150f;
	[Export] public float Speed = 330f;
	[Export] public float Interval = 6f;
	[Export] public int MaxWolves = 6;

	private float _timer = 1.5f;
	private int _spawned;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		if (Frames.Length == 0 || !SaveManager.Instance.HasItem(RequiredItemId))
			QueueFree();
	}

	public override void _Process(double delta)
	{
		if (_spawned >= MaxWolves)
			return;
		_timer -= (float)delta;
		if (_timer > 0f)
			return;
		_timer = Interval * _rng.RandfRange(0.7f, 1.3f);
		_spawned++;
		var wolf = new LoboQueHuye
		{
			Frames = Frames[_rng.RandiRange(0, Frames.Length - 1)],
			RunAnimation = RunAnimation,
			SpriteScale = SpriteScale,
			Speed = Speed * _rng.RandfRange(0.85f, 1.15f),
			DespawnX = DespawnX,
		};
		AddChild(wolf);
		wolf.GlobalPosition = new Vector2(SpawnX, SpawnY);
	}
}

public partial class LoboQueHuye : CharacterBody2D
{
	public SpriteFrames Frames;
	public string RunAnimation = "run";
	public float SpriteScale = 1.2f;
	public float Speed = 330f;
	public float DespawnX = 20f;
	private const float Gravity = 900f;
	private const float HopSpeed = 430f;
	private float _life = 14f;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 1;
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(40f, 30f) } });
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = Frames,
			Scale = new Vector2(SpriteScale, SpriteScale),
			FlipH = true,
			TextureFilter = TextureFilterEnum.Nearest,
			Position = new Vector2(0f, -10f),
		};
		AddChild(sprite);
		sprite.Play(RunAnimation);
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 v = Velocity;
		v.X = -Speed;
		v.Y = IsOnFloor() ? 0f : v.Y + Gravity * (float)delta;
		// A step or log in the way: hop it, like a wolf would, instead of running on the spot.
		if (IsOnFloor() && IsOnWall())
			v.Y = -HopSpeed;
		Velocity = v;
		MoveAndSlide();
		_life -= (float)delta;
		if (GlobalPosition.X < DespawnX || GlobalPosition.Y > 2000f || _life <= 0f)
			QueueFree();
	}
}
