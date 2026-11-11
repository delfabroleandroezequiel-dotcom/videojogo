using Godot;

namespace Metroidvania.World;

// Viga baja: a sagging crossbeam hanging into a cart line (or any low tunnel). Its underside sits
// Clearance px above the floor/deck: with the default 40, a standing player (64 px tall) hits it
// and a crouching one (32 px) slides under. Hitting it hurts and shoves you back — on a cart, that
// can mean being knocked off. The node is the beam's underside centre. Glowing nails along the
// edge so it reads in the dark at speed.
[Tool]
public partial class LowBeam : Node2D
{
	[Export] public float Width = 92f;
	[Export] public float BeamHeight = 30f;
	// Distance from the beam's underside up to the ceiling it hangs from (drawn as the hanger).
	[Export] public float HangLength = 60f;
	[Export] public int Damage = 20;
	[Export] public float Knockback = 160f;

	public override void _Ready()
	{
		BuildVisual();
		if (Engine.IsEditorHint())
			return;
		Hazard hazard = Hazard.CreateArea(this, false, Damage, Knockback);
		hazard.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Width, BeamHeight) }, Position = new Vector2(0f, -BeamHeight / 2f) });
	}

	// PixelLab beam (VigaBaja.png, 92x36: two iron rings on top, the timber below them), drawn 1:1 at
	// the default Width and stretched to it otherwise; it hangs from two pixel ropes (Soga.png, the
	// same tile as Rope/HangRope) tied to the rings.
	private const string BeamArtPath = "res://assets/sprites/deco/pixellab/mina/VigaBaja.png";
	private const string RopeArtPath = "res://assets/sprites/deco/pixellab/Soga.png";
	// Ring centres in the art, from its centre, and how far the rings stick up above the timber.
	private const float RingOffsetX = 33f;
	private const float RingHeight = 6f;

	private void BuildVisual()
	{
		foreach (Node child in GetChildren())
			if (child.Name.ToString().StartsWith("Arte"))
				child.Free();

		var art = GD.Load<Texture2D>(BeamArtPath);
		var rope = GD.Load<Texture2D>(RopeArtPath);
		float k = Width / art.GetWidth();
		float artH = art.GetHeight() * k;
		// The timber's underside is the node origin (y = 0); the rings poke above it.
		float ringTop = -artH;
		foreach (float side in new[] { -1f, 1f })
		{
			float ropeLen = HangLength + BeamHeight - artH + RingHeight * k;
			AddChild(new Sprite2D
			{
				Name = side < 0f ? "ArteSogaIzq" : "ArteSogaDer",
				Texture = rope,
				TextureFilter = TextureFilterEnum.Nearest,
				TextureRepeat = TextureRepeatEnum.Enabled,
				RegionEnabled = true,
				RegionRect = new Rect2(0f, 0f, rope.GetWidth(), Mathf.Max(1f, ropeLen)),
				Centered = false,
				Position = new Vector2(side * RingOffsetX * k - rope.GetWidth() / 2f, ringTop + RingHeight * k - Mathf.Max(1f, ropeLen)),
			});
		}
		AddChild(new Sprite2D
		{
			Name = "ArteViga",
			Texture = art,
			TextureFilter = TextureFilterEnum.Nearest,
			Scale = new Vector2(k, k),
			Position = new Vector2(0f, -artH / 2f),
		});
		// The edge you have to read at speed: a faint warm glow along the underside.
		AddChild(new Line2D
		{
			Name = "ArteBorde",
			Points = new[] { new Vector2(-Width / 2f + 4f, -1f), new Vector2(Width / 2f - 4f, -1f) },
			Width = 2f,
			DefaultColor = new Color(1f, 0.55f, 0.2f, 0.55f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		});
	}
}
