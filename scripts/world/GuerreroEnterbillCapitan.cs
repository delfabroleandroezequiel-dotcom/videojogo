using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Capitán de Enterbill — officer, arming sword and cape. He makes the squad better:
// - Orden: the moment he spots you he raises the sword and shouts ("Hold the line!"). Every enemy
//   within OrderRadius is alerted from afar and, for OrderDuration, attacks on a shorter cooldown
//   (a blue pulse marks who got the order). Mid-fight he repeats it when the order ran out and he
//   still has soldiers around.
// - Duelist: lunging thrust into a rising diagonal cut (attack). He reads the parry: with your
//   parry up he feints — holds the lunge and goes the instant it drops; a whiffed parry gets the
//   thrust right away.
public partial class GuerreroEnterbillCapitan : GuerreroReino
{
	[Export] public float OrderRadius = 520f;
	[Export] public float OrderDuration = 8f;
	[Export] public float OrderCooldownFactor = 0.65f;
	[Export] public float ThrustWindup = 0.4f;
	[Export] public float MaxFeintHold = 0.9f;

	// attack: 1 cocked, 2-4 lunging thrust, 5 diagonal smear. attack2 (order): 2-6 sword raised.
	[Export] public int WindupFrame = 1;
	[Export] public int ThrustFrame = 2;
	[Export] public int CutFrame = 5;
	[Export] public int OrderFrom = 1;
	[Export] public int OrderTo = 7;

	private ulong _orderReadyMsec;
	private readonly List<MeleeEnemy> _buffed = new();

	protected override void OnSpottedPlayer()
	{
		if (Time.GetTicksMsec() >= _orderReadyMsec && !Attacking)
			TryAttackNow();
	}

	protected override async Task<bool> Kit()
	{
		if (Time.GetTicksMsec() >= _orderReadyMsec && CountAllies() > 0)
			return await GiveOrder();

		var player = PlayerRef;
		float windup = ThrustWindup * WindupScale;
		WindupScale = 1f;
		if (player is not null && PlayerReads.WhiffedParryToward(player, GlobalPosition.X))
			windup = 0.1f;
		if (!await Telegraph("attack", WindupFrame, windup))
			return false;
		float held = 0f;
		while (PlayerRef is { } p && PlayerReads.IsBlockingToward(p, GlobalPosition.X) && held < MaxFeintHold)
		{
			if (!await Wait(0.04f))
				return false;
			held += 0.04f;
		}
		MaybeShout();
		Lunge(300f, 0.13f);
		if (!await Strike("attack", ThrustFrame, CutFrame - 1, ThrustFrame, 38f, new Vector2(58f, 16f), y: -6f,
			trail: new TrailArc(new Vector2(-24f, -6f), 84f, -4f, 4f, 0.08f, 8f)))
			return false;
		TurnToTarget();
		return await Strike("attack", CutFrame, 8, CutFrame, 34f, new Vector2(54f, 56f), y: -10f,
			trail: new TrailArc(new Vector2(2f, -8f), 50f, 60f, -90f, 0.1f, 44f));
	}

	private async Task<bool> GiveOrder()
	{
		_orderReadyMsec = Time.GetTicksMsec() + (ulong)((OrderDuration + 4f) * 1000f);
		ExtraCooldown = -0.3f;
		for (int f = OrderFrom; f <= OrderTo; f++)
		{
			Pose("attack2", f);
			if (f == OrderFrom + 2)
			{
				Bark("Orden", 2.2f);
				FlashBody(0.3f);
				RallyAllies();
			}
			if (!await Wait(1f / 10f))
				return false;
		}
		return true;
	}

	private int CountAllies()
	{
		int n = 0;
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
			if (node != this && node is MeleeEnemy e && e.GlobalPosition.DistanceTo(GlobalPosition) < OrderRadius)
				n++;
		return n;
	}

	private void RallyAllies()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node == this || node is not Enemy enemy || enemy.GlobalPosition.DistanceTo(GlobalPosition) > OrderRadius)
				continue;
			enemy.Alert(OrderDuration);
			Pulse(enemy);
			if (enemy is MeleeEnemy melee && !_buffed.Contains(melee))
				Hasten(melee);
		}
	}

	// The restore timer belongs to the tree, not to the captain, so killing him mid-order still
	// gives the soldiers their normal pace back.
	private void Hasten(MeleeEnemy ally)
	{
		_buffed.Add(ally);
		float original = ally.AttackCooldown;
		ally.AttackCooldown = original * OrderCooldownFactor;
		var buffed = _buffed;
		GetTree().CreateTimer(OrderDuration).Timeout += () =>
		{
			if (IsInstanceValid(ally))
				ally.AttackCooldown = original;
			buffed.Remove(ally);
		};
	}

	// A quick additive blue ring on whoever got the order, so the buff reads.
	private void Pulse(Node2D who)
	{
		var ring = new Line2D
		{
			Width = 2.5f,
			DefaultColor = TrailGlowColor,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Closed = true,
		};
		for (int i = 0; i < 24; i++)
			ring.AddPoint(new Vector2(Mathf.Cos(i * Mathf.Tau / 24f), Mathf.Sin(i * Mathf.Tau / 24f)) * 18f);
		who.AddChild(ring);
		ring.Position = new Vector2(0f, -4f);
		var tween = ring.CreateTween().SetParallel();
		tween.TweenProperty(ring, "scale", new Vector2(2.4f, 2.4f), 0.45f).From(new Vector2(0.6f, 0.6f));
		tween.TweenProperty(ring, "modulate:a", 0f, 0.45f);
		tween.Chain().TweenCallback(Callable.From(ring.QueueFree));
	}
}
