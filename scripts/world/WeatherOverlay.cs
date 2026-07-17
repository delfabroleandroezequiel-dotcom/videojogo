using Godot;

namespace Metroidvania.World;

// Rolls a random weather (or none) each time the map loads. F2 still cycles through the
// options manually on top of that, so we can override the roll while testing.
public partial class WeatherOverlay : CanvasLayer
{
	[Export] public SpriteFrames[] WeatherFrames = System.Array.Empty<SpriteFrames>();
	[Export] public AudioStream[] WeatherSounds = System.Array.Empty<AudioStream>();
	[Export] public string AnimationName = "loop";

	private AnimatedSprite2D _sprite;
	private AudioStreamPlayer _audio;
	private int _index = -1; // -1 = no weather

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("Weather");
		_audio = GetNode<AudioStreamPlayer>("WeatherAudio");

		// -1..Length-1 inclusive, so "no weather" is just one more equally likely option.
		_index = WeatherFrames.Length == 0 ? -1 : GD.RandRange(-1, WeatherFrames.Length - 1);
		ApplyState();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("debug_cycle_weather"))
			return;

		_index++;
		if (_index >= WeatherFrames.Length)
			_index = -1;

		ApplyState();
		GetViewport().SetInputAsHandled();
	}

	private void ApplyState()
	{
		if (_index < 0 || WeatherFrames.Length == 0)
		{
			_sprite.Visible = false;
			_audio.Stop();
			return;
		}

		_sprite.Visible = true;
		_sprite.SpriteFrames = WeatherFrames[_index];
		_sprite.Play(AnimationName);

		if (_index < WeatherSounds.Length && WeatherSounds[_index] is not null)
		{
			_audio.Stream = WeatherSounds[_index];
			_audio.Play();
		}
		else
		{
			_audio.Stop();
		}
	}
}
