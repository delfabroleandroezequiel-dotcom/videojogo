using Godot;

namespace Metroidvania.World;

// A soft oval shadow on the ground under a character, so they sit on the floor instead of floating
// over it. Every physics frame it looks straight down from the character's feet for solid ground
// (world + one-way platforms) and lies there, shrinking and fading with height; hidden when there's
// no ground within MaxDistance. Drawn before its character's sprite, on top of the floor tiles.
public partial class ContactShadow : Node2D
{
	public float Width = 46f;
	public float FeetOffset = 30f;
	public float MaxDistance = 220f;
	public float MaxAlpha = 0.55f;
	public uint GroundMask = 1u | 32u;

	private Sprite2D _blob;
	private Godot.Collections.Array<Node> _tileLayers;

	public override void _Ready()
	{
		// First child, so it draws before the character's own sprite (Visual) but after the floor
		// tiles that come earlier in the map.
		Callable.From(() => GetParent()?.MoveChild(this, 0)).CallDeferred();
		_blob = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Modulate = new Color(0f, 0f, 0f, MaxAlpha),
			Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded },
		};
		AddChild(_blob);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (GetParent() is not Node2D owner || _blob.Texture is null)
			return;
		Vector2 feet = owner.GlobalPosition + new Vector2(0f, FeetOffset);
		var query = PhysicsRayQueryParameters2D.Create(feet + new Vector2(0f, -4f), feet + new Vector2(0f, MaxDistance), GroundMask);
		if (owner is CollisionObject2D body)
			query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0)
		{
			Visible = false;
			return;
		}
		Visible = true;
		Vector2 ground = (Vector2)hit["position"];
		float height = Mathf.Clamp((ground.Y - feet.Y) / MaxDistance, 0f, 1f);
		Position = owner.ToLocal(ground);
		MatchGroundLayer(owner, ground);
		float size = Width * Mathf.Lerp(1f, 0.55f, height) / _blob.Texture.GetWidth();
		_blob.Scale = new Vector2(size, size * 0.28f);
		_blob.Modulate = new Color(0f, 0f, 0f, MaxAlpha * (1f - height));
	}

	// Some maps paint the floor on tile layers above the characters (feet sink into the stones), which
	// would hide the shadow: lift it to just above the highest tile layer painted at the ground point.
	// On floors drawn below the character it keeps the character's own layer.
	private void MatchGroundLayer(Node2D owner, Vector2 ground)
	{
		_tileLayers ??= GetTree().CurrentScene?.FindChildren("*", "TileMapLayer", true, false) ?? new Godot.Collections.Array<Node>();
		int ownerZ = AbsoluteZ(owner);
		int top = ownerZ;
		foreach (Node node in _tileLayers)
		{
			if (node is not TileMapLayer layer || !IsInstanceValid(layer) || !layer.IsVisibleInTree())
				continue;
			Vector2I cell = layer.LocalToMap(layer.ToLocal(ground + new Vector2(0f, 4f)));
			if (layer.GetCellSourceId(cell) == -1)
				continue;
			top = Mathf.Max(top, AbsoluteZ(layer) + 1);
		}
		ZAsRelative = false;
		ZIndex = top;
	}

	private static int AbsoluteZ(Node node)
	{
		int z = 0;
		for (Node current = node; current is CanvasItem item; current = current.GetParent())
		{
			z += item.ZIndex;
			if (!item.ZAsRelative)
				break;
		}
		return z;
	}
}
