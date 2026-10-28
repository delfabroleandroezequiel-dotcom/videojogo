using Godot;

namespace Metroidvania.World;

// Ráfaga (after Celeste's wind): a horizontal wind zone that blows in gusts. Calm for CalmTime,
// streaks warn for WarningTime, then for GustTime it shoves the player along Direction at Strength
// px/s (walking against it barely holds ground, in the air it carries you). Time your jumps between
// gusts. Origin = top-left of the zone.
public partial class WindGust : Node2D
{
	[Export] public Vector2 Size = new(700f, 260f);
	[Export] public int Direction = 1;
	[Export] public float Strength = 230f;
	[Export] public float CalmTime = 1.8f;
	[Export] public float WarningTime = 0.6f;
	[Export] public float GustTime = 1.6f;
	[Export] public float StartOffset;
	[Export] public Color WindColor = new(0.8f, 0.9f, 1f, 1f);

	private float _cycle;
	private GpuParticles2D _streaks;
	private GpuParticles2D _hint;

	private float Cycle => CalmTime + WarningTime + GustTime;

	public override void _Ready()
	{
		_cycle = Mathf.PosMod(StartOffset, Cycle);
		AddChild(TrapKit.Poly(TrapKit.Rect(Size / 2f, Size), WindColor with { A = 0.05f }));
		_streaks = MakeStreaks(60, 900f, 0.8f);
		_hint = MakeStreaks(10, 300f, 0.35f);
	}

	private GpuParticles2D MakeStreaks(int amount, float speed, float alpha)
	{
		var p = new GpuParticles2D
		{
			Amount = amount,
			Lifetime = Size.X / speed,
			Emitting = false,
			Texture = TrapKit.Dot(),
			Material = TrapKit.Additive(),
			Modulate = WindColor with { A = alpha },
			Position = new Vector2(Direction > 0 ? 0f : Size.X, Size.Y / 2f),
			VisibilityRect = new Rect2(-Size.X - 50f, -Size.Y, Size.X * 2f + 100f, Size.Y * 2f),
			ProcessMaterial = new ParticleProcessMaterial
			{
				EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
				EmissionBoxExtents = new Vector3(4f, Size.Y / 2f, 0f),
				Direction = new Vector3(Direction, 0f, 0f),
				Spread = 2f,
				InitialVelocityMin = speed * 0.8f,
				InitialVelocityMax = speed,
				Gravity = Vector3.Zero,
				ScaleMin = 0.02f,
				ScaleMax = 0.035f,
			},
		};
		AddChild(p);
		return p;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_cycle = Mathf.PosMod(_cycle + dt, Cycle);
		bool warning = _cycle >= CalmTime && _cycle < CalmTime + WarningTime;
		bool gust = _cycle >= CalmTime + WarningTime;
		_hint.Emitting = warning;
		_streaks.Emitting = gust;
		if (!gust)
			return;
		var player = TrapKit.FindPlayer(this);
		if (player is not null && new Rect2(GlobalPosition, Size).HasPoint(player.GlobalPosition) && player.HasMethod("AddExternalDisplacement"))
			player.Call("AddExternalDisplacement", new Vector2(Direction * Strength * dt, 0f));
	}
}
