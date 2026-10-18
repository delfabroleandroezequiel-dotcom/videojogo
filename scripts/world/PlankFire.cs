using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Anything made of planks that can catch fire (WoodenScaffold, a Flammable Puente). Barrel blasts
// and spreading fire find them through the "flammable" group and light whatever is in reach.
public interface IFlammable
{
	void IgniteAt(Vector2 globalPoint, float radius);
}

// Shared plank-fire kit so every wooden thing burns the same way: procedural flame tongues with a
// light, embers and smoke; burn damage ticks; fire jumping to nearby flammables and barrels; bandit
// GroundFire catching; charred boards falling once a plank gives way. Pure helpers — the owner keeps
// its own per-plank state and timing.
public static class PlankFire
{
	public const string FlammableGroup = "flammable";
	public static readonly Color Char = new(0.13f, 0.1f, 0.09f, 1f);

	private static Texture2D _dot;
	private static Texture2D Dot => _dot ??= GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres");
	private static CanvasItemMaterial _additive;
	public static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	// ── Flames ──────────────────────────────────────────────────────────────────────────────────

	// Hidden fire for one plank (rect in the owner's local space). Tongues rise from the top face
	// of a walkway plank, or stack along the centre line of a vertical barricade plank.
	public static Node2D Build(Rect2 rect, bool vertical, float flameHeight, RandomNumberGenerator seeded)
	{
		var fire = new Node2D { Visible = false, ZIndex = 2 };
		Vector2 basePoint = vertical ? rect.GetCenter() : new Vector2(rect.GetCenter().X, rect.Position.Y + 2f);
		const int tongues = 3;
		for (int t = 0; t < tongues; t++)
		{
			float along = t / (float)(tongues - 1);
			Vector2 offset = vertical
				? new Vector2(0f, Mathf.Lerp(-rect.Size.Y * 0.35f, rect.Size.Y * 0.35f, along))
				: new Vector2(Mathf.Lerp(-rect.Size.X * 0.35f, rect.Size.X * 0.35f, along), 0f);
			float h = flameHeight * seeded.RandfRange(0.6f, 1f);
			float w = seeded.RandfRange(5f, 8f);
			var outer = new Polygon2D
			{
				Position = basePoint + offset + (vertical ? new Vector2(0f, h * 0.3f) : Vector2.Zero),
				Polygon = FlameShape(w, h),
				Color = new Color(1f, 0.38f, 0.08f, 0.85f),
				Material = Additive,
			};
			outer.SetMeta("phase", seeded.RandfRange(0f, Mathf.Tau));
			outer.AddChild(new Polygon2D { Polygon = FlameShape(w * 0.5f, h * 0.55f), Color = new Color(1f, 0.85f, 0.45f, 0.9f), Material = Additive });
			fire.AddChild(outer);
		}
		fire.AddChild(new PointLight2D { Name = "Light", Position = basePoint + new Vector2(0f, -8f), Texture = Dot, TextureScale = 0.9f, Energy = 0f, Color = new Color(1f, 0.55f, 0.2f) });
		fire.AddChild(new CpuParticles2D
		{
			Texture = Dot, Amount = 5, Lifetime = 0.9f, Emitting = false, Position = basePoint + new Vector2(0f, -10f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(rect.Size.X * 0.4f, 2f),
			Direction = Vector2.Up, Spread = 25f, Gravity = new Vector2(0f, -70f),
			InitialVelocityMin = 20f, InitialVelocityMax = 50f, ScaleAmountMin = 0.015f, ScaleAmountMax = 0.035f,
			Color = new Color(1f, 0.7f, 0.3f), Material = Additive, LocalCoords = false,
		});
		fire.AddChild(new CpuParticles2D
		{
			Texture = Dot, Amount = 3, Lifetime = 1.4f, Emitting = false, Position = basePoint + new Vector2(0f, -flameHeight * 0.8f),
			Direction = Vector2.Up, Spread = 20f, Gravity = new Vector2(0f, -30f),
			InitialVelocityMin = 15f, InitialVelocityMax = 30f, ScaleAmountMin = 0.12f, ScaleAmountMax = 0.22f,
			ColorRamp = new Gradient { Colors = new[] { new Color(0.12f, 0.1f, 0.1f, 0.45f), new Color(0.1f, 0.1f, 0.1f, 0f) }, Offsets = new[] { 0f, 1f } },
			LocalCoords = false,
		});
		return fire;
	}

	public static void SetLit(Node2D fire, bool lit)
	{
		fire.Visible = lit;
		foreach (Node child in fire.GetChildren())
			if (child is CpuParticles2D particles)
				particles.Emitting = lit;
	}

	// Per-frame flicker; grow (0..1) fades the fire in when it first catches.
	public static void Animate(Node2D fire, float time, float grow, float seed)
	{
		int k = 0;
		foreach (Node child in fire.GetChildren())
		{
			if (child is Polygon2D tongue)
			{
				float phase = (float)tongue.GetMeta("phase");
				tongue.Scale = new Vector2(1f, grow * (0.75f + 0.3f * Mathf.Sin(time * 13f + phase) + 0.12f * Mathf.Sin(time * 29f + phase * 2f)));
				tongue.Skew = 0.18f * Mathf.Sin(time * 7f + phase + k++);
			}
			else if (child is PointLight2D light)
			{
				light.Energy = grow * (0.7f + 0.2f * Mathf.Sin(time * 17f + seed));
			}
		}
	}

	// Teardrop flame tongue with its base at (0,0), pointing up.
	private static Vector2[] FlameShape(float w, float h) => new[]
	{
		new Vector2(-w, 0f), new Vector2(-w * 0.8f, -h * 0.35f), new Vector2(-w * 0.35f, -h * 0.7f),
		new Vector2(0f, -h), new Vector2(w * 0.4f, -h * 0.65f), new Vector2(w * 0.85f, -h * 0.3f), new Vector2(w, 0f),
		new Vector2(0f, h * 0.15f),
	};

	// ── Gameplay ────────────────────────────────────────────────────────────────────────────────

	// The area a burning plank hurts: licks up above a walkway, out to both faces of a barricade.
	public static Rect2 FlameArea(Rect2 globalPlank, bool vertical, float flameHeight) => vertical
		? globalPlank.Grow(10f)
		: new Rect2(globalPlank.Position - new Vector2(0f, flameHeight), globalPlank.Size + new Vector2(0f, flameHeight));

	// Burn ticks for the player and enemies inside the flames, at most one per body per interval
	// (ticks is the owner's own "next tick" table, keyed by instance id).
	public static void HurtBodies(SceneTree tree, Rect2 flames, int damage, float interval, Dictionary<ulong, ulong> ticks)
	{
		if (damage <= 0)
			return;
		ulong now = Time.GetTicksMsec();
		foreach (Node node in tree.GetNodesInGroup("player"))
		{
			if (node is Node2D player && flames.Intersects(BodyRect(player)) && TickReady(player, now, interval, ticks)
				&& player.GetNodeOrNull<Stats>("Stats") is { IsInvulnerable: false } stats)
				stats.TakeDamage(damage, element: DamageElement.Fire);
		}
		foreach (Node node in tree.GetNodesInGroup("enemy"))
		{
			if (node is Node2D enemy && flames.Intersects(BodyRect(enemy)) && TickReady(enemy, now, interval, ticks))
				ProjectileReflect.TryHitEnemy(enemy, damage * 2, Vector2.Up, 0f, DamageElement.Fire);
		}
	}

	private static bool TickReady(Node2D body, ulong now, float interval, Dictionary<ulong, ulong> ticks)
	{
		ulong id = body.GetInstanceId();
		if (ticks.TryGetValue(id, out ulong next) && now < next)
			return false;
		ticks[id] = now + (ulong)(interval * 1000f);
		return true;
	}

	// Rough body box from the feet origin: 24 wide, 56 tall.
	private static Rect2 BodyRect(Node2D body) => new(body.GlobalPosition + new Vector2(-12f, -48f), new Vector2(24f, 56f));

	// Fire reaching out of a burning plank (global area already grown by the reach): other
	// flammables and any barrel standing in it.
	public static void SpreadFrom(Node source, Rect2 area)
	{
		SceneTree tree = source.GetTree();
		foreach (Node node in tree.GetNodesInGroup(FlammableGroup))
		{
			if (node != source && node is IFlammable other)
				other.IgniteAt(area.GetCenter(), Mathf.Max(area.Size.X, area.Size.Y) / 2f);
		}
		foreach (Node node in tree.GetNodesInGroup("explosive_barrel"))
		{
			if (node is ExplosiveBarrel barrel && area.HasPoint(barrel.GlobalPosition + new Vector2(0f, -4f)))
				barrel.Ignite();
		}
	}

	// A bandit's ember (GroundFire) landing on the planks sets them alight.
	public static void CatchGroundFire(SceneTree tree, IFlammable target)
	{
		foreach (Node node in tree.GetNodesInGroup(GroundFire.GroupName))
		{
			if (node is GroundFire fire && fire.IsBurning)
				target.IgniteAt(fire.GlobalPosition, 14f);
		}
	}

	public static float DistanceToRect(Vector2 p, Rect2 r)
	{
		float dx = Mathf.Max(Mathf.Max(r.Position.X - p.X, 0f), p.X - r.End.X);
		float dy = Mathf.Max(Mathf.Max(r.Position.Y - p.Y, 0f), p.Y - r.End.Y);
		return Mathf.Sqrt(dx * dx + dy * dy);
	}

	// ── Collapse FX ─────────────────────────────────────────────────────────────────────────────

	// Takes a piece already in the scene at its global start position and lets it tumble down and
	// fade out, then frees it.
	public static void Tumble(Node2D piece, RandomNumberGenerator rng, float life = 1.1f)
	{
		Vector2 origin = piece.GlobalPosition;
		float startRotation = piece.Rotation;
		Vector2 velocity = new(rng.RandfRange(-50f, 50f), rng.RandfRange(-60f, 10f));
		float spin = rng.RandfRange(-5f, 5f);
		Tween tween = piece.CreateTween();
		tween.TweenMethod(Callable.From<float>(t =>
		{
			if (!GodotObject.IsInstanceValid(piece))
				return;
			piece.GlobalPosition = origin + velocity * t + new Vector2(0f, 1100f) * t * t * 0.5f;
			piece.Rotation = startRotation + spin * t;
			piece.Modulate = piece.Modulate with { A = 1f - Mathf.Clamp((t - life * 0.55f) / (life * 0.45f), 0f, 1f) };
		}), 0f, life, life);
		tween.TweenCallback(Callable.From(piece.QueueFree));
	}

	public static void Sparks(Node context, Vector2 at)
	{
		if (context.GetTree().CurrentScene is not Node scene)
			return;
		var sparks = new CpuParticles2D
		{
			Texture = Dot, Amount = 10, Lifetime = 0.5f, OneShot = true, Explosiveness = 0.9f,
			Direction = Vector2.Up, Spread = 80f, Gravity = new Vector2(0f, 400f),
			InitialVelocityMin = 40f, InitialVelocityMax = 140f, ScaleAmountMin = 0.02f, ScaleAmountMax = 0.05f,
			Color = new Color(1f, 0.6f, 0.2f), Material = Additive, LocalCoords = false,
		};
		scene.AddChild(sparks);
		sparks.GlobalPosition = at;
		sparks.Emitting = true;
		sparks.GetTree().CreateTimer(0.8f).Timeout += sparks.QueueFree;
	}
}
