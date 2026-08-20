using System;
using Godot;

namespace Metroidvania.World;

// Same fade-to-opaque/fade-back shape as SceneFader, but for a same-scene jump instead of a
// scene change — hides the instant reposition a same-map teleport (see Gate.cs) would otherwise
// let the camera visibly sweep across on its way to the new position, exposing whatever unfinished
// map geometry sits between the two points.
public static class TeleportFader
{
	public static async void FadeTeleport(SceneTree tree, Action onMidFade, float fadeDuration = 0.25f)
	{
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

		onMidFade();

		Tween fadeOut = tree.CreateTween();
		fadeOut.TweenProperty(fade, "color:a", 0f, fadeDuration);
		await tree.ToSignal(fadeOut, Tween.SignalName.Finished);

		overlay.QueueFree();
	}
}
