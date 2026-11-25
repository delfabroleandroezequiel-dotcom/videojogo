using Godot;

namespace Metroidvania.World;

// Guillotina (La Pirámide): a heavy bronze blade hidden in a slot in the ceiling (the node is the
// slot). AlPasar: it arms when the player walks under it (DetectionWidth wide, anywhere below), shakes
// in its slot for WarningTime (dust falls: the tell), drops DropDistance with gravity, stays bitten
// into the floor for HoldTime — a temporary wall you mustn't walk into — and winds back up slowly.
// Ritmo: same cycle on a fixed Interval (StartOffset to desync a row). Hurts on the way down and while
// bitten; never while rising. Damage + knockback away from the blade, never an instant kill.
// DropDistance = distance from the slot to the floor below it.
public partial class Guillotina : Node2D
{
	public enum Disparo { AlPasar, Ritmo }

	[Export] public Disparo Trigger = Disparo.AlPasar;
	[Export] public float DropDistance = 240f;
	[Export] public float BladeWidth = 84f;
	[Export] public float BladeHeight = 70f;
	[Export] public float DetectionWidth = 120f;
	[Export] public float WarningTime = 0.38f;
	[Export] public float DropGravity = 4200f;
	[Export] public float HoldTime = 0.7f;
	[Export] public float RetractTime = 1.1f;
	[Export] public float Cooldown = 0.5f;
	[Export] public float Interval = 2.4f;
	[Export] public float StartOffset;
	[Export] public int Damage = 45;
	[Export] public float KnockbackForce = 460f;

	private enum Phase { Ready, Warning, Dropping, Holding, Rising, Cooling }

	private Phase _phase = Phase.Ready;
	private float _timer;
	private float _y; // blade bottom, measured down from the slot
	private float _vy;
	private Hazard _hazard;
	private CollisionShape2D _shape;
	private float _time;

	private float RestY => BladeHeight * 0.15f;

	public override void _Ready()
	{
		ZIndex = 2;
		_y = RestY;
		_hazard = Hazard.CreateArea(this, false, Damage, KnockbackForce);
		_hazard.HitWhileOverlapping = true;
		_shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(BladeWidth - 10f, BladeHeight - 6f) } };
		_hazard.AddChild(_shape);
		_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_timer = Trigger == Disparo.Ritmo ? Interval - StartOffset : 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_timer -= dt;
		switch (_phase)
		{
			case Phase.Ready:
				if (Trigger == Disparo.Ritmo ? _timer <= 0f : PlayerBelow())
					Enter(Phase.Warning, WarningTime);
				break;
			case Phase.Warning:
				if (_timer <= 0f)
				{
					_vy = 0f;
					Enter(Phase.Dropping, 0f);
					_hazard.SetDeferred(Area2D.PropertyName.Monitoring, true);
				}
				break;
			case Phase.Dropping:
				_vy += DropGravity * dt;
				_y += _vy * dt;
				if (_y >= DropDistance)
				{
					_y = DropDistance;
					Enter(Phase.Holding, HoldTime);
					TrapKit.Shake(this, 5f, 0.2f);
					TrapKit.Burst(this, GlobalPosition + new Vector2(0f, DropDistance), new Color(0.85f, 0.72f, 0.5f, 1f), 18, 200f);
					Metroidvania.Shared.Sfx.PlayIfExistsAt(this, "World/Mining", "mine");
				}
				break;
			case Phase.Holding:
				if (_timer <= 0f)
				{
					Enter(Phase.Rising, RetractTime);
					_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);
				}
				break;
			case Phase.Rising:
				_y = Mathf.Lerp(RestY, DropDistance, Mathf.Max(0f, _timer) / RetractTime);
				if (_timer <= 0f)
				{
					_y = RestY;
					Enter(Phase.Cooling, Cooldown);
				}
				break;
			case Phase.Cooling:
				if (_timer <= 0f)
					Enter(Phase.Ready, Trigger == Disparo.Ritmo ? Interval : 0f);
				break;
		}
		_shape.Position = new Vector2(0f, _y - BladeHeight / 2f);
		QueueRedraw();
	}

	private void Enter(Phase p, float t)
	{
		_phase = p;
		_timer = t;
	}

	private bool PlayerBelow()
	{
		if (TrapKit.FindPlayer(this) is not Node2D p)
			return false;
		Vector2 d = p.GlobalPosition - GlobalPosition;
		return Mathf.Abs(d.X) <= DetectionWidth / 2f && d.Y > 0f && d.Y < DropDistance + 40f;
	}

	public override void _Draw()
	{
		float shake = _phase == Phase.Warning ? Mathf.Sin(_time * 70f) * 2.5f : 0f;
		float bottom = _y + (_phase == Phase.Warning ? -4f : 0f);
		// Slot.
		DrawRect(new Rect2(-BladeWidth / 2f - 10f, -8f, BladeWidth + 20f, 14f), new Color(0.12f, 0.1f, 0.08f, 1f));
		// Rods.
		Color rod = new(0.32f, 0.27f, 0.2f, 1f);
		DrawLine(new Vector2(-BladeWidth * 0.3f + shake, 0f), new Vector2(-BladeWidth * 0.3f + shake, bottom - BladeHeight), rod, 4f);
		DrawLine(new Vector2(BladeWidth * 0.3f + shake, 0f), new Vector2(BladeWidth * 0.3f + shake, bottom - BladeHeight), rod, 4f);
		// Blade: heavy slab with a crescent edge.
		float top = bottom - BladeHeight;
		var pts = new Vector2[10];
		pts[0] = new Vector2(-BladeWidth / 2f + shake, top);
		pts[1] = new Vector2(BladeWidth / 2f + shake, top);
		for (int i = 0; i <= 7; i++)
		{
			float k = i / 7f;
			float x = Mathf.Lerp(BladeWidth / 2f, -BladeWidth / 2f, k);
			float sag = Mathf.Sin(k * Mathf.Pi) * 10f;
			pts[2 + i] = new Vector2(x + shake, bottom - 14f + sag);
		}
		DrawColoredPolygon(pts, new Color(0.45f, 0.33f, 0.18f, 1f));
		var edge = new Vector2[8];
		System.Array.Copy(pts, 2, edge, 0, 8);
		bool hot = _phase == Phase.Dropping || _phase == Phase.Holding;
		DrawPolyline(edge, hot ? new Color(1.6f, 1.3f, 0.9f, 1f) : new Color(0.9f, 0.8f, 0.6f, 1f), 3f);
		if (_phase == Phase.Warning)
		{
			for (int i = 0; i < 5; i++)
			{
				float x = -BladeWidth / 2f + BladeWidth * ((i * 37 + (int)(_time * 60f)) % 100) / 100f;
				float fy = (_time * 400f + i * 60f) % 120f;
				DrawCircle(new Vector2(x, 10f + fy), 1.6f, new Color(0.85f, 0.72f, 0.5f, 0.7f));
			}
		}
	}
}
