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
	private static readonly Rect2 TileSourceRect = new(438, 0, 164, 340);

	public override void _Ready()
	{
		var collision = GetNode<CollisionShape2D>("CollisionShape2D");
		if (collision.Shape is RectangleShape2D shape)
		{
			var sizedShape = (RectangleShape2D)shape.Duplicate();
			sizedShape.Size = new Vector2(Width, Length);
			collision.Shape = sizedShape;
		}

		BuildVisual();

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void BuildVisual()
	{
		if (LadderTexture is null)
			return;

		float scale = VisualWidth / TileSourceRect.Size.X;
		float tileHeight = TileSourceRect.Size.Y * scale;
		int tileCount = Mathf.Max(1, Mathf.CeilToInt(Length / tileHeight));
		float startY = -Length * 0.5f;

		for (int i = 0; i < tileCount; i++)
		{
			float remaining = Length - i * tileHeight;
			float sourceHeight = Mathf.Clamp(remaining / scale, 0f, TileSourceRect.Size.Y);
			if (sourceHeight <= 0f)
				break;

			var sprite = new Sprite2D
			{
				Texture = LadderTexture,
				RegionEnabled = true,
				RegionRect = new Rect2(TileSourceRect.Position, new Vector2(TileSourceRect.Size.X, sourceHeight)),
				Centered = false,
				Scale = new Vector2(scale, scale),
				Position = new Vector2(-VisualWidth * 0.5f, startY + i * tileHeight),
			};
			AddChild(sprite);
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
