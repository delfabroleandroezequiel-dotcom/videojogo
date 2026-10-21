using Godot;

namespace Metroidvania.Player;

// Coraza de sombra's look: the player's own shadow clinging to their back — an ink-black copy of the
// current animation frame, a hair bigger and set just behind the body, with a violet neon rim. It moves
// exactly as the player does (it reads their sprite every frame) and it's the one that takes the hits:
// each one flashes it white and shakes it, and as its charges run out it thins and starts to flicker
// apart. Child of the player's Visual node, so facing flips it with the body.
public partial class ShadowCarapace : Node2D
{
	public AnimatedSprite2D Source;
	public int Charges = 3;
	public int MaxCharges = 3;
	public Color Rim = new(0.72f, 0.38f, 1f, 1f);
	// Set behind the body (local, before the facing flip) and slightly up, so it reads as "behind".
	public Vector2 BackOffset = new(-5f, -2f);

	private Sprite2D _ink;
	private Sprite2D _rim;
	private float _flash;
	private float _age;

	public override void _Ready()
	{
		ZIndex = -1;
		ZAsRelative = true;
		_ink = new Sprite2D { Modulate = new Color(0.02f, 0f, 0.04f, 0.85f) };
		_rim = new Sprite2D { Material = NeonSilhouette.CreateMaterial(Rim, 1.6f) };
		AddChild(_rim);
		AddChild(_ink);
		Sync();
	}

	public void Hit(int chargesLeft)
	{
		Charges = chargesLeft;
		_flash = 1f;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		_flash = Mathf.Max(0f, _flash - dt * 5f);
		Sync();
	}

	// Copies the body's current frame and placement, then layers the damage state on top.
	private void Sync()
	{
		if (!IsInstanceValid(Source) || Source.SpriteFrames is null)
			return;
		Texture2D frame = Source.SpriteFrames.GetFrameTexture(Source.Animation, Source.Frame);
		float wear = 1f - Charges / (float)Mathf.Max(1, MaxCharges);
		Vector2 shake = _flash > 0f ? new Vector2((float)GD.RandRange(-3.0, 3.0), (float)GD.RandRange(-2.0, 2.0)) * _flash : Vector2.Zero;
		// Worn down, it flickers apart: random dropouts, more often the fewer charges remain.
		bool dropout = GD.Randf() < wear * 0.25f;
		foreach (Sprite2D s in new[] { _ink, _rim })
		{
			s.Texture = frame;
			s.Offset = Source.Offset;
			s.FlipH = Source.FlipH;
			s.Position = Source.Position + BackOffset + shake;
			s.Scale = Source.Scale * 1.07f;
			s.Visible = !dropout;
		}
		float breathe = 0.85f + 0.15f * Mathf.Sin(_age * 4f);
		_ink.Modulate = new Color(0.02f, 0f, 0.04f, Mathf.Lerp(0.85f, 0.45f, wear));
		_rim.Modulate = Colors.White.Lerp(new Color(3f, 3f, 3f, 1f), _flash) with { A = Mathf.Lerp(0.9f, 0.5f, wear) * breathe };
	}
}
