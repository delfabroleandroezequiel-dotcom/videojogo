using System.Collections.Generic;
using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The Ecos a kill pays out, delivered as a coin shower instead of a silent += : a handful of cyan
// wisps (same look as EcosRemnant, smaller) bursts out of the corpse, hangs in the air for a beat,
// then each one curves into the player, shedding sparks, and pops in a flash on arrival. Every wisp
// banks its share of the reward as it lands, so the HUD counter climbs in steps. No sound yet — a
// real one is to be picked. Built in code, spawned by Enemy.GrantEcos.
public partial class EcosBurst : Node2D
{
	private const string DotPath = "res://resources/particles/SoftDotGradient.tres";
	private const string LightPath = "res://resources/lighting/PointLightGradient.tres";
	private const int MinOrbs = 8;
	private const int MaxOrbs = 15;
	// Outward burst, then the hover beat before the wisps home in (staggered so they land in a run).
	private const float BurstSpeedMin = 110f;
	private const float BurstSpeedMax = 210f;
	private const float BurstDrag = 6f;
	private const float HoverTime = 0.3f;
	private const float LaunchStagger = 0.035f;
	// Homing: speed ramps up, steering turns the leftover sideways drift into a curve. The turn
	// tightens the longer a wisp flies, and inside DirectDistance (or after DirectAfter) it flies
	// straight in — a capped turn rate at full speed would otherwise circle the player forever.
	private const float HomeStartSpeed = 60f;
	private const float HomeAcceleration = 1400f;
	private const float HomeMaxSpeed = 900f;
	private const float HomeTurnRate = 7f;
	private const float HomeTurnGrowth = 30f;
	private const float DirectDistance = 48f;
	private const float DirectAfter = 0.35f;
	private const float ArriveDistance = 16f;
	// Safety net: whatever hasn't arrived by then is banked anyway.
	private const float MaxLifetime = 3f;

	private static readonly Color WispColor = new(0.45f, 0.95f, 1f, 1f);

	private sealed class Orb
	{
		public Node2D Node;
		public CpuParticles2D Sparks;
		public Vector2 Velocity;
		public float LaunchAt;
		public int Share;
		public bool Done;
	}

	private readonly List<Orb> _orbs = new();
	private CanvasItemMaterial _additive;
	private Texture2D _soft;
	private float _age;
	private int _unbanked;

	public static void Spawn(Node context, Vector2 origin, int amount)
	{
		if (amount <= 0)
			return;
		if (context.GetTree()?.CurrentScene is not Node scene)
		{
			SaveManager.Instance.AddEcos(amount);
			return;
		}
		var burst = new EcosBurst { TopLevel = true, ZIndex = 50 };
		scene.AddChild(burst);
		burst.GlobalPosition = origin;
		burst.Build(amount);
	}

