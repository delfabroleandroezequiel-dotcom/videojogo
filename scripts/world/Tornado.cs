using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Procedural tornado (no art), drawn as a pixel-art hurricane column — distinct stacked swooshes
// rising up a funnel, bright crest on top, broken dash tails at the sides — by the
// HurricaneTornado shader on a ColorRect. White by default, warm fire tones when the caster is
// fire-imbued. Grows in from the ground, travels, then fades.
// Gameplay: damage only — it never moves enemies (forced displacement caused bugs: enemies pushed
// into walls, off ledges, AI desync). Every TickInterval everything inside the column takes a hit;
// caster decides the stats (the player's spell passes its own attack power in Setup).
public partial class Tornado : Node2D
{
	private const string ShaderPath = "res://resources/shaders/HurricaneTornado.gdshader";

	[ExportGroup("Shape")]
	[Export] public float Height = 220f;
	// Starts as a point on the ground and widens up to TopRadius (ConePower < 1 = widens fast).
	[Export] public float TipRadius = 3f;
	[Export] public float TopRadius = 62f;
	[Export] public float ConePower = 0.65f;
	// How strongly each ribbon curves up at its ends.
	[Export] public float Flatten = 0.5f;
	[Export] public float BandCount = 13f;
	[Export] public float ThicknessBottom = 5f;
	[Export] public float ThicknessTop = 20f;
	// Ribbons per second climbing the column.
	[Export] public float RiseSpeed = 1.4f;
	[Export] public float SpinSpeed = 70f;
	[Export] public float SwayAmplitude = 5f;
	// Size of one "pixel" of the effect, matched to the sprites' pixel scale.
	[Export] public float PixelSize = 2f;
	// Four flat tones, brightest to darkest.
	[Export] public Color[] WindColors =
	{
		new(1f, 1f, 1f), new(0.84f, 0.886f, 0.949f), new(0.576f, 0.655f, 0.769f), new(0.337f, 0.396f, 0.541f),
	};
	[Export] public Color[] FireColors =
	{
		new(1f, 0.953f, 0.722f), new(0.949f, 0.71f, 0.267f), new(0.706f, 0.384f, 0.165f), new(0.369f, 0.165f, 0.102f),
	};

	[ExportGroup("Motion")]
	[Export] public float Speed = 150f;
	[Export] public float Lifetime = 3.5f;
	[Export] public float GrowTime = 0.3f;
	[Export] public float FadeTime = 0.4f;

	[ExportGroup("Damage")]
	[Export] public int AttackPower = 10;
	[Export] public float TickInterval = 0.3f;
	// Half-width of the damage zone — roughly the visible body of the funnel.
	[Export] public float HitHalfWidth = 40f;
	// Enemy layer.
	[Export(PropertyHint.Layers2DPhysics)] public uint TargetMask = 4;

	[ExportGroup("Growing variant")]
	// Size gained per new enemy touched, and the cap (Tornado creciente).
	[Export] public float GrowPerEnemy = 0.18f;
	[Export] public float MaxSizeScale = 1.6f;

	private const float BottomMargin = 8f;

	// Size multiplier on the whole column (shape, hit zone and, when growing, damage). Mini
	// tornadoes start small; the growing variant raises it as it touches enemies.
	public float SizeScale { get; private set; } = 1f;

	private Vector2 _travel = Vector2.Right;
	private bool _fire;
	private bool _growing;
	private int _baseAttackPower;
	private float _time;
	private float _tickTimer;
	private float _presence;
	private ShaderMaterial _material;
	private ColorRect _rect;
	private Area2D _area;
	private CollisionShape2D _hitShape;
	private readonly System.Collections.Generic.HashSet<ulong> _touched = new();

	public void Setup(float direction, int attackPower, bool fire = false) =>
		Setup(new Vector2(direction >= 0f ? 1f : -1f, 0f), attackPower, fire);

	// travel: direction of motion (normalized; non-horizontal for airborne mini tornadoes).
	// sizeScale < 1 = mini. growing = Tornado creciente (grows + hits harder per enemy touched).
	public void Setup(Vector2 travel, int attackPower, bool fire = false, float sizeScale = 1f, bool growing = false)
	{
		_travel = travel.LengthSquared() > 0f ? travel.Normalized() : Vector2.Right;
		AttackPower = attackPower;
		_baseAttackPower = attackPower;
		_fire = fire;
		SizeScale = sizeScale;
		_growing = growing;
		// A growing tornado starts weak and earns its damage back as it grows (see DamageEnemies).
		if (growing)
			AttackPower = Mathf.Max(1, Mathf.RoundToInt(attackPower * sizeScale));
	}

