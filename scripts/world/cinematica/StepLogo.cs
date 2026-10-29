using System.Threading.Tasks;
using Godot;

namespace Metroidvania.World;

// Title card over black: the logo comes into focus out of a blur with a slow push-in while Music
// plays, holds, then blurs and fades away with the music. Lasts Duration seconds in total.
[GlobalClass]
public partial class StepLogo : CinematicStep
{
	[Export] public Texture2D Logo;
	[Export] public AudioStream Music;
	[Export] public float MusicVolumeDb = -4f;
	[Export] public float Duration = 18f;
	[Export] public float FocusTime = 5f;
	[Export] public float FadeOut = 3f;
	// Blur radius in logo texels at the start (and what it drifts back to while fading out).
	[Export] public float StartBlur = 28f;
	[Export] public float EndBlur = 12f;
	[Export] public float StartScale = 0.9f;
	[Export] public float EndScale = 1f;
	// Fraction of the screen height the logo image fills.
	[Export(PropertyHint.Range, "0.1,1")] public float HeightFraction = 0.8f;

	public override async Task Run(Cinematic cinematic)
	{
		if (Logo is null)
			return;

		var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/LogoDesenfoque.gdshader") };
		material.SetShaderParameter("radius", StartBlur);
		var layer = new CanvasLayer { Layer = 6, Name = "Logo" };
		var black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		black.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(black);

		Vector2 view = cinematic.GetViewport().GetVisibleRect().Size;
		float height = view.Y * HeightFraction;
		var size = new Vector2(height * Logo.GetWidth() / Logo.GetHeight(), height);
		var rect = new TextureRect
		{
			Texture = Logo,
			Material = material,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Size = size,
			Position = (view - size) / 2f,
			PivotOffset = size / 2f,
			Scale = Vector2.One * StartScale,
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		layer.AddChild(rect);
		cinematic.AddChild(layer);

		AudioStreamPlayer player = null;
		if (Music is not null)
		{
			player = new AudioStreamPlayer { Stream = Music, VolumeDb = MusicVolumeDb };
			layer.AddChild(player);
			player.Play();
		}

		void SetBlur(float v) => material.SetShaderParameter("radius", v);
		float hold = Mathf.Max(0f, Duration - FadeOut);

		Tween zoom = cinematic.CreateTween();
		zoom.TweenProperty(rect, "scale", Vector2.One * EndScale, Duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		Tween show = cinematic.CreateTween().SetParallel();
		show.TweenProperty(rect, "modulate:a", 1f, FocusTime * 0.6f).SetTrans(Tween.TransitionType.Sine);
		show.TweenMethod(Callable.From<float>(SetBlur), StartBlur, 0f, FocusTime).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		await cinematic.WaitSeconds(hold);

		Tween hide = cinematic.CreateTween().SetParallel();
		hide.TweenProperty(rect, "modulate:a", 0f, FadeOut).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		hide.TweenMethod(Callable.From<float>(SetBlur), 0f, EndBlur, FadeOut).SetEase(Tween.EaseType.In);
		if (player is not null)
			hide.TweenProperty(player, "volume_db", -60f, FadeOut).SetEase(Tween.EaseType.In);
		await cinematic.ToSignal(hide, Tween.SignalName.Finished);

		player?.Stop();
		// The black stays until the scene changes; only the logo and the music go.
		rect.QueueFree();
	}
}
