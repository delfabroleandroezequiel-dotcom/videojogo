using Godot;

namespace Metroidvania.World;

// Which LacquerPreset each map gets: a map whose scene file name starts with one of the
// ByScenePrefix keys uses that preset (longest matching prefix wins), any other map uses Default.
// A null preset leaves the map ungraded. Lives in resources/rendering/Laqueado/ConfigLaqueado.tres.
[GlobalClass]
public partial class LacquerConfig : Resource
{
	public const string DefaultPath = "res://resources/rendering/Laqueado/ConfigLaqueado.tres";

	[Export] public LacquerPreset Default;
	// prefix (String) → LacquerPreset. Untyped so the hand-written .tres literal loads as-is.
	[Export] public Godot.Collections.Dictionary ByScenePrefix = new();

	private static LacquerConfig _cached;

	public static LacquerConfig Get() =>
		_cached ??= ResourceLoader.Exists(DefaultPath) ? GD.Load<LacquerConfig>(DefaultPath) : new LacquerConfig();

	public LacquerPreset PresetFor(string scenePath)
	{
		string sceneName = scenePath.GetFile().GetBaseName();
		LacquerPreset best = Default;
		int bestLength = -1;
		foreach ((Variant key, Variant value) in ByScenePrefix)
		{
			string prefix = key.AsString();
			if (prefix.Length > bestLength && sceneName.StartsWith(prefix))
			{
				best = value.As<LacquerPreset>();
				bestLength = prefix.Length;
			}
		}
		return best;
	}
}
