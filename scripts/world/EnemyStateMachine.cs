namespace Metroidvania.World;

// Not a Node — just a plain object an Enemy/Boss subclass owns and drives from its own
// _PhysicsProcess (Machine.Update(delta)). Kept outside the scene tree on purpose: no extra
// nodes to wire per enemy, and states are just C# objects you `new` up.
//
// Example (inside a boss's _Ready):
//   _machine.ChangeState(new IdleState(this));
// Example (inside a state):
//   if (Owner.Stats.CurrentHealth < threshold)
//       Machine.ChangeState(new EnrageState((Boss)Owner));
public class EnemyStateMachine
{
	public EnemyState Current { get; private set; }

	public void ChangeState(EnemyState next)
	{
		if (next is null || next == Current)
			return;

		Current?.Exit();
		Current = next;
		Current.Enter();
	}

	public void Update(double delta) => Current?.Update(delta);
}
