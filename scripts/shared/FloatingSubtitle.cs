using Godot;

namespace Metroidvania.Shared;

// Small outlined line over someone's head, fading in and out over `length` seconds (normally the
// voice clip's length). It lives in the current scene, not under the speaker, so a scaled or
// mirrored speaker doesn't stretch or flip the text, and it follows the speaker each frame. `height`
// is measured up from the speaker's origin. Used by VozAmbiente chatter and the player's own
// thoughts (Player.SayThought).
public static class FloatingSubtitle
{
	public static Label Show(Node2D speaker, string key, float length, float height)
	{
		if (!GodotObject.IsInstanceValid(speaker) || speaker.GetTree()?.CurrentScene is not Node scene)
			return null;
		SceneTree tree = speaker.GetTree();

		var label = new Label
		{
			Text = TranslationServer.Translate(key),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(260f, 0f),
			Size = new Vector2(260f, 0f),
			Modulate = new Color(1f, 1f, 1f, 0f),
			ZIndex = 20,
			AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.78f));
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("outline_size", 4);
		label.GrowVertical = Control.GrowDirection.Begin;
		scene.AddChild(label);

		void Follow()
		{
			if (GodotObject.IsInstanceValid(speaker) && GodotObject.IsInstanceValid(label))
				label.GlobalPosition = speaker.GlobalPosition + new Vector2(-130f, -height);
		}
		Follow();
		Callable follow = Callable.From(Follow);
		tree.Connect(SceneTree.SignalName.ProcessFrame, follow);
		label.TreeExiting += () =>
		{
			if (tree.IsConnected(SceneTree.SignalName.ProcessFrame, follow))
				tree.Disconnect(SceneTree.SignalName.ProcessFrame, follow);
		};

		Tween tween = label.CreateTween();
		tween.TweenProperty(label, "modulate:a", 1f, 0.2f);
		tween.TweenInterval(Mathf.Max(0.4f, length - 0.2f));
		tween.TweenProperty(label, "modulate:a", 0f, 0.4f);
		tween.TweenCallback(Callable.From(label.QueueFree));
		return label;
	}
}
