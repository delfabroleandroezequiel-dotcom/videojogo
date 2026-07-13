namespace Metroidvania.World;

// One state = one small class. Enter/Exit run once on transition, Update runs every physics
// frame while this state is active. Owner is typed as Enemy so any state can reach the shared
// stuff (Stats, Visual, GlobalPosition); a state that needs boss-only members just also keeps
// its own typed reference to the concrete Boss/enemy passed into its constructor.
public abstract class EnemyState
{
	protected readonly Enemy Owner;

	protected EnemyState(Enemy owner)
	{
		Owner = owner;
	}

	public virtual void Enter() { }
	public virtual void Update(double delta) { }
	public virtual void Exit() { }
}
