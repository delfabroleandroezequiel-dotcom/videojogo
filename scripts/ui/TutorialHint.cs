using Godot;
using Metroidvania.Shared;

namespace Metroidvania.UI;

// On-screen tutorial sign owned by the scene that needs it (not an autoload). The text is a
// translation key whose {0}, {1}, ... are replaced with the keys currently bound to the given
// actions, so a remapped control shows up correctly. Hidden until ShowHint is called (or TextKey
// is set in the scene).
public partial class TutorialHint : CanvasLayer
{
	[Export] public string TextKey = "";
	[Export] public string[] Actions = System.Array.Empty<string>();

	private Label _label;

	public override void _Ready()
	{
		_label = GetNode<Label>("Panel/Margin/Text");
		if (string.IsNullOrEmpty(TextKey))
			Visible = false;
		else
			ShowHint(TextKey, Actions);
	}

	public void ShowHint(string textKey, string[] actions)
	{
		TextKey = textKey;
		Actions = actions ?? System.Array.Empty<string>();
		var bindings = new object[Actions.Length];
		for (int i = 0; i < Actions.Length; i++)
			bindings[i] = BindingLabel(Actions[i]);
		_label.Text = string.Format(TranslationServer.Translate(TextKey), bindings);
		Visible = true;
	}

	public void HideHint() => Visible = false;

	private static string BindingLabel(string action)
	{
		Key key = GameConfig.Instance?.GetBinding(action) ?? Key.None;
		if (key == Key.None)
			return action;
		// The headless display server can't map layouts; the physical key's name is the next best thing.
		if (DisplayServer.GetName() == "headless")
			return OS.GetKeycodeString(key);
		return OS.GetKeycodeString(DisplayServer.KeyboardGetKeycodeFromPhysical(key));
	}
}
