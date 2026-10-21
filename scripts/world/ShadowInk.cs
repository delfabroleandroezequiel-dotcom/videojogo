using Godot;

namespace Metroidvania.World;

// Smoke and ink bleeding off anything made of shadow (clones, echoes, puppets, sentinels, summons):
// dark puffs left behind in the world as it moves — so they trail and linger instead of following —
// plus the odd violet ember; when the shadow goes, its last smoke hangs on for a beat.
// Attach with ShadowInk.Attach(node); it frees itself with its parent.
public partial class ShadowInk : Node2D
{
	public float Rate = 22f;
	public float Spread = 14f;
	public Color InkColor = new(0.02f, 0.01f, 0.04f, 1f);
	public Color EmberColor = new(0.7f, 0.35f, 1f, 1f);

	private const int MaxPuffs = 48;
	private const float PuffLife = 0.7f;

	private struct Puff
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public float Age;
		public float Radius;
		public bool Ember;
	}

	private readonly Puff[] _puffs = new Puff[MaxPuffs];
	private int _next;
	private float _emitAccumulator;
	private Node2D _source;

	public override void _Ready()
	{
		// Puffs live in world space (TopLevel) so they stay where they were shed.
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
		ZIndex = 4;
		for (int i = 0; i < MaxPuffs; i++)
			_puffs[i].Age = PuffLife;
		_source = GetParent() as Node2D;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (IsInstanceValid(_source) && _source.Visible)
		{
			_emitAccumulator += dt * Rate;
			while (_emitAccumulator >= 1f)
			{
				_emitAccumulator -= 1f;
				Emit(_source.GlobalPosition);
			}
		}
		for (int i = 0; i < MaxPuffs; i++)
		{
			ref Puff p = ref _puffs[i];
			if (p.Age >= PuffLife)
				continue;
			p.Age += dt;
			p.Velocity *= 1f - 2f * dt;
			p.Position += p.Velocity * dt;
		}
		QueueRedraw();
	}

	private void Emit(Vector2 at)
	{
		ref Puff p = ref _puffs[_next];
		_next = (_next + 1) % MaxPuffs;
		p.Position = at + new Vector2((float)GD.RandRange(-Spread, Spread), (float)GD.RandRange(-Spread * 1.6f, Spread * 0.6f));
		p.Velocity = new Vector2((float)GD.RandRange(-12.0, 12.0), (float)GD.RandRange(-40.0, -10.0));
		p.Age = 0f;
		p.Radius = (float)GD.RandRange(3.0, 7.0);
		p.Ember = GD.Randf() < 0.15f;
	}

	public override void _Draw()
	{
		foreach (Puff p in _puffs)
		{
			if (p.Age >= PuffLife)
				continue;
			float t = p.Age / PuffLife;
			if (p.Ember)
				DrawCircle(p.Position, 1.6f * (1f - t), EmberColor with { A = 0.9f * (1f - t) });
			else
				DrawCircle(p.Position, p.Radius * (1f + t), InkColor with { A = 0.55f * (1f - t) });
		}
	}

	public override void _ExitTree()
	{
		// A clone vanishing leaves its last smoke hanging for a beat instead of popping.
		if (GetTree()?.CurrentScene is not Node scene || !IsInstanceValid(this))
			return;
		var residue = new ShadowInkResidue { Puffs = (Puff[])_puffs.Clone(), InkColor = InkColor, EmberColor = EmberColor };
		scene.CallDeferred(Node.MethodName.AddChild, residue);
	}

	public static ShadowInk Attach(Node2D shadow)
	{
		var ink = new ShadowInk { Name = "TintaSombra" };
		shadow.AddChild(ink);
		return ink;
	}

	// What's left of the smoke once its source is gone: same puffs, no more emission.
	private partial class ShadowInkResidue : Node2D
	{
		public Puff[] Puffs;
		public Color InkColor;
		public Color EmberColor;

		public override void _Ready() => ZIndex = 4;

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			bool alive = false;
			for (int i = 0; i < Puffs.Length; i++)
			{
				ref Puff p = ref Puffs[i];
				if (p.Age >= PuffLife)
					continue;
				p.Age += dt;
				p.Velocity *= 1f - 2f * dt;
				p.Position += p.Velocity * dt;
				alive = true;
			}
			if (!alive)
				QueueFree();
			QueueRedraw();
		}

		public override void _Draw()
		{
			foreach (Puff p in Puffs)
			{
				if (p.Age >= PuffLife)
					continue;
				float t = p.Age / PuffLife;
				if (p.Ember)
					DrawCircle(p.Position, 1.6f * (1f - t), EmberColor with { A = 0.9f * (1f - t) });
				else
					DrawCircle(p.Position, p.Radius * (1f + t), InkColor with { A = 0.55f * (1f - t) });
			}
		}
	}
}
