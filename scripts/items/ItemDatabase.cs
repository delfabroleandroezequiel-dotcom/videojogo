using System.Collections.Generic;
using Godot;

namespace Metroidvania.Items;

public partial class ItemDatabase : Node
{
	private const string ItemsFolder = "res://resources/items/";

	public static ItemDatabase Instance { get; private set; }

	private readonly Dictionary<string, Item> _items = new();

	public override void _Ready()
	{
		Instance = this;
		LoadAll();
	}

	private void LoadAll()
	{
		using DirAccess dir = DirAccess.Open(ItemsFolder);
		if (dir is null)
			return;

		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (fileName != "")
		{
			if (fileName.EndsWith(".tres"))
			{
				Item item = GD.Load<Item>(ItemsFolder + fileName);
				if (item is not null && !string.IsNullOrEmpty(item.Id))
					_items[item.Id] = item;
			}

			fileName = dir.GetNext();
		}

		dir.ListDirEnd();
	}

	public Item Get(string id) => _items.GetValueOrDefault(id);
	public IReadOnlyCollection<Item> GetAll() => _items.Values;
}
