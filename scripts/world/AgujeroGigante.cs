using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// One of the Gigante's body holes: a weak point that attacks. Child of the body part it sits on
// (torso, arm, head) at the ring's centre, in that part's own pixels, so it moves, bends and scales
// with the body. GiganteEnsamblado picks which hole fires and when; the hole only runs the attack.
//
// It has its own health (a StaticBody2D "Herida" in the enemy layer/group with a Stats): the
// player's sword or a parried shot sent back into it damages it, and at 0 the hole closes for good.
// Every attack starts with the same neon-green blink tell, then:
//  Proyectiles — a burst of parryable neon shots aimed at the player.
//  Succion     — inhales: pulls the player in (also over the surface while climbing); too close
//                and it swallows them (damage) and spits them out.
//  Viento      — a gust aimed at the player that shoves them away (slides them while climbing).
//  Gas         — a lingering poison cloud on the hole that cuts off that part of the climb.
//  Larvas      — crawlers that creep over the body toward the player and bite (costs grip stamina).
//  Tentaculo   — a tentacle lashes out; if it catches the player it holds them (snare) and hurts
//                until it lets go or gets cut (its tip can be hit).
public partial class AgujeroGigante : Node2D
{
	public enum Ataque { Proyectiles, Succion, Viento, Gas, Larvas, Tentaculo }

	[Signal] public delegate void AgujeroCerradoEventHandler();

	[Export] public Ataque Tipo = Ataque.Proyectiles;
	// Hole radius in the parent part's pixels (hurtbox + glow).
	[Export] public float Radio = 45f;
	[Export] public int Vida = 60;
	[Export] public int Danio = 12;
	[Export] public float Telegrafo = 0.7f;
	[Export] public Color ColorNeon = new(0.35f, 1f, 0.45f);

	[ExportGroup("Proyectiles")]
	[Export] public PackedScene Proyectil;
	[Export] public int Disparos = 3;
	[Export] public float EntreDisparos = 0.2f;
	[Export] public float VelocidadDisparo = 330f;

	[ExportGroup("Succion")]
	[Export] public float AlcanceSuccion = 700f;
	[Export] public float FuerzaSuccion = 230f;
	[Export] public float DuracionSuccion = 2.4f;
	[Export] public float RadioTragar = 70f;
	[Export] public int DanioTragar = 25;

	[ExportGroup("Viento")]
	[Export] public float AlcanceViento = 750f;
	[Export] public float FuerzaViento = 420f;
	[Export] public float DuracionViento = 1.6f;
	[Export] public float ConoVientoGrados = 30f;

	[ExportGroup("Gas")]
	[Export] public float RadioGas = 170f;
	[Export] public float DuracionGas = 5f;

	[ExportGroup("Larvas")]
	[Export] public SpriteFrames LarvaFrames;
	[Export] public int Larvas = 3;

	[ExportGroup("Tentaculo")]
	[Export] public float LargoTentaculo = 700f;
	[Export] public float AgarreTentaculo = 2.2f;

	public bool Cerrado { get; private set; }
	public bool Ocupado { get; private set; }
	// Climbable zones of the whole body (larvae crawl only on them); set by the Gigante.
	public ZonaTrepable[] Zonas = System.Array.Empty<ZonaTrepable>();

	private Polygon2D _glow;
	private Polygon2D _plug;
	private StaticBody2D _herida;
	private Stats _stats;

