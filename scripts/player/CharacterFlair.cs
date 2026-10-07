using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.Player;

// Identity layer drawn on top of the bought sprite sheet, without editing it:
//  - Cape: a verlet cloth strip hanging from the shoulders, widening downward, behind the body —
//    streams back while running, drapes when still, lifts/falls with jumps.
//  - Eye glow: a 1-2 px spark on the face in the current element's colour (+ soft halo).
//  - Element aura: soft particles around the body while imbued (embers / bubbles / frost / sparks).
// Neck and eye points come from a per-frame table measured on the sheet (MainCharBodyPoints.json,
// written by the SpriteFrames generator: [neckX, neckY, eyeX, eyeY] per frame, eye -1 = no face).
public partial class CharacterFlair : Node2D
{
	public bool CapeEnabled = true;
	public bool EyeGlowEnabled = true;
	public bool AuraEnabled = true;

	public int CapeSegments = 9;
	public float CapeSegmentLength = 5f;
	public float CapeWidthTop = 4f;
	public float CapeWidthBottom = 12f;
	public float CapeGravity = 420f;
	public float CapeIdleWind = 60f;
	public float CapeWindPerSpeed = 2.2f;
	public float CapeFlutterIdle = 60f;
	public float CapeFlutterMoving = 220f;
	// Where the cape hangs from, relative to the measured neck point (frame pixels; the sheet faces right).
	public Vector2 CapeShoulderOffset = new(-2.6f, 1.7f);
	public Color CapeColor = new(0.55f, 0.11f, 0.15f);
	public Color CapeShadeColor = new(0.36f, 0.07f, 0.1f);
	public Color CapeOutlineColor = new(0.16f, 0.03f, 0.05f);

	private AnimatedSprite2D _sprite;
	private Node2D _visual;
	private CharacterBody2D _body;
	private Dictionary<string, Vector4[]> _points;
	private System.Func<DamageElement> _element;
	private System.Func<Color> _elementColor;

	private Vector2[] _capePos;
	private Vector2[] _capePrev;
	private Polygon2D _capeShade;
	private Polygon2D _capeBody;
	private Line2D _capeOutline;
	private bool _capeReady;
	private float _time;

	private Polygon2D _eyeCore;
	private Sprite2D _eyeHalo;
	private CpuParticles2D _aura;
	private DamageElement _auraElement = (DamageElement)(-1);

	public void Setup(AnimatedSprite2D sprite, Node2D visual, CharacterBody2D body, Dictionary<string, Vector4[]> points,
		System.Func<DamageElement> element, System.Func<Color> elementColor)
	{
		_sprite = sprite;
		_visual = visual;
		_body = body;
		_points = points;
		_element = element;
		_elementColor = elementColor;
	}

	// Reads {"anim": [[neckX, neckY, eyeX, eyeY], ...], ...}.
	public static Dictionary<string, Vector4[]> LoadPoints(string path)
	{
		var result = new Dictionary<string, Vector4[]>();
		if (!FileAccess.FileExists(path))
			return result;
		Variant parsed = Json.ParseString(FileAccess.GetFileAsString(path));
		if (parsed.VariantType != Variant.Type.Dictionary)
			return result;
		foreach (var (key, value) in parsed.AsGodotDictionary())
		{
			Godot.Collections.Array frames = value.AsGodotArray();
			var list = new Vector4[frames.Count];
			for (int i = 0; i < frames.Count; i++)
			{
				Godot.Collections.Array p = frames[i].AsGodotArray();
				list[i] = new Vector4((float)p[0].AsDouble(), (float)p[1].AsDouble(), (float)p[2].AsDouble(), (float)p[3].AsDouble());
			}
			result[key.AsString()] = list;
		}
		return result;
	}

	public override void _Ready()
	{
		// Absolute z: the player draws at 1, so 0 is behind it and 2 in front of it.
		_capeShade = new Polygon2D { TopLevel = true, ZAsRelative = false, ZIndex = 0, Color = CapeShadeColor, Antialiased = false };
		_capeBody = new Polygon2D { TopLevel = true, ZAsRelative = false, ZIndex = 0, Color = CapeColor, Antialiased = false };
		_capeOutline = new Line2D { TopLevel = true, ZAsRelative = false, ZIndex = 0, Width = 1f, DefaultColor = CapeOutlineColor, Antialiased = false };
		AddChild(_capeShade);
		AddChild(_capeBody);
		AddChild(_capeOutline);
		_capePos = new Vector2[CapeSegments + 1];
		_capePrev = new Vector2[CapeSegments + 1];

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_eyeHalo = new Sprite2D
		{
			TopLevel = true, ZAsRelative = false, ZIndex = 2,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Scale = new Vector2(0.09f, 0.07f),
			Material = additive,
		};
		_eyeCore = new Polygon2D
		{
			TopLevel = true, ZAsRelative = false, ZIndex = 2,
			Polygon = new[] { new Vector2(-1f, -0.5f), new Vector2(1f, -0.5f), new Vector2(1f, 0.5f), new Vector2(-1f, 0.5f) },
			Material = additive,
			Antialiased = false,
		};
		AddChild(_eyeHalo);
		AddChild(_eyeCore);

		_aura = new CpuParticles2D
		{
			Emitting = false,
			Amount = 18,
			Lifetime = 0.9f,
			LocalCoords = false,
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(9f, 20f),
			Material = additive,
			ZAsRelative = false,
			ZIndex = 2,
		};
		AddChild(_aura);
	}

