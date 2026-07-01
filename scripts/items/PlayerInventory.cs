using System.Linq;
using Godot;
using Metroidvania.Player;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.Items;

public partial class PlayerInventory : Node
{
	[Signal] public delegate void ItemCollectedEventHandler(string itemId);
	[Signal] public delegate void RingEquippedEventHandler(string itemId);
	[Signal] public delegate void RingUnequippedEventHandler(string itemId);

	private PlayerAbilities _abilities;
	private Stats _stats;

	public override void _Ready()
	{
		_abilities = GetParent().GetNode<PlayerAbilities>("Abilities");
		_stats = GetParent().GetNode<Stats>("Stats");

		foreach (string itemId in SaveManager.Instance.GetEquippedRings())
		{
			Item item = ItemDatabase.Instance.Get(itemId);
			if (item is not null)
				_stats.ApplyBonus(item.HealthBonus, item.StaminaBonus, item.AttackBonus, item.DefenseBonus);
		}
	}

	public bool HasItem(string itemId) => SaveManager.Instance.HasItem(itemId);

	public void CollectItem(Item item)
	{
		if (item is null || string.IsNullOrEmpty(item.Id) || SaveManager.Instance.HasItem(item.Id))
			return;

		SaveManager.Instance.CollectItem(item.Id);

		if (item.Type == ItemType.Ability && !string.IsNullOrEmpty(item.AbilityId))
			_abilities.Unlock(item.AbilityId);

		EmitSignal(SignalName.ItemCollected, item.Id);
	}

	public bool EquipRing(string itemId)
	{
		Item item = ItemDatabase.Instance.Get(itemId);
		if (item is null || item.Type != ItemType.Ring || !SaveManager.Instance.HasItem(itemId))
			return false;

		if (!SaveManager.Instance.EquipRing(itemId))
			return false;

		_stats.ApplyBonus(item.HealthBonus, item.StaminaBonus, item.AttackBonus, item.DefenseBonus);
		EmitSignal(SignalName.RingEquipped, itemId);
		return true;
	}

	public void UnequipRing(string itemId)
	{
		Item item = ItemDatabase.Instance.Get(itemId);
		if (item is null || !SaveManager.Instance.GetEquippedRings().Contains(itemId))
			return;

		SaveManager.Instance.UnequipRing(itemId);
		_stats.ApplyBonus(-item.HealthBonus, -item.StaminaBonus, -item.AttackBonus, -item.DefenseBonus);
		EmitSignal(SignalName.RingUnequipped, itemId);
	}
}
