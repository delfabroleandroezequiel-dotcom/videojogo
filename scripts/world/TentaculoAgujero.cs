using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Tentacle lashed out of a Gigante hole (AgujeroGigante.Tentaculo). Child of the hole. It shoots
// toward where the player was, wriggling; if its tip touches them it latches on: the tip sticks to
// the player, who is snared (pinned, on the ground or on the climb) and takes a hit every
// TickInterval until Agarre runs out. The tip is an enemy-layer body with its own Stats — cut it
// (hit it) and the tentacle lets go and pulls back. Lengths are in world pixels.
public partial class TentaculoAgujero : Node2D
{
	[Export] public float Largo = 520f;
	[Export] public float Agarre = 2.2f;
	[Export] public int Danio = 6;
	[Export] public float TickInterval = 0.6f;
	[Export] public float Extend = 0.35f;
	[Export] public float Retract = 0.4f;
	[Export] public Color ColorNeon = new(0.75f, 0.35f, 1f);

	public Node2D Objetivo;

	private const int Segments = 14;
	private Line2D _line;
	private Line2D _core;
	private StaticBody2D _tip;
	private Vector2 _aimGlobal;
	private float _reach;      // 0..1 of the way out
	private float _time;
	private float _phaseTimer;
	private float _tick;
	private bool _holding;
	private bool _retracting;
	private bool _cut;

	public override void _Ready()
	{
		// Work in world units whatever the part's scale.
		Scale = Vector2.One / GetParent<Node2D>().GlobalScale.X;
		_aimGlobal = IsInstanceValid(Objetivo) ? Objetivo.GlobalPosition + new Vector2(0f, -30f) : GlobalPosition + Vector2.Down * Largo;

		_line = new Line2D { Width = 22f, DefaultColor = ColorNeon, Material = NeonFx.Additive, JointMode = Line2D.LineJointMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		_line.WidthCurve = new Curve();
		_line.WidthCurve.AddPoint(new Vector2(0f, 1f));
		_line.WidthCurve.AddPoint(new Vector2(1f, 0.35f));
		_core = new Line2D { Width = 8f, DefaultColor = new Color(1f, 0.85f, 1f), Material = NeonFx.Additive, JointMode = Line2D.LineJointMode.Round };
		AddChild(_line);
		AddChild(_core);

		_tip = new StaticBody2D { CollisionLayer = ProjectileReflect.EnemyLayer, CollisionMask = 0 };
		_tip.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 26f } });
		var stats = new Stats { Name = "Stats", MaxHealth = 20, AttackPower = Danio };
		_tip.AddChild(stats);
		_tip.AddToGroup("enemy");
		AddChild(_tip);
		stats.Died += () => { _cut = true; StartRetract(); };
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_phaseTimer += dt;

		if (_retracting)
		{
			_reach = Mathf.Max(0f, _reach - dt / Retract);
			if (_reach <= 0f)
			{
				QueueFree();
				return;
			}
		}
		else if (_holding)
		{
			if (IsInstanceValid(Objetivo))
				_aimGlobal = Objetivo.GlobalPosition + new Vector2(0f, -30f);
			_tick -= dt;
			if (Objetivo is Metroidvania.Player.Player player)
			{
				player.Snare(0.15f);
				if (_tick <= 0f)
				{
					_tick = TickInterval;
					player.GetNode<Stats>("Stats").TakeDamage(Danio, ignoreInvulnerability: true, armInvulnerability: false);
				}
			}
			if (_phaseTimer >= Agarre)
				StartRetract();
		}
		else
		{
			_reach = Mathf.Min(1f, _reach + dt / Extend);
			if (IsInstanceValid(Objetivo) && TipGlobal().DistanceTo(Objetivo.GlobalPosition + new Vector2(0f, -30f)) < 45f)
			{
				_holding = true;
				_phaseTimer = 0f;
				_tick = 0f;
			}
			else if (_reach >= 1f && _phaseTimer > Extend + 0.35f)
				StartRetract();
		}

		Rebuild();
	}

	private void StartRetract()
	{
		_retracting = true;
		_holding = false;
		if (IsInstanceValid(_tip))
			_tip.CollisionLayer = 0;
	}

	private Vector2 TipGlobal()
	{
		Vector2 full = _aimGlobal - GlobalPosition;
		if (full.Length() > Largo)
			full = full.Normalized() * Largo;
		return GlobalPosition + full * _reach;
	}

	private void Rebuild()
	{
		Vector2 tipLocal = ToLocal(TipGlobal());
		Vector2 normal = tipLocal.Orthogonal().Normalized();
		var points = new Vector2[Segments + 1];
		for (int i = 0; i <= Segments; i++)
		{
			float s = i / (float)Segments;
			// Travelling wave, pinned at the root, fading out at a latched tip.
			float wave = Mathf.Sin(s * 9f - _time * 12f) * 26f * s * (1f - s) * (_holding ? 0.4f : 1f);
			points[i] = tipLocal * s + normal * wave;
		}
		_line.Points = points;
		_core.Points = points;
		if (IsInstanceValid(_tip))
			_tip.Position = tipLocal;
		if (_cut)
			_line.DefaultColor = _line.DefaultColor.Lerp(new Color(0.3f, 0.1f, 0.35f), 0.2f);
	}
}
