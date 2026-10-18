using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Stone pressure plate set into the floor (place the node on the floor surface). Stepping on it
// sinks it with a click and calls TargetMethod on every node in Targets — "something happens
// somewhere else": ArrowTrap.Activate (a volley), CrushingPlatform.Activate (Trigger = Manual),
// FallingStalactite.Activate, ExplosiveBarrel.Activate, Door.Close (StartsOpen door slamming
// behind you), Door.Open… Pressed/Released signals are there too for anything else.
// OneShot (default): the ambush fires once and the plate stays down. Otherwise it springs back up
// when nothing is on it and fires again the next time.
// Hidden = flush with the floor and close to its color — a careful look still shows the seams.
// Everything is drawn in code. [Tool] so the plate previews in the editor.
[Tool]
public partial class PressurePlate : Node2D
{
	[Signal] public delegate void PressedEventHandler();
	[Signal] public delegate void ReleasedEventHandler();

	[Export] public NodePath[] Targets = System.Array.Empty<NodePath>();
	[Export] public string TargetMethod = "Activate";
	// Seconds between the click and the targets firing — a beat for the player to realise.
	[Export] public float Delay = 0.15f;
	[Export] public bool OneShot = true;
	[Export] public bool PressedByEnemies;

	private float _width = 40f;
	[Export]
	public float Width { get => _width; set { _width = Mathf.Max(12f, value); Rebuild(); } }

	private bool _hidden;
	[Export]
	public bool Hidden { get => _hidden; set { _hidden = value; Rebuild(); } }

	private Color _stoneColor = new(0.45f, 0.43f, 0.4f, 1f);
	[Export]
	public Color StoneColor { get => _stoneColor; set { _stoneColor = value; Rebuild(); } }

	private const float SinkDepth = 3f;

	private Node2D _visual;
	private Node2D _plate;
	private Area2D _trigger;
	private bool _down;
	private bool _spent;

	public override void _Ready()
	{
		Rebuild();
		if (Engine.IsEditorHint())
			return;
		_trigger = new Area2D
		{
			Name = "Trigger",
			CollisionLayer = 0,
			CollisionMask = PressedByEnemies ? 2u | 4u : 2u,
			Monitorable = false,
		};
		_trigger.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(_width - 6f, 10f) }, Position = new Vector2(0f, -5f) });
		AddChild(_trigger);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint() || _spent)
			return;
		bool weighed = IsWeighed();
		if (weighed && !_down)
			Press();
		else if (!weighed && _down && !OneShot)
			Release();
	}

	private bool IsWeighed()
	{
		foreach (Node2D body in _trigger.GetOverlappingBodies())
		{
			if (body is CharacterBody2D character && !character.IsOnFloor())
				continue;
			if (body.IsInGroup("player") || (PressedByEnemies && body.IsInGroup("enemy")))
				return true;
		}
		return false;
	}

	private async void Press()
	{
		_down = true;
		if (OneShot)
			_spent = true;
		MovePlate(SinkDepth, 0.06f);
		Sfx.PlayAt(this, "World/Mining", "mine");
		EmitSignal(SignalName.Pressed);

		if (Delay > 0f)
		{
			await ToSignal(GetTree().CreateTimer(Delay), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
		}
		foreach (NodePath path in Targets)
		{
			if (GetNodeOrNull(path) is { } target && target.HasMethod(TargetMethod))
				target.Call(TargetMethod);
			else
				GD.PushWarning($"PressurePlate {Name}: target '{path}' missing or has no {TargetMethod}()");
		}
	}

	private void Release()
	{
		_down = false;
		MovePlate(0f, 0.2f);
		EmitSignal(SignalName.Released);
	}

	private void MovePlate(float depth, float time)
	{
		Tween tween = CreateTween();
		tween.TweenProperty(_plate, "position:y", depth, time).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
	}

	// ── Visual ──────────────────────────────────────────────────────────────────────────────────

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;
		_visual?.QueueFree();
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);

		float half = _width / 2f;
		Color stone = _stoneColor;
		Color dark = stone.Darkened(0.55f);
		// Raised plate stands 4 px proud of the floor; a hidden one sits flush (its top at the surface).
		float lip = _hidden ? 0f : 4f;

		// Recess frame cut into the floor.
		_visual.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(-half - 2f, 0f), new Vector2(half + 2f, 0f), new Vector2(half + 2f, 5f), new Vector2(-half - 2f, 5f) },
			Color = dark,
		});

		_plate = new Node2D { Name = "Plate" };
		_visual.AddChild(_plate);
		float top = -lip;
		_plate.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(-half + 1f, top), new Vector2(half - 1f, top), new Vector2(half, top + 1.5f), new Vector2(half, 4f), new Vector2(-half, 4f), new Vector2(-half, top + 1.5f) },
			Color = _hidden ? stone.Darkened(0.08f) : stone,
		});
		// Highlight on the top edge and a carved rune/groove pattern, fainter when hidden.
		float a = _hidden ? 0.25f : 0.7f;
		_plate.AddChild(new Line2D { Points = new[] { new Vector2(-half + 1.5f, top + 0.5f), new Vector2(half - 1.5f, top + 0.5f) }, Width = 1f, DefaultColor = stone.Lightened(0.35f) with { A = a } });
		int grooves = Mathf.Max(1, Mathf.RoundToInt(_width / 14f));
		for (int i = 1; i < grooves + 1; i++)
		{
			float x = Mathf.Lerp(-half, half, i / (float)(grooves + 1));
			_plate.AddChild(new Line2D { Points = new[] { new Vector2(x, top + 1.5f), new Vector2(x, 3.5f) }, Width = 1f, DefaultColor = dark with { A = a } });
		}
		if (!_hidden)
		{
			// Little chevron mark so a visible plate reads as "a mechanism", not a loose slab.
			float cy = (top + 4f) / 2f;
			_plate.AddChild(new Line2D { Points = new[] { new Vector2(-3f, cy - 1.2f), new Vector2(0f, cy + 1.2f), new Vector2(3f, cy - 1.2f) }, Width = 1f, DefaultColor = new Color(0.75f, 0.62f, 0.35f, 0.8f) });
		}
	}
}
