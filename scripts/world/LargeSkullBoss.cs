namespace Metroidvania.World;

// Starting point only, per explicit instruction: just the flight-follow (inherited as-is from
// FlyingEnemy — its hunt steering already tracks the player's full position, height included, so
// no override was needed here) plus the orbiting skull barrier (see OrbitingSkull.cs, placed as
// children in LargeSkullBoss.tscn) that keeps the player from reaching her directly. The sheet's
// other three rows (fire eruption windup, recovery, smoke-vanish/teleport) are real and mapped out
// but deliberately not wired to anything yet — no attack behavior until asked for.
public partial class LargeSkullBoss : FlyingEnemy
{
}
