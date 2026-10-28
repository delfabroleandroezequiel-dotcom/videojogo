using Godot;

namespace Metroidvania.World;

// Game-wide finishing pass ("capa de laqueado"): a full-screen Laqueado.gdshader rect on a
// CanvasLayer below the HUD, so it grades the world but never the UI. Added to every map by
// LevelBootstrap; the look comes from LacquerConfig (per scene-name prefix, e.g. caves vs the rest).
public partial class LacquerLayer : CanvasLayer
{
	private const string ShaderPath = "res://resources/shaders/Laqueado.gdshader";
	// Under the player HUD (layer 10), over the world and parallax.
	private const int DrawLayer = 5;

	// The map's scene file; picks the preset. Empty = the current scene's.
	public string ScenePath = "";

	public override void _Ready()
	{
		Layer = DrawLayer;
		string scenePath = string.IsNullOrEmpty(ScenePath) ? GetTree().CurrentScene?.SceneFilePath ?? "" : ScenePath;
		LacquerPreset preset = LacquerConfig.Get().PresetFor(scenePath);
		if (preset is null)
			return;
		var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };
		preset.ApplyTo(material);
		var rect = new ColorRect { Material = material, MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(rect);
	}
}
