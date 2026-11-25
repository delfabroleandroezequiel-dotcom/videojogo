using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// A rubble plug that stays until a BanderaGuardada with the same Bandera id has been set somewhere
// else in the world (a cross-scene shortcut: pull the lever in one room, the way opens in another).
// Node origin = top-left; Width x Height. Drawn in code as piled dark rock with dying embers.
[Tool]
public partial class BloqueoPorBandera : StaticBody2D
{
	private float _width = 120f;
	private float _height = 200f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(16f, value); QueueRedraw(); }
	}

	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(16f, value); QueueRedraw(); }
	}

	[Export] public string Bandera = "";

	public override void _Ready()
	{
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;
		if (!string.IsNullOrEmpty(Bandera) && SaveManager.Instance.IsPickupCollected(Bandera))
		{
			QueueFree();
			return;
		}
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, _height) }, Position = new Vector2(_width / 2f, _height / 2f) });
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(0f, 0f, _width, _height), new Color(0.15f, 0.11f, 0.1f, 1f));
		var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.Abs(Position.X) + Mathf.Abs(Position.Y) + 1f) };
		for (int i = 0; i < (int)(_width * _height / 900f); i++)
		{
			var c = new Vector2(rng.RandfRange(6f, _width - 6f), rng.RandfRange(6f, _height - 6f));
			DrawCircle(c, rng.RandfRange(5f, 12f), new Color(0.22f, 0.16f, 0.14f, 1f));
			if (rng.Randf() < 0.15f)
				DrawCircle(c, 2f, new Color(1f, 0.45f, 0.12f, 0.7f));
		}
	}
}
