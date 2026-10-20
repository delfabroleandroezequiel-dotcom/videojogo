using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A timed lightning beam between an emitter and a receiver: off → warning (sparks at the emitter
// and a thin flickering line) → a jagged neon bolt that hurts whoever stands in it → off again.
// Aim it by rotating the node: the beam runs along the node's local +Y for Length pixels, so a
// default instance on a ceiling fires straight down. StartDelay offsets the cycle, so a row of them
// can fire in sequence. Drawn entirely in code (glow + bolt + white core Line2Ds, a light, spark
// particles), like the rest of the neon kit. [Tool] only to preview the beam's reach in the editor.
[Tool]
public partial class LightningBeamTrap : Node2D
{
	private float _length = 240f;

	[Export]
	public float Length
	{
		get => _length;
		set
		{
			_length = Mathf.Max(16f, value);
			QueueRedraw();
		}
	}

	[ExportGroup("Timing")]
	[Export] public float OffTime = 1.8f;
	[Export] public float WarningTime = 0.6f;
	[Export] public float OnTime = 1.2f;
	// Seconds into the cycle this instance starts at (stagger a row of beams).
	[Export] public float StartDelay;

	[ExportGroup("Damage")]
	[Export] public int Damage = 20;
	[Export] public float KnockbackForce = 260f;
	[Export] public bool HurtsEnemies = true;
	// Width of the hurting strip around the bolt.
	[Export] public float Thickness = 16f;
	// Per-body pause between hits while standing in the beam.
	[Export] public float RehitDelay = 0.6f;

	[ExportGroup("Look")]
	[Export] public Color BeamColor = new(0.45f, 0.8f, 1f, 1f);
	[Export] public float BoltJitter = 6f;
	[Export] public float BoltSegment = 12f;
	[Export] public float BoltRefresh = 0.05f;
	[Export] public float LightEnergy = 1.4f;
	// Sfx category/key played when the bolt fires (empty key = silent).
	[Export] public string SoundCategory = "Magic";
	[Export] public string SoundKey = "Spell Impact";

	private enum Phase { Off, Warning, On }

	private Phase _phase = Phase.Off;
	private float _cycleTime;
	private float _boltTimer;
	private Line2D _warningLine;
	private Line2D _glow;
	private Line2D _bolt;
	private Line2D _core;
	private PointLight2D _light;
	private GpuParticles2D _emitterSparks;
	private GpuParticles2D _receiverSparks;
	private Area2D _hitArea;
	private readonly Dictionary<ulong, float> _rehitTimers = new();
	private readonly RandomNumberGenerator _rng = new();

	private float Cycle => OffTime + WarningTime + OnTime;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		_rng.Randomize();
		Build();
		_cycleTime = Mathf.PosMod(StartDelay, Mathf.Max(0.01f, Cycle));
		SetPhase(PhaseAt(_cycleTime), silent: true);
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		// Editor preview: the beam's reach as a dashed line, the emitter and receiver as dots.
		DrawDashedLine(Vector2.Zero, new Vector2(0f, _length), BeamColor, 2f, 8f);
		DrawCircle(Vector2.Zero, 6f, BeamColor.Darkened(0.4f));
		DrawCircle(new Vector2(0f, _length), 6f, BeamColor.Darkened(0.4f));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		_cycleTime = Mathf.PosMod(_cycleTime + dt, Mathf.Max(0.01f, Cycle));
		Phase phase = PhaseAt(_cycleTime);
		if (phase != _phase)
			SetPhase(phase, silent: false);

		switch (_phase)
		{
			case Phase.Warning:
				// Flicker faster as the strike gets closer.
				float t = (_cycleTime - OffTime) / Mathf.Max(0.01f, WarningTime);
				float flicker = Mathf.Sin(_cycleTime * Mathf.Lerp(18f, 60f, t)) > 0f ? 1f : 0.25f;
				_warningLine.Modulate = BeamColor with { A = 0.55f * flicker };
				break;
			case Phase.On:
				_boltTimer -= dt;
				if (_boltTimer <= 0f)
				{
					_boltTimer = BoltRefresh;
					RedrawBolt();
				}
				_light.Energy = LightEnergy * _rng.RandfRange(0.75f, 1.15f);
				HurtOverlapping(dt);
				break;
		}
	}

	private Phase PhaseAt(float time) =>
		time < OffTime ? Phase.Off : time < OffTime + WarningTime ? Phase.Warning : Phase.On;

	private void SetPhase(Phase phase, bool silent)
	{
		_phase = phase;
		_warningLine.Visible = phase == Phase.Warning;
		_emitterSparks.Emitting = phase != Phase.Off;
		bool on = phase == Phase.On;
		_glow.Visible = on;
		_bolt.Visible = on;
		_core.Visible = on;
		_light.Visible = on;
		_receiverSparks.Emitting = on;
		_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, on);
		if (on)
		{
			_boltTimer = 0f;
			_rehitTimers.Clear();
			if (!silent && !string.IsNullOrEmpty(SoundKey))
				Sfx.PlayAt(this, SoundCategory, SoundKey);
		}
	}

	// ── Damage ──

	private void HurtOverlapping(float dt)
	{
		var keys = new List<ulong>(_rehitTimers.Keys);
		foreach (ulong id in keys)
			_rehitTimers[id] -= dt;

		foreach (Node2D body in _hitArea.GetOverlappingBodies())
		{
			bool isPlayer = body.IsInGroup("player");
			if (!isPlayer && !(HurtsEnemies && body.IsInGroup("enemy")))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable || stats.CurrentHealth <= 0)
				continue;
			ulong id = body.GetInstanceId();
			if (_rehitTimers.TryGetValue(id, out float wait) && wait > 0f)
				continue;
			_rehitTimers[id] = RehitDelay;

			stats.TakeDamage(Damage);
			if (body.HasMethod("ApplyKnockback"))
			{
				// Thrown sideways out of the beam, away from its centre line.
				float side = ToLocal(body.GlobalPosition).X >= 0f ? 1f : -1f;
				Vector2 push = GlobalTransform.BasisXform(new Vector2(side, 0f)).Normalized() + Vector2.Up * 0.3f;
				body.Call("ApplyKnockback", push.Normalized(), KnockbackForce);
			}
		}
	}

	// ── Visuals ──

	private void Build()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");

		AddChild(MakeCap(Vector2.Zero, flip: false, dot, additive));
		AddChild(MakeCap(new Vector2(0f, _length), flip: true, dot, additive));

		_warningLine = new Line2D
		{
			Points = new[] { Vector2.Zero, new Vector2(0f, _length) },
			Width = 1.5f,
			DefaultColor = Colors.White,
			Material = additive,
			Visible = false,
		};
		AddChild(_warningLine);

		_glow = new Line2D { Width = 16f, DefaultColor = BeamColor with { A = 0.35f }, Material = additive, JointMode = Line2D.LineJointMode.Round };
		_bolt = new Line2D { Width = 4f, DefaultColor = BeamColor, Material = additive, JointMode = Line2D.LineJointMode.Sharp };
		_core = new Line2D { Width = 1.5f, DefaultColor = new Color(1f, 1f, 1f, 0.95f), Material = additive };
		AddChild(_glow);
		AddChild(_bolt);
		AddChild(_core);

		_light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = BeamColor,
			Energy = LightEnergy,
			TextureScale = Mathf.Max(1.5f, _length / 90f),
			Position = new Vector2(0f, _length * 0.5f),
			Visible = false,
		};
		AddChild(_light);

		_emitterSparks = MakeSparks(Vector2.Zero, new Vector3(0f, 1f, 0f), dot, additive);
		_receiverSparks = MakeSparks(new Vector2(0f, _length), new Vector3(0f, -1f, 0f), dot, additive);

		_hitArea = new Area2D
		{
			CollisionLayer = 0,
			CollisionMask = HurtsEnemies ? 2u | 4u : 2u,
			Monitoring = false,
			Monitorable = false,
		};
		_hitArea.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Thickness, _length) },
			Position = new Vector2(0f, _length * 0.5f),
		});
		AddChild(_hitArea);
	}

	// A small dark housing with a glowing lens, facing along the beam.
	private Node2D MakeCap(Vector2 at, bool flip, Texture2D dot, Material additive)
	{
		var cap = new Node2D { Position = at, Scale = new Vector2(1f, flip ? -1f : 1f) };
		cap.AddChild(new Polygon2D
		{
			Color = new Color(0.12f, 0.13f, 0.17f, 1f),
			Polygon = new[] { new Vector2(-10f, -6f), new Vector2(10f, -6f), new Vector2(7f, 4f), new Vector2(-7f, 4f) },
		});
		cap.AddChild(new Line2D
		{
			Points = new[] { new Vector2(-10f, -6f), new Vector2(10f, -6f), new Vector2(7f, 4f), new Vector2(-7f, 4f), new Vector2(-10f, -6f) },
			Width = 1f,
			DefaultColor = new Color(0.35f, 0.38f, 0.45f, 1f),
		});
		cap.AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = BeamColor with { A = 0.8f }, Scale = Vector2.One * 0.12f, Position = new Vector2(0f, 3f) });
		return cap;
	}

	private GpuParticles2D MakeSparks(Vector2 at, Vector3 direction, Texture2D dot, Material additive)
	{
		var sparks = new GpuParticles2D
		{
			Position = at,
			Amount = 14,
			Lifetime = 0.3,
			Emitting = false,
			Texture = dot,
			Material = additive,
			Modulate = BeamColor,
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = direction,
				Spread = 70f,
				InitialVelocityMin = 40f,
				InitialVelocityMax = 120f,
				Gravity = new Vector3(0f, 200f, 0f),
				ScaleMin = 0.03f,
				ScaleMax = 0.07f,
			},
		};
		AddChild(sparks);
		return sparks;
	}

	// A fresh jagged path from emitter to receiver: ends pinned, the middle kinks sideways at random.
	private void RedrawBolt()
	{
		int segments = Mathf.Max(2, Mathf.CeilToInt(_length / BoltSegment));
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float y = _length * i / segments;
			float jitter = i == 0 || i == segments ? 0f : _rng.RandfRange(-BoltJitter, BoltJitter);
			points[i] = new Vector2(jitter, y);
		}
		_glow.Points = points;
		_bolt.Points = points;
		_core.Points = points;
	}
}
