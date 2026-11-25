using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Paso de brasas (Las Lavas): an invisible platform over the lava. Nothing gives it away except the
// embers that keep dropping from the ceiling: they land ON it (instead of sinking into the lava) and
// leave a short glowing scar on its top for a moment. Standing on it also lights a faint glow under
// your feet. It's the zone's "invisible wall" with a purpose: a secret path to a reward or a shortcut.
// Node origin = left end of the walkable top; Width long. Solid (StaticBody), not one-way.
[Tool]
public partial class PasoDeBrasas : Node2D
{
	private float _width = 140f;

	[Export]
	public float Width
	{
		get => _width;
		set { _width = Mathf.Max(32f, value); QueueRedraw(); }
	}

	[Export] public float Thickness = 14f;
	[Export] public float EmberFallFrom = 260f;
	[Export] public float EmberSpeed = 340f;
	[Export] public float MinInterval = 0.9f;
	[Export] public float MaxInterval = 2.1f;
	[Export] public Color GlowColor = new(1f, 0.55f, 0.15f, 1f);

	private sealed class Ember
	{
		public float X;
		public float Y;
	}

	private sealed class Scar
	{
		public float X;
		public float Age;
	}

	private readonly List<Ember> _embers = new();
	private readonly List<Scar> _scars = new();
	private float _next;
	private Area2D _top;
	private float _feetGlow;
	private float _feetX;
	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		QueueRedraw();
		if (Engine.IsEditorHint())
			return;
		var body = new StaticBody2D { Name = "Cuerpo" };
		body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, Thickness) }, Position = new Vector2(_width / 2f, Thickness / 2f) });
		AddChild(body);
		_top = new Area2D { CollisionLayer = 0, CollisionMask = 2, Monitorable = false };
		_top.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width, 16f) }, Position = new Vector2(_width / 2f, -8f) });
		AddChild(_top);
		_rng.Seed = (ulong)(Mathf.Abs(GlobalPosition.X) + 3f);
		_next = _rng.RandfRange(0f, MaxInterval);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;
		_next -= dt;
		if (_next <= 0f)
		{
			_next = _rng.RandfRange(MinInterval, MaxInterval);
			_embers.Add(new Ember { X = _rng.RandfRange(8f, _width - 8f), Y = -EmberFallFrom });
		}
		for (int i = _embers.Count - 1; i >= 0; i--)
		{
			Ember e = _embers[i];
			e.Y += EmberSpeed * dt;
			if (e.Y >= 0f)
			{
				_scars.Add(new Scar { X = e.X });
				_embers.RemoveAt(i);
			}
		}
		for (int i = _scars.Count - 1; i >= 0; i--)
		{
			_scars[i].Age += dt;
			if (_scars[i].Age > 1.1f)
				_scars.RemoveAt(i);
		}
		_feetGlow = Mathf.Max(0f, _feetGlow - dt * 2f);
		foreach (Node2D b in _top.GetOverlappingBodies())
		{
			if (!b.IsInGroup("player"))
				continue;
			_feetGlow = 1f;
			_feetX = Mathf.Clamp(ToLocal(b.GlobalPosition).X, 0f, _width);
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Engine.IsEditorHint())
		{
			DrawRect(new Rect2(0f, 0f, _width, Thickness), new Color(GlowColor, 0.25f));
			return;
		}
		foreach (Ember e in _embers)
		{
			DrawCircle(new Vector2(e.X, e.Y), 4f, new Color(GlowColor, 0.35f));
			DrawCircle(new Vector2(e.X, e.Y), 2f, new Color(1f, 0.85f, 0.5f, 1f));
		}
		foreach (Scar s in _scars)
		{
			float a = 1f - s.Age / 1.1f;
			float half = 22f + s.Age * 30f;
			DrawLine(new Vector2(Mathf.Max(0f, s.X - half), 0f), new Vector2(Mathf.Min(_width, s.X + half), 0f), new Color(GlowColor, a * 0.45f), 6f);
			DrawLine(new Vector2(Mathf.Max(0f, s.X - half * 0.6f), 0f), new Vector2(Mathf.Min(_width, s.X + half * 0.6f), 0f), new Color(1f, 0.8f, 0.45f, a), 2f);
		}
		if (_feetGlow > 0f)
			DrawLine(new Vector2(Mathf.Max(0f, _feetX - 40f), 0f), new Vector2(Mathf.Min(_width, _feetX + 40f), 0f), new Color(GlowColor, _feetGlow * 0.5f), 4f);
	}
}
