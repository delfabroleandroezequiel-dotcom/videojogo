using Godot;

namespace Metroidvania.World;

public partial class Ladder : Area2D
{
	[Export] public float Length = 128f;
	[Export] public float Width = 20f;
	[Export] public Texture2D LadderTexture;
	[Export] public float VisualWidth = 48f;

	// The source sheet has a lot of transparent padding around the actual rungs;
	// this is the column that contains the art, tall enough to tile seamlessly.
	// Default matches Escalera.png; other ladder textures override it per scene.
	[Export] public Rect2 TileSourceRect = new(438, 0, 164, 340);

	// Lies along X instead of Y (Length is then its width): the player hangs at its height and
	// shuffles sideways (climb_side) — a ladder laid across a wall or a gap. Down or jump lets go.
	[Export] public bool Horizontal;

	// Seen in profile against a side wall: the player climbs it side-on (the pack's side-view
	// "ladder_climb"), facing the ladder — MiraDerecha = the ladder is to the player's right.
	[Export] public bool DeCostado;
	[Export] public bool MiraDerecha = true;
	// Profile art for DeCostado (rungs drawn pointing right; flipped to point at the player).
	[Export] public Texture2D SideTexture;
	[Export] public Rect2 SideTileSourceRect = new(0, 0, 13, 59);
	[Export] public float SideVisualWidth = 16f;

	public override void _Ready()
	{
		var collision = GetNode<CollisionShape2D>("CollisionShape2D");
		if (collision.Shape is RectangleShape2D shape)
		{
			var sizedShape = (RectangleShape2D)shape.Duplicate();
			sizedShape.Size = Horizontal ? new Vector2(Length, Width) : new Vector2(Width, Length);
			collision.Shape = sizedShape;
		}

		BuildVisual();

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void BuildVisual()
	{
		bool side = DeCostado && SideTexture is not null;
		Texture2D texture = side ? SideTexture : LadderTexture;
		Rect2 source = side ? SideTileSourceRect : TileSourceRect;
		float visualWidth = side ? SideVisualWidth : VisualWidth;
		if (texture is null)
			return;

		float scale = visualWidth / source.Size.X;
		float tileHeight = source.Size.Y * scale;
		int tileCount = Mathf.Max(1, Mathf.CeilToInt(Length / tileHeight));
		float startY = -Length * 0.5f;
		// The same vertical strip, turned on its side for a horizontal ladder.
		var strip = new Node2D { Name = "Visual", Rotation = Horizontal ? Mathf.Pi / 2f : 0f, Scale = new Vector2(side && MiraDerecha ? -1f : 1f, 1f) };
		AddChild(strip);

		for (int i = 0; i < tileCount; i++)
		{
			float remaining = Length - i * tileHeight;
			float sourceHeight = Mathf.Clamp(remaining / scale, 0f, source.Size.Y);
			if (sourceHeight <= 0f)
				break;

			var sprite = new Sprite2D
			{
				Texture = texture,
				TextureFilter = TextureFilterEnum.Nearest,
				RegionEnabled = true,
				RegionRect = new Rect2(source.Position, new Vector2(source.Size.X, sourceHeight)),
				Centered = false,
				Scale = new Vector2(scale, scale),
				Position = new Vector2(-visualWidth * 0.5f, startY + i * tileHeight),
			};
			strip.AddChild(sprite);
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.EnterLadder(this);
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.ExitLadder(this);
	}
}
