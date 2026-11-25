using Godot;

namespace Metroidvania.World;

// Costra (Las Lavas): a thin basalt crust over the lava. Stand on it and its cracks start glowing;
// after BreakDelay it shatters into the lava and comes back RespawnTime later (only once nobody is in
// the way). An AnimatableBody2D on purpose: AcidPool never takes it as "safe ground", so a fall into
// the lava never puts you back on a crust that's already gone. Node origin = top-left; drawn in code.
[Tool]
public partial class Costra : AnimatableBody2D
{
	private float _width = 96f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(32f, value); QueueRedraw(); }
	}

	[Export] public float Thickness = 18f;
	[Export] public float BreakDelay = 0.55f;
	[Export] public float RespawnTime = 2.6f;
	[Export] public Color CrackColor = new(1f, 0.5f, 0.15f, 1f);

	private CollisionShape2D _shape;
	private Area2D _top;
	private float _stress = -1f;
	private float _respawn;
	private bool _gone;

	public override void _Ready()
	{
		SyncToPhysics = false;
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;
		_shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, Thickness) }, Position = new Vector2(_width / 2f, Thickness / 2f) };
		AddChild(_shape);
		_top = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_top.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width - 6f, 12f) }, Position = new Vector2(_width / 2f, -6f) });
		AddChild(_top);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		if (_gone)
		{
			_respawn -= dt;
			if (_respawn <= 0f && _top.GetOverlappingBodies().Count == 0)
			{
				_gone = false;
				_stress = -1f;
				_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
				Visible = true;
			}
			return;
		}
		if (_stress < 0f)
		{
			foreach (Node2D b in _top.GetOverlappingBodies())
				if (b.IsInGroup("player"))
					_stress = 0f;
		}
		else
		{
			_stress += dt;
			if (_stress >= BreakDelay)
				Shatter();
		}
		QueueRedraw();
	}

	// Start cracking without anyone standing on it (DerrumbeEnCadena).
	public void Activate()
	{
		if (!_gone && _stress < 0f)
			_stress = 0f;
	}

	private void Shatter()
	{
		_gone = true;
		_respawn = RespawnTime;
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		Visible = false;
		var bits = new CpuParticles2D
		{
			OneShot = true,
			Explosiveness = 0.9f,
			Amount = 18,
			Lifetime = 0.8f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width / 2f, 4f),
			Direction = Vector2.Down,
			Spread = 60f,
			Gravity = new Vector2(0f, 900f),
			InitialVelocityMin = 40f,
			InitialVelocityMax = 140f,
			ScaleAmountMin = 2f,
			ScaleAmountMax = 5f,
			Color = new Color(0.25f, 0.17f, 0.14f, 1f),
		};
		GetParent().AddChild(bits);
		bits.GlobalPosition = GlobalPosition + new Vector2(_width / 2f, Thickness / 2f);
		bits.Emitting = true;
		bits.Finished += bits.QueueFree;
	}

	public override void _Draw()
	{
		float shake = _stress > 0f ? (float)GD.RandRange(-1.5, 1.5) : 0f;
		var off = new Vector2(shake, 0f);
		DrawRect(new Rect2(off, new Vector2(_width, Thickness)), new Color(0.2f, 0.14f, 0.12f, 1f));
		DrawRect(new Rect2(off, new Vector2(_width, 3f)), new Color(0.34f, 0.24f, 0.2f, 1f));
		float heat = _stress < 0f ? 0.25f : 0.4f + 0.6f * Mathf.Clamp(_stress / BreakDelay, 0f, 1f);
		int n = Mathf.Max(2, (int)(_width / 32f));
		for (int i = 1; i <= n; i++)
		{
			float x = _width * i / (n + 1f) + ((i * 37) % 11 - 5);
			DrawLine(off + new Vector2(x, 2f), off + new Vector2(x + 6f, Thickness * 0.55f), new Color(CrackColor, heat), 1.5f);
			DrawLine(off + new Vector2(x + 6f, Thickness * 0.55f), off + new Vector2(x - 3f, Thickness - 2f), new Color(CrackColor, heat * 0.8f), 1.5f);
		}
	}
}
