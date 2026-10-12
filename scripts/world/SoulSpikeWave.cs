using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// DeathKnight's rising-slash wave: a row of spectral spikes erupting out of the floor one after
// another, marching away from the knight. Each spike follows the ground under it (raycast), the
// march stops at a wall or a ledge, and each one can hurt the player once while it's up. Drawn in
// code (additive polygons + a soft light), no sprites.
public partial class SoulSpikeWave : Node2D
{
	public float Direction = 1f;
	public Stats ThrowerStats;
	public float DamageMultiplier = 1f;
	public Color GlowColor = new(0.3f, 1f, 0.85f, 1f);
	public int Count = 7;
	public float Spacing = 30f;
	public float Interval = 0.07f;
	public float SpikeHeight = 46f;
	public float SpikeWidth = 14f;

	private bool _hitPlayer;

	public override void _Ready() => March();

	private async void March()
	{
		Vector2 at = GlobalPosition;
		for (int i = 0; i < Count; i++)
		{
			Vector2 next = at + new Vector2(Direction * Spacing, 0f);
			// Blocked by a wall between the spikes → the wave dies there.
			var wallQuery = PhysicsRayQueryParameters2D.Create(at + new Vector2(0f, -10f), next + new Vector2(0f, -10f), 1u);
			if (i > 0 && GetWorld2D().DirectSpaceState.IntersectRay(wallQuery).Count > 0)
				break;
			// Follow the floor; no floor (a ledge) → stop.
			var floorQuery = PhysicsRayQueryParameters2D.Create(next + new Vector2(0f, -24f), next + new Vector2(0f, 40f), 1u);
			var floor = GetWorld2D().DirectSpaceState.IntersectRay(floorQuery);
			if (floor.Count == 0)
				break;
			at = (Vector2)floor["position"];
			Erupt(at, 1f + 0.06f * i);
			await ToSignal(GetTree().CreateTimer(Interval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
		}
		await ToSignal(GetTree().CreateTimer(0.6f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			QueueFree();
	}

	private void Erupt(Vector2 ground, float sizeScale)
	{
		var spike = new Node2D { GlobalPosition = ground };
		AddChild(spike);
		spike.GlobalPosition = ground;
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		float h = SpikeHeight * sizeScale;
		float w = SpikeWidth * sizeScale;
		// Main blade plus two smaller side shards.
		var main = new Polygon2D
		{
			Polygon = new[] { new Vector2(-w * 0.5f, 0f), new Vector2(0f, -h), new Vector2(w * 0.5f, 0f) },
			Color = GlowColor,
			Material = additive,
		};
		var core = new Polygon2D
		{
			Polygon = new[] { new Vector2(-w * 0.18f, 0f), new Vector2(0f, -h * 0.85f), new Vector2(w * 0.18f, 0f) },
			Color = new Color(0.9f, 1f, 0.97f, 1f),
			Material = additive,
		};
		var left = new Polygon2D
		{
			Polygon = new[] { new Vector2(-w * 0.9f, 0f), new Vector2(-w * 0.55f, -h * 0.45f), new Vector2(-w * 0.2f, 0f) },
			Color = GlowColor * new Color(1f, 1f, 1f, 0.7f),
			Material = additive,
		};
		var right = new Polygon2D
		{
			Polygon = new[] { new Vector2(w * 0.2f, 0f), new Vector2(w * 0.6f, -h * 0.55f), new Vector2(w * 0.95f, 0f) },
			Color = GlowColor * new Color(1f, 1f, 1f, 0.7f),
			Material = additive,
		};
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = GlowColor,
			Energy = 0.9f,
			TextureScale = 0.35f,
			Position = new Vector2(0f, -h * 0.4f),
		};
		spike.AddChild(light);
		spike.AddChild(left);
		spike.AddChild(right);
		spike.AddChild(main);
		spike.AddChild(core);

		// Shoots up out of the floor, lingers a moment, sinks and fades.
		spike.Scale = new Vector2(1f, 0.05f);
		Tween tween = spike.CreateTween();
		tween.TweenProperty(spike, "scale", Vector2.One, 0.06f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenInterval(0.22f);
		tween.TweenProperty(spike, "scale", new Vector2(0.6f, 0.0f), 0.2f).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(spike, "modulate:a", 0f, 0.2f);
		tween.TweenCallback(Callable.From(spike.QueueFree));

		HurtAt(ground, h, w);
	}

	private void HurtAt(Vector2 ground, float h, float w)
	{
		if (_hitPlayer)
			return;
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = new RectangleShape2D { Size = new Vector2(w * 1.6f, h) },
			Transform = new Transform2D(0f, ground + new Vector2(0f, -h * 0.5f)),
			CollisionMask = 2,
			CollideWithBodies = true,
		};
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 4))
		{
			if (hit["collider"].AsGodotObject() is not Node2D body || !body.IsInGroup("player"))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.IsInvulnerable)
				continue;
			_hitPlayer = true;
			stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier)), isProjectile: true);
			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", new Vector2(Direction * 0.4f, -1f).Normalized(), 300f);
			return;
		}
	}
}
