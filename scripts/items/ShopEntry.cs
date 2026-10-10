using Godot;

namespace Metroidvania.Items;

// One line of a merchant's stock: what's sold, for how much gold, and how many in total
// (0 = unlimited). Bought counts persist per shop (SaveManager.GetShopPurchased), so a limited
// stock stays sold out across visits and saves.
[GlobalClass]
public partial class ShopEntry : Resource
{
	[Export] public Item Item;
	[Export] public int Price = 50;
	[Export] public int Stock;
}
