using Godot;

namespace Metroidvania.World;

// Opens Target (anything with an Open() method — Door, EnergyBarrier) once every EnergyBarrier in
// Seals is open, in any order: e.g. a sealed way down that needs the keys brought back from
// several places. Also checks right after load, so a save that already opened all the seals
// (key-locked barriers remember being unlocked) starts with the target open.
public partial class SealLock : Node
{
	[Export] public NodePath[] Seals = System.Array.Empty<NodePath>();
	[Export] public NodePath Target;

	private EnergyBarrier[] _seals = System.Array.Empty<EnergyBarrier>();

	public override void _Ready()
	{
		_seals = new EnergyBarrier[Seals.Length];
		for (int i = 0; i < Seals.Length; i++)
		{
			_seals[i] = GetNodeOrNull<EnergyBarrier>(Seals[i]);
			if (_seals[i] is not null)
				_seals[i].Opened += CheckSeals;
		}
		// Deferred: the barriers restore their saved open state in their own _Ready.
		CallDeferred(MethodName.CheckSeals);
	}

	private void CheckSeals()
	{
		if (_seals.Length == 0)
			return;
		foreach (EnergyBarrier seal in _seals)
		{
			if (seal is null || !seal.IsOpen)
				return;
		}
		GetNodeOrNull(Target)?.Call("Open");
	}
}
