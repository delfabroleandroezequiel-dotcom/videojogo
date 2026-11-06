using Godot;

namespace Metroidvania.World;

// Emboscada de vagoneta: turns a MineCart ride into a fight. Place it anywhere in the map; when
// the cart (CartPath) — with the player aboard and travelling in Direction — passes this node's X,
// it spawns Count copies of EnemyScene:
//  - DropOnCart: bandits leaping down from a ledge DropHeight above the deck. Each is spawned with
//    the lead the cart needs to be under it when it lands (lead = cart speed × fall time), so it
//    really lands ON the cart and fights you there; knock it off over the acid and it's gone.
//  - Ahead: flyers (bats) appearing AheadDistance in front of the cart and HeightAboveDeck up, so
//    the cart runs into them — they keep pace on the slow climbs (MinSpeed < bat chase speed).
// One-shot per scene load. The ambush itself is a visible event, so it reads as "they were waiting
// for you", never as enemies popping out of thin air in view: keep spawn points off-screen ahead.
public partial class CartAmbush : Node2D
{
	public enum AmbushKind { DropOnCart, Ahead }

	[Export] public NodePath CartPath;
	[Export] public PackedScene EnemyScene;
	[Export] public AmbushKind Kind = AmbushKind.DropOnCart;
	[Export] public int Count = 1;
	[Export] public float Spacing = 40f;
	// Only trigger while riding in this direction (+1 = towards the rail end).
	[Export] public int Direction = 1;
	[Export] public float DropHeight = 180f;
	[Export] public float AheadDistance = 360f;
	[Export] public float HeightAboveDeck = 110f;
	// Gravity used for the lead computation; matches Enemy.Gravity's default.
	[Export] public float EnemyGravity = 900f;

	private MineCart _cart;
	private bool _done;

	public override void _Ready()
	{
		_cart = GetNodeOrNull<MineCart>(CartPath);
		if (_cart == null)
			GD.PushWarning($"{Name}: CartPath doesn't point to a MineCart.");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_done || _cart == null || EnemyScene == null || !_cart.IsRolling || _cart.Direction != Direction)
			return;
		float x = _cart.GlobalPosition.X;
		bool passed = Direction > 0 ? x >= GlobalPosition.X : x <= GlobalPosition.X;
		if (!passed || !PlayerAboard())
			return;
		_done = true;
		Spawn();
	}

	private bool PlayerAboard() =>
		GetTree().GetFirstNodeInGroup("player") is Node2D p && Mathf.Abs(p.GlobalPosition.X - _cart.GlobalPosition.X) < _cart.Width && p.GlobalPosition.Y < _cart.GlobalPosition.Y;

	private void Spawn()
	{
		Node parent = GetParent();
		float deckY = _cart.GlobalPosition.Y - _cart.DeckHeight;
		float vx = _cart.RailVelocity.X;
		for (int i = 0; i < Count; i++)
		{
			var enemy = EnemyScene.Instantiate<Node2D>();
			Vector2 at;
			if (Kind == AmbushKind.DropOnCart)
			{
				float fall = Mathf.Sqrt(2f * DropHeight / EnemyGravity);
				// Land on the front half of the deck, so you see them coming down.
				float onDeck = Direction * (_cart.Width * 0.25f) - Direction * i * Spacing;
				at = new Vector2(_cart.GlobalPosition.X + vx * fall + onDeck, deckY - DropHeight - 32f);
			}
			else
			{
				at = new Vector2(_cart.GlobalPosition.X + Direction * (AheadDistance + i * Spacing), deckY - HeightAboveDeck - i * 24f);
			}
			parent.AddChild(enemy);
			enemy.GlobalPosition = at;
		}
	}
}
