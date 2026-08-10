using Godot;

namespace Metroidvania.World;

// Forwards LightPower to the child FlickerLight's BaseEnergy so each placed instance (lantern,
// candles, etc.) can be tuned straight from the root node's own Inspector, instead of needing
// "Editable Children" enabled just to reach the nested Light node every time.
// [Tool] so the change previews live while placing/tuning it in the editor.
[Tool]
public partial class LitDecoration : Node2D
{
	private float _lightPower = 1.5f;

	[Export]
	public float LightPower
	{
		get => _lightPower;
		set { _lightPower = value; ApplyLightPower(); }
	}

	// Null until either the Inspector sets it or _Ready adopts whatever the child FlickerLight's
	// own TextureScale already was — unlike LightPower's fixed literal default, this can't just
	// hardcode e.g. 1.3, because several already-placed instances (CuevaLantern, etc.) set the
	// child's texture_scale directly to their own tuned value before this property existed; a
	// fixed default would silently shrink/grow their light range the moment this script runs.
	private float? _lightRange;

	[Export]
	public float LightRange
	{
		get => _lightRange ?? 1f;
		set { _lightRange = value; ApplyLightRange(); }
	}

	public override void _Ready()
	{
		ApplyLightPower();
		ApplyLightRange();
	}

	private void ApplyLightPower()
	{
		if (!IsInsideTree())
			return;

		FlickerLight light = GetNodeOrNull<FlickerLight>("Light");
		if (light is not null)
			light.BaseEnergy = _lightPower;
	}

	private void ApplyLightRange()
	{
		if (!IsInsideTree())
			return;

		FlickerLight light = GetNodeOrNull<FlickerLight>("Light");
		if (light is null)
			return;

		// Targets BaseTextureScale (the stable value FlickerLight's own flicker wobbles around
		// each frame), not TextureScale directly — the live PointLight2D property gets overwritten
		// by FlickerLight._Process on the very next frame regardless of what's set here.
		_lightRange ??= light.BaseTextureScale;
		light.BaseTextureScale = _lightRange.Value;
	}
}
