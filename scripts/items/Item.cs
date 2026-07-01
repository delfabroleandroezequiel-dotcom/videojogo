using Godot;

namespace Metroidvania.Items;

public enum ItemType
{
	Ability,
	Ring,
	Quest,
	Lore
}

[GlobalClass]
public partial class Item : Resource
{
	[Export] public string Id = "";
	[Export] public string ItemName = "";
	[Export(PropertyHint.MultilineText)] public string Description = "";
	[Export] public ItemType Type = ItemType.Lore;

	[Export] public string AbilityId = "";

	[Export] public int HealthBonus;
	[Export] public int StaminaBonus;
	[Export] public int AttackBonus;
	[Export] public int DefenseBonus;
}
