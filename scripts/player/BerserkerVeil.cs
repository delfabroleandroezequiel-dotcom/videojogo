using Godot;

namespace Metroidvania.Player;

// Screen state while the berserker shadow is out: the world drains to a red neon monochrome with a
// vignette that throbs on a double heartbeat (ShadowCastVeil.gdshader, red tint). Kept at a middling
// strength on purpose so the green attack tells stay readable through it. Runs on real time — hit-stops
// mustn't stall the heartbeat. Fades in on creation; Release() fades it out and frees it.
public partial class BerserkerVeil : CanvasLayer
{
	public float BaseStrength = 0.42f;
	public float BeatStrength = 0.18f;
	public float BeatsPerMinute = 96f;
	public Vector3 Tint = new(1f, 0.22f, 0.28f);

	private ShaderMaterial _material;
	private float _fade;
	private bool _releasing;
	private ulong _lastUsec;
	private float _clock;

	public override void _Ready()
	{
		Layer = 1;
		_material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/ShadowCastVeil.gdshader") };
		_material.SetShaderParameter("tint", Tint);
		var rect = new ColorRect { Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(rect);
		_lastUsec = Time.GetTicksUsec();
	}

	public void Release() => _releasing = true;

	public override void _Process(double delta)
	{
		ulong now = Time.GetTicksUsec();
		float dt = Mathf.Min((now - _lastUsec) / 1_000_000f, 0.1f);
		_lastUsec = now;
		_clock += dt;

		_fade = Mathf.MoveToward(_fade, _releasing ? 0f : 1f, dt * (_releasing ? 2.5f : 5f));
		if (_releasing && _fade <= 0f)
		{
			QueueFree();
			return;
		}

		// Lub-dub: two quick pulses, then a rest, once per beat.
		float beat = _clock * BeatsPerMinute / 60f % 1f;
		float pulse = Mathf.Exp(-Mathf.Pow((beat - 0.05f) / 0.05f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((beat - 0.25f) / 0.05f, 2f));
		_material.SetShaderParameter("strength", _fade * (BaseStrength + BeatStrength * pulse));
	}
}
