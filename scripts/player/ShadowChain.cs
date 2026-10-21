using System;
using Godot;

namespace Metroidvania.Player;

// Robo de sombra's "get over here": an electric-yellow neon chain with a kunai head that flies out
// from the player to the target (curving in as if it homed), bites, then — the rip — reels back
// dragging a yellow-and-black silhouette of the enemy's shadow along it. Soft-edged Line2D layers
// (ink, glow, core) like BladeTrail, a jagged electric crackle running the length, a vibrating sag
// while taut. Fires OnHooked when the head arrives and OnRipped when the shadow reaches the player.
// World-space (TopLevel); follows both ends while it lives.
public partial class ShadowChain : Node2D
{
	public Node2D From;
	public Node2D Target;
	public Vector2 FromOffset = new(0f, -18f);
	public Vector2 TargetOffset = new(0f, -18f);
	public Texture2D ShadowFrame;
	public Vector2 ShadowScale = Vector2.One;
	public bool ShadowFlip;
	public Color Glow = new(1f, 0.85f, 0.1f, 1f);
	public Color Core = new(1f, 1f, 0.85f, 1f);
	public Color Ink = new(0.03f, 0.02f, 0f, 1f);
	public float ExtendTime = 0.14f;
	public float HoldTime = 0.1f;
	public float ReelTime = 0.24f;
	public Action OnHooked;
	public Action OnRipped;

	private const int Points = 24;
	private Line2D _ink;
	private Line2D _glow;
	private Line2D _core;
	private Line2D _crackle;
	private Sprite2D _ripped;
	private float _age;
	private bool _hooked;
	private bool _ripped_;
	private Vector2 _lastTarget;

	private Line2D Layer(Color color, float width, bool additive)
	{
		var line = new Line2D
		{
			Width = width,
			DefaultColor = color,
			Texture = BladeTrail.SoftEdgeTexture,
			TextureMode = Line2D.LineTextureMode.Stretch,
			JointMode = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Antialiased = true,
		};
		if (additive)
			line.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		AddChild(line);
		return line;
	}

	public override void _Ready()
	{
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
		ZIndex = 26;
		_ink = Layer(Ink with { A = 0.8f }, 9f, false);
		_glow = Layer(Glow with { A = 0.75f }, 8f, true);
		_core = Layer(Core, 2.2f, true);
		_crackle = new Line2D { Width = 1.4f, DefaultColor = Core, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		AddChild(_crackle);
		if (IsInstanceValid(Target))
			_lastTarget = Target.GlobalPosition + TargetOffset;
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		if (!IsInstanceValid(From))
		{
			QueueFree();
			return;
		}
		Vector2 start = From.GlobalPosition + FromOffset;
		if (IsInstanceValid(Target))
			_lastTarget = Target.GlobalPosition + TargetOffset;

		float reelStart = ExtendTime + HoldTime;
		// Where the head is: flying out, biting, then the shadow coming back along the chain.
		float reach;
		if (_age < ExtendTime)
			reach = 1f - Mathf.Pow(1f - _age / ExtendTime, 3f);
		else if (_age < reelStart)
			reach = 1f;
		else
			reach = 1f - Mathf.Clamp((_age - reelStart) / ReelTime, 0f, 1f);

		if (!_hooked && _age >= ExtendTime)
		{
			_hooked = true;
			OnHooked?.Invoke();
			SpawnRipped();
		}
		if (_hooked && !_ripped_ && _age >= reelStart + ReelTime)
		{
			_ripped_ = true;
			OnRipped?.Invoke();
		}
		if (_age >= reelStart + ReelTime + 0.12f)
		{
			QueueFree();
			return;
		}

		Vector2 end = start.Lerp(_lastTarget, reach);
		// Sags and curls while flying (the homing curve), snaps taut and buzzes while it holds.
		float taut = _age < ExtendTime ? 0f : 1f;
		float sag = Mathf.Lerp(28f, 5f, taut) * reach;
		float buzz = taut * 2.5f;
		Vector2 dir = (end - start);
		Vector2 normal = dir.LengthSquared() > 0.01f ? dir.Normalized().Orthogonal() : Vector2.Up;
		var pts = new Vector2[Points];
		var crackle = new Vector2[Points];
		for (int i = 0; i < Points; i++)
		{
			float t = i / (float)(Points - 1);
			float bow = Mathf.Sin(Mathf.Pi * t);
			Vector2 p = start.Lerp(end, t) + Vector2.Down * sag * bow
				+ normal * Mathf.Sin(_age * 90f + t * 20f) * buzz * bow;
			pts[i] = p;
			crackle[i] = p + normal * (float)GD.RandRange(-4.0, 4.0) * bow;
		}
		_ink.Points = pts;
		_glow.Points = pts;
		_core.Points = pts;
		_crackle.Points = crackle;
		_crackle.Visible = GD.Randf() < 0.7f;
		Modulate = new Color(1f, 1f, 1f, GD.Randf() < 0.1f ? 0.6f : 1f);

		if (_ripped is not null && _hooked)
		{
			// The torn-out shadow rides the chain head back, shuddering.
			_ripped.GlobalPosition = end + new Vector2((float)GD.RandRange(-2.0, 2.0), 18f);
			float pull = Mathf.Clamp((_age - reelStart) / ReelTime, 0f, 1f);
			_ripped.Scale = ShadowScale * Mathf.Lerp(1f, 0.85f, pull);
			_ripped.Modulate = (GD.Randf() < 0.15f ? Core : Glow) with { A = 0.9f };
		}
		QueueRedraw();
	}

	private void SpawnRipped()
	{
		if (ShadowFrame is null)
			return;
		_ripped = new Sprite2D
		{
			Texture = ShadowFrame,
			FlipH = ShadowFlip,
			Scale = ShadowScale,
			Material = NeonSilhouette.CreateMaterial(Glow, 1.8f),
			ZIndex = -1,
		};
		AddChild(_ripped);
	}

	public override void _Draw()
	{
		// Kunai head: a dark blade with a hot rim, pointing along the chain.
		if (_core.Points.Length < 2)
			return;
		Vector2 tip = _core.Points[^1];
		Vector2 back = (_core.Points[^2] - tip).Normalized();
		Vector2 side = back.Orthogonal();
		Vector2[] blade = { tip - back * 6f, tip + back * 10f + side * 5f, tip + back * 6f, tip + back * 10f - side * 5f };
		DrawColoredPolygon(blade, Ink);
		DrawPolyline(new[] { blade[0], blade[1], blade[2], blade[3], blade[0] }, Glow, 1.6f);
	}
}
