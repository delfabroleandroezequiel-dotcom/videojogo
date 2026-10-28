using Godot;

namespace Metroidvania.World;

// Farol implosivo (after Dead Cells' cavern lanterns): a hanging lantern that wakes when the player
// is inside Radius while airborne — it flares, sucks them toward it for ChargeTime, then bursts.
// Stay on the ground near it, or get out of the pull, and it never catches you. Recharges after
// RechargeTime. Origin = the lantern; it hangs on a chain ChainLength up.
public partial class ImplodingLantern : Node2D
{
	[Export] public float Radius = 160f;
	[Export] public float ChargeTime = 0.8f;
	[Export] public float PullSpeed = 150f;
	[Export] public float BlastRadius = 95f;
	[Export] public int Damage = 30;
	[Export] public float Knockback = 420f;
	[Export] public float RechargeTime = 3f;
	[Export] public float ChainLength = 80f;
	[Export] public Color GlowColor = new(1f, 0.75f, 0.3f, 1f);

	private enum State { Ready, Charging, Spent }

	private State _state = State.Ready;
	private float _timer;
	private Node2D _body;
	private PointLight2D _light;
	private Sprite2D _halo;
	private GpuParticles2D _suck;

	public override void _Ready()
	{
		Build();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		var player = TrapKit.FindPlayer(this) as CharacterBody2D;
		float t = (float)Time.GetTicksMsec() / 1000f;
		_body.Rotation = Mathf.Sin(t * 1.4f) * 0.06f;

		switch (_state)
		{
			case State.Ready:
				_light.Energy = 0.9f + Mathf.Sin(t * 5f) * 0.1f;
				if (player is not null && !player.IsOnFloor() && player.GlobalPosition.DistanceTo(GlobalPosition) <= Radius)
				{
					_state = State.Charging;
					_timer = ChargeTime;
					_suck.Emitting = true;
				}
				break;
			case State.Charging:
				_timer -= dt;
				float k = 1f - _timer / ChargeTime;
				_light.Energy = 1f + 3f * k;
				_halo.Scale = Vector2.One * (0.5f + 0.6f * k) * (Mathf.Sin(t * 50f) > 0f ? 1f : 0.85f);
				_body.Scale = Vector2.One * (1f - 0.25f * k);
				if (player is not null && player.GlobalPosition.DistanceTo(GlobalPosition) <= Radius * 1.3f && player.HasMethod("AddExternalDisplacement"))
				{
					Vector2 dir = (GlobalPosition - player.GlobalPosition).Normalized();
					player.Call("AddExternalDisplacement", dir * PullSpeed * dt);
				}
				if (_timer <= 0f)
					Explode(player);
				break;
			case State.Spent:
				_timer -= dt;
				if (_timer <= 0f)
				{
					_state = State.Ready;
					Tween tw = CreateTween();
					tw.TweenProperty(_body, "modulate", Colors.White, 0.4f);
					tw.Parallel().TweenProperty(_body, "scale", Vector2.One, 0.4f);
				}
				break;
		}
	}

	private void Explode(Node2D player)
	{
		_state = State.Spent;
		_timer = RechargeTime;
		_suck.Emitting = false;
		_light.Energy = 0.15f;
		_halo.Scale = Vector2.One * 0.3f;
		_body.Modulate = new Color(0.35f, 0.3f, 0.3f, 1f);
		TrapKit.Blast(this, GlobalPosition, BlastRadius, GlowColor);
		TrapKit.Shake(this, 6f, 0.25f);
		if (player is not null && player.GlobalPosition.DistanceTo(GlobalPosition) <= BlastRadius + 16f)
			TrapKit.Hurt(player, Damage, player.GlobalPosition - GlobalPosition, Knockback);
	}

	private void Build()
	{
		AddChild(TrapKit.Line(new[] { new Vector2(0f, -ChainLength), new Vector2(0f, -16f) }, 2f, new Color(0.3f, 0.3f, 0.33f), false));
		_body = new Node2D();
		AddChild(_body);
		_body.AddChild(TrapKit.Poly(new[] { new Vector2(-8f, -16f), new Vector2(8f, -16f), new Vector2(13f, -6f), new Vector2(13f, 10f), new Vector2(7f, 16f), new Vector2(-7f, 16f), new Vector2(-13f, 10f), new Vector2(-13f, -6f) }, new Color(0.18f, 0.16f, 0.14f)));
		_body.AddChild(TrapKit.Poly(TrapKit.Rect(new Vector2(0f, 2f), new Vector2(16f, 18f)), GlowColor));
		_halo = new Sprite2D { Texture = TrapKit.Dot(), Material = TrapKit.Additive(), Modulate = GlowColor with { A = 0.8f }, Scale = Vector2.One * 0.5f };
		AddChild(_halo);
		_light = TrapKit.Light(GlowColor, 1f, 2.2f);
		AddChild(_light);
		_suck = new GpuParticles2D
		{
			Amount = 40,
			Lifetime = 0.5,
			Emitting = false,
			Texture = TrapKit.Dot(),
			Material = TrapKit.Additive(),
			Modulate = GlowColor,
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
				EmissionRingRadius = Radius * 0.8f,
				EmissionRingInnerRadius = Radius * 0.6f,
				EmissionRingHeight = 1f,
				EmissionRingAxis = new Vector3(0f, 0f, 1f),
				RadialAccelMin = -900f,
				RadialAccelMax = -700f,
				Gravity = Vector3.Zero,
				ScaleMin = 0.03f,
				ScaleMax = 0.06f,
			},
		};
		AddChild(_suck);
	}
}
