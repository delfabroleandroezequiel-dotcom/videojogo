using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// What a ranged ShadowSummon (archer/mage shadow) fires: one neon bolt in the summon's colour that
// flies straight, hurts the first living enemy it reaches and dies on walls. A single procedural
// shot for every ranged enemy type instead of reusing each one's own player-hunting projectile.
public partial class ShadowBolt : Node2D
{
	private const float Speed = 380f;
	private const float Lifetime = 1.2f;
	private const float HitHalfWidth = 18f;
	private const float HitHalfHeight = 40f;
	private const int TrailPoints = 8;

	private Vector2 _direction;
	private int _damage;
	private Color _color;
	private float _age;
	private bool _done;
	private Line2D _trail;
	private readonly Queue<Vector2> _history = new();

	public void Setup(Vector2 direction, int damage, Color color)
	{
		_direction = direction.Normalized();
		_damage = damage;
		_color = color;
	}

	public override void _Ready()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		// Top-level so the trail stays where the bolt has been instead of moving with it.
		_trail = new Line2D
		{
			TopLevel = true,
			Width = 5f,
			Material = additive,
			DefaultColor = _color,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
			Gradient = new Gradient { Colors = new[] { new Color(_color, 0f), _color }, Offsets = new[] { 0f, 1f } },
		};
		AddChild(_trail);
		AddChild(new Polygon2D
		{
			Material = additive,
			Color = _color.Lightened(0.5f),
			Polygon = new[] { new Vector2(7f, 0f), new Vector2(0f, 3.5f), new Vector2(-7f, 0f), new Vector2(0f, -3.5f) },
			Rotation = _direction.Angle(),
		});
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done)
			return;
		_age += (float)delta;
		if (_age >= Lifetime)
		{
			End(false);
			return;
		}

		Vector2 from = GlobalPosition;
		Vector2 to = from + _direction * Speed * (float)delta;
		var query = PhysicsRayQueryParameters2D.Create(from, to, 1u);
		var wall = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (wall.Count > 0)
		{
			GlobalPosition = (Vector2)wall["position"];
			End(true);
			return;
		}
		GlobalPosition = to;

		_history.Enqueue(to);
		while (_history.Count > TrailPoints)
			_history.Dequeue();
		_trail.Points = _history.ToArray();

		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy || enemy.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 } stats)
				continue;
			Vector2 offset = enemy.GlobalPosition - GlobalPosition;
			if (Mathf.Abs(offset.X) <= HitHalfWidth && Mathf.Abs(offset.Y) <= HitHalfHeight)
			{
				stats.TakeDamage(_damage, isProjectile: true, ignoreInvulnerability: true);
				End(true);
				return;
			}
		}
	}

	private void End(bool impact)
	{
		_done = true;
		if (impact)
			ImpactEffect.SpawnAt(this, GlobalPosition);
		Tween fade = CreateTween();
		fade.TweenProperty(this, "modulate:a", 0f, 0.12f);
		fade.TweenCallback(Callable.From(QueueFree));
	}
}
