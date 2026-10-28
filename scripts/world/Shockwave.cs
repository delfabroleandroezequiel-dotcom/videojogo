using Godot;

namespace Metroidvania.World;

// Onda expansiva (after Inside's shockwave sections): a device that charges for ChargeTime, then
// blasts a wavefront across Range px along Direction. When the front passes the player, they're
// hit unless something solid stands between the device and them — so the room is about dashing
// from cover to cover between blasts. Origin = the emitter (mount it on a wall, roughly at chest
// height of the corridor).
public partial class Shockwave : Node2D
{
	[Export] public int Direction = 1;
	[Export] public float Range = 1100f;
	[Export] public float BandHeight = 260f;
	[Export] public float Interval = 3.2f;
	[Export] public float ChargeTime = 1.4f;
	[Export] public float FrontSpeed = 1100f;
	[Export] public float StartOffset;
	[Export] public int Damage = 35;
	[Export] public float Knockback = 520f;
	[Export] public Color WaveColor = new(0.75f, 0.9f, 1f, 1f);

	private float _timer;
	private bool _running;
	private float _front;
	private bool _hit;
	private Node2D _wave;
	private Sprite2D _core;
	private PointLight2D _light;
	private GpuParticles2D _charge;

	public override void _Ready()
	{
		_timer = Mathf.PosMod(StartOffset, Interval);
		AddChild(TrapKit.Poly(TrapKit.Rect(new Vector2(-Direction * 10f, 0f), new Vector2(24f, 70f)), new Color(0.15f, 0.16f, 0.2f)));
		_core = new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = WaveColor with { A = 0.3f }, Scale = Vector2.One * 0.3f };
		AddChild(_core);
		_light = TrapKit.Light(WaveColor, 0.4f, 1.5f);
		AddChild(_light);
		_charge = new GpuParticles2D
		{
			Amount = 30, Lifetime = 0.4, Emitting = false, Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = WaveColor,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring, EmissionRingRadius = 70f, EmissionRingInnerRadius = 50f, EmissionRingHeight = 1f, EmissionRingAxis = new Vector3(0f, 0f, 1f),
				RadialAccelMin = -500f, RadialAccelMax = -400f, Gravity = Vector3.Zero, ScaleMin = 0.03f, ScaleMax = 0.05f,
			},
		};
		AddChild(_charge);
		_wave = new Node2D { Visible = false };
		AddChild(_wave);
		float h = BandHeight / 2f;
		_wave.AddChild(TrapKit.Line(new[] { new Vector2(0f, -h), new Vector2(0f, h) }, 26f, WaveColor with { A = 0.3f }));
		_wave.AddChild(TrapKit.Line(new[] { new Vector2(0f, -h), new Vector2(0f, h) }, 5f, Colors.White));
		_wave.AddChild(TrapKit.Light(WaveColor, 1.6f, BandHeight / 80f));
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (!_running)
		{
			_timer += dt;
			bool charging = _timer >= Interval - ChargeTime;
			_charge.Emitting = charging;
			float k = charging ? (_timer - (Interval - ChargeTime)) / ChargeTime : 0f;
			_core.Scale = Vector2.One * (0.3f + 0.6f * k);
			_core.Modulate = WaveColor with { A = 0.3f + 0.7f * k };
			_light.Energy = 0.4f + 2f * k;
			if (charging && k > 0.6f)
				TrapKit.Shake(this, 1.5f, 0.05f);
			if (_timer >= Interval)
			{
				_timer = 0f;
				_running = true;
				_front = 0f;
				_hit = false;
				_wave.Visible = true;
				TrapKit.Shake(this, 6f, 0.3f);
			}
			return;
		}

		float prev = _front;
		_front += FrontSpeed * dt;
		_wave.Position = new Vector2(Direction * _front, 0f);
		_wave.Modulate = new Color(1f, 1f, 1f, 1f - _front / Range * 0.6f);
		var player = TrapKit.FindPlayer(this);
		if (!_hit && player is not null)
		{
			Vector2 local = ToLocal(player.GlobalPosition);
			float along = local.X * Direction;
			if (along >= prev && along < _front && Mathf.Abs(local.Y) <= BandHeight / 2f && !Covered(player))
				_hit = TrapKit.Hurt(player, Damage, new Vector2(Direction, -0.4f), Knockback);
		}
		if (_front >= Range)
		{
			_running = false;
			_wave.Visible = false;
		}
	}

	// Something solid between the emitter and the player's body (checked at chest and head height).
	private bool Covered(Node2D player)
	{
		var space = GetWorld2D().DirectSpaceState;
		foreach (float dy in new[] { -10f, -30f })
		{
			Vector2 target = player.GlobalPosition + new Vector2(0f, dy);
			Vector2 from = new(GlobalPosition.X, target.Y);
			var q = PhysicsRayQueryParameters2D.Create(from, target, 1);
			if (space.IntersectRay(q).Count == 0)
				return false;
		}
		return true;
	}
}
