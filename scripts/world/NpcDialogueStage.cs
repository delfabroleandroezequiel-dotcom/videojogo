using Godot;

namespace Metroidvania.World;

[GlobalClass]
public partial class NpcDialogueStage : Resource
{
	[Export] public int MinStage;
	[Export] public string[] Lines = { "NPC_DEFAULT_LINE" };
}
