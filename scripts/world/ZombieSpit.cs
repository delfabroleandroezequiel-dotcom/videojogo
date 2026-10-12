using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// ZombieSpitter's glob, drawn with the pack's own projectile sheet (ZombieSpitSpriteFrames "fly").
// Lobbed under gravity; splashes the player with Poison damage, and where it lands on a floor it
// leaves a PoisonPuddle (if one is given). Walls just splash it.
public partial class ZombieSpit : Node2D
{
	private const string FramesPath = "res://resources/sprites/ZombieSpitSpriteFrames.tres";

	public Vector2 Velocity;
	public Stats ThrowerStats;
	public float DamageMultiplier = 0.7f;
	public float GravityScale = 650f;
	public float VisualScale = 2f;
	public PackedScene PuddleScene;
	public Color GlowColor = new(0.45f, 1f, 0.55f, 1f);

	private float _age;

	public override void _Ready()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var dot = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
		AddChild(new Sprite2D { Texture = dot, Material = additive, Modulate = new Color(GlowColor, 0.6f), Scale = Vector2.One * 0.2f });
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = GD.Load<SpriteFrames>(FramesPath),
			Scale = new Vector2(VisualScale, VisualScale),
			// Glob drawn at x24-37 y28-34 of a 64 cell: centre it on the node.
			Position = new Vector2(1.5f * VisualScale, 1f * VisualScale),
			FlipH = Velocity.X < 0f,
		};
		AddChild(sprite);
		sprite.Play("fly");
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		if (_age > 3f)
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
			Vector2 at = (Vector2)hit["position"];
			GroundSlamFx.Chip(this, at, 0.3f, new GroundSlamFx.Palette
			{
				Hot = GlowColor,
				Core = new Color(0.9f, 1f, 0.9f, 1f),
				Rock = new Color(0.3f, 0.45f, 0.3f, 1f),
				Dust = new Color(0.4f, 0.6f, 0.4f, 0.5f),
				Light = GlowColor,
			});
			if (((Vector2)hit["normal"]).Y < -0.7f && PuddleScene is not null && GetTree().CurrentScene is Node scene)
			{
				Node2D puddle = PuddleScene.Instantiate<Node2D>();
				scene.AddChild(puddle);
				puddle.GlobalPosition = at;
			}
			QueueFree();
			return;
		}
		GlobalPosition = next;
		Rotation = Velocity.X < 0f ? Velocity.Angle() - Mathf.Pi : Velocity.Angle();

		if (PlayerReads.Find(this) is { } player && GlobalPosition.DistanceTo(player.GlobalPosition) < 20f)
		{
			if (player.GetNodeOrNull<Stats>("Stats") is { } stats && !stats.IsInvulnerable)
				stats.TakeDamage(Mathf.Max(1, Mathf.RoundToInt((ThrowerStats?.AttackPower ?? 10) * DamageMultiplier)),
					isProjectile: true, element: DamageElement.Poison);
			QueueFree();
		}
	}
}
