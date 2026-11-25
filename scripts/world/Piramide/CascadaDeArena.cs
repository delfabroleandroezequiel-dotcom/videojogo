using Godot;

namespace Metroidvania.World;

// Cascada de arena (La Pirámide): a column of sand pouring from a slot in the ceiling (the node is the
// slot) down Height px. While it pours it pushes whoever is inside straight down at PushSpeed and
// kills any upward speed — you can't jump up through it, and if it catches you mid-jump over a pit it
// slams you into the pit. With SueltaAgarre it also knocks you off ropes and ladders. It doesn't hurt
// by itself (Damage 0): the danger is where it pushes you.
//
// Modes: Ciclo (OffTime quiet → WarningTime a thin trickle + dust, harmless → PourTime full column)
// · Siempre · Manual (Activate() pours for ManualPourTime; a PressurePlate/TrapRelay target).
public partial class CascadaDeArena : Node2D
{
	public enum Modo { Ciclo, Siempre, Manual }

	[Export] public Modo Mode = Modo.Ciclo;
	[Export] public float Width = 70f;
	[Export] public float Height = 400f;
	[Export] public float OffTime = 2.2f;
	[Export] public float WarningTime = 0.8f;
	[Export] public float PourTime = 2.0f;
	[Export] public float StartOffset;
	[Export] public float ManualPourTime = 2.5f;
	[Export] public float PushSpeed = 420f;
	[Export] public bool SueltaAgarre = true;
	[Export] public int Damage;
	[Export] public Color SandColor = new(0.86f, 0.7f, 0.42f, 1f);

	private Area2D _area;
	private float _t;
	private float _manualLeft;
	private float _time;
	private float _rehit;
	private CpuParticles2D _grains;
	private CpuParticles2D _dust;

	// 0 = quiet, 1 = trickle (warning), 2 = pouring.
	public int State { get; private set; }

	public override void _Ready()
	{
		_t = StartOffset;
		ZIndex = 2;
		_area = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitoring = true };
		_area.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(Width, Height) },
			Position = new Vector2(0f, Height / 2f),
		});
		AddChild(_area);

		_grains = new CpuParticles2D
		{
			Emitting = false,
			Amount = 70,
			Lifetime = Mathf.Sqrt(2f * Height / 1400f) + 0.05f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(Width / 2f, 2f),
			Direction = Vector2.Down,
			Spread = 3f,
			Gravity = new Vector2(0f, 1400f),
			InitialVelocityMin = 60f,
			InitialVelocityMax = 140f,
			ScaleAmountMin = 1.5f,
			ScaleAmountMax = 3f,
			Color = SandColor,
		};
		AddChild(_grains);
		_dust = new CpuParticles2D
		{
			Emitting = false,
			Amount = 24,
			Lifetime = 0.7f,
			Position = new Vector2(0f, Height),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(Width / 2f, 2f),
			Direction = Vector2.Up,
			Spread = 70f,
			Gravity = new Vector2(0f, 120f),
			InitialVelocityMin = 40f,
			InitialVelocityMax = 110f,
			ScaleAmountMin = 3f,
			ScaleAmountMax = 6f,
			Color = new Color(SandColor, 0.45f),
		};
		AddChild(_dust);
	}

	public void Activate()
	{
		if (Mode == Modo.Manual)
			_manualLeft = ManualPourTime;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_rehit = Mathf.Max(0f, _rehit - dt);
		State = ComputeState(dt);
		_grains.Emitting = State > 0;
		_grains.Amount = State == 2 ? 70 : 70;
		_grains.EmissionRectExtents = new Vector2(State == 2 ? Width / 2f : 6f, 2f);
		_dust.Emitting = State == 2;

		if (State == 2)
		{
			foreach (Node2D body in _area.GetOverlappingBodies())
			{
				if (!body.IsInGroup("player"))
					continue;
				if (SueltaAgarre && body is Metroidvania.Player.Player pl && pl.EstaAgarrado)
					pl.SoltarAgarre(0f);
				if (body is CharacterBody2D cb && cb.Velocity.Y < 0f)
					cb.Velocity = new Vector2(cb.Velocity.X * 0.7f, 0f);
				if (body.HasMethod("AddExternalDisplacement"))
					body.Call("AddExternalDisplacement", new Vector2(0f, PushSpeed * dt));
				if (Damage > 0 && _rehit <= 0f && TrapKit.Hurt(body, Damage, Vector2.Down, 0f))
					_rehit = 0.8f;
			}
		}
		QueueRedraw();
	}

	private int ComputeState(float dt)
	{
		switch (Mode)
		{
			case Modo.Siempre:
				return 2;
			case Modo.Manual:
				if (_manualLeft <= 0f)
					return 0;
				_manualLeft -= dt;
				return 2;
			default:
				float total = OffTime + WarningTime + PourTime;
				_t = (_t + dt) % total;
				if (_t < OffTime)
					return 0;
				return _t < OffTime + WarningTime ? 1 : 2;
		}
	}

	public override void _Draw()
	{
		// The slot in the ceiling is always drawn: you learn to look up for it.
		DrawRect(new Rect2(-Width / 2f - 8f, -6f, Width + 16f, 12f), new Color(0.12f, 0.1f, 0.08f, 1f));
		DrawRect(new Rect2(-Width / 2f - 8f, -6f, Width + 16f, 3f), new Color(SandColor, 0.5f));
		if (State == 1)
		{
			float w = 4f + 2f * Mathf.Sin(_time * 25f);
			DrawRect(new Rect2(-w / 2f, 0f, w, Height), new Color(SandColor, 0.55f));
			return;
		}
		if (State != 2)
			return;
		int strands = Mathf.Max(3, (int)(Width / 10f));
		for (int i = 0; i < strands; i++)
		{
			float x = -Width / 2f + Width * (i + 0.5f) / strands;
			float wob = 2f * Mathf.Sin(_time * 7f + i * 1.9f);
			float a = 0.55f + 0.25f * Mathf.Sin(_time * 5f + i);
			DrawLine(new Vector2(x + wob, 0f), new Vector2(x - wob, Height), new Color(SandColor, a), Width / strands + 1f);
		}
		DrawRect(new Rect2(-Width / 2f, Height - 10f, Width, 10f), new Color(SandColor.Darkened(0.15f), 0.8f));
	}
}
