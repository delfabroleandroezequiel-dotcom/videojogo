using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Reusable greybox breakable wall — the classic metroidvania "hit it and it's gone" secret.
// Solid collision blocks the passage like normal terrain until its Stats child runs out of
// health, then collision disables, an Explosion.tscn plays (reusing the existing VixMix impact
// art as a placeholder — not dedicated "rubble" art, just the closest fit already in the
// project), and the wall frees itself.
// Detected by Player's AttackHitbox for free, no extra wiring: AttackHitbox's collision_mask
// already includes the World physics layer (see Hitbox.cs/Player.tscn), and Hitbox looks for a
// child node literally named "Stats" on whatever StaticBody2D it hits — same contract Enemy.cs
// uses, which is why this is a StaticBody2D itself (with Stats as its own direct child) instead
// of a wrapper Node2D like MiniCorridor/CorridorBlock.
// ShowFill defaults to false — a real secret blends into the wall around it; flip it on while
// greyboxing so you can actually see where you placed it, same idea as MiniCorridor's ShowFill.
// [Tool] so the shape/fill preview updates live in the editor, before ever pressing Play.
[Tool]
public partial class SecretWall : StaticBody2D
{
	private const string ExplosionScenePath = "res://scenes/world/Explosion.tscn";

	private float _width = 64f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(8f, value); Rebuild(); }
	}

	private float _height = 96f;

	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(8f, value); Rebuild(); }
	}

	private int _maxHealth = 15;

	// Health pool the Stats child breaks at — defaults to exactly Player's base AttackPower (15,
	// see Player.tscn's Stats), so an un-upgraded hit breaks it in one; raise it for a wall that
	// needs a stronger weapon or a couple of hits.
	[Export]
	public int MaxHealth
	{
		get => _maxHealth;
		set { _maxHealth = Mathf.Max(1, value); Rebuild(); }
	}

	[Export] public Color FillColor = new(0.5f, 0.5f, 0.55f, 0.6f);

	private bool _showFill;

	[Export]
	public bool ShowFill
	{
		get => _showFill;
		set { _showFill = value; Rebuild(); }
	}

	public override void _Ready()
	{
		Rebuild();

		if (!Engine.IsEditorHint())
			GetNode<Stats>("Stats").Died += Break;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		Vector2 size = new(_width, _height);

		if (GetNodeOrNull<Stats>("Stats") is Stats stats)
		{
			stats.MaxHealth = _maxHealth;
			stats.ResetToFull();
		}

		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
		{
			// Always assign a fresh shape instead of mutating collision.Shape in place — same
			// reason as CorridorBlock: the RectangleShape2D from the .tscn is a single shared
			// resource, so every instance would otherwise resize the same object together.
			collision.Shape = new RectangleShape2D { Size = size };
		}

		if (GetNodeOrNull<Polygon2D>("Fill") is Polygon2D fill)
		{
			fill.Visible = _showFill;
			fill.Color = FillColor;
			Vector2 half = size / 2f;
			fill.Polygon = new[]
			{
				new Vector2(-half.X, -half.Y),
				new Vector2(half.X, -half.Y),
				new Vector2(half.X, half.Y),
				new Vector2(-half.X, half.Y),
			};
		}
	}

	private void Break()
	{
		if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is CollisionShape2D collision)
			collision.Disabled = true;

		Visible = false;

		PackedScene explosionScene = GD.Load<PackedScene>(ExplosionScenePath);
		var explosion = explosionScene.Instantiate<Node2D>();
		GetTree().CurrentScene.AddChild(explosion);
		explosion.GlobalPosition = GlobalPosition;

		QueueFree();
	}
}
