using Godot;

namespace Metroidvania.World;

// Ambient townsfolk/animal: wanders left and right inside a range around where it was placed,
// pausing at random. No collision, no dialogue — pure life for the street. With Quieto it just
// loops its idle in place (juggler, street vendor), facing the way FlipInicial says.
public partial class Paseante : Node2D
{
	[Export] public SpriteFrames Frames;
	[Export] public float RangoIzquierda = 150f;
	[Export] public float RangoDerecha = 150f;
	[Export] public float Velocidad = 30f;
	[Export] public float PausaMin = 1.5f;
	[Export] public float PausaMax = 5f;
	[Export] public float CaminataMin = 1.5f;
	[Export] public float CaminataMax = 4.5f;
	[Export] public bool Quieto;
	[Export] public bool MirarIzquierda;
	[Export] public string AnimIdle = "idle";
	[Export] public string AnimCaminar = "walk";
	[Export] public float Escala = 1.158f;

	private AnimatedSprite2D _sprite;
	private readonly RandomNumberGenerator _rng = new();
	private float _origenX;
	private float _timer;
	private float _dir;

	public override void _Ready()
	{
		_rng.Randomize();
		_sprite = GetNode<AnimatedSprite2D>("Sprite");
		if (Frames != null)
			_sprite.SpriteFrames = Frames;
		_sprite.Scale = new Vector2(Escala, Escala);
		ApoyarPies();
		_origenX = Position.X;
		_sprite.FlipH = MirarIzquierda;
		Play(AnimIdle);
		// Desync the crowd: random start frame and a random first pause.
		if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(AnimIdle))
			_sprite.Frame = _rng.RandiRange(0, _sprite.SpriteFrames.GetFrameCount(AnimIdle) - 1);
		_timer = _rng.RandfRange(0f, PausaMax);
	}

	public override void _Process(double delta)
	{
		if (Quieto)
			return;

		_timer -= (float)delta;
		if (_dir == 0f)
		{
			if (_timer > 0f)
				return;
			float x = Position.X - _origenX;
			// Prefer heading back toward the middle of the range when near an end.
			_dir = x > RangoDerecha * 0.6f ? -1f : x < -RangoIzquierda * 0.6f ? 1f : (_rng.Randf() < 0.5f ? -1f : 1f);
			_timer = _rng.RandfRange(CaminataMin, CaminataMax);
			_sprite.FlipH = _dir < 0f;
			Play(AnimCaminar);
			return;
		}

		float next = Position.X + _dir * Velocidad * (float)delta;
		bool fuera = next - _origenX > RangoDerecha || next - _origenX < -RangoIzquierda;
		if (_timer <= 0f || fuera)
		{
			_dir = 0f;
			_timer = _rng.RandfRange(PausaMin, PausaMax);
			Play(AnimIdle);
			return;
		}
		Position = new Vector2(next, Position.Y);
	}

	// Put the lowest opaque row of the first idle frame on this node's origin (the floor), so every
	// sprite stands on the ground whatever empty margin PixelLab left under its feet.
	private void ApoyarPies()
	{
		SpriteFrames sf = _sprite.SpriteFrames;
		if (sf == null || !sf.HasAnimation(AnimIdle) || sf.GetFrameCount(AnimIdle) == 0)
			return;
		Image img = sf.GetFrameTexture(AnimIdle, 0)?.GetImage();
		if (img == null)
			return;
		int bottom = img.GetHeight() - 1;
		for (; bottom > 0; bottom--)
		{
			bool any = false;
			for (int x = 0; x < img.GetWidth() && !any; x++)
				any = img.GetPixel(x, bottom).A > 0.5f;
			if (any)
				break;
		}
		_sprite.Centered = true;
		_sprite.Position = new Vector2(0f, -(bottom + 1 - img.GetHeight() / 2f) * Escala);
	}

	private void Play(string anim)
	{
		if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim) && (_sprite.Animation != anim || !_sprite.IsPlaying()))
			_sprite.Play(anim);
	}
}