	private void Build(int amount)
	{
		_unbanked = amount;
		int count = Mathf.Min(amount, Mathf.Clamp(6 + amount / 5, MinOrbs, MaxOrbs));
		_additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		_soft = GD.Load<Texture2D>(DotPath);
		var light = GD.Load<Texture2D>(LightPath);
		for (int i = 0; i < count; i++)
		{
			// Mostly upward fan, like coins popping out of a slot machine.
			float angle = Mathf.DegToRad((float)GD.RandRange(-160.0, -20.0));
			float speed = (float)GD.RandRange(BurstSpeedMin, BurstSpeedMax);
			var node = new Node2D();
			node.AddChild(new Sprite2D { Texture = _soft, Material = _additive, Modulate = WispColor with { A = 0.7f }, Scale = Vector2.One * 0.26f });
			node.AddChild(new Sprite2D { Texture = _soft, Material = _additive, Modulate = WispColor.Lightened(0.8f), Scale = Vector2.One * 0.1f });
			node.AddChild(new PointLight2D { Texture = light, Color = WispColor, Energy = 0.5f, TextureScale = 0.06f });
			// Sparks shed along the flight path (world coords, so they hang where they fell off).
			var sparks = new CpuParticles2D
			{
				Amount = 10,
				Lifetime = 0.35f,
				Texture = _soft,
				Material = _additive,
				EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
				EmissionSphereRadius = 1.5f,
				Spread = 180f,
				Gravity = Vector2.Zero,
				InitialVelocityMin = 3f,
				InitialVelocityMax = 10f,
				ScaleAmountMin = 0.025f,
				ScaleAmountMax = 0.05f,
				Color = WispColor,
				ColorRamp = new Gradient { Colors = new[] { WispColor.Lightened(0.5f), WispColor with { A = 0f } } },
				LocalCoords = false,
			};
			node.AddChild(sparks);
			AddChild(node);
			_orbs.Add(new Orb
			{
				Node = node,
				Sparks = sparks,
				Velocity = Vector2.FromAngle(angle) * speed,
				LaunchAt = HoverTime + i * LaunchStagger,
				// Remainder goes to the first wisps so the shares always add up to the reward.
				Share = amount / count + (i < amount % count ? 1 : 0),
			});
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_age += dt;
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		bool playerDead = player?.GetNodeOrNull<Stats>("Stats") is { CurrentHealth: <= 0 };
		// Died with wisps still in the air: they go out with the rest of the death (the drop was
		// already taken), not banked into the fresh count afterwards.
		if (playerDead)
		{
			_unbanked = 0;
			QueueFree();
			return;
		}
		if (player is null || _age > MaxLifetime)
		{
			QueueFree();
			return;
		}

		Vector2 target = player.GlobalPosition;
		bool any = false;
		foreach (Orb orb in _orbs)
		{
			if (orb.Done)
				continue;
			any = true;
			Vector2 position = orb.Node.GlobalPosition;
			if (_age < orb.LaunchAt)
			{
				// Burst out and settle into a gentle bob.
				orb.Velocity = orb.Velocity.Lerp(Vector2.Zero, Mathf.Min(1f, BurstDrag * dt));
				position += orb.Velocity * dt + new Vector2(0f, Mathf.Sin((_age + orb.LaunchAt) * 14f) * 0.15f);
				if (_age + dt >= orb.LaunchAt)
				{
					// Kick off sideways so the flight in curves instead of beelining.
					Vector2 toPlayer = (target - position).Normalized();
					float side = GD.Randf() < 0.5f ? -1f : 1f;
					orb.Velocity = (toPlayer.Orthogonal() * side * 0.9f - toPlayer * 0.3f).Normalized() * HomeStartSpeed * 3f;
				}
			}
			else
			{
				Vector2 toPlayer = target - position;
				if (toPlayer.Length() <= ArriveDistance)
				{
					Arrive(orb, player);
					continue;
				}
				float speed = Mathf.Min(HomeMaxSpeed, Mathf.Max(HomeStartSpeed, orb.Velocity.Length()) + HomeAcceleration * dt);
				float flown = _age - orb.LaunchAt;
				Vector2 heading = toPlayer.Length() <= DirectDistance || flown >= DirectAfter
					? toPlayer.Normalized()
					: orb.Velocity.Normalized().Slerp(toPlayer.Normalized(), Mathf.Min(1f, (HomeTurnRate + HomeTurnGrowth * flown) * dt));
				orb.Velocity = heading * speed;
				Vector2 step = orb.Velocity * dt;
				// Don't overshoot on a fast frame.
				if (step.Length() >= toPlayer.Length())
				{
					Arrive(orb, player);
					continue;
				}
				position += step;
			}
			orb.Node.GlobalPosition = position;
		}
		if (!any)
			QueueFree();
	}

	private void Arrive(Orb orb, Node2D player)
	{
		orb.Done = true;
		orb.Node.QueueFree();
		_unbanked -= orb.Share;
		SaveManager.Instance.AddEcos(orb.Share);
		SpawnArrivalFlash(player);
	}

	// A quick swell-and-fade glow plus a ring of sparks on the player, parented to them so it rides
	// along while they move.
	private void SpawnArrivalFlash(Node2D player)
	{
		var flash = new Node2D { ZIndex = 50 };
		player.AddChild(flash);
		var glow = new Sprite2D { Texture = _soft, Material = _additive, Modulate = WispColor with { A = 0.6f }, Scale = Vector2.One * 0.15f };
		flash.AddChild(glow);
		flash.AddChild(new CpuParticles2D
		{
			Emitting = true,
			OneShot = true,
			Explosiveness = 1f,
			Amount = 6,
			Lifetime = 0.3f,
			Texture = _soft,
			Material = _additive,
			Spread = 180f,
			Gravity = Vector2.Zero,
			InitialVelocityMin = 30f,
			InitialVelocityMax = 60f,
			DampingMin = 120f,
			DampingMax = 160f,
			ScaleAmountMin = 0.03f,
			ScaleAmountMax = 0.05f,
			Color = WispColor.Lightened(0.4f),
			ColorRamp = new Gradient { Colors = new[] { WispColor.Lightened(0.6f), WispColor with { A = 0f } } },
		});
		Tween tween = flash.CreateTween();
		tween.SetParallel();
		tween.TweenProperty(glow, "scale", Vector2.One * 0.45f, 0.25f).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(glow, "modulate:a", 0f, 0.25f);
		tween.Chain().TweenInterval(0.1f);
		tween.Chain().TweenCallback(Callable.From(flash.QueueFree));
	}

	// Left the tree before every wisp landed (scene change, lifetime cap): bank the rest so a kill
	// never pays less than it used to.
	public override void _ExitTree()
	{
		if (_unbanked > 0)
			SaveManager.Instance.AddEcos(_unbanked);
		_unbanked = 0;
	}
}
