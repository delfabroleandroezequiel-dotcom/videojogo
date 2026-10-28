using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Small shared helpers for the procedural trap batch (FarolImplosivo, BloqueFurioso, ...):
// the additive material, the soft dot / light textures the neon kit uses, and the "hurt whatever
// this is if it's the player (or an enemy)" call with knockback, so each trap stays about its
// behaviour instead of repeating the same boilerplate.
public static class TrapKit
{
	public const string DotPath = "res://resources/particles/SoftDotGradient.tres";
	public const string LightPath = "res://resources/lighting/PointLightGradient.tres";

	public static CanvasItemMaterial Additive() => new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
	public static Texture2D Dot() => GD.Load<Texture2D>(DotPath);
	public static Texture2D LightTexture() => GD.Load<Texture2D>(LightPath);

	public static bool IsPlayer(Node body) => body is not null && body.IsInGroup("player");

	// Damages a player/enemy body. Returns false if it had no Stats, was invulnerable or dead.
	public static bool Hurt(Node2D body, int damage, Vector2 knockDirection, float knockback, bool instantKill = false)
	{
		if (body?.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.CurrentHealth <= 0)
			return false;
		if (instantKill)
		{
			stats.Kill();
			return true;
		}
		if (stats.IsInvulnerable)
			return false;
		// Traps are never parried (see Hazard.TryHit): several of these hit every frame on contact.
		System.Func<bool> interceptor = stats.IncomingHitInterceptor;
		stats.IncomingHitInterceptor = null;
		try
		{
			stats.TakeDamage(damage);
		}
		finally
		{
			stats.IncomingHitInterceptor = interceptor;
		}
		if (knockback > 0f && body.HasMethod("ApplyKnockback"))
		{
			Vector2 dir = knockDirection == Vector2.Zero ? Vector2.Up : knockDirection.Normalized();
			body.Call("ApplyKnockback", dir, knockback);
		}
		return true;
	}

	public static PointLight2D Light(Color color, float energy, float scale)
	{
		return new PointLight2D { Texture = LightTexture(), Color = color, Energy = energy, TextureScale = scale };
	}

	public static Line2D Line(Vector2[] points, float width, Color color, bool additive = true)
	{
		var line = new Line2D { Points = points, Width = width, DefaultColor = color };
		if (additive)
			line.Material = Additive();
		return line;
	}

	public static Polygon2D Poly(Vector2[] points, Color color) => new() { Polygon = points, Color = color };

	public static Vector2[] Rect(Vector2 centre, Vector2 size)
	{
		Vector2 h = size / 2f;
		return new[] { centre + new Vector2(-h.X, -h.Y), centre + new Vector2(h.X, -h.Y), centre + new Vector2(h.X, h.Y), centre + new Vector2(-h.X, h.Y) };
	}

	public static Vector2[] Circle(Vector2 centre, float radius, int segments = 20)
	{
		var pts = new Vector2[segments];
		for (int i = 0; i < segments; i++)
			pts[i] = centre + Vector2.Right.Rotated(Mathf.Tau * i / segments) * radius;
		return pts;
	}

	// A one-shot burst of additive sparks at a world point (explosions, pops, impacts).
	public static void Burst(Node context, Vector2 at, Color color, int amount = 24, float speed = 220f, float lifetime = 0.45f, float scale = 0.07f)
	{
		Node parent = context.GetTree().CurrentScene;
		if (parent is null)
			return;
		var sparks = new GpuParticles2D
		{
			Amount = amount,
			Lifetime = lifetime,
			OneShot = true,
			Explosiveness = 1f,
			Texture = Dot(),
			Material = Additive(),
			Modulate = color,
			ZIndex = 6,
			ProcessMaterial = new ParticleProcessMaterial
			{
				Direction = new Vector3(0f, -1f, 0f),
				Spread = 180f,
				InitialVelocityMin = speed * 0.4f,
				InitialVelocityMax = speed,
				Gravity = new Vector3(0f, 400f, 0f),
				ScaleMin = scale * 0.5f,
				ScaleMax = scale,
			},
		};
		parent.AddChild(sparks);
		sparks.GlobalPosition = at;
		sparks.Emitting = true;
		sparks.GetTree().CreateTimer(lifetime + 0.3f).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(sparks))
				sparks.QueueFree();
		};
	}

	// Expanding additive ring + flash (explosions).
	public static void Blast(Node context, Vector2 at, float radius, Color color)
	{
		Node parent = context.GetTree().CurrentScene;
		if (parent is null)
			return;
		var root = new Node2D { ZIndex = 6 };
		parent.AddChild(root);
		root.GlobalPosition = at;
		var flash = new Sprite2D { Texture = Dot(), Material = Additive(), Modulate = color, Scale = Vector2.One * 0.2f };
		var ring = new Line2D { Points = Circle(Vector2.Zero, 1f, 28), Closed = true, Width = 0.12f, DefaultColor = color, Material = Additive() };
		root.AddChild(flash);
		root.AddChild(ring);
		var light = Light(color, 2.5f, radius / 60f);
		root.AddChild(light);
		Tween t = root.CreateTween();
		t.TweenProperty(flash, "scale", Vector2.One * (radius / 30f), 0.15f);
		t.Parallel().TweenProperty(ring, "scale", Vector2.One * radius, 0.25f).SetEase(Tween.EaseType.Out);
		t.Parallel().TweenProperty(root, "modulate:a", 0f, 0.35f);
		t.Parallel().TweenProperty(light, "energy", 0f, 0.35f);
		t.TweenCallback(Callable.From(root.QueueFree));
		Burst(context, at, color, 30, radius * 3f);
	}

	public static void Shake(Node context, float strength, float duration)
	{
		if (context.GetTree().GetFirstNodeInGroup("player") is Node p && p.HasMethod("ShakeCamera"))
			p.Call("ShakeCamera", strength, duration);
	}

	public static Node2D FindPlayer(Node context) => context.GetTree().GetFirstNodeInGroup("player") as Node2D;
}
