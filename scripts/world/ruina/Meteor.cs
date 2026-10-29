using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// One burning rock falling from the sky onto Target (procedural, no sprites): first a pulsing
// red mark on the ground (the warning — fair, readable), then the meteor streaks down at an
// angle with a fiery trail, bursts on impact (rock shards, dust, shake), hurts the player if
// they're inside the blast and, unless it's a plain rock, leaves a patch of fire burning there.
public partial class Meteor : Node2D
{
	public Vector2 Target;
	public bool IsRock;
	public bool FromRight;
	public int Damage = 18;
	public PackedScene FireScene;
	public float WarningTime = 0.85f;
	public float FallTime = 0.55f;
	public float BlastRadius = 52f;

	private static readonly GroundSlamFx.Palette FirePalette = new()
	{
		Hot = new Color(1f, 0.45f, 0.1f, 1f),
		Core = new Color(1f, 0.92f, 0.6f, 1f),
		Rock = new Color(0.3f, 0.2f, 0.17f, 1f),
		Dust = new Color(0.35f, 0.25f, 0.2f, 0.65f),
		Light = new Color(1f, 0.5f, 0.2f, 1f),
	};
	private static readonly GroundSlamFx.Palette RockPalette = new()
	{
		Hot = new Color(0.9f, 0.6f, 0.35f, 1f),
		Core = new Color(1f, 0.9f, 0.75f, 1f),
		Rock = new Color(0.4f, 0.37f, 0.34f, 1f),
		Dust = new Color(0.55f, 0.5f, 0.45f, 0.6f),
		Light = new Color(1f, 0.7f, 0.4f, 1f),
	};

	private float _time;
	private Vector2 _start;
	private Vector2 _head;
	private readonly Vector2[] _trail = new Vector2[10];
	private int _trailCount;
	private bool _landed;

	public override void _Ready()
	{
		ZIndex = 8;
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
		_start = Target + new Vector2(FromRight ? 260f : -260f, -560f);
		_head = _start;
		if (!IsRock)
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		if (_time > WarningTime && !_landed)
		{
			float t = Mathf.Clamp((_time - WarningTime) / FallTime, 0f, 1f);
			_head = _start.Lerp(Target, t * t);
			for (int i = _trail.Length - 1; i > 0; i--)
				_trail[i] = _trail[i - 1];
			_trail[0] = _head;
			_trailCount = Mathf.Min(_trailCount + 1, _trail.Length);
			if (t >= 1f)
				Land();
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_landed)
			return;
		// The warning mark grows and pulses faster as the hit comes.
		float warn = Mathf.Clamp(_time / (WarningTime + FallTime), 0f, 1f);
		float pulse = 0.55f + 0.45f * Mathf.Sin(_time * Mathf.Lerp(8f, 26f, warn));
		var markColor = new Color(1f, 0.25f, 0.1f, (IsRock ? 0.35f : 0.5f) * pulse);
		DrawSetTransform(Target, 0f, new Vector2(1f, 0.28f));
		DrawCircle(Vector2.Zero, BlastRadius * Mathf.Lerp(0.4f, 1f, warn), markColor);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		if (_time <= WarningTime)
			return;

		// Trail: thick to thin, hot to dark.
		for (int i = 0; i < _trailCount - 1; i++)
		{
			float k = 1f - i / (float)_trail.Length;
			Color c = IsRock ? new Color(0.5f, 0.45f, 0.4f, 0.35f * k) : new Color(1f, 0.45f * k + 0.1f, 0.1f, 0.8f * k);
			DrawLine(_trail[i], _trail[i + 1], c, (IsRock ? 6f : 12f) * k);
		}
		if (IsRock)
		{
			DrawCircle(_head, 9f, new Color(0.35f, 0.31f, 0.28f));
			DrawCircle(_head + new Vector2(-2f, -2f), 5f, new Color(0.5f, 0.45f, 0.4f));
		}
		else
		{
			DrawCircle(_head, 20f, new Color(1f, 0.3f, 0.05f, 0.35f));
			DrawCircle(_head, 11f, new Color(1f, 0.6f, 0.15f, 0.8f));
			DrawCircle(_head, 5f, new Color(1f, 0.95f, 0.7f, 1f));
		}
	}

	private void Land()
	{
		_landed = true;
		GroundSlamFx.Impact(GetParent(), Target, IsRock ? 0.6f : 1.1f, IsRock ? RockPalette : FirePalette);
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player)
		{
			Vector2 offset = player.GlobalPosition - Target;
			if (Mathf.Abs(offset.X) <= BlastRadius && offset.Y > -110f && offset.Y < 30f
				&& player.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false } stats)
			{
				stats.TakeDamage(Damage);
				player.ApplyKnockback(new Vector2(Mathf.Sign(offset.X == 0f ? 1f : offset.X), -0.4f).Normalized(), 260f);
			}
			float distance = player.GlobalPosition.DistanceTo(Target);
			if (distance < 600f)
				player.ShakeCamera(Mathf.Lerp(IsRock ? 3f : 6f, 1f, distance / 600f), 0.3f);
		}
		if (FireScene?.Instantiate() is Node2D fire)
		{
			fire.Set("HoldDuration", 3.5f);
			GetParent().AddChild(fire);
			fire.GlobalPosition = Target;
		}
		QueueFree();
	}
}