	public override void _Ready()
	{
		_glow = NeonFx.Circle(Radio * 1.15f, ColorNeon, additive: true);
		_glow.Modulate = new Color(1, 1, 1, 0);
		AddChild(_glow);

		_herida = new StaticBody2D { Name = "Herida", CollisionLayer = ProjectileReflect.EnemyLayer, CollisionMask = 0 };
		_herida.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Radio } });
		_stats = new Stats { Name = "Stats", MaxHealth = Vida, AttackPower = Danio };
		_herida.AddChild(_stats);
		_herida.AddToGroup("enemy");
		AddChild(_herida);
		_stats.Died += Cerrar;
		_stats.HitTaken += _ => Flash(new Color(1.8f, 1.8f, 1.8f), 0.12f);
	}

	public float GlobalRadius => Radio * GlobalScale.X;

	// Where loose FX and shots go: the level, so they outlive the hole and don't ride the body.
	private Node FxParent => GetTree().CurrentScene ?? GetTree().Root;

	// How far from the hole this attack can actually reach the player (world pixels) — the Gigante
	// only picks holes whose attack would land.
	public float Alcance => Tipo switch
	{
		Ataque.Proyectiles => 1600f,
		Ataque.Succion => AlcanceSuccion,
		Ataque.Viento => AlcanceViento,
		Ataque.Gas => RadioGas * GlobalScale.X + 250f,
		Ataque.Larvas => 1000f,
		_ => LargoTentaculo,
	};

	private void Cerrar()
	{
		if (Cerrado)
			return;
		Cerrado = true;
		_herida.QueueFree();
		// A dark scar plugs the ring so a closed hole reads at a glance.
		_plug = NeonFx.Circle(Radio * 0.8f, new Color(0.12f, 0.08f, 0.07f, 0.92f), additive: false);
		AddChild(_plug);
		_glow.Modulate = new Color(1, 1, 1, 0);
		EmitSignal(SignalName.AgujeroCerrado);
	}

	private void Flash(Color color, float time)
	{
		if (_plug is not null)
			return;
		_glow.Color = color;
		Tween t = CreateTween();
		t.TweenProperty(_glow, "color", ColorNeon, time);
	}

	// ── Run one attack ──────────────────────────────────────────────────────────────────────────
	public async void Atacar(Node2D target)
	{
		if (Cerrado || Ocupado || target is null)
			return;
		Ocupado = true;

		// Tell: three quick neon blinks, the last held.
		Tween tell = CreateTween();
		float blink = Telegrafo / 5f;
		for (int i = 0; i < 2; i++)
		{
			tell.TweenProperty(_glow, "modulate:a", 1f, blink * 0.5f);
			tell.TweenProperty(_glow, "modulate:a", 0.15f, blink * 0.5f);
		}
		tell.TweenProperty(_glow, "modulate:a", 1f, blink);
		await ToSignal(GetTree().CreateTimer(Telegrafo), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || Cerrado || !IsInstanceValid(target))
		{
			EndAttack();
			return;
		}

		switch (Tipo)
		{
			case Ataque.Proyectiles: await Proyectiles(target); break;
			case Ataque.Succion: await Flujo(target, inhale: true); break;
			case Ataque.Viento: await Flujo(target, inhale: false); break;
			case Ataque.Gas: Gas(); break;
			case Ataque.Larvas: SoltarLarvas(target); break;
			case Ataque.Tentaculo: await Tentaculo(target); break;
		}
		EndAttack();
	}

	private void EndAttack()
	{
		if (!IsInstanceValid(this))
			return;
		Ocupado = false;
		if (!Cerrado)
			CreateTween().TweenProperty(_glow, "modulate:a", 0f, 0.3f);
	}

	private static Vector2 Aim(Node2D target) => target.GlobalPosition + new Vector2(0f, -30f);

	private async System.Threading.Tasks.Task Proyectiles(Node2D target)
	{
		if (Proyectil is null)
			return;
		for (int i = 0; i < Disparos; i++)
		{
			if (!IsInstanceValid(this) || Cerrado || !IsInstanceValid(target))
				return;
			var shot = Proyectil.Instantiate<Projectile>();
			shot.Speed = VelocidadDisparo;
			FxParent.AddChild(shot);
			shot.GlobalPosition = GlobalPosition;
			shot.Launch(Aim(target) - GlobalPosition, _stats);
			Flash(new Color(1.6f, 2f, 1.6f), 0.15f);
			await ToSignal(GetTree().CreateTimer(EntreDisparos), SceneTreeTimer.SignalName.Timeout);
		}
	}

	// Suction (inhale) or gust (exhale): moves the player every physics frame for the duration.
	private async System.Threading.Tasks.Task Flujo(Node2D target, bool inhale)
	{
		float duration = inhale ? DuracionSuccion : DuracionViento;
		float range = inhale ? AlcanceSuccion : AlcanceViento;
		Vector2 gustDir = (Aim(target) - GlobalPosition).Normalized();
		float elapsed = 0f;
		while (elapsed < duration)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || Cerrado || !IsInstanceValid(target))
				return;
			float dt = (float)GetPhysicsProcessDeltaTime();
			elapsed += dt;

			Vector2 toTarget = Aim(target) - GlobalPosition;
			float distance = toTarget.Length();
			// Streaks: flowing into the hole, or out along the gust.
			if (Engine.GetPhysicsFrames() % 2 == 0)
			{
				Vector2 dir = inhale ? Vector2.FromAngle(GD.Randf() * Mathf.Tau) : gustDir.Rotated(GD.RandRange(-1, 1) * Mathf.DegToRad(ConoVientoGrados));
				float far = range * (float)GD.RandRange(0.35, 0.8);
				Vector2 a = inhale ? GlobalPosition + dir * far : GlobalPosition + dir * GlobalRadius;
				Vector2 b = inhale ? GlobalPosition + dir * GlobalRadius : GlobalPosition + dir * far;
				NeonFx.Streak(FxParent, a, b, inhale ? new Color(0.6f, 1f, 0.8f) : new Color(0.8f, 1f, 1f), 0.35f);
			}
			if (distance > range || target is not Metroidvania.Player.Player player)
				continue;

			float falloff = 1f - distance / range;
			if (inhale)
			{
				player.AddExternalDisplacement(-toTarget.Normalized() * FuerzaSuccion * (0.4f + falloff) * dt);
				if (distance < RadioTragar * GlobalScale.X + GlobalRadius * 0.5f)
				{
					// Swallowed: bite, then spat back out the way they came.
					player.GetNode<Stats>("Stats").TakeDamage(DanioTragar);
					player.ApplyKnockbackThroughBlock(toTarget.Normalized(), 520f);
					NeonFx.Burst(FxParent, GlobalPosition, ColorNeon, GlobalRadius * 2.5f);
					return;
				}
			}
			else
			{
				float angle = Mathf.Abs(gustDir.AngleTo(toTarget));
				if (angle <= Mathf.DegToRad(ConoVientoGrados))
					player.AddExternalDisplacement(gustDir * FuerzaViento * (0.5f + falloff) * dt);
			}
		}
	}

	private void Gas()
	{
		var gas = new GasAgujero { Radio = RadioGas, Duracion = DuracionGas };
		AddChild(gas);
	}

	private void SoltarLarvas(Node2D target)
	{
		if (LarvaFrames is null)
			return;
		for (int i = 0; i < Larvas; i++)
		{
			var larva = new LarvaAgujero { Frames = LarvaFrames, Objetivo = target, Zonas = Zonas, Danio = Mathf.Max(4, Danio / 2) };
			// On the part, so the body's motion carries it.
			GetParent().AddChild(larva);
			larva.GlobalPosition = GlobalPosition + Vector2.FromAngle(i * Mathf.Tau / Larvas) * GlobalRadius * 0.6f;
		}
		NeonFx.Burst(FxParent, GlobalPosition, new Color(0.9f, 0.7f, 1f), GlobalRadius * 2f);
	}

	private async System.Threading.Tasks.Task Tentaculo(Node2D target)
	{
		var tentacle = new TentaculoAgujero { Largo = LargoTentaculo, Agarre = AgarreTentaculo, Danio = Mathf.Max(3, Danio / 2), Objetivo = target };
		AddChild(tentacle);
		await ToSignal(tentacle, Node.SignalName.TreeExiting);
	}
}

