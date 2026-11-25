using Godot;

namespace Metroidvania.World;

// Lanzallamas cíclico (Las Lavas): drives a Flamethrower child (purely visual on its own) with a
// rhythm and gives it teeth: a hazard box along the stream while it burns. OffTime → sputter
// warning (WarningTime: a short weak puff) → OnTime burning. Aim by rotating this node (the stream
// goes along local +X). Manual = only when Activate() is called (a TrapRelay volley), burning OnTime.
public partial class LanzallamasCiclico : Node2D
{
	[Export] public float OnTime = 1.1f;
	[Export] public float OffTime = 1.6f;
	[Export] public float WarningTime = 0.45f;
	[Export] public float StartOffset;
	[Export] public bool Manual;
	[Export] public int Damage = 30;
	[Export] public float KnockbackForce = 320f;

	private Flamethrower _flame;
	private Hazard _hazard;
	private float _t;
	private bool _on;
	private float _manualTimer;

	public override void _Ready()
	{
		foreach (Node child in GetChildren())
			if (child is Flamethrower f)
				_flame = f;
		if (_flame is null)
		{
			GD.PushWarning($"LanzallamasCiclico {Name}: no Flamethrower child");
			return;
		}
		_hazard = Hazard.CreateArea(this, false, Damage, KnockbackForce);
		_hazard.HitWhileOverlapping = true;
		_hazard.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(_flame.Range * 0.92f, _flame.Width * 0.55f) },
			Position = new Vector2(_flame.Range * 0.46f, 0f),
		});
		_hazard.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_t = StartOffset;
		CallDeferred(MethodName.SetBurning, false);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_flame is null)
			return;
		float dt = (float)delta;
		if (Manual)
		{
			if (_manualTimer > 0f)
			{
				_manualTimer -= dt;
				if (_manualTimer <= 0f)
					SetBurning(false);
			}
			return;
		}
		float total = OffTime + WarningTime + OnTime;
		_t = (_t + dt) % total;
		bool burning = _t >= OffTime + WarningTime;
		bool warning = !burning && _t >= OffTime;
		if (burning != _on)
			SetBurning(burning);
		if (warning && !_on && !_flame.IsActive)
			_flame.SetActive(true);
		if (warning && _flame.IsActive && _t >= OffTime + 0.15f)
			_flame.SetActive(false);
	}

	public void Activate()
	{
		if (_flame is null)
			return;
		_manualTimer = OnTime;
		SetBurning(true);
	}

	private void SetBurning(bool on)
	{
		_on = on;
		_flame?.SetActive(on);
		_hazard?.SetDeferred(Area2D.PropertyName.Monitoring, on);
	}
}
