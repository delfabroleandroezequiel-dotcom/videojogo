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
	[Export] public float Width = 28f;
	[Export] public float BeamHeight = 26f;
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

	private void BuildVisual()
	{
		var wood = new Color(0.4f, 0.26f, 0.15f);
		float w = Width / 2f;
		AddChild(new Line2D { Points = new[] { new Vector2(-w + 4f, -BeamHeight), new Vector2(-w + 4f, -BeamHeight - HangLength) }, Width = 4f, DefaultColor = wood.Darkened(0.3f) });
		AddChild(new Line2D { Points = new[] { new Vector2(w - 4f, -BeamHeight), new Vector2(w - 4f, -BeamHeight - HangLength) }, Width = 4f, DefaultColor = wood.Darkened(0.3f) });
		AddChild(new Polygon2D { Polygon = new[] { new Vector2(-w, -BeamHeight), new Vector2(w, -BeamHeight), new Vector2(w, 0f), new Vector2(-w, 0f) }, Color = wood });
		AddChild(new Line2D
		{
			Points = new[] { new Vector2(-w, -1f), new Vector2(w, -1f) },
			Width = 2f,
			DefaultColor = new Color(1f, 0.55f, 0.2f, 0.9f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		});
	}
}
