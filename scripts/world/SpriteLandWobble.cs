using Godot;

namespace Metroidvania.World;

// Shakes a decoration sprite when the player lands on / walks over it — for floating moss islands
// and similar art placed on top of a (one-way) platform. Builds its own detection Area2D over the top
// band of the texture, so it only needs the script on the Sprite2D. The shake moves Offset, never
// Position, so the piece stays exactly where it was placed in the editor.
public partial class SpriteLandWobble : Sprite2D
{
	// Fraction of the texture height (from the top) that counts as "on top of it".
	[Export(PropertyHint.Range, "0.05,1,0.05")] public float TopBand = 0.35f;
	[Export] public float DipPixels = 6f;
	[Export] public float ShakePixels = 4f;
	[Export] public float Duration = 0.45f;
	[Export] public float Cooldown = 0.25f;

	private Vector2 _baseOffset;
	private Tween _tween;
	private double _lastTrigger = -10.0;

	public override void _Ready()
	{
		if (Engine.IsEditorHint() || Texture is null)
			return;
		_baseOffset = Offset;

		Vector2 size = Texture.GetSize();
		Vector2 topLeft = (Centered ? -size / 2f : Vector2.Zero) + Offset;
		float bandHeight = size.Y * TopBand;
		var area = new Area2D { Name = "LandArea", CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		area.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(size.X, bandHeight) },
			Position = topLeft + new Vector2(size.X / 2f, bandHeight / 2f),
		});
		AddChild(area);
		area.BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;
		double now = Time.GetTicksMsec() / 1000.0;
		if (now - _lastTrigger < Cooldown)
			return;
		_lastTrigger = now;
		Wobble();
	}

	public void Wobble()
	{
		_tween?.Kill();
		Offset = _baseOffset;
		_tween = CreateTween();
		// Dip under the weight, then a decaying side-to-side shake back to rest.
		float step = Duration / 6f;
		_tween.TweenProperty(this, "offset", _baseOffset + new Vector2(0f, DipPixels), step).SetEase(Tween.EaseType.Out);
		float amp = ShakePixels;
		for (int i = 0; i < 4; i++)
		{
			float side = i % 2 == 0 ? amp : -amp;
			float dip = DipPixels * (1f - (i + 1) / 5f);
			_tween.TweenProperty(this, "offset", _baseOffset + new Vector2(side, dip), step);
			amp *= 0.55f;
		}
		_tween.TweenProperty(this, "offset", _baseOffset, step).SetEase(Tween.EaseType.InOut);
	}
}
