using Godot;

namespace Metroidvania.World;

// Homemade (no sprite sheet) flamethrower stream: a single Sprite2D stretched along local +X
// carrying FlameCone.gdshader for the turbulent, flickering cone body, plus embers/smoke
// GpuParticles2D layered on top and a FlickerLight for the warm glow it casts on its
// surroundings. Aim by rotating this node; SetActive ramps the stream in/out from the nozzle
// instead of an instant on/off, so it reads as building up pressure rather than popping into
// existence.
public partial class Flamethrower : Node2D
{
	[Export] public float Range = 170f;
	[Export] public float Width = 46f;
	[Export] public float RampDuration = 0.25f;

	private Sprite2D _cone;
	private GpuParticles2D _embers;
	private GpuParticles2D _smoke;
	private FlickerLight _light;
	private Tween _rampTween;

	public bool IsActive { get; private set; }

	public override void _Ready()
	{
		_cone = GetNode<Sprite2D>("Cone");
		_embers = GetNode<GpuParticles2D>("Embers");
		_smoke = GetNode<GpuParticles2D>("Smoke");
		_light = GetNode<FlickerLight>("FlickerLight");

		ApplyRange();
		SetActive(false, instant: true);
	}

	// Cone is not centered, so its local origin sits at the nozzle (0,0) and it only ever grows
	// to the right — scaling it during the ramp doesn't need to also move its position.
	private void ApplyRange()
	{
		_cone.Position = new Vector2(0f, -Width * 0.5f);
		_embers.Position = new Vector2(10f, 0f);
		_smoke.Position = new Vector2(Range * 0.85f, 0f);
	}

	public void SetActive(bool active, bool instant = false)
	{
		IsActive = active;
		_embers.Emitting = active;
		_smoke.Emitting = active;
		_light.Visible = active;

		_rampTween?.Kill();
		float targetScaleX = active ? Range / 64f : 0f;
		if (instant)
		{
			_cone.Scale = new Vector2(targetScaleX, Width / 64f);
			_cone.Visible = active;
			return;
		}

		_cone.Visible = true;
		_rampTween = CreateTween();
		_rampTween.TweenProperty(_cone, "scale:x", targetScaleX, RampDuration).SetTrans(Tween.TransitionType.Sine);
		if (!active)
			_rampTween.TweenCallback(Callable.From(() => _cone.Visible = false));
	}
}