	public override void _Ready()
	{
		_material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		_material.SetShaderParameter("bottom_margin", BottomMargin);
		_material.SetShaderParameter("cone_power", ConePower);
		_material.SetShaderParameter("band_count", BandCount);
		_material.SetShaderParameter("flatten", Flatten);
		_material.SetShaderParameter("rise_speed", RiseSpeed);
		_material.SetShaderParameter("spin_speed", SpinSpeed);
		_material.SetShaderParameter("pix", PixelSize);
		Color[] palette = _fire ? FireColors : WindColors;
		for (int i = 0; i < 4; i++)
			_material.SetShaderParameter($"c{i}", palette[Mathf.Min(i, palette.Length - 1)]);

		_rect = new ColorRect { Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore };
		AddChild(_rect);

		_area = new Area2D { CollisionLayer = 0, CollisionMask = TargetMask, Monitorable = false };
		_hitShape = new CollisionShape2D { Shape = new RectangleShape2D() };
		_area.AddChild(_hitShape);
		AddChild(_area);

		ApplySize();
		UpdateShader();
	}

	// Scales the column's geometry (not the node, so the effect's pixel size stays constant).
	private void ApplySize()
	{
		float s = SizeScale;
		float height = Height * s;
		float topRadius = TopRadius * s;
		float sway = SwayAmplitude * s;
		// Ribbon streaks can overshoot the top radius by up to 70%, plus sway.
		var size = new Vector2(Mathf.Ceil(topRadius * 2f * 1.7f + sway * 2f + 16f), Mathf.Ceil(height + BottomMargin * 2f));

		_material.SetShaderParameter("rect_size", size);
		_material.SetShaderParameter("column_height", height);
		_material.SetShaderParameter("tip_radius", TipRadius * Mathf.Max(s, 0.6f));
		_material.SetShaderParameter("top_radius", topRadius);
		_material.SetShaderParameter("thickness_bottom", ThicknessBottom * Mathf.Max(s, 0.6f));
		_material.SetShaderParameter("thickness_top", ThicknessTop * s);
		_material.SetShaderParameter("sway", sway);
		_rect.Size = size;
		_rect.Position = new Vector2(-size.X / 2f, -(size.Y - BottomMargin));

		((RectangleShape2D)_hitShape.Shape).Size = new Vector2(HitHalfWidth * 2f * s, height);
		_hitShape.Position = new Vector2(0f, -height / 2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;

		// Grow in, hold, fade out.
		_presence = _time < GrowTime ? _time / GrowTime
			: _time > Lifetime - FadeTime ? Mathf.Max(0f, (Lifetime - _time) / FadeTime)
			: 1f;
		Modulate = new Color(1f, 1f, 1f, _time > Lifetime - FadeTime ? _presence : 1f);
		if (_time >= Lifetime)
		{
			QueueFree();
			return;
		}

		Position += _travel * Speed * dt;
		UpdateShader();

		_tickTimer -= dt;
		if (_tickTimer <= 0f)
		{
			_tickTimer = TickInterval;
			DamageEnemies();
		}
	}

	private void UpdateShader()
	{
		// Grows bottom-up while appearing; stays full height while fading (alpha handles that).
		float grown = _time < GrowTime ? _time / GrowTime : 1f;
		_material.SetShaderParameter("presence", grown);
		_material.SetShaderParameter("t_time", _time);
	}

	private void DamageEnemies()
	{
		bool grew = false;
		foreach (Node2D body in _area.GetOverlappingBodies())
		{
			if (body is not Enemy enemy || enemy.Stats is null)
				continue;
			enemy.Stats.TakeDamage(AttackPower, ignoreInvulnerability: true, armInvulnerability: false);
			if (_growing && _touched.Add(enemy.GetInstanceId()) && SizeScale < MaxSizeScale)
			{
				SizeScale = Mathf.Min(MaxSizeScale, SizeScale + GrowPerEnemy);
				grew = true;
			}
		}
		if (grew)
		{
			// Damage follows size: a tornado that has grown back to full size hits like a normal one.
			AttackPower = Mathf.Max(1, Mathf.RoundToInt(_baseAttackPower * SizeScale));
			ApplySize();
		}
	}
}
