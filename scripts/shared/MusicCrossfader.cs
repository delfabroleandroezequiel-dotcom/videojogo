using Godot;

namespace Metroidvania.Shared;

// Plays one music track at a time and crossfades when the track changes — for fights whose music
// follows their phases (a boss, the training duel). Owned by whoever drives the phases, not global:
// it dies with the scene. While a voice line plays (Sfx.IsVoicePlaying) the music ducks so the
// dialogue sits on top of it.
public partial class MusicCrossfader : Node
{
	[Export] public float VolumeDb = -6f;
	[Export] public float DuckDb = -9f;
	[Export] public float DuckSpeedDb = 30f;

	private const float SilentDb = -60f;

	private AudioStreamPlayer _current;
	private float _fadeGain = 1f;
	private float _duck;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
	}

	public void Play(AudioStream stream, float fadeSeconds = 1.5f)
	{
		if (stream is null || _current?.Stream == stream)
			return;
		FadeOutCurrent(fadeSeconds);
		var player = new AudioStreamPlayer { Stream = stream, VolumeDb = SilentDb, ProcessMode = ProcessModeEnum.Always };
		// Loops whatever the import setting says: a phase lasts as long as the player takes.
		player.Finished += () => player.Play();
		AddChild(player);
		player.Play();
		_current = player;
		_fadeGain = 0f;
		CreateTween().TweenProperty(this, nameof(FadeGain), 1f, Mathf.Max(fadeSeconds, 0.01f));
	}

	public void Stop(float fadeSeconds = 2f)
	{
		FadeOutCurrent(fadeSeconds);
		_current = null;
	}

	// Tweened through a property so the duck below can keep adjusting the same player's volume.
	public float FadeGain
	{
		get => _fadeGain;
		set => _fadeGain = value;
	}

	public override void _Process(double delta)
	{
		float target = Sfx.IsVoicePlaying ? 1f : 0f;
		_duck = Mathf.MoveToward(_duck, target, (float)delta * DuckSpeedDb / Mathf.Max(-DuckDb, 1f));
		if (IsInstanceValid(_current))
			_current.VolumeDb = GainToDb(_fadeGain) + VolumeDb + DuckDb * _duck;
	}

	private void FadeOutCurrent(float fadeSeconds)
	{
		if (!IsInstanceValid(_current))
			return;
		AudioStreamPlayer old = _current;
		Tween fade = old.CreateTween();
		fade.TweenProperty(old, "volume_db", SilentDb, Mathf.Max(fadeSeconds, 0.01f));
		fade.TweenCallback(Callable.From(old.QueueFree));
	}

	private static float GainToDb(float gain) => gain <= 0.001f ? SilentDb : Mathf.LinearToDb(gain);
}
