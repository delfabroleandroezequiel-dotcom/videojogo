using Godot;

namespace Metroidvania.World;

// Espejo solar (La Pirámide): a bronze mirror on a stone pivot that reflects every RayoSolar hitting
// it (both faces). A sword swing within GolpeAlcance turns it to the next angle of Angulos (or
// +StepDegrees if Angulos is empty) — same "hit it with the weapon" language as Lever.GolpeConArma.
// The turn is animated and the beam follows the animated angle, so a beam visibly sweeps across the
// room while the mirror rotates (that sweep is part of the hazard). AngleDegrees is the mirror
// surface's world angle: 0 = horizontal, 45 = "\" in screen space (y down), 135 = "/".
// Rotatable = false makes it a fixed mirror (the room's given, not the player's tool).
public partial class EspejoSolar : Node2D
{
	[Export] public float Length = 72f;
	[Export] public float AngleDegrees = 45f;
	[Export] public float[] Angulos = System.Array.Empty<float>();
	[Export] public float StepDegrees = 45f;
	[Export] public bool Rotatable = true;
	[Export] public float GolpeAlcance = 95f;
	[Export] public float TurnTime = 0.35f;
	// Draws a stone column from the pivot down this far (0 = hanging/wall-mounted, no column).
	[Export] public float PedestalHeight = 0f;
	[Export] public Color FrameColor = new(0.55f, 0.38f, 0.16f, 1f);

	private float _angle;
	private int _index;
	private float _cooldown;
	private Tween _turn;

	[Signal] public delegate void GiradoEventHandler(float angleDegrees);

	public float CurrentAngle => _angle;

	public override void _Ready()
	{
		AddToGroup("espejo_solar");
		_angle = AngleDegrees;
		_index = 0;
		if (Angulos.Length > 0)
		{
			// Start on the listed angle closest to AngleDegrees.
			float best = float.MaxValue;
			for (int i = 0; i < Angulos.Length; i++)
			{
				float d = Mathf.Abs(Mathf.AngleDifference(Mathf.DegToRad(Angulos[i]), Mathf.DegToRad(AngleDegrees)));
				if (d < best)
				{
					best = d;
					_index = i;
				}
			}
			_angle = Angulos[_index];
		}
		ZIndex = 3;
	}

	public override void _Process(double delta)
	{
		_cooldown = Mathf.Max(0f, _cooldown - (float)delta);
		if (!Rotatable || _cooldown > 0f || !Input.IsActionJustPressed("attack"))
			return;
		if (TrapKit.FindPlayer(this) is Node2D p && p.GlobalPosition.DistanceTo(GlobalPosition) <= GolpeAlcance)
			Turn();
	}

	public void Turn()
	{
		float target;
		if (Angulos.Length > 0)
		{
			_index = (_index + 1) % Angulos.Length;
			target = Angulos[_index];
			// Always rotate forward (shortest positive way), so the sweep direction is predictable.
			while (target < _angle)
				target += 180f;
		}
		else
			target = _angle + StepDegrees;
		_cooldown = TurnTime + 0.1f;
		_turn?.Kill();
		_turn = CreateTween();
		_turn.TweenMethod(Callable.From<float>(a => { _angle = a; QueueRedraw(); }), _angle, target, TurnTime)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_turn.TweenCallback(Callable.From(() =>
		{
			_angle = Mathf.PosMod(_angle, 180f);
			EmitSignal(SignalName.Girado, _angle);
		}));
		Metroidvania.Shared.Sfx.PlayIfExistsAt(this, "World/Doors", "Chest Close");
		TrapKit.Burst(this, GlobalPosition, new Color(1f, 0.8f, 0.4f, 1f), 10, 120f);
	}

	private Vector2 Axis => Vector2.Right.Rotated(Mathf.DegToRad(_angle));

	// Ray (origin + dir·t) against the mirror segment.
	public bool Intersect(Vector2 origin, Vector2 dir, out float t)
	{
		t = 0f;
		Vector2 half = Axis * (Length / 2f);
		Vector2 a = GlobalPosition - half;
		Vector2 b = GlobalPosition + half;
		Variant hit = Geometry2D.SegmentIntersectsSegment(origin, origin + dir * 100000f, a, b);
		if (hit.VariantType == Variant.Type.Nil)
			return false;
		t = origin.DistanceTo((Vector2)hit);
		return true;
	}

	public Vector2 Reflect(Vector2 dir)
	{
		Vector2 n = new Vector2(-Axis.Y, Axis.X);
		return (dir - 2f * dir.Dot(n) * n).Normalized();
	}

	public override void _Draw()
	{
		if (PedestalHeight > 0f)
		{
			DrawRect(new Rect2(-10f, 0f, 20f, PedestalHeight), new Color(0.42f, 0.36f, 0.28f, 1f));
			DrawRect(new Rect2(-16f, PedestalHeight - 10f, 32f, 10f), new Color(0.36f, 0.3f, 0.23f, 1f));
		}
		DrawCircle(Vector2.Zero, 9f, new Color(0.3f, 0.24f, 0.16f, 1f));
		Vector2 half = Axis * (Length / 2f);
		Vector2 n = new Vector2(-Axis.Y, Axis.X);
		DrawLine(-half, half, FrameColor, 9f);
		DrawLine(-half + n * 2f, half + n * 2f, new Color(1.3f, 1.2f, 0.95f, 1f), 3f);
		DrawLine(-half - n * 2f, half - n * 2f, new Color(1.3f, 1.2f, 0.95f, 1f), 3f);
		if (Rotatable)
		{
			// Small notches on the pivot: reads as "this turns".
			for (int i = 0; i < 4; i++)
				DrawLine(Vector2.Right.Rotated(i * Mathf.Pi / 2f) * 9f, Vector2.Right.Rotated(i * Mathf.Pi / 2f) * 14f,
					new Color(0.85f, 0.7f, 0.4f, 1f), 2f);
		}
	}
}
