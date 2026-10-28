using Godot;

namespace Metroidvania.World;

// Onda eléctrica (after Dead Cells' electric waves): an emitter on the floor that, every Interval,
// crackles for WarningTime and then sends a wall of electricity Height tall racing along the floor
// for Length pixels in Direction. Jump it. Hits once per wave. Origin = the emitter on the floor.
public partial class ElectricWave : Node2D
{
	[Export] public int Direction = 1;
	[Export] public float Length = 600f;
	[Export] public float Speed = 300f;
	[Export] public float Height = 44f;
	[Export] public float Interval = 2.6f;
	[Export] public float WarningTime = 0.5f;
	[Export] public float StartOffset;
	[Export] public int Damage = 20;
	[Export] public float Knockback = 300f;
	[Export] public Color WaveColor = new(0.5f, 0.85f, 1f, 1f);

	private float _cycle;
	private bool _running;
	private float _front;
	private bool _hitThisWave;
	private Node2D _wave;
	private Line2D _bolt;
	private Line2D _glow;
	private PointLight2D _light;
	private GpuParticles2D _sparks;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		_rng.Randomize();
		_cycle = Mathf.PosMod(StartOffset, Interval);
		Build();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (!_running)
		{
			_cycle += dt;
			_sparks.Emitting = _cycle >= Interval - WarningTime;
			if (_cycle >= Interval)
			{
				_cycle = 0f;
				_running = true;
				_front = 0f;
				_hitThisWave = false;
				_wave.Visible = true;
			}
			return;
		}

		_front += Speed * dt;
		_wave.Position = new Vector2(Direction * _front, 0f);
		RedrawBolt();
		_light.Energy = 1.2f + _rng.RandfRange(-0.3f, 0.3f);

		if (!_hitThisWave && TrapKit.FindPlayer(this) is Node2D player)
		{
			Vector2 local = ToLocal(player.GlobalPosition);
			float feet = local.Y + 32f;
			if (Mathf.Abs(local.X - Direction * _front) <= 18f && feet > -Height + 4f && feet <= 8f)
			{
				_hitThisWave = TrapKit.Hurt(player, Damage, new Vector2(Direction, -0.5f), Knockback);
			}
		}
		if (_front >= Length)
		{
			_running = false;
			_wave.Visible = false;
		}
	}

	private void RedrawBolt()
	{
		const int segs = 7;
		var pts = new Vector2[segs + 1];
		for (int i = 0; i <= segs; i++)
			pts[i] = new Vector2(_rng.RandfRange(-7f, 7f), -Height * i / segs);
		_bolt.Points = pts;
		_glow.Points = pts;
	}

	private void Build()
	{
		// the emitter: a small grounded coil
		AddChild(TrapKit.Poly(new[] { new Vector2(-14f, 0f), new Vector2(-10f, -14f), new Vector2(10f, -14f), new Vector2(14f, 0f) }, new Color(0.15f, 0.16f, 0.2f)));
		AddChild(TrapKit.Line(new[] { new Vector2(-8f, -6f), new Vector2(8f, -9f), new Vector2(-8f, -12f) }, 1.5f, WaveColor));
		_sparks = new GpuParticles2D
		{
			Amount = 16, Lifetime = 0.25, Emitting = false, Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = WaveColor,
			Position = new Vector2(0f, -14f),
			ProcessMaterial = new ParticleProcessMaterial { Direction = new Vector3(0f, -1f, 0f), Spread = 90f, InitialVelocityMin = 40f, InitialVelocityMax = 120f, Gravity = new Vector3(0f, 300f, 0f), ScaleMin = 0.03f, ScaleMax = 0.05f },
		};
		AddChild(_sparks);

		_wave = new Node2D { Visible = false };
		AddChild(_wave);
		_glow = TrapKit.Line(System.Array.Empty<Vector2>(), 14f, WaveColor with { A = 0.35f });
		_bolt = TrapKit.Line(System.Array.Empty<Vector2>(), 3f, Colors.White);
		_wave.AddChild(_glow);
		_wave.AddChild(_bolt);
		_light = TrapKit.Light(WaveColor, 1.2f, 1.2f);
		_light.Position = new Vector2(0f, -Height / 2f);
		_wave.AddChild(_light);
		// a faint track showing how far it runs
		AddChild(TrapKit.Line(new[] { Vector2.Zero, new Vector2(Direction * Length, 0f) }, 2f, WaveColor with { A = 0.25f }));
	}
}
