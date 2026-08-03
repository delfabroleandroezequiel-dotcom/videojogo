using Godot;

namespace Metroidvania.World;

// Fog made entirely from Godot's own primitives, not any external art file: a handful of soft
// radial-gradient blobs (see FogBlobGradient.tres, a plain Gradient rasterized by GradientTexture2D)
// tinted green, drifting sideways at their own pace and wrapping around a horizontal band.
public partial class GroundFog : Node2D
{
	[Export] public Texture2D BlobTexture;
	[Export] public int BlobCount = 6;
	[Export] public float AreaWidth = 1400f;
	[Export] public float AreaHeight = 120f;
	[Export] public float MinScale = 1.5f;
	[Export] public float MaxScale = 3f;
	[Export] public float MinSpeed = 6f;
	[Export] public float MaxSpeed = 18f;
	// Vertical sine bob on top of the horizontal drift — each blob gets its own phase and speed
	// multiplier so they ride the "air" independently instead of bobbing in lockstep, which is what
	// actually sells it as air currents rather than a single wave passing through.
	[Export] public float BobAmplitude = 10f;
	[Export] public float BobSpeed = 0.5f;
	[Export] public Color TintColor = new(0.35f, 0.85f, 0.4f, 0.55f);

	private struct Blob
	{
		public Sprite2D Sprite;
		public float Speed;
		public float BaseY;
		public float BobPhase;
		public float BobSpeedMultiplier;
	}

	private Blob[] _blobs;
	private readonly RandomNumberGenerator _rng = new();
	private float _elapsed;

	public override void _Ready()
	{
		_rng.Randomize();
		_blobs = new Blob[BlobCount];
		for (int i = 0; i < BlobCount; i++)
		{
			float baseY = _rng.RandfRange(-AreaHeight / 2f, AreaHeight / 2f);
			var sprite = new Sprite2D
			{
				Texture = BlobTexture,
				Modulate = TintColor,
				Position = new Vector2(_rng.RandfRange(-AreaWidth / 2f, AreaWidth / 2f), baseY),
				Scale = Vector2.One * _rng.RandfRange(MinScale, MaxScale),
			};
			AddChild(sprite);
			_blobs[i] = new Blob
			{
				Sprite = sprite,
				Speed = _rng.RandfRange(MinSpeed, MaxSpeed),
				BaseY = baseY,
				BobPhase = _rng.RandfRange(0f, Mathf.Tau),
				BobSpeedMultiplier = _rng.RandfRange(0.7f, 1.3f),
			};
		}
	}

	public override void _Process(double delta)
	{
		_elapsed += (float)delta;
		float halfWidth = AreaWidth / 2f;
		for (int i = 0; i < _blobs.Length; i++)
		{
			Vector2 pos = _blobs[i].Sprite.Position;
			pos.X += _blobs[i].Speed * (float)delta;
			if (pos.X > halfWidth)
				pos.X -= AreaWidth;

			float bobTime = _elapsed * BobSpeed * _blobs[i].BobSpeedMultiplier + _blobs[i].BobPhase;
			pos.Y = _blobs[i].BaseY + Mathf.Sin(bobTime) * BobAmplitude;

			_blobs[i].Sprite.Position = pos;
		}
	}
}