	private Vector4 FramePoints()
	{
		if (_points.TryGetValue(_sprite.Animation, out Vector4[] frames) && frames.Length > 0)
			return frames[Mathf.Clamp(_sprite.Frame, 0, frames.Length - 1)];
		return new Vector4(96f, 52f, -1f, -1f);
	}

	// Frame pixel -> world, through the sprite and the (facing-flipped) Visual node.
	private Vector2 ToWorld(Vector2 px)
	{
		Texture2D texture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
		Vector2 size = texture?.GetSize() ?? new Vector2(192f, 128f);
		return _visual.ToGlobal(_sprite.Position + (px - size / 2f + _sprite.Offset) * _sprite.Scale);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_sprite is null || !IsInstanceValid(_sprite))
			return;
		float dt = (float)delta;
		_time += dt;
		Vector4 p = FramePoints();
		bool visible = _visual.IsVisibleInTree();
		Color modulate = _visual.Modulate;

		UpdateCape(new Vector2(p.X, p.Y), dt, visible, modulate);
		UpdateEyes(new Vector2(p.Z, p.W), visible, modulate);
		UpdateAura(new Vector2(p.X, p.Y), visible);
	}

	private void UpdateCape(Vector2 neckPx, float dt, bool visible, Color modulate)
	{
		_capeShade.Visible = _capeBody.Visible = _capeOutline.Visible = CapeEnabled && visible;
		if (!CapeEnabled)
			return;

		Vector2 anchor = ToWorld(neckPx + CapeShoulderOffset);
		if (!_capeReady || anchor.DistanceTo(_capePos[0]) > 120f)
		{
			for (int i = 0; i <= CapeSegments; i++)
				_capePos[i] = _capePrev[i] = anchor + new Vector2(0f, i * CapeSegmentLength);
			_capeReady = true;
		}

		float facing = _visual.Scale.X >= 0f ? 1f : -1f;
		float speed = Mathf.Abs(_body.Velocity.X);
		float flutter = speed > 1f ? CapeFlutterMoving : CapeFlutterIdle;
		for (int i = 1; i <= CapeSegments; i++)
		{
			float k = (float)i / CapeSegments;
			Vector2 accel = new(-facing * (CapeIdleWind + speed * CapeWindPerSpeed), CapeGravity);
			accel.Y += Mathf.Sin(_time * 9f - i * 0.8f) * flutter * k;
			accel.X += Mathf.Sin(_time * 5f - i * 0.6f) * flutter * 0.25f * k * -facing;
			Vector2 velocity = (_capePos[i] - _capePrev[i]) * 0.9f;
			_capePrev[i] = _capePos[i];
			_capePos[i] += velocity + accel * dt * dt;
		}
		for (int iteration = 0; iteration < 6; iteration++)
		{
			_capePos[0] = anchor;
			for (int i = 1; i <= CapeSegments; i++)
			{
				Vector2 d = _capePos[i] - _capePos[i - 1];
				float length = d.Length();
				if (length < 0.0001f)
					continue;
				Vector2 correction = d * (1f - CapeSegmentLength / length);
				if (i == 1)
				{
					_capePos[i] -= correction;
				}
				else
				{
					_capePos[i] -= correction * 0.5f;
					_capePos[i - 1] += correction * 0.5f;
				}
			}
		}
		_capePos[0] = anchor;

		Vector2[] outer = CapeOutline(1f, 0f);
		_capeShade.Polygon = outer;
		// Lighter body sits slightly toward the outside of the cloth, leaving a shaded inner edge.
		_capeBody.Polygon = CapeOutline(0.62f, 1.5f);
		var closed = new Vector2[outer.Length + 1];
		outer.CopyTo(closed, 0);
		closed[^1] = outer[0];
		_capeOutline.Points = closed;
		_capeShade.Modulate = _capeBody.Modulate = _capeOutline.Modulate = modulate;
	}

	// The cloth as a closed polygon around the rope: half-width grows from top to bottom.
	private Vector2[] CapeOutline(float widthScale, float shift)
	{
		int count = _capePos.Length;
		var left = new Vector2[count];
		var right = new Vector2[count];
		for (int i = 0; i < count; i++)
		{
			Vector2 tangent = (_capePos[Mathf.Min(i + 1, count - 1)] - _capePos[Mathf.Max(i - 1, 0)]).Normalized();
			Vector2 normal = new(-tangent.Y, tangent.X);
			float half = Mathf.Lerp(CapeWidthTop, CapeWidthBottom, (float)i / (count - 1)) * widthScale;
			left[i] = (_capePos[i] + normal * (half + shift)).Round();
			right[i] = (_capePos[i] - normal * (half - shift)).Round();
		}
		var polygon = new Vector2[count * 2];
		for (int i = 0; i < count; i++)
		{
			polygon[i] = left[i];
			polygon[count * 2 - 1 - i] = right[i];
		}
		return polygon;
	}

	private void UpdateEyes(Vector2 eyePx, bool visible, Color modulate)
	{
		bool show = EyeGlowEnabled && visible && eyePx.X >= 0f;
		_eyeCore.Visible = _eyeHalo.Visible = show;
		if (!show)
			return;
		Vector2 eye = ToWorld(eyePx).Round();
		Color tint = _elementColor();
		_eyeCore.GlobalPosition = eye;
		_eyeHalo.GlobalPosition = eye;
		_eyeCore.Color = tint.Lightened(0.55f);
		// A slow, subtle pulse so it reads as alive.
		float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 3.2f);
		_eyeHalo.Modulate = new Color(tint, 0.55f * pulse * modulate.A);
		_eyeCore.Modulate = new Color(1f, 1f, 1f, modulate.A);
	}

	private void UpdateAura(Vector2 neckPx, bool visible)
	{
		DamageElement element = _element();
		bool on = AuraEnabled && visible && element != DamageElement.Normal;
		_aura.GlobalPosition = ToWorld(neckPx + new Vector2(0f, 18f));
		if (element != _auraElement)
		{
			_auraElement = element;
			ConfigureAura(element);
		}
		if (_aura.Emitting != on)
			_aura.Emitting = on;
	}

	private void ConfigureAura(DamageElement element)
	{
		Color tint = _elementColor();
		var fade = new Gradient();
		fade.SetColor(0, tint with { A = 0f });
		fade.AddPoint(0.25f, tint with { A = 0.85f });
		fade.SetColor(1, tint with { A = 0f });
		_aura.ColorRamp = fade;
		switch (element)
		{
			case DamageElement.Fire: // embers rising
				_aura.Gravity = new Vector2(0f, -70f);
				_aura.Direction = Vector2.Up;
				_aura.Spread = 25f;
				_aura.InitialVelocityMin = 10f;
				_aura.InitialVelocityMax = 35f;
				_aura.ScaleAmountMin = 0.04f;
				_aura.ScaleAmountMax = 0.08f;
				_aura.Lifetime = 0.9f;
				break;
			case DamageElement.Poison: // slow bubbles drifting up
				_aura.Gravity = new Vector2(0f, -25f);
				_aura.Direction = Vector2.Up;
				_aura.Spread = 40f;
				_aura.InitialVelocityMin = 4f;
				_aura.InitialVelocityMax = 14f;
				_aura.ScaleAmountMin = 0.05f;
				_aura.ScaleAmountMax = 0.11f;
				_aura.Lifetime = 1.3f;
				break;
			case DamageElement.Ice: // frost glitter sinking
				_aura.Gravity = new Vector2(0f, 30f);
				_aura.Direction = Vector2.Down;
				_aura.Spread = 60f;
				_aura.InitialVelocityMin = 3f;
				_aura.InitialVelocityMax = 12f;
				_aura.ScaleAmountMin = 0.025f;
				_aura.ScaleAmountMax = 0.05f;
				_aura.Lifetime = 1.1f;
				break;
			default: // lightning: fast short crackle in every direction
				_aura.Gravity = Vector2.Zero;
				_aura.Direction = Vector2.Up;
				_aura.Spread = 180f;
				_aura.InitialVelocityMin = 50f;
				_aura.InitialVelocityMax = 110f;
				_aura.ScaleAmountMin = 0.02f;
				_aura.ScaleAmountMax = 0.045f;
				_aura.Lifetime = 0.25f;
				break;
		}
	}
}
