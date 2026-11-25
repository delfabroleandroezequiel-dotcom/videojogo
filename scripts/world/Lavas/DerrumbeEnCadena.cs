using Godot;

namespace Metroidvania.World;

// Derrumbe en cadena (Las Lavas, "La Torre que se derrumba"): when Activate() is called (a
// PressurePlate at the foot of the climb), the Costras in Targets start to crack one after another
// — FirstDelay, then Gap seconds between each — from the first of the list to the last. List them
// bottom to top: the tower falls apart under and behind you while you climb. Every crust comes back
// on its own after its RespawnTime, so a failed climb can be retried; the chain re-arms ReArm
// seconds after it finishes.
public partial class DerrumbeEnCadena : Node
{
	[Export] public NodePath[] Targets = System.Array.Empty<NodePath>();
	[Export] public float FirstDelay = 1.0f;
	[Export] public float Gap = 0.85f;
	[Export] public float ReArm = 6f;

	private bool _running;

	public async void Activate()
	{
		if (_running)
			return;
		_running = true;
		await ToSignal(GetTree().CreateTimer(FirstDelay), Timer.SignalName.Timeout);
		foreach (NodePath path in Targets)
		{
			if (!IsInstanceValid(this))
				return;
			GetNodeOrNull<Costra>(path)?.Activate();
			await ToSignal(GetTree().CreateTimer(Gap), Timer.SignalName.Timeout);
		}
		if (!IsInstanceValid(this))
			return;
		await ToSignal(GetTree().CreateTimer(ReArm), Timer.SignalName.Timeout);
		_running = false;
	}
}
