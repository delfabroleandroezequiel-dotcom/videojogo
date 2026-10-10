using Godot;
using Metroidvania.Items;
using Metroidvania.UI;

namespace Metroidvania.World;

// An Npc that opens a shop instead of (or right after) talking. Everything else — sprite,
// facing the player, interact prompt — is the regular Npc. Stock is a list of ShopEntry
// resources set in the Inspector; ShopId keys what's been bought in the save, so two merchants
// selling the same item keep separate stocks (defaults to this node's path in its scene).
public partial class MerchantNpc : Npc
{
	[Export] public ShopEntry[] Stock = { };
	[Export] public string ShopId = "";
	// Said once per visit before the shop first opens (leave empty to open straight away).
	[Export] public string[] GreetingLines = { };

	private bool _greeted;

	protected override void Interact()
	{
		if (!_greeted && GreetingLines.Length > 0)
		{
			_greeted = true;
			DialogueBox.Instance.Show(NpcName, GreetingLines, OpenShop);
			return;
		}

		OpenShop();
	}

	private void OpenShop()
	{
		string shopId = string.IsNullOrEmpty(ShopId) ? GetPath().ToString() : ShopId;
		ShopUI.Open(this, NpcName, shopId, Stock);
	}
}