// Small procedural neon helpers shared by the Gigante's hole attacks.
internal static class NeonFx
{
	private static CanvasItemMaterial _additive;

	public static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	public static Polygon2D Circle(float radius, Color color, bool additive, int sides = 20)
	{
		var points = new Vector2[sides];
		for (int i = 0; i < sides; i++)
			points[i] = Vector2.FromAngle(i * Mathf.Tau / sides) * radius;
		return new Polygon2D { Polygon = points, Color = color, Material = additive ? Additive : null };
	}

	// A short glowing streak that travels from a to b and fades (suction / wind lines).
	public static void Streak(Node parent, Vector2 a, Vector2 b, Color color, float time)
	{
		var line = new Line2D { Width = 3f, DefaultColor = color, Material = Additive, Points = new[] { Vector2.Zero, (b - a).Normalized() * 40f } };
		parent.AddChild(line);
		line.GlobalPosition = a;
		Tween t = line.CreateTween().SetParallel();
		t.TweenProperty(line, "global_position", b, time);
		t.TweenProperty(line, "modulate:a", 0f, time).SetEase(Tween.EaseType.In);
		t.Chain().TweenCallback(Callable.From(line.QueueFree));
	}

	// An expanding ring that fades (spit-out, larva burst).
	public static void Burst(Node parent, Vector2 at, Color color, float radius)
	{
		Polygon2D ring = Circle(radius * 0.3f, color, additive: true);
		parent.AddChild(ring);
		ring.GlobalPosition = at;
		Tween t = ring.CreateTween().SetParallel();
		t.TweenProperty(ring, "scale", Vector2.One * 3.3f, 0.35f).SetEase(Tween.EaseType.Out);
		t.TweenProperty(ring, "modulate:a", 0f, 0.35f);
		t.Chain().TweenCallback(Callable.From(ring.QueueFree));
	}
}
