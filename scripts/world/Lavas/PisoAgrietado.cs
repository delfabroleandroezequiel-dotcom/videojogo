using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Piso agrietado (Las Lavas): a cracked basalt slab that only gives way under the pound (diving
// down attack). Walking, jumping or slashing it does nothing — it's a lock seen long before the key.
// Remembered per save like the breakable walls (SaveManager broken walls, committed at a hoguera).
// Node origin = top-left corner; Width x Height. Drawn in code: dark slab, glowing uneven crack.
[Tool]
public partial class PisoAgrietado : StaticBody2D
{
	private float _width = 120f;
	private float _height = 40f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(32f, value); QueueRedraw(); }
	}

	[Export]
	public float Height
	{
		get => _height;
		set { _height = Mathf.Max(12f, value); QueueRedraw(); }
	}

	[Export] public string CustomPersistenceId = "";
	[Export] public Color CrackColor = new(1f, 0.5f, 0.15f, 1f);

	private Area2D _top;
	private CollisionShape2D _shape;
	private bool _broken;
	private Vector2[] _crack;

	private string PersistenceId => string.IsNullOrEmpty(CustomPersistenceId) ? GetPath().ToString() : CustomPersistenceId;

	public override void _Ready()
	{
		BuildCrack();
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;
		if (SaveManager.Instance.IsWallBroken(PersistenceId))
		{
			QueueFree();
			return;
		}
		_shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, _height) }, Position = new Vector2(_width / 2f, _height / 2f) };
		AddChild(_shape);
		_top = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_top.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, 24f) }, Position = new Vector2(_width / 2f, -10f) });
		AddChild(_top);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _broken || _top is null)
			return;
		foreach (Node2D body in _top.GetOverlappingBodies())
		{
			if (body is Metroidvania.Player.Player player && player.IsPounding)
			{
				Break();
				return;
			}
		}
	}

	public void Break()
	{
		if (_broken)
			return;
		_broken = true;
		SaveManager.Instance.MarkWallBroken(PersistenceId);
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		var debris = new CpuParticles2D
		{
			OneShot = true,
			Explosiveness = 0.95f,
			Amount = 36,
			Lifetime = 1.1f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(_width / 2f, _height / 2f),
			Position = new Vector2(_width / 2f, _height / 2f),
			Direction = Vector2.Down,
			Spread = 70f,
			Gravity = new Vector2(0f, 900f),
			InitialVelocityMin = 60f,
			InitialVelocityMax = 220f,
			ScaleAmountMin = 3f,
			ScaleAmountMax = 7f,
			Color = new Color(0.22f, 0.16f, 0.14f, 1f),
		};
		GetParent().AddChild(debris);
		debris.GlobalPosition = GlobalPosition + new Vector2(_width / 2f, _height / 2f);
		debris.Emitting = true;
		debris.Finished += debris.QueueFree;
		Sfx.PlayIfExistsAt(this, "World/Mining", "mine");
		Visible = false;
		CallDeferred(Node.MethodName.QueueFree);
	}

	private void BuildCrack()
	{
		// A jagged line across the slab, seeded by position so each slab cracks its own way.
		var rng = new RandomNumberGenerator { Seed = (ulong)(Mathf.Abs(Position.X * 31f + Position.Y * 17f) + 7f) };
		int n = Mathf.Max(4, (int)(_width / 18f));
		_crack = new Vector2[n + 1];
		for (int i = 0; i <= n; i++)
			_crack[i] = new Vector2(_width * i / n, _height * 0.45f + rng.RandfRange(-_height * 0.3f, _height * 0.3f));
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(0f, 0f, _width, _height), new Color(0.16f, 0.12f, 0.11f, 1f));
		DrawRect(new Rect2(0f, 0f, _width, 4f), new Color(0.3f, 0.22f, 0.19f, 1f));
		if (_crack is null)
			return;
		DrawPolyline(_crack, new Color(CrackColor, 0.35f), 6f);
		DrawPolyline(_crack, CrackColor, 2f);
		for (int i = 1; i < _crack.Length - 1; i += 2)
			DrawLine(_crack[i], _crack[i] + new Vector2(4f, -_height * 0.35f), new Color(CrackColor, 0.7f), 1.5f);
	}
}
