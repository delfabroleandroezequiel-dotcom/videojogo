using Godot;

namespace Metroidvania.UI;

// The player's shadow essence (the shadow spells' mana, filled by parrying), drawn in code under the
// Jekyria health/stamina piece — that art only has two rails. A neon violet strip: it pops brighter
// for a moment whenever essence comes in, and breathes softly while full.
public partial class ShadowEssenceBar : Control
{
	[Export] public float BarWidth = 160f;
	[Export] public float BarHeight = 6f;
	// Lines the strip up under the stamina rail (its left end, at the Jekyria piece's 0.45 scale).
	[Export] public float LeftInset = 78f;
	[Export] public Color FillColor = new(0.62f, 0.32f, 1f, 1f);
	[Export] public Color BackColor = new(0.06f, 0.04f, 0.09f, 0.85f);
	[Export] public Color EdgeColor = new(0.35f, 0.25f, 0.5f, 0.9f);
	[Export] public float GainFlashTime = 0.35f;

	private float _ratio;
	private float _shownRatio;
	private float _flash;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(LeftInset + BarWidth + 4f, BarHeight + 6f);
	}

	public void SetRatio(float ratio)
	{
		ratio = Mathf.Clamp(ratio, 0f, 1f);
		if (ratio > _ratio + 0.001f)
			_flash = GainFlashTime;
		_ratio = ratio;
	}

	private ulong _lastTicksUsec;

	public override void _Process(double delta)
	{
		// Real time, not scaled delta: the parry's own hit-stop must not stall the bar it's filling.
		ulong now = Time.GetTicksUsec();
		float dt = _lastTicksUsec == 0 ? (float)delta : Mathf.Min((now - _lastTicksUsec) / 1_000_000f, 0.1f);
		_lastTicksUsec = now;
		_shownRatio = Mathf.MoveToward(_shownRatio, _ratio, dt * (_ratio > _shownRatio ? 2.5f : 4f));
		_flash = Mathf.Max(0f, _flash - dt);
		QueueRedraw();
	}

	public override void _Draw()
	{
		var outer = new Rect2(LeftInset - 1f, 2f, BarWidth + 2f, BarHeight + 2f);
		DrawRect(outer, EdgeColor, false, 1f);
		var back = new Rect2(LeftInset, 3f, BarWidth, BarHeight);
		DrawRect(back, BackColor);

		if (_shownRatio <= 0f)
			return;
		float flash = _flash / GainFlashTime;
		bool full = _ratio >= 0.999f;
		float breathe = full ? 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 1000f * 4f) : 0f;
		float glow = Mathf.Max(flash, breathe * 0.6f);

		var fill = new Rect2(LeftInset, 3f, BarWidth * _shownRatio, BarHeight);
		Color c = FillColor.Lerp(Colors.White, 0.45f * flash);
		DrawRect(fill.Grow(1.5f + 2f * glow), FillColor with { A = 0.18f + 0.3f * glow });
		DrawRect(fill, c);
		// Bright top line, the neon "tube" highlight.
		DrawLine(fill.Position + new Vector2(0f, 1f), fill.Position + new Vector2(fill.Size.X, 1f), Colors.White with { A = 0.35f + 0.4f * glow }, 1f);
	}
}
