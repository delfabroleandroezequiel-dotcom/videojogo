using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// Pared de escombro: a plug of loose rubble that a sword can't move but a blast can. Solid wall
// (Size, centred on the node) drawn as a heap of rocks with pale cracks; anything that explodes
// next to it calls MineRockWall.BlastAt(...) — GrisuPocket does — and every wall within the radius
// collapses. That's the mine's own "key": the gas that hurts you is also the tool that opens
// passages (teach it on a secret in Mina02, require it later).
// PersistenceId (optional) remembers it broken for this save, via the same OpenedGates record
// Door/Gate/CrumbleFloor use. [Tool] so Size previews in the editor.
[Tool]
public partial class MineRockWall : StaticBody2D
{
	public const string Group = "mine_rock_wall";

	private Vector2 _size = new(40f, 120f);

	[Export]
	public Vector2 Size
	{
		get => _size;
		set { _size = value; if (IsInsideTree()) Rebuild(); }
	}

	[Export] public string PersistenceId = "";
	[Export] public int Seed = 7;

	private Node2D _visual;
	private CollisionShape2D _shape;
	private bool _broken;

	// Breaks every rubble wall whose rectangle comes within `radius` of `center`.
	public static void BlastAt(SceneTree tree, Vector2 center, float radius)
	{
		foreach (Node node in tree.GetNodesInGroup(Group))
		{
			if (node is not MineRockWall wall || wall._broken)
				continue;
			var rect = new Rect2(wall.GlobalPosition - wall._size / 2f, wall._size);
			Vector2 closest = new(Mathf.Clamp(center.X, rect.Position.X, rect.End.X), Mathf.Clamp(center.Y, rect.Position.Y, rect.End.Y));
			if (closest.DistanceTo(center) <= radius)
				wall.Shatter();
		}
	}

	public override void _Ready()
	{
		Rebuild();
		if (Engine.IsEditorHint())
			return;
		AddToGroup(Group);
		if (!string.IsNullOrEmpty(PersistenceId) && SaveManager.Instance != null && SaveManager.Instance.IsGateOpened(PersistenceId))
		{
			_broken = true;
			_shape.Disabled = true;
			_visual.Visible = false;
		}
	}

	public void Shatter()
	{
		if (_broken)
			return;
		_broken = true;
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		if (!string.IsNullOrEmpty(PersistenceId))
			SaveManager.Instance?.MarkGateOpened(PersistenceId);

		GroundSlamFx.Impact(this, GlobalPosition + new Vector2(0f, _size.Y / 2f), _size.Y / 100f);
		Metroidvania.Shared.Sfx.PlayAt(this, "World/Mining", "mine", 1100f);
		// The rocks of the visual tumble out and fade.
		foreach (Node child in _visual.GetChildren())
		{
			if (child is not Node2D rock)
				continue;
			var fly = new Vector2((float)GD.RandRange(-90, 90), (float)GD.RandRange(-60, 40));
			var tween = rock.CreateTween().SetParallel();
			tween.TweenProperty(rock, "position", rock.Position + fly + new Vector2(0f, _size.Y / 2f), 0.6f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tween.TweenProperty(rock, "modulate:a", 0f, 0.6f).SetDelay(0.2f);
		}
		GetTree().CreateTimer(1f).Timeout += () => _visual.Visible = false;
	}

	private void Rebuild()
	{
		_shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (_shape == null)
		{
			_shape = new CollisionShape2D { Name = "CollisionShape2D" };
			AddChild(_shape);
		}
		_shape.Shape = new RectangleShape2D { Size = _size };

		_visual?.QueueFree();
		_visual = new Node2D { Name = "Visual" };
		AddChild(_visual);
		var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
		// Heap of irregular rocks filling the rectangle, darker at the bottom.
		float cell = 18f;
		for (float y = -_size.Y / 2f + cell / 2f; y < _size.Y / 2f; y += cell * 0.8f)
		{
			for (float x = -_size.X / 2f + cell / 2f; x < _size.X / 2f + 1f; x += cell * 0.85f)
			{
				float r = cell * rng.RandfRange(0.5f, 0.75f);
				var pts = new Vector2[7];
				for (int i = 0; i < pts.Length; i++)
					pts[i] = new Vector2(r * rng.RandfRange(0.7f, 1.1f), 0f).Rotated(Mathf.Tau * i / pts.Length);
				float shade = Mathf.Lerp(0.5f, 0.32f, (y + _size.Y / 2f) / _size.Y) + rng.RandfRange(-0.04f, 0.04f);
				var rock = new Polygon2D
				{
					Polygon = pts,
					Position = new Vector2(Mathf.Clamp(x + rng.RandfRange(-3f, 3f), -_size.X / 2f + 6f, _size.X / 2f - 6f), y),
					Color = new Color(shade + 0.05f, shade, shade - 0.04f),
				};
				_visual.AddChild(rock);
			}
		}
		// Pale cracks: the "this can come down" hint.
		var crack = new Line2D { Width = 1.5f, DefaultColor = new Color(0.95f, 0.85f, 0.6f, 0.8f) };
		for (int i = 0; i < 6; i++)
			crack.AddPoint(new Vector2(rng.RandfRange(-_size.X * 0.3f, _size.X * 0.3f), -_size.Y / 2f + _size.Y * i / 5f));
		_visual.AddChild(crack);
	}
}
