using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// Receptor solar (La Pirámide): a stone sun disc that a RayoSolar has to keep lit for ChargeTime to
// fire its Targets (TargetMethod, default "Open": a Door with RequiresLever, a Gate, a MareaDeLava's
// "Drain", a RayoSolar's "Activate"...). StaysLit = once charged it never goes out (remembered per
// save under PersistenceId, so a solved door stays solved — the Door itself also needs its own
// PersistenceId). StaysLit off = a "hold" receptor: when the beam leaves it, OffMethod ("Close") is
// called on the same targets (doors that only stay open while the sun hits the disc).
public partial class ReceptorSolar : Node2D
{
	[Export] public NodePath[] Targets = System.Array.Empty<NodePath>();
	[Export] public string TargetMethod = "Open";
	[Export] public string OffMethod = "Close";
	[Export] public float ChargeTime = 0.5f;
	[Export] public bool StaysLit = true;
	[Export] public string PersistenceId = "";
	[Export] public float Radius = 24f;
	[Export] public Color GlowColor = new(1f, 0.78f, 0.35f, 1f);

	[Signal] public delegate void EncendidoEventHandler();
	[Signal] public delegate void ApagadoEventHandler();

	private ulong _lastLitFrame;
	private float _charge;
	private bool _on;
	private float _time;
	private PointLight2D _light;

	public bool IsOn => _on;

	public override void _Ready()
	{
		AddToGroup("receptor_solar");
		ZIndex = 3;
		_light = TrapKit.Light(GlowColor, 0f, 1.6f);
		AddChild(_light);
		if (StaysLit && !string.IsNullOrEmpty(PersistenceId) && SaveManager.Instance.IsGateOpened(PersistenceId))
		{
			_on = true;
			_charge = ChargeTime;
		}
	}

	// Called by a RayoSolar every physics frame its burning beam ends on this disc.
	public void Iluminar() => _lastLitFrame = Engine.GetPhysicsFrames();

	public bool Intersect(Vector2 origin, Vector2 dir, out float t)
	{
		t = 0f;
		Vector2 oc = origin - GlobalPosition;
		float b = oc.Dot(dir);
		float c = oc.LengthSquared() - Radius * Radius;
		float disc = b * b - c;
		if (disc < 0f)
			return false;
		t = -b - Mathf.Sqrt(disc);
		return t > 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		bool lit = Engine.GetPhysicsFrames() - _lastLitFrame <= 2;
		if (_on && StaysLit)
		{
			_light.Energy = 1.2f + 0.2f * Mathf.Sin(_time * 3f);
			QueueRedraw();
			return;
		}
		_charge = lit ? Mathf.Min(ChargeTime, _charge + dt) : Mathf.Max(0f, _charge - dt * 1.5f);
		if (!_on && _charge >= ChargeTime)
			TurnOn();
		else if (_on && !StaysLit && _charge <= 0f)
			TurnOff();
		_light.Energy = (_charge / Mathf.Max(0.01f, ChargeTime)) * 1.2f;
		QueueRedraw();
	}

	private void TurnOn()
	{
		_on = true;
		if (StaysLit && !string.IsNullOrEmpty(PersistenceId))
			SaveManager.Instance.MarkGateOpened(PersistenceId);
		CallTargets(TargetMethod);
		EmitSignal(SignalName.Encendido);
		TrapKit.Burst(this, GlobalPosition, GlowColor, 28, 220f);
		TrapKit.Shake(this, 3f, 0.25f);
		Metroidvania.Shared.Sfx.PlayIfExistsAt(this, "World/Doors", "Portcullis Gate");
	}

	private void TurnOff()
	{
		_on = false;
		CallTargets(OffMethod);
		EmitSignal(SignalName.Apagado);
	}

	private void CallTargets(string method)
	{
		if (string.IsNullOrEmpty(method))
			return;
		foreach (NodePath path in Targets)
		{
			if (GetNodeOrNull(path) is Node n && n.HasMethod(method))
				n.Call(method);
		}
	}

	public override void _Draw()
	{
		float k = _charge / Mathf.Max(0.01f, ChargeTime);
		DrawCircle(Vector2.Zero, Radius + 6f, new Color(0.34f, 0.29f, 0.22f, 1f));
		DrawCircle(Vector2.Zero, Radius, new Color(0.24f, 0.2f, 0.15f, 1f));
		Color ray = new(GlowColor.R * (0.4f + 1.4f * k), GlowColor.G * (0.4f + 1.2f * k), GlowColor.B * (0.4f + k), 1f);
		for (int i = 0; i < 12; i++)
		{
			Vector2 d = Vector2.Right.Rotated(i * Mathf.Tau / 12f);
			DrawLine(d * Radius * 0.45f, d * Radius * (i % 2 == 0 ? 0.95f : 0.75f), ray, 2.5f);
		}
		DrawCircle(Vector2.Zero, Radius * 0.35f, ray);
	}
}
