using Godot;

namespace Metroidvania.World;

public static class SceneFader
{
	// True from the moment a transition starts until the new scene is in. The OLD scene keeps running
	// during the fade + threaded load (the player keeps falling through a pit exit, enemies keep
	// swinging), so anything that would kill the player then — FallDeathY, hazards — must be ignored
	// (see Player.OnDied), or the death screen pops up over the new map for no reason.
	public static bool IsChangingScene { get; private set; }

	public static async void ChangeSceneWithFade(SceneTree tree, string scenePath, float fadeDuration = 0.25f)
	{
		IsChangingScene = true;
		CanvasLayer overlay = new() { Layer = 20 };
		ColorRect fade = new()
		{
			Color = new Color(0f, 0f, 0f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(fade);
		tree.Root.AddChild(overlay);

		Tween fadeIn = tree.CreateTween();
		fadeIn.TweenProperty(fade, "color:a", 1f, fadeDuration);
		await tree.ToSignal(fadeIn, Tween.SignalName.Finished);

		ResourceLoader.LoadThreadedRequest(scenePath);
		ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(scenePath);
		while (status == ResourceLoader.ThreadLoadStatus.InProgress)
		{
			await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			status = ResourceLoader.LoadThreadedGetStatus(scenePath);
		}

		if (status == ResourceLoader.ThreadLoadStatus.Loaded)
			tree.ChangeSceneToPacked((PackedScene)ResourceLoader.LoadThreadedGet(scenePath));
		else
			tree.ChangeSceneToFile(scenePath);

		// The swap itself is deferred to the end of the frame — wait for it before letting deaths count again.
		await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		IsChangingScene = false;

		Tween fadeOut = tree.CreateTween();
		fadeOut.TweenProperty(fade, "color:a", 0f, fadeDuration);
		await tree.ToSignal(fadeOut, Tween.SignalName.Finished);

		overlay.QueueFree();
	}
}
