using Godot;

namespace Metroidvania.World;

// The shadow-mark stack floating over a marked enemy: one dark, violet-rimmed shard per mark, with a
// wisp of smoke rising off them, pulsing faster as the marks are about to fade. Also owns the
// fade-out timer (Expired) so it runs regardless of what the enemy's own _PhysicsProcess does.
public partial class ShadowMarkVisual : Node2D
{
	[Signal] public delegate void ExpiredEventHandler();

	public Color RimColor = new(0.7f, 0.35f, 1f, 1f);
	public Color CoreColor = new(0.04f, 0.02f, 0.07f, 1f);

	private int _marks;
	private float _timeLeft;
	private float _duration = 1f;
	private float _pop;

	public void SetMarks(int marks, float duration)
	{
		if (marks > _marks)
			_pop = 1f;
		_marks = marks;
		_timeLeft = duration;
		_duration = Mathf.Max(0.01f, duration);
		Visible = marks > 0;
		QueueRedraw();
	}

	public override void _Ready() => ZIndex = 30;

	public override void _Process(double delta)
	{
		if (_marks <= 0)
			return;
		float dt = (float)delta;
		_pop = Mathf.Max(0f, _pop - dt * 4f);
		_timeLeft -= dt;
		if (_timeLeft <= 0f)
		{
			EmitSignal(SignalName.Expired);
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_marks <= 0)
			return;
		float t = Time.GetTicksMsec() / 1000f;
		// Last third of the duration: the pulse speeds up so the player sees the marks slipping.
		float urgency = _timeLeft < _duration / 3f ? 14f : 5f;
		float pulse = 0.75f + 0.25f * Mathf.Sin(t * urgency);
		const float spacing = 11f;
		float start = -(_marks - 1) * spacing / 2f;
		for (int i = 0; i < _marks; i++)
		{
			Vector2 c = new(start + i * spacing, Mathf.Sin(t * 3f + i) * 1.5f);
			float s = (6f + 3f * _pop) * pulse;
			Vector2[] shard = { c + new Vector2(0f, -s * 1.4f), c + new Vector2(s * 0.7f, 0f), c + new Vector2(0f, s * 1.1f), c + new Vector2(-s * 0.7f, 0f) };
			Vector2[] glow = System.Array.ConvertAll(shard, p => c + (p - c) * 1.6f);
			DrawColoredPolygon(glow, RimColor with { A = 0.25f * pulse });
			DrawColoredPolygon(shard, CoreColor);
			DrawPolyline(new[] { shard[0], shard[1], shard[2], shard[3], shard[0] }, RimColor with { A = 0.9f }, 1.5f);
			// A thread of smoke curling up off each shard.
			for (int k = 1; k <= 3; k++)
			{
				float rise = (t * 18f + k * 9f + i * 5f) % 26f;
				Vector2 w = c + new Vector2(Mathf.Sin(t * 2f + k + i) * 3f, -s * 1.4f - rise);
				DrawCircle(w, 2.2f * (1f - rise / 26f), CoreColor with { A = 0.6f * (1f - rise / 26f) });
			}
		}
	}
}
