using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// A rope stretched horizontally between two anchors (this node's position = left anchor, the right
// one Length to the right), sagging slightly in the middle. The player hangs under it and moves
// hand over hand left/right, with the same ladder_climb animation ladders use. Up (or just jumping
// into it) grabs, down lets go, jump hops off upward. The grab zone is built in code below the rope.
// Color is visual only: the rope art is desaturated once, then tinted per rope via RopeColor.
// [Tool] so length/sag/color preview live in the editor.
[Tool]
public partial class HangRope : Node2D
{
	public enum RopeColorPreset { Natural, Red, Blue, Green, Gold, Black, Custom }

	private const string TexturePath = "res://assets/sprites/deco/rope.png";

	// Same crop as Rope.TileSourceRect: the column of rope.png that actually contains the rope.
	private static readonly Rect2I TileSourceRect = new(386, 26, 252, 1488);

	private static readonly Dictionary<RopeColorPreset, Color> PresetColors = new()
	{
		{ RopeColorPreset.Natural, new Color(0.62f, 0.47f, 0.32f) },
		{ RopeColorPreset.Red, new Color(0.75f, 0.2f, 0.18f) },
		{ RopeColorPreset.Blue, new Color(0.25f, 0.4f, 0.8f) },
		{ RopeColorPreset.Green, new Color(0.3f, 0.6f, 0.28f) },
		{ RopeColorPreset.Gold, new Color(0.95f, 0.78f, 0.3f) },
		{ RopeColorPreset.Black, new Color(0.22f, 0.2f, 0.22f) },
	};

	// Grayscale, rotated-sideways crop shared by every HangRope (Line2D tiles along U).
	private static Texture2D _cachedTexture;

	private float _length = 240f;

	[Export(PropertyHint.Range, "16,4000,1")]
	public float Length
	{
		get => _length;
		set { _length = Mathf.Max(16f, value); Rebuild(); }
	}

	private float _sag = 10f;

	// How far the middle hangs below the anchors, in world pixels. 0 = perfectly straight.
	[Export(PropertyHint.Range, "0,200,1")]
	public float Sag
	{
		get => _sag;
		set { _sag = Mathf.Max(0f, value); Rebuild(); }
	}

	private float _visualWidth = 5f;

	[Export(PropertyHint.Range, "1,40,0.5")]
	public float VisualWidth
	{
		get => _visualWidth;
		set { _visualWidth = Mathf.Max(1f, value); Rebuild(); }
	}

	private RopeColorPreset _ropeColor = RopeColorPreset.Natural;

	[Export]
	public RopeColorPreset RopeColor
	{
		get => _ropeColor;
		set { _ropeColor = value; ApplyColor(); }
	}

	private Color _customColor = Colors.White;

	// Only used when RopeColor is Custom.
	[Export]
	public Color CustomColor
	{
		get => _customColor;
		set { _customColor = value; ApplyColor(); }
	}

	// Distance from the rope down to the player's origin while hanging (hands on the rope).
	[Export] public float HangOffsetY = 40f;
	// How far below the rope the player can be and still grab it.
	[Export] public float GrabZoneHeight = 70f;
	[Export] public float ShimmySpeed = 110f;

	private Line2D _line;
	private Area2D _grabZone;

	public float LeftX => GlobalPosition.X;
	public float RightX => GlobalPosition.X + _length * GlobalScale.X;

	public override void _Ready()
	{
		Rebuild();
		if (Engine.IsEditorHint())
			return;

		_grabZone.BodyEntered += OnBodyEntered;
		_grabZone.BodyExited += OnBodyExited;
	}

	// The rope's world Y at a given world X (parabolic sag), clamped to the rope's span.
	public float RopeYAt(float globalX)
	{
		float t = Mathf.Clamp((globalX - LeftX) / Mathf.Max(1f, RightX - LeftX), 0f, 1f);
		return GlobalPosition.Y + _sag * 4f * t * (1f - t) * GlobalScale.Y;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.EnterHangRope(this);
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Metroidvania.Player.Player player)
			player.ExitHangRope(this);
	}

	private static Texture2D LoadTexture()
	{
		if (_cachedTexture is not null)
			return _cachedTexture;

		Image sheet = GD.Load<Texture2D>(TexturePath).GetImage();
		if (sheet.IsCompressed())
			sheet.Decompress();
		Image crop = sheet.GetRegion(TileSourceRect);
		crop.Convert(Image.Format.Rgba8);

		// Desaturate so any tint reads as that color — multiplying the golden source art by blue
		// or green just came out muddy.
		byte[] data = crop.GetData();
		for (int i = 0; i < data.Length; i += 4)
		{
			float lum = 0.299f * data[i] + 0.587f * data[i + 1] + 0.114f * data[i + 2];
			byte v = (byte)Mathf.Clamp(lum * 1.25f, 0f, 255f);
			data[i] = v;
			data[i + 1] = v;
			data[i + 2] = v;
		}
		crop = Image.CreateFromData(crop.GetWidth(), crop.GetHeight(), false, Image.Format.Rgba8, data);

		// Line2D tiles along the line's length — turn the vertical strand sideways.
		crop.Rotate90(ClockDirection.Counterclockwise);
		_cachedTexture = ImageTexture.CreateFromImage(crop);
		return _cachedTexture;
	}

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		if (_line is null)
		{
			_line = new Line2D
			{
				TextureMode = Line2D.LineTextureMode.Tile,
				TextureRepeat = TextureRepeatEnum.Enabled,
				JointMode = Line2D.LineJointMode.Round,
				DefaultColor = Colors.White,
			};
			AddChild(_line);
		}

		_line.Texture = LoadTexture();
		_line.Width = _visualWidth;

		int pointCount = Mathf.Clamp(Mathf.CeilToInt(_length / 16f), 2, 64) + 1;
		var points = new Vector2[pointCount];
		for (int i = 0; i < pointCount; i++)
		{
			float t = i / (float)(pointCount - 1);
			points[i] = new Vector2(t * _length, _sag * 4f * t * (1f - t));
		}
		_line.Points = points;
		ApplyColor();

		if (_grabZone is null)
		{
			_grabZone = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
			_grabZone.AddChild(new CollisionShape2D());
			AddChild(_grabZone);
		}

		// Always a fresh shape — mutating a shared one would resize every HangRope instance.
		var shape = _grabZone.GetChild<CollisionShape2D>(0);
		shape.Shape = new RectangleShape2D { Size = new Vector2(_length, GrabZoneHeight + _sag) };
		shape.Position = new Vector2(_length * 0.5f, (GrabZoneHeight + _sag) * 0.5f);
	}

	private void ApplyColor()
	{
		if (_line is null)
			return;
		_line.DefaultColor = _ropeColor == RopeColorPreset.Custom ? _customColor : PresetColors[_ropeColor];
	}
}
