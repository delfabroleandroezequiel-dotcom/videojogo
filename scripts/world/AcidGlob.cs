using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A glob of acid spat by the AcidSlime: arcs under gravity, splashes the player for a little
// Poison damage, and where it lands on the floor it leaves a PoisonPuddle behind. Drawn in code (a
// glowing green blob), no sprite.
public partial class AcidGlob : Node2D
{
	public Vector2 Velocity;
	public Stats ThrowerStats;
	public float DamageMultiplier = 0.4f;
	public PackedScene PuddleScene;
	public float GravityScale = 700f;
	public Color GlowColor = new(0.55f, 1f, 0.25f, 1f);

	private float _age;

	public override void _Ready()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = GlowColor, Scale = Vector2.One * 0.22f });
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = new Color(0.9f, 1f, 0.7f, 1f), Scale = Vector2.One * 0.08f });
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		if (_age > 2.5f)
		{
			QueueFree();
			return;
		}
		Velocity.Y += GravityScale * dt;
		Vector2 next = GlobalPosition + Velocity * dt;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next, 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			// Only floors leave a puddle; walls just splash.
			if (((Vector2)hit["normal"]).Y < -0.7f && PuddleScene is not null && GetTree().CurrentScene is Node scene)
			{
				Node2D puddle = PuddleScene.Instantiate<Node2D>();
				scene.AddChild(puddle);
				puddle.GlobalPosition = (Vector2)hit["position"];
			}
			QueueFree();
			return;
		}
		GlobalPosition = next;

		if (PlayerReads.Find(this) is { } player && GlobalPosition.DistanceTo(player.GlobalPosition) < 20f)
		{
			if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable)
				stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier)),
					isProjectile: true, element: DamageElement.Poison);
			QueueFree();
		}
	}
}
