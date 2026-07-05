using Godot;

namespace Metroidvania.Shared;

public partial class LoopingAudioPlayer : AudioStreamPlayer
{
	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Finished += () => Play();
	}
}
