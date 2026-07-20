using Godot;

namespace Metroidvania.World;

// Reusable greybox hazard: set Count and get that many SpikeTrap instances placed side by side,
// pre-staggered so they don't all pop out at once — same idea as CorridorBlock, applied to
// SpikeTrap.tscn instead of a rectangle. Rebuild wipes and regenerates every SpikeTrap child from
// scratch on any change, so this is a "just set the count" piece, not one for hand-tuning
// individual spikes afterward — detach a spike into its own SpikeTrap.tscn instance if one needs
// to be special.
// [Tool] so the row previews live in the editor, before ever pressing Play.
[Tool]
public partial class SpikeAccordion : Node2D
{
	private const string SpikeTrapScenePath = "res://scenes/world/SpikeTrap.tscn";

	private int _count = 5;

	[Export(PropertyHint.Range, "1,40,1")]
	public int Count
	{
		get => _count;
		set
		{
			_count = Mathf.Max(1, value);
			Rebuild();
		}
	}

	private float _spacing = 80f;

	[Export]
	public float Spacing
	{
		get => _spacing;
		set
		{
			_spacing = value;
			Rebuild();
		}
	}

	private float _staggerStep = 0.4f;

	// Added to each next spike's SpikeTrap.StartOffset so the row pops up in a wave instead of
	// all at once (matches the hand-placed 0, 0.4, 0.8, 1.2... pattern already used in the level).
	[Export]
	public float StaggerStep
	{
		get => _staggerStep;
		set
		{
			_staggerStep = value;
			Rebuild();
		}
	}

	public override void _Ready() => Rebuild();

	private void Rebuild()
	{
		if (!IsInsideTree())
			return;

		foreach (Node child in GetChildren())
			child.Free();

		PackedScene spikeTrapScene = GD.Load<PackedScene>(SpikeTrapScenePath);
		for (int i = 0; i < _count; i++)
		{
			SpikeTrap spike = spikeTrapScene.Instantiate<SpikeTrap>();
			spike.Name = $"SpikeTrap{i + 1}";
			spike.Position = new Vector2(i * _spacing, 0f);
			spike.StartOffset = i * _staggerStep;
			AddChild(spike);
			spike.Owner = this;
		}
	}
}
