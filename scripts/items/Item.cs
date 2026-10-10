using Godot;

namespace Metroidvania.Items;

public enum ItemType
{
	Ability,
	Ring,
	Quest,
	Lore,
	HealUpgrade,
	Key
}

[GlobalClass]
public partial class Item : Resource
{
	[Export] public string Id = "";
	[Export] public string ItemName = "";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public ItemType Type = ItemType.Lore;
	// Shown in item slots (shop). Optional — slots fall back to the item's initial without it.
	[Export] public Texture2D Icon;
	// Stackable items are held as a count instead of a one-time "collected" flag — e.g. small keys
	// that get used up by the lock they open (see SaveManager.GetItemCount/ConsumeItem).
	[Export] public bool Stackable;

	[Export] public string AbilityId = "";

	[Export] public int HealthBonus;
	[Export] public int StaminaBonus;
	[Export] public int AttackBonus;
	[Export] public int DefenseBonus;
}
