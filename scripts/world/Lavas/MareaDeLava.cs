using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// Marea de lava (Las Lavas): moves a lava pool (an AcidPool child/sibling, Pool) up and down.
// The pool's placed position is low tide; high tide is Rise px above it. Three modes:
//  - Ciclo: low LowTime → warning (bubbles + the surface trembles) → rises in RiseTime → stays
//    HighTime → sinks in FallTime → low again. A room has two states and the player learns to wait.
//  - Fuga: sits at low tide until Activate() (a PressurePlate), then rises at FugaSpeed until it
//    has risen FugaStop px, killing on contact (the escape climb). Back to low after a death
//    (the scene reloads).
//  - Esclusa: starts high and stays high until Drain() (a Lever) lowers it for good; remembered
//    per save under PersistenceId (a flag in SaveManager's collected pickups).
// Geysers read SurfaceY to know when they're drowned. Draw the pool Depth big enough to fill the
// pit at high tide (the visual moves with it).
public partial class MareaDeLava : Node2D
{
	public enum Modo { Ciclo, Fuga, Esclusa }

	[Export] public NodePath Pool;
	[Export] public Modo Mode = Modo.Ciclo;
	[Export] public float Rise = 160f;
	[Export] public float LowTime = 2.4f;
	[Export] public float WarningTime = 0.9f;
	[Export] public float RiseTime = 1.2f;
	[Export] public float HighTime = 2.2f;
	[Export] public float FallTime = 1.4f;
	[Export] public float StartOffset;
	[Export] public float FugaSpeed = 70f;
	[Export] public float FugaStop = 1400f;
	[Export] public float DrainTime = 3f;
	[Export] public string PersistenceId = "";
	[Export] public Color BubbleColor = new(1f, 0.55f, 0.15f, 1f);

	private AcidPool _pool;
	private float _lowY;
	private float _t;
	private bool _fugaRunning;
	private bool _drained;
	private Tween _drainTween;
	private CpuParticles2D _bubbles;

	public float SurfaceY => _pool?.GlobalPosition.Y ?? GlobalPosition.Y;

	public override void _Ready()
	{
		_pool = GetNodeOrNull<AcidPool>(Pool);
		if (_pool is null)
		{
			GD.PushWarning($"MareaDeLava {Name}: no Pool");
			return;
		}
		_lowY = _pool.Position.Y;
		_t = StartOffset;
		_bubbles = new CpuParticles2D
		{
			Emitting = false,
			Amount = 40,
			Lifetime = 0.6f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_pool.Width / 2f, 2f),
			Direction = Vector2.Up,
			Spread = 20f,
			Gravity = new Vector2(0f, 260f),
			InitialVelocityMin = 50f,
			InitialVelocityMax = 130f,
			ScaleAmountMin = 2f,
			ScaleAmountMax = 4f,
			Color = BubbleColor,
			ZIndex = 3,
		};
		_pool.AddChild(_bubbles);
		_bubbles.Position = new Vector2(_pool.Width / 2f, 0f);

		if (Mode == Modo.Esclusa)
		{
			_drained = !string.IsNullOrEmpty(PersistenceId) && SaveManager.Instance.IsPickupCollected(PersistenceId);
			SetLevel(_drained ? _lowY : _lowY - Rise);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_pool is null)
			return;
		float dt = (float)delta;
		switch (Mode)
		{
			case Modo.Ciclo:
				Cycle(dt);
				break;
			case Modo.Fuga:
				if (!_fugaRunning)
					break;
				float risen = _lowY - _pool.Position.Y;
				if (risen < FugaStop)
					SetLevel(_pool.Position.Y - FugaSpeed * dt);
				_bubbles.Emitting = true;
				break;
		}
	}

	private void Cycle(float dt)
	{
		float total = LowTime + WarningTime + RiseTime + HighTime + FallTime;
		_t = (_t + dt) % total;
		float t = _t;
		float level;
		bool warn = false;
		if (t < LowTime)
			level = 0f;
		else if ((t -= LowTime) < WarningTime)
		{
			level = 0f;
			warn = true;
		}
		else if ((t -= WarningTime) < RiseTime)
			level = Ease(t / RiseTime);
		else if ((t -= RiseTime) < HighTime)
			level = 1f;
		else
			level = 1f - Ease((t - HighTime) / FallTime);
		float tremble = warn ? Mathf.Sin(Time.GetTicksMsec() * 0.06f) * 2f : 0f;
		SetLevel(_lowY - level * Rise + tremble);
		_bubbles.Emitting = warn || (level > 0f && level < 1f);
	}

	private static float Ease(float x) => x * x * (3f - 2f * x);

	private void SetLevel(float y) => _pool.Position = new Vector2(_pool.Position.X, y);

	// Fuga: start the rise (PressurePlate / TrapRelay target).
	public void Activate()
	{
		if (Mode != Modo.Fuga || _fugaRunning || _pool is null)
			return;
		_fugaRunning = true;
		_pool.InstantKill = true;
	}

	// Esclusa: lower it for good (Lever.Activated).
	public void Drain()
	{
		if (_pool is null || _drained)
			return;
		_drained = true;
		if (!string.IsNullOrEmpty(PersistenceId))
			SaveManager.Instance.MarkPickupCollected(PersistenceId);
		_bubbles.Emitting = true;
		_drainTween = CreateTween();
		_drainTween.TweenProperty(_pool, "position:y", _lowY, DrainTime).SetTrans(Tween.TransitionType.Sine);
		_drainTween.TweenCallback(Callable.From(() => _bubbles.Emitting = false));
	}
}
