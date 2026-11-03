using System.Linq;
using Godot;
using Metroidvania.Player;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Items;

public partial class PlayerInventory : Node
{
	[Signal] public delegate void ItemCollectedEventHandler(string itemId);
	[Signal] public delegate void RingEquippedEventHandler(string itemId);
	[Signal] public delegate void RingUnequippedEventHandler(string itemId);

	// The sword in hand is an item, not a flag: a new game starts with the wooden one, which barely
	// scratches anything (a natural lock on the Cave before leaving Nix), until Arthur hands over his.
	// With neither in the save (old saves, test maps) the player keeps the real sword.
	public const string WoodenSwordId = "item_espada_madera";
	public const string ArthurSwordId = "item_espada_arthur";
	[Export] public Material WoodenSwordPalette = GD.Load<Material>("res://resources/sprites/MainCharPaletteEspadaMadera.tres");
	[Export] public Material SwordPalette = GD.Load<Material>("res://resources/sprites/MainCharPalette.tres");

	private PlayerAbilities _abilities;
	private Stats _stats;
	private HealFlask _healFlask;
	private bool _woodenSwordShown;

	// The best sword the player owns is the one in hand; null with neither (the default sword).
	public string HeldSwordId =>
		SaveManager.Instance.HasItem(ArthurSwordId) ? ArthurSwordId
		: SaveManager.Instance.HasItem(WoodenSwordId) ? WoodenSwordId
		: null;

	public override void _Ready()
	{
		_abilities = GetParent().GetNode<PlayerAbilities>("Abilities");
		_stats = GetParent().GetNode<Stats>("Stats");
		_healFlask = GetParent().GetNode<HealFlask>("HealFlask");

		foreach (string itemId in SaveManager.Instance.GetEquippedRings())
		{
			Item item = ItemDatabase.Instance.Get(itemId);
			if (item is not null)
				_stats.ApplyBonus(item.HealthBonus, item.StaminaBonus, item.AttackBonus, item.DefenseBonus);
		}

		ItemCollected += itemId =>
		{
			if (itemId is WoodenSwordId or ArthurSwordId)
				ApplySword();
		};
		// Deferred: the player sets its own base palette in _Ready, after this child's.
		Callable.From(ApplySword).CallDeferred();
	}

	private void ApplySword()
	{
		// A scene that dresses the player itself (ModoJoven's training with Arthur) owns the sword
		// there: its own palette and damage, tuned for that fight.
		if (GetTree().GetFirstNodeInGroup(ModoJoven.Group) is not null)
			return;
		string heldId = HeldSwordId;
		Item sword = heldId is null ? null : ItemDatabase.Instance.Get(heldId);
		_stats.WeaponDamageCap = sword?.WeaponDamageCap ?? 0;
		_stats.WeaponBreaksPoise = sword?.WeaponBreaksPoise ?? true;
		bool wooden = heldId == WoodenSwordId;
		// Only swap the palette when the blade changes, so an elemental look in progress stays.
		if (wooden == _woodenSwordShown)
			return;
		_woodenSwordShown = wooden;
		if (GetParent() is Metroidvania.Player.Player player)
			player.SetBasePalette(wooden ? WoodenSwordPalette : SwordPalette);
	}

	public bool HasItem(string itemId) => SaveManager.Instance.HasItem(itemId);

	public void CollectItem(Item item)
	{
		if (item is null || string.IsNullOrEmpty(item.Id))
			return;

		if (item.Stackable)
		{
			SaveManager.Instance.AddItemCount(item.Id, 1);
			EmitSignal(SignalName.ItemCollected, item.Id);
			return;
		}

		if (SaveManager.Instance.HasItem(item.Id))
			return;

		SaveManager.Instance.CollectItem(item.Id);

		if (item.Type == ItemType.Ability && !string.IsNullOrEmpty(item.AbilityId))
			_abilities.Unlock(item.AbilityId);

		if (item.Type == ItemType.HealUpgrade)
			_healFlask.UnlockCharge();

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
