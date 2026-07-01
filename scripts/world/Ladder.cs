using Godot;

namespace Metroidvania.World;

public partial class Ladder : Area2D
{
	[Export] public float Length = 128f;
	[Export] public float Width = 20f;
	[Export] public Color RailColor = new Color(0.42f, 0.27f, 0.12f);
	[Export] public Color RungColor = new Color(0.55f, 0.36f, 0.16f);

	public override void _Ready()
	{
		var collision = GetNode<CollisionShape2D>("CollisionShape2D");
		if (collision.Shape is RectangleShape2D shape)
		{
			var sizedShape = (RectangleShape2D)shape.Duplicate();
			sizedShape.Size = new Vector2(Width, Length);
			collision.Shape = sizedShape;
		}

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		QueueRedraw();
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

	public override void _Draw()
	{
		float halfWidth = Width * 0.5f;
		float halfLength = Length * 0.5f;
		float railThickness = 3f;

		DrawRect(new Rect2(-halfWidth, -halfLength, railThickness, Length), RailColor);
		DrawRect(new Rect2(halfWidth - railThickness, -halfLength, railThickness, Length), RailColor);

		const float rungSpacing = 18f;
		int rungCount = Mathf.Max(1, Mathf.FloorToInt(Length / rungSpacing));
		for (int i = 0; i <= rungCount; i++)
		{
			float y = -halfLength + i * (Length / rungCount);
			DrawRect(new Rect2(-halfWidth, y - 2f, Width, 4f), RungColor);
		}
	}
}
