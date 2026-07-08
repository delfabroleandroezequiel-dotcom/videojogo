using Godot;
using Metroidvania.Items;

namespace Metroidvania.World;

[GlobalClass]
public partial class LootEntry : Resource
{
	[Export] public PackedScene DropScene;
	[Export(PropertyHint.Range, "0,1,0.01")] public float DropChance = 1f;
	[Export] public int MinAmount = 1;
	[Export] public int MaxAmount = 1;
	[Export] public Item RewardItem;
}
