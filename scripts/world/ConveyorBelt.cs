using Godot;

namespace Metroidvania.World;

// Cinta transportadora (after Mega Man's conveyors): a floor strip that carries whatever stands on
// it at Speed px/s (negative = leftwards) — StaticBody2D.ConstantLinearVelocity, so the player's
// own floor logic does the carrying. Walking against it is slow, with it is fast; good for throwing
// the player into a trap's timing. Origin = top-left of the belt.
public partial class ConveyorBelt : StaticBody2D
{
	[Export] public float Width = 400f;
	[Export] public float Height = 24f;
	[Export] public float Speed = 120f;
	[Export] public Color BeltColor = new(0.14f, 0.14f, 0.16f, 1f);
	[Export] public Color ArrowColor = new(1f, 0.8f, 0.2f, 1f);

	private float _scroll;

	public override void _Ready()
	{
		CollisionLayer = 1;
		ConstantLinearVelocity = new Vector2(Speed, 0f);
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, Height) }, Position = new Vector2(Width / 2f, Height / 2f) });
	}

	public override void _Process(double delta)
	{
		_scroll = Mathf.PosMod(_scroll + Speed * (float)delta, 32f);
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(0f, 0f, Width, Height), BeltColor);
		for (float x = 0f; x <= Width; x += Height)
			DrawCircle(new Vector2(Mathf.Min(x, Width), Height / 2f), Height * 0.3f, BeltColor.Lightened(0.15f));
		float dir = Mathf.Sign(Speed == 0f ? 1f : Speed);
		for (float x = -32f; x < Width + 32f; x += 32f)
		{
			float px = x + _scroll;
			if (px < 6f || px > Width - 6f)
				continue;
			DrawPolyline(new[] { new Vector2(px - dir * 5f, 2f), new Vector2(px + dir * 3f, 6f), new Vector2(px - dir * 5f, 10f) }, ArrowColor, 2f);
		}
		DrawLine(new Vector2(0f, 0f), new Vector2(Width, 0f), ArrowColor with { A = 0.5f }, 1.5f);
	}
}
