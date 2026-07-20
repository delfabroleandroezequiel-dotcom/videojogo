using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

public partial class Hitbox : Area2D
{
	[Export] public float KnockbackForce = 250f;

	[Signal] public delegate void HitDealtEventHandler();

	private CollisionShape2D _shape;
	private Stats _attackerStats;
	private string _impactFramesPath;
	private DamageElement _element;
	private bool _ignoreTargetInvulnerability;

	public override void _Ready()
	{
		_shape = GetNode<CollisionShape2D>("CollisionShape2D");
		BodyEntered += OnBodyEntered;
	}

	// ignoreTargetInvulnerability is for deliberate multi-hit moves (e.g. Player's triple-hit
	// thrust) whose own consecutive pulses would otherwise be swallowed by the i-frames their
	// first pulse just armed on the same target. Every other caller leaves this false and gets
	// today's normal single-hit-per-i-frame-window behavior.
	public void Activate(Stats attackerStats, string impactFramesPath = null, DamageElement element = DamageElement.Normal, bool ignoreTargetInvulnerability = false)
	{
		_attackerStats = attackerStats;
		_impactFramesPath = impactFramesPath;
		_element = element;
		_ignoreTargetInvulnerability = ignoreTargetInvulnerability;
		_shape.Disabled = false;
	}

	public void Deactivate() => _shape.Disabled = true;

	private void OnBodyEntered(Node2D body)
	{
		Stats targetStats = body.GetNodeOrNull<Stats>("Stats");
		if (targetStats is null || targetStats == _attackerStats)
			return;
		if (targetStats.IsInvulnerable && !_ignoreTargetInvulnerability)
			return;

		targetStats.TakeDamage(_attackerStats.AttackPower, element: _element, ignoreInvulnerability: _ignoreTargetInvulnerability);
		Vector2 impactPoint = (GlobalPosition + body.GlobalPosition) / 2f;
		BloodEffect.SpawnAt(this, body.GlobalPosition);
		string framesPath = _impactFramesPath ?? "res://resources/sprites/HitSparkSpriteFrames.tres";
		string animation = _impactFramesPath is null ? "spark" : "impact";
		VfxSpawner.SpawnAt(this, impactPoint, framesPath, animation, new Vector2(0f, 10f));
		EmitSignal(SignalName.HitDealt);

		if (body.HasMethod("ApplyKnockback"))
		{
			Vector2 knockDirection = (body.GlobalPosition - GlobalPosition);
			knockDirection = knockDirection == Vector2.Zero ? Vector2.Right : knockDirection.Normalized();
			body.Call("ApplyKnockback", knockDirection, KnockbackForce);
		}
	}
}
